using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;

namespace Linnarsson.Strt
{
    public class AnnotationBuilder
    {
        public static AnnotationBuilder GetAnnotationBuilder(Props props, StrtGenome genome)
        {
            PathHandler ph = new PathHandler(props);
            if (genome.Annotation == "VEGA")
                return new AnnotationBuilder(props, new BioMartAnnotationReader(ph, genome, "VEGA"));
            if (genome.Annotation.StartsWith("ENSE"))
                return new AnnotationBuilder(props, new BioMartAnnotationReader(ph, genome, genome.Annotation));
            return new AnnotationBuilder(props, new UCSCAnnotationReader(ph, genome));
        }
        private int SpliceFlankLen { get; set; }
        private int minJunctionLen = 30;
        private int minFlankLen = 10;
        public int maxExonsForAllJunctions = 40;
        private Props props;
        private PathHandler ph;
        private AnnotationReader annotationReader;

        public AnnotationBuilder(Props props, AnnotationReader annotationReader)
        {
            SpliceFlankLen = props.SpliceFlankLength;
            SplicedGeneFeature.SetSpliceFlankLen(SpliceFlankLen);
            this.annotationReader = annotationReader;
            this.props = props;
            ph = new PathHandler(props);
        }

        public void BuildExonSplices(StrtGenome genome)
        {
            string junctionsChrId = genome.Annotation;
            Console.WriteLine("Genome: {0} Anntations: {1}", genome.Annotation, genome.Build);
            Background.Progress(0);
            char[] fillNts = new char[] { 'G', 'G', 'T' };
            Dictionary<string, List<GeneFeature>> gfByChr = annotationReader.BuildGeneModelsByChr();
            Console.WriteLine("Read data for {0} chromosomes from annotation files:", gfByChr.Count);
            Console.WriteLine(string.Join(",", gfByChr.Keys.ToArray()));
            Console.WriteLine(annotationReader.GetPseudogeneCount() + " genes are annotated as pseudogenes.");
            Console.WriteLine("SpliceFlankLength=" + SpliceFlankLen + " MinJunctionLen=" + minJunctionLen +
                              " MinFlankSideLen=" + minFlankLen + " FillNts=" + new String(fillNts));
            int nDone = 0;
            int nTooManyExons = 0;
            DnaSequence jChrSeq = new LongDnaSequence();
            Dictionary<string, string> chrIdToFileMap = ph.GetGenomeFilesMap(genome);
            StreamWriter refWriter = PrepareAnnotationsFile(genome);
            StreamWriter chrWriter = PrepareJunctionChrFile(genome, junctionsChrId);
            int nFiles = (int)(chrIdToFileMap.Count * 1.2);
            int sideExonMaxCopy = props.MinExtractionInsertLength / 2;
            foreach (string chrId in chrIdToFileMap.Keys)
            {
                if (chrId == junctionsChrId || !gfByChr.ContainsKey(chrId))
                    continue;
                Background.Message("Reading chr" + chrId + "...");
                DnaSequence chrSeq = AbstractGenomeAnnotations.readChromosomeFile(chrIdToFileMap[chrId]);
                Console.WriteLine("Processing chr" + chrId + " (" + gfByChr[chrId].Count + " genes)...");
                Background.Message("Processing chr" + chrId + "...");
                /* We can not rely on that the versions of each gene are consecutive in input file.
                   One and the same junction-sequence could also potentially have two different gene names.
                   Thus, sort the genes along the chromosome and check if every new junction is already
                   in the junctionChr starting from the last gene with any overlap to current gene. */
                List<long> gfEnds = new List<long>();
                List<long> gfJPos = new List<long>();
                int lastOverlappingGfIdx = 0;
                List<GeneFeature> chrGfs = gfByChr[chrId];
                chrGfs.Sort((gf1, gf2) => gf1.Start - gf2.Start);
                foreach (GeneFeature gf in chrGfs) // GeneFeatures now sorted by position on chromosome
                {
                    if (!genome.GeneVariants && gf.IsVariant())
                        continue;
                    gfEnds.Add(gf.End);
                    gfJPos.Add(jChrSeq.Count);
                    refWriter.WriteLine(gf.ToString());
                    if (gf.ExonStarts.Length > 1)
                    {
                        if (gf.ExonStarts.Length > maxExonsForAllJunctions)
                            nTooManyExons++;
                        List<int> jStarts = new List<int>();
                        List<int> jEnds = new List<int>();
                        List<string> jIds = new List<string>();
                        List<int> offsets = new List<int>();
                        int nExons = gf.ExonEnds.Length;
                        for (int eLeft = 0; eLeft <= gf.ExonStarts.Length - 2; eLeft++)
                        {
                            int eLeftLen = gf.GetExonLength(eLeft);
                            if (eLeftLen >= minFlankLen)
                            {
                                for (int eRight = eLeft + 1; eRight <= gf.ExonCount - 1; eRight++)
                                {
                                    int eRightLen = gf.GetExonLength(eRight);
                                    if (eRightLen >= minFlankLen) // && (eLeftLen + eRightLen) >= minJunctionLen)
                                    {
                                        string leftPartId = string.Format("{0}>{1}", eLeft + 1, eRight + 1);
                                        string rightPartId = string.Format("{0}<{1}", eLeft + 1, eRight + 1);
                                        if (gf.Strand == '-')
                                        { // Ids always have lower exonNo first. "Larger" part is referred exon.
                                            leftPartId = string.Format("{0}<{1}", nExons - eRight, nExons - eLeft);
                                            rightPartId = string.Format("{0}>{1}", nExons - eRight, nExons - eLeft);
                                        }
                                        int startInLeft = gf.ExonEnds[eLeft] - SpliceFlankLen + 1;
                                        DnaSequence leftSpliceSeq = chrSeq.SubSequence(startInLeft, SpliceFlankLen);
                                        int i = SpliceFlankLen - eLeftLen - 1;
                                        if (eLeftLen < minJunctionLen && eLeft > 0)
                                        {
                                            int c = Math.Min(sideExonMaxCopy, gf.GetExonLength(eLeft-1));
                                            int p = gf.ExonEnds[eLeft - 1];
                                            while (c-- > 0 && i >= 0)
                                                leftSpliceSeq[i--] = chrSeq[p--];
                                            if (i > 0) // Add a guranteed wrong nt.
                                                leftSpliceSeq[i--] = IupacEncoding.Complement(chrSeq[p--]);
                                        }
                                        for (; i >= 0; i--)
                                            leftSpliceSeq.SetNucleotide(i, fillNts[i % 3]); // Fill out with non-alignable nts
                                        int startInRight = gf.ExonStarts[eRight];
                                        DnaSequence rightSpliceSeq = chrSeq.SubSequence(startInRight, SpliceFlankLen);
                                        i = eRightLen;
                                        if (eRightLen < minJunctionLen && eRight < gf.ExonCount - 1)
                                        {
                                            int c = Math.Min(sideExonMaxCopy, gf.GetExonLength(eRight + 1));
                                            int p = gf.ExonStarts[eRight + 1];
                                            while (c-- > 0 && i < rightSpliceSeq.Count)
                                                rightSpliceSeq[i++] = chrSeq[p++];
                                            if (i < rightSpliceSeq.Count) // Add a guranteed wrong nt.
                                                rightSpliceSeq[i++] = IupacEncoding.Complement(chrSeq[p++]);
                                        }
                                        for (; i < rightSpliceSeq.Count; i++)
                                            rightSpliceSeq.SetNucleotide(i, fillNts[i % 3]); // Fill out with non-alignable nts
                                        DnaSequence junction = new ShortDnaSequence(leftSpliceSeq);
                                        junction.Append(rightSpliceSeq);
                                        while (gf.Start > gfEnds[lastOverlappingGfIdx])
                                            lastOverlappingGfIdx++;
                                        int jLeftStart = (int)jChrSeq.Match(junction, gfJPos[lastOverlappingGfIdx]);
                                        if (jLeftStart == -1)
                                        {
                                            jLeftStart = (int)jChrSeq.Count;
                                            jChrSeq.Append(junction);
                                            chrWriter.WriteLine(junction.ToString());
                                        }
                                        int jRightStart = jLeftStart + SpliceFlankLen;

                                        jStarts.Add(jLeftStart);
                                        jEnds.Add(jLeftStart + SpliceFlankLen - 1);
                                        jIds.Add(leftPartId);
                                        offsets.Add(startInLeft - jLeftStart);

                                        jStarts.Add(jRightStart);
                                        jEnds.Add(jRightStart + SpliceFlankLen - 1);
                                        jIds.Add(rightPartId);
                                        offsets.Add(startInRight - jRightStart);
                                    }
                                    if (gf.ExonStarts.Length > maxExonsForAllJunctions)
                                        break;
                                }
                            }
                        }
                        if (jStarts.Count > 0)
                        {
                            SplicedGeneFeature jLoc =
                                new SplicedGeneFeature(gf.Name, junctionsChrId, gf.Strand,
                                             jStarts.ToArray(), jEnds.ToArray(),
                                             offsets.ToArray(), jIds.ToArray());
                            refWriter.WriteLine(jLoc.ToString());
                        }
                    }
                }
                Background.Progress(++nDone * 100 / nFiles);
                if (Background.CancellationPending) return;
            }
            refWriter.Close();
            chrWriter.Close();
            Background.Progress(90);
            Console.WriteLine(nTooManyExons + " genes with more than " + maxExonsForAllJunctions +
                              " exons only get coverage of the consecutive exon's junctions.");
            Background.Progress(100);
            Console.WriteLine("Length of artificial splice chromosome:" + jChrSeq.Count);
        }

        private StreamWriter PrepareJunctionChrFile(StrtGenome genome, string junctionChrId)
        {
            string jChrPath = ph.GetJunctionChrPath(genome);
            Console.WriteLine("Artificial exon junction chromosome: " + jChrPath);
            if (File.Exists(jChrPath))
            {
                File.Delete(jChrPath + ".old");
                File.Move(jChrPath, jChrPath + ".old");
            }
            StreamWriter chrWriter = new StreamWriter(jChrPath, false);
            chrWriter.WriteLine(">" + junctionChrId);
            return chrWriter;
        }

        private StreamWriter PrepareAnnotationsFile(StrtGenome genome)
        {
            string annotationsPath = ph.GetAnnotationsPath(genome);
            Console.WriteLine("Annotations file: " + annotationsPath);
            if (File.Exists(annotationsPath))
            {
                File.Delete(annotationsPath + ".old");
                File.Move(annotationsPath, annotationsPath + ".old");
            }
            StreamWriter refWriter = new StreamWriter(annotationsPath, false);
            CopyCTRLData(genome, refWriter);
            return refWriter;
        }

        private void CopyCTRLData(StrtGenome genome, StreamWriter refWriter)
        {
            string chrCTRLPath = ph.GetChrCTRLPath();
            if (File.Exists(chrCTRLPath))
            {
                Console.WriteLine("Adding control chromosome and annotations.");
                string chrDest = Path.Combine(PathHandler.GetGenomeSequenceFolder(genome), Path.GetFileName(chrCTRLPath));
                if (!File.Exists(chrDest))
                    File.Copy(chrCTRLPath, chrDest);
                using (StreamReader CTRLReader = ph.GetCTRLGenesPath().OpenRead())
                {
                    string CTRLData = CTRLReader.ReadToEnd();
                    foreach (string line in CTRLData.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        refWriter.WriteLine(line);
                }
            }
        }

        public void UpdateSilverBulletGenes(StrtGenome genome, string errorsPath)
        {
            if (!errorsPath.Contains(genome.GetBowtieIndexName()))
                throw new ArgumentException("The update has to be of the same build as the updated genome");
            Dictionary<string, int> geneToNewPos = ReadErrorsFile(errorsPath);
            Console.WriteLine("There are {0} genes to have their first/last extended.", geneToNewPos.Count);
            Background.Progress(5);
            string annotationPath = ph.GetAnnotationsPath(genome);
            long fileSize = new FileInfo(annotationPath).Length;
            string updatedPath = annotationPath + ".extended";
            StreamWriter writer = updatedPath.OpenWrite();
            string lastOriginal = "";
            string originalFlag = GeneFeature.nonUTRExtendedIndicator;
            long nc = 0;
            Console.WriteLine("Updating annotation file...");
            foreach (LocusFeature gf in UCSCAnnotationReader.IterAnnotationFile(annotationPath))
            {
                string gfTxt = gf.ToString();
                nc += gfTxt.Length;
                Background.Progress((int)(100 * (nc + 2) / (double)fileSize));
                if (StrtGenome.IsSyntheticChr(gf.Chr))
                {
                    writer.WriteLine(gfTxt);
                    continue;
                }
                if (gf.Name.EndsWith(originalFlag))
                {
                    lastOriginal = gf.Name;
                    writer.WriteLine(gfTxt);
                }
                else
                {
                    try
                    {
                        int newPos = geneToNewPos[gf.Name];
                        if (!lastOriginal.StartsWith(gf.Name))
                        {
                            gf.Name += originalFlag;
                            writer.WriteLine(gf.ToString());
                            gf.Name = gf.Name.Substring(0, gf.Name.Length - originalFlag.Length);
                        }
                        if (newPos < 0)
                            gf.Start = -newPos;
                        else
                            gf.End = newPos;
                        writer.WriteLine(gf.ToString());
                    }
                    catch (KeyNotFoundException)
                    {
                        writer.WriteLine(gfTxt);
                    }
                    lastOriginal = "";
                }
            }
            writer.Close();
            Console.WriteLine("The updated annotations are stored in {0}.\n" +
                              "You need to replace the old file manually.", updatedPath);
        }

        private Dictionary<string, int> ReadErrorsFile(string errorsPath)
        {
            Dictionary<string, int> updates = new Dictionary<string, int>();
            StreamReader errReader = errorsPath.OpenRead();
            string line = errReader.ReadLine();
            string[] fields = line.Split('\t');
            int leftIdx = Array.FindIndex(fields, f => f == "NewLeftExonStart");
            int rightIdx = Array.FindIndex(fields, f => f == "NewRightExonStart");
            while (line != null)
            {
                fields = line.Split('\t');
                if (fields.Length >= rightIdx)
                {
                    string geneName = fields[0];
                    string newLeftStart = fields[leftIdx];
                    string newRightStart = fields[rightIdx];
                    int newPos;
                    if (int.TryParse(newLeftStart, out newPos)) updates[geneName] = -newPos;
                    else if (int.TryParse(newRightStart, out newPos)) updates[geneName] = newPos;
                }
                line = errReader.ReadLine();
            }
            errReader.Close();
            return updates;
        }
    }
}
