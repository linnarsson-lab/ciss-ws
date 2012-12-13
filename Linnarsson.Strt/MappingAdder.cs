using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    /// <summary>
    /// Handles the addition of a (multi-)mapped read to the proper positions in the genome.
    /// Depending on Props, a multiread is added to all its transcript mappings, or only one of them.
    /// A non-transcript read is added only to one (random, if several) mapping.
    /// At the same time, counts the number of newly mapping reads as well as duplicates.
    /// New: Multireads will only be added if no mapping is to a repeat sequence
    /// </summary>
    public class MappingAdder
    {
        private delegate void AddMappingToTranscripts(MultiReadMappings mrm, out bool hasSomeTrMapping, out bool hasSomeNewMapping);
        private AddMappingToTranscripts addMappingToTranscripts;
        private AbstractGenomeAnnotations Annotations;
        private RandomTagFilterByBc randomTagFilter;
        private int[] nUniqueByBarcode;
        private int[] nDuplicatesByBarcode;

        /// <summary>
        /// Total number of reads that have at least some unique position, strand, rndTag, barcode combination.
        /// </summary>
        public int TotalNUniqueReadSignatures { get { return nUniqueByBarcode.Sum(); } }

        /// <summary>
        /// Number of reads with distinct signatures, i.e. at least some position, strand, and rndTag combination is unique.
        /// </summary>
        /// <param name="bcIdx">Barcode index</param>
        public int NUniqueReadSignatures(int bcIdx)
        {
            return nUniqueByBarcode[bcIdx];
        }

        /// <summary>
        /// Total number of reads that exactly replicate some other read's signature.
        /// </summary>
        public int TotalNDuplicateReads { get { return nDuplicatesByBarcode.Sum(); } }

        /// <summary>
        /// Number of reads that are copies of a first unique signature. i.e., the position, strand, and rndTag are exactly the same.
        /// </summary>
        public int[] NDuplicateReadsByBc()
        {
            return nDuplicatesByBarcode;
        }

        public MappingAdder(AbstractGenomeAnnotations annotations, RandomTagFilterByBc randomTagFilter, Barcodes barcodes)
        {
            this.Annotations = annotations;
            this.randomTagFilter = randomTagFilter;
            nDuplicatesByBarcode = new int[barcodes.AllCount];
            nUniqueByBarcode = new int[barcodes.AllCount];
            SetMapperMethod();
        }

        /// <summary>
        /// Add the read to one or several of its mappings, depending on Props.
        /// If no transcript mapping is found, the read will be added to a random non-transcript mapping
        /// New: Multireads will be added to its transcript mappings only if every repeat mapping is also a transcript
        /// </summary>
        /// <param name="mrm"></param>
        /// <returns>True if the read has some mapping to a transcript</returns>
        public bool Add(MultiReadMappings mrm)
        {
            bool hasSomeTrMapping, hasSomeNewMapping;
            addMappingToTranscripts(mrm, out hasSomeTrMapping, out hasSomeNewMapping);
            if (!hasSomeTrMapping) // If no transcript mapping is found, add the read to the (random) first mapping it got
                hasSomeNewMapping = randomTagFilter.Add(mrm[0]);
            if (hasSomeNewMapping)
                nUniqueByBarcode[mrm.BarcodeIdx]++;
            else
                nDuplicatesByBarcode[mrm.BarcodeIdx]++;
            return hasSomeTrMapping;
        }

        private void SetMapperMethod()
        {
            if (Props.props.ShowTranscriptSharingGenes)
            {
                if (Props.props.DirectionalReads && Props.props.UseMost5PrimeExonMapping)
                    addMappingToTranscripts = AddToMost5PrimeExonMappingWSharedGenes;
                else
                    addMappingToTranscripts = AddToAllExonMappingsWSharedGenes;
            }
            else
            {
                if (Props.props.DirectionalReads && Props.props.UseMost5PrimeExonMapping)
                    addMappingToTranscripts = AddToMost5PrimeExonMapping;
                else
                    addMappingToTranscripts = AddToAllExonMappings;
            }
        }

        /// <summary>
        /// Adds to every position where the read aligns to some transcript.
        /// </summary>
        /// <param name="mrm"></param>
        /// <returns>true if any transcript was hit</returns>
        private void AddToAllExonMappings(MultiReadMappings mrm, out bool hasSomeTrMapping, out bool hasSomeNewMapping)
        {
            hasSomeTrMapping = false;
            hasSomeNewMapping = false;
            // New: If any repeat mapping of a multiread is not a transcript, we do not want to annotate exons
            if (mrm.NMappings > 1)
            {
                foreach (MultiReadMapping m in mrm.IterMappings())
                    if (Annotations.IsARepeat(m.Chr, m.HitMidPos) && !Annotations.IsTranscript(m.Chr, m.Strand, m.HitMidPos))
                        return;
            }
            // End new
            foreach (MultiReadMapping m in mrm.IterMappings())
            {
                if (Annotations.IsTranscript(m.Chr, m.Strand, m.HitMidPos))
                {
                    hasSomeTrMapping = true;
                    hasSomeNewMapping |= randomTagFilter.Add(m);
                }
            }
        }

        /// <summary>
        /// Adds only to the single position where the (multi)read aligns closest to the 5' end of some transcript.
        /// If several positions have equal distance to a 5' end one is chosen by random.
        /// </summary>
        /// <param name="mrm"></param>
        /// <returns>true if any transcript was hit</returns>
        private void AddToMost5PrimeExonMapping(MultiReadMappings mrm, out bool hasSomeTrMapping, out bool hasSomeNewMapping)
        {
            hasSomeTrMapping = false;
            hasSomeNewMapping = false;
            MultiReadMapping bestMapping = null;
            int bestDist = int.MaxValue;
            foreach (MultiReadMapping m in mrm.IterMappings())
            {
                // New: If any repeat mapping of a multiread is not a transcript, we do not want to annotate exons
                if (mrm.NMappings > 1 && Annotations.IsARepeat(m.Chr, m.HitMidPos) && !Annotations.IsTranscript(m.Chr, m.Strand, m.HitMidPos))
                    return;
                // End new
                foreach (FtInterval ivl in Annotations.IterExonAnnotations(m.Chr, m.Strand, m.HitMidPos))
                {
                    int dist = ivl.GetTranscriptPos(m.HitMidPos);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestMapping = m;
                    }
                }
            }
            if (bestDist < int.MaxValue)
            {
                hasSomeTrMapping = true;
                hasSomeNewMapping = randomTagFilter.Add(bestMapping);
            }
        }

        /// <summary>
        /// Adds to every position where the read aligns to some transcript.
        /// Records the identities of the transcripts that compete for the read if it is a multiread
        /// </summary>
        /// <param name="mrm"></param>
        /// <returns>true if any transcript was hit</returns>
        private void AddToAllExonMappingsWSharedGenes(MultiReadMappings mrm, out bool hasSomeTrMapping, out bool hasSomeNewMapping)
        {
            hasSomeTrMapping = false;
            hasSomeNewMapping = false;
            // New: If any repeat mapping of a multiread is not a transcript, we do not want to annotate exons
            if (mrm.NMappings > 1)
            {
                foreach (MultiReadMapping m in mrm.IterMappings())
                    if (Annotations.IsARepeat(m.Chr, m.HitMidPos) && !Annotations.IsTranscript(m.Chr, m.Strand, m.HitMidPos))
                        return;
            }
            // End new
            Dictionary<IFeature, object> sharingRealFeatures = new Dictionary<IFeature, object>();
            foreach (MultiReadMapping m in mrm.IterMappings())
            {
                bool isTranscript = false;
                foreach (FtInterval ivl in Annotations.IterExonAnnotations(m.Chr, m.Strand, m.HitMidPos))
                {
                    isTranscript = true;
                    sharingRealFeatures[ivl.Feature.RealFeature] = null;
                }
                if (isTranscript)
                {
                    hasSomeTrMapping = true;
                    hasSomeNewMapping |= randomTagFilter.Add(m, sharingRealFeatures);
                }
            }
        }

        /// <summary>
        /// Adds only to the single position where the (multi)read aligns closest to the 5' end of some transcript.
        /// If several positions have equal distance to a 5' end one is chosen by random.
        /// Records the identities of the transcripts that compete for the read if it is a multiread
        /// </summary>
        /// <param name="mrm"></param>
        /// <returns>true if any transcript was hit</returns>
        private void AddToMost5PrimeExonMappingWSharedGenes(MultiReadMappings mrm, out bool hasSomeTrMapping, out bool hasSomeNewMapping)
        {
            Dictionary<IFeature, object> sharingRealFeatures = new Dictionary<IFeature, object>();
            hasSomeTrMapping = false;
            hasSomeNewMapping = false;
            MultiReadMapping bestMapping = null;
            int bestDist = int.MaxValue;
            foreach (MultiReadMapping m in mrm.IterMappings())
            {
                // New: If any repeat mapping of a multiread is not a transcript, we do not want to annotate exons
                if (mrm.NMappings > 1 && Annotations.IsARepeat(m.Chr, m.HitMidPos) && !Annotations.IsTranscript(m.Chr, m.Strand, m.HitMidPos))
                    return;
                // End new
                foreach (FtInterval ivl in Annotations.IterExonAnnotations(m.Chr, m.Strand, m.HitMidPos))
                {
                    sharingRealFeatures[ivl.Feature.RealFeature] = null;
                    int dist = ivl.GetTranscriptPos(m.HitMidPos);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestMapping = m;
                    }
                }
            }
            if (bestDist < int.MaxValue)
            {
                hasSomeTrMapping = true;
                hasSomeNewMapping = randomTagFilter.Add(bestMapping, sharingRealFeatures);
            }
        }
    }
}
