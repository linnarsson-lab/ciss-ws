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
        private delegate MultiReadMapping AddMappingToTranscripts(MultiReadMappings mrm, out bool hasSomeTrMapping, out bool hasSomeNewMapping);
        private AddMappingToTranscripts addMappingToTranscripts;
        private GenomeAnnotations Annotations;
        private RandomTagFilterByBc randomTagFilter;
        private int[] nUniqueByBarcode;
        private int[] nDuplicatesByBarcode;
        private Dictionary<IFeature, object> sharingRealFeatures; // Used to tell which features compete for multireads
        private MultiReadMapping[] mappingChoices; // Used to list the valid available exon mappings of a multireads
        private Random rnd = new Random(DateTime.Now.Millisecond); // Used in random selection of multireads mappings

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

        public MappingAdder(GenomeAnnotations annotations, RandomTagFilterByBc randomTagFilter, Barcodes barcodes)
        {
            this.Annotations = annotations;
            this.randomTagFilter = randomTagFilter;
            nDuplicatesByBarcode = new int[barcodes.Count];
            nUniqueByBarcode = new int[barcodes.Count];
            SetMapperMethod();
            mappingChoices = new MultiReadMapping[Props.props.MaxAlternativeMappings];
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
            MultiReadMapping nonTrMrm = addMappingToTranscripts(mrm, out hasSomeTrMapping, out hasSomeNewMapping);
            if (nonTrMrm != null) // If no transcript mapping is found, add the read to the non-exon repeat or random first mapping it got
                hasSomeNewMapping = randomTagFilter.Add(nonTrMrm, false);
            if (hasSomeNewMapping)
                nUniqueByBarcode[mrm.BcIdx]++;
            else
                nDuplicatesByBarcode[mrm.BcIdx]++;
            return hasSomeTrMapping;
        }

        private void SetMapperMethod()
        {
            if (Props.props.ShowTranscriptSharingGenes)
            {
                sharingRealFeatures = new Dictionary<IFeature, object>(Props.props.MaxAlternativeMappings * 2);
                if (Props.props.SelectedMappingType == MultiReadMappingType.Most5Prime)
                    addMappingToTranscripts = AddToMost5PrimeExonMappingWSharedGenes;
                else if (Props.props.SelectedMappingType == MultiReadMappingType.All)
                    addMappingToTranscripts = AddToAllExonMappingsWSharedGenes;
                else
                    addMappingToTranscripts = AddToARandomExonMappingsWSharedGenes;
            }
            else
            {
                if (Props.props.SelectedMappingType == MultiReadMappingType.Most5Prime)
                    addMappingToTranscripts = AddToMost5PrimeExonMapping;
                else if (Props.props.SelectedMappingType == MultiReadMappingType.All)
                    addMappingToTranscripts = AddToAllExonMappings;
                else
                    addMappingToTranscripts = AddToARandomExonMapping;
            }
        }

        /// <summary>
        /// Adds to every position where the read aligns to some transcript.
        /// </summary>
        /// <param name="mrm"></param>
        /// <returns>a non-exon mapping that should be added to when no exon mapping could be added to, else null on success</returns>
        private MultiReadMapping AddToAllExonMappings(MultiReadMappings mrm, out bool hasSomeTrMapping, out bool hasSomeNewMapping)
        {
            hasSomeTrMapping = false;
            hasSomeNewMapping = false;
            if (mrm.NMappings > 1)
            { // If any repeat mapping of a multiread is not a transcript, we do not want to annotate exons
                foreach (MultiReadMapping m in mrm.IterMappings())
                    if (Annotations.IsARepeat(m.Chr, m.HitMidPos) && !Annotations.IsTranscript(m.Chr, m.Strand, m.HitMidPos))
                        return m;
            }
            foreach (MultiReadMapping m in mrm.IterMappings())
            {
                if (Annotations.IsTranscript(m.Chr, m.Strand, m.HitMidPos))
                {
                    hasSomeTrMapping = true;
                    hasSomeNewMapping |= randomTagFilter.Add(m, true);
                }
            }
            return hasSomeTrMapping ? null : mrm[0];
        }

        /// <summary>
        /// Adds only to the single position where the (multi)read aligns closest to the 5' end of some transcript.
        /// If several positions have equal distance to a 5' end one is chosen by random.
        /// </summary>
        /// <param name="mrm"></param>
        /// <returns>a non-exon mapping that should be added to when no exon mapping could be added to, else null on success</returns>
        private MultiReadMapping AddToMost5PrimeExonMapping(MultiReadMappings mrm, out bool hasSomeTrMapping, out bool hasSomeNewMapping)
        {
            hasSomeTrMapping = false;
            hasSomeNewMapping = false;
            MultiReadMapping bestMapping = null;
            int bestDist = int.MaxValue;
            foreach (MultiReadMapping m in mrm.IterMappings())
            {
                // New: If any repeat mapping of a multiread is not a transcript, we do not want to annotate exons
                if (mrm.NMappings > 1 && Annotations.IsARepeat(m.Chr, m.HitMidPos) && !Annotations.IsTranscript(m.Chr, m.Strand, m.HitMidPos))
                    return m;
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
                hasSomeNewMapping = randomTagFilter.Add(bestMapping, true);
            }
            return hasSomeTrMapping ? null : mrm[0];
        }

        /// <summary>
        /// Adds to every position where the read aligns to some transcript.
        /// Records the identities of the transcripts that compete for the read if it is a multiread
        /// </summary>
        /// <param name="mrm"></param>
        /// <returns>a non-exon mapping that should be added to when no exon mapping could be added to, else null on success</returns>
        private MultiReadMapping AddToAllExonMappingsWSharedGenes(MultiReadMappings mrm, out bool hasSomeTrMapping, out bool hasSomeNewMapping)
        {
            hasSomeTrMapping = false;
            hasSomeNewMapping = false;
            if (mrm.NMappings > 1)
            { // If any repeat mapping of a multiread is not a transcript, we do not want to annotate exons
                foreach (MultiReadMapping m in mrm.IterMappings())
                    if (Annotations.IsARepeat(m.Chr, m.HitMidPos) && !Annotations.IsTranscript(m.Chr, m.Strand, m.HitMidPos))
                        return m;
            }
            sharingRealFeatures.Clear();
            int nMrms = 0;
            foreach (MultiReadMapping m in mrm.IterMappings())
            {
                bool isTranscript = false;
                foreach (FtInterval ivl in Annotations.IterExonAnnotations(m.Chr, m.Strand, m.HitMidPos))
                {
                    isTranscript = true;
                    if (Props.props.ShowTranscriptSharingGenes)
                        sharingRealFeatures[ivl.Feature.RealFeature] = null;
                }
                if (isTranscript)
                    mappingChoices[nMrms++] = m;
            }
            for (int mrmIdx = 0; mrmIdx < nMrms; mrmIdx++)
            {
                hasSomeTrMapping = true;
                hasSomeNewMapping |= randomTagFilter.Add(mappingChoices[mrmIdx], sharingRealFeatures, true);
            }
            return hasSomeTrMapping ? null : mrm[0];
        }

        /// <summary>
        /// Adds only to the single position where the (multi)read aligns closest to the 5' end of some transcript.
        /// If several positions have equal distance to a 5' end one is chosen by random.
        /// Records the identities of the transcripts that compete for the read if it is a multiread
        /// </summary>
        /// <param name="mrm"></param>
        /// <returns>a non-exon mapping that should be added to when no exon mapping could be added to, else null on success</returns>
        private MultiReadMapping AddToMost5PrimeExonMappingWSharedGenes(MultiReadMappings mrm, out bool hasSomeTrMapping, out bool hasSomeNewMapping)
        {
            sharingRealFeatures.Clear();
            hasSomeTrMapping = false;
            hasSomeNewMapping = false;
            MultiReadMapping bestMapping = null;
            int bestDist = int.MaxValue;
            foreach (MultiReadMapping m in mrm.IterMappings())
            {
                // If any repeat mapping of a multiread is not a transcript, we do not want to annotate exons
                if (mrm.NMappings > 1 && Annotations.IsARepeat(m.Chr, m.HitMidPos) && !Annotations.IsTranscript(m.Chr, m.Strand, m.HitMidPos))
                    return m;
                foreach (FtInterval ivl in Annotations.IterExonAnnotations(m.Chr, m.Strand, m.HitMidPos))
                {
                    if (Props.props.ShowTranscriptSharingGenes)
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
                hasSomeNewMapping = randomTagFilter.Add(bestMapping, sharingRealFeatures, true);
            }
            return hasSomeTrMapping ? null : mrm[0];
        }

        /// <summary>
        /// Adds a multireads to one random of its possible transcript mappings
        /// </summary>
        /// <param name="mrm"></param>
        /// <param name="hasSomeTrMapping"></param>
        /// <param name="hasSomeNewMapping"></param>
        /// <returns>a non-exon mapping that should be added to when no exon mapping could be added to, else null on success</returns>
        private MultiReadMapping AddToARandomExonMapping(MultiReadMappings mrm, out bool hasSomeTrMapping, out bool hasSomeNewMapping)
        {
            hasSomeTrMapping = false;
            hasSomeNewMapping = false;
            if (mrm.NMappings > 1)
            { // If any repeat mapping of a multiread is not a transcript, we do not want to annotate exons
                foreach (MultiReadMapping m in mrm.IterMappings())
                    if (Annotations.IsARepeat(m.Chr, m.HitMidPos) && !Annotations.IsTranscript(m.Chr, m.Strand, m.HitMidPos))
                        return m;
            }
            int nMrms = 0;
            foreach (MultiReadMapping m in mrm.IterMappings())
            {
                if (Annotations.IsTranscript(m.Chr, m.Strand, m.HitMidPos))
                    mappingChoices[nMrms++] = m;
            }
            if (nMrms > 0)
            {
                hasSomeTrMapping = true;
                hasSomeNewMapping = randomTagFilter.Add(mappingChoices[rnd.Next(nMrms)], true);
            }
            return hasSomeTrMapping ? null : mrm[0];
        }

        /// <summary>
        /// Adds a multireads to one random of its possible transcript mappings
        /// Records the identities of the transcripts that compete for the read if it is a multiread
        /// </summary>
        /// <param name="mrm"></param>
        /// <param name="hasSomeTrMapping"></param>
        /// <param name="hasSomeNewMapping"></param>
        /// <returns>a non-exon mapping that should be added to when no exon mapping could be added to, else null on success</returns>
        private MultiReadMapping AddToARandomExonMappingsWSharedGenes(MultiReadMappings mrm, out bool hasSomeTrMapping, out bool hasSomeNewMapping)
        {
            hasSomeTrMapping = false;
            hasSomeNewMapping = false;
            if (mrm.NMappings > 1)
            { // If any repeat mapping of a multiread is not a transcript, we do not want to annotate exons
                foreach (MultiReadMapping m in mrm.IterMappings())
                    if (Annotations.IsARepeat(m.Chr, m.HitMidPos) && !Annotations.IsTranscript(m.Chr, m.Strand, m.HitMidPos))
                        return m;
            }
            sharingRealFeatures.Clear();
            int nMrms = 0;
            foreach (MultiReadMapping m in mrm.IterMappings())
            {
                bool isTranscript = false;
                foreach (FtInterval ivl in Annotations.IterExonAnnotations(m.Chr, m.Strand, m.HitMidPos))
                {
                    isTranscript = true;
                    if (Props.props.ShowTranscriptSharingGenes)
                        sharingRealFeatures[ivl.Feature.RealFeature] = null;
                }
                if (isTranscript)
                    mappingChoices[nMrms++] = m;
            }
            if (nMrms > 0)
            {
                hasSomeTrMapping = true;
                hasSomeNewMapping = randomTagFilter.Add(mappingChoices[rnd.Next(nMrms)], sharingRealFeatures, true);
            }
            return hasSomeTrMapping ? null : mrm[0];
        }

    }
}
