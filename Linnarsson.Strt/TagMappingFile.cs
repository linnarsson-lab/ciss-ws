using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Linnarsson.Dna;
using Linnarsson.Mathematics;

namespace Linnarsson.Strt
{
    public class GeneIntervals
    {
        public static string spliceChrId;
 
        private string realChr;
        private char realChrStrand;
        private List<Pair<int, int>> realChrIntervals = new List<Pair<int, int>>();
        private Pair<int, int> spliceChrIterval;

        public GeneIntervals(GeneFeature gf)
        {
            if (gf.Chr == spliceChrId)
                throw new Exception("Splice chr comes before real chr: " + gf.Name + " " + gf.Chr);
            realChr = gf.Chr;
            realChrStrand = gf.Strand;
            for (int i = 0; i < gf.ExonCount; i++)
            {
                realChrIntervals.Add(new Pair<int, int>(gf.ExonStarts[i], gf.ExonEnds[i]));
            }
        }
        public void AddSplice(LocusFeature gf)
        {
            if (gf.Chr != spliceChrId)
                throw new Exception("Second annotation is not splice chr: " + gf.Name + " " + gf.Chr);
            spliceChrIterval = new Pair<int, int>(gf.Start, gf.End);
        }

        public bool Contains(string chr, char strand, int hitStartPos, int hitLen)
        {
            if (strand != realChrStrand) return false;
            int midHitPos = hitStartPos + hitLen / 2;
            if ((chr == spliceChrId) &&
                (midHitPos >= spliceChrIterval.First) && (midHitPos <= spliceChrIterval.Second))
                return true;
            if (chr != realChr) return false;
            foreach (Pair<int, int> iv in realChrIntervals)
                if ((midHitPos >= iv.First) && (midHitPos <= iv.Second))
                return true;
            return false;
        }
    }

    public class TagMappingFile
    {
        private Dictionary<string, GeneIntervals> geneIntervals = new Dictionary<string, GeneIntervals>();
 
        public TagMappingFile(StrtGenome genome)
        {
            GeneIntervals.spliceChrId = genome.Annotation;
            string annotationsPath = genome.MakeAnnotationsPath();
            Console.WriteLine("Reading genes from " + annotationsPath);
            foreach (LocusFeature gf in new UCSCAnnotationReader(genome).IterAnnotationFile(annotationsPath))
            {
                GeneIntervals gi;
                if (geneIntervals.TryGetValue(gf.Name, out gi))
                    gi.AddSplice(gf);
                else
                {
                    gi = new GeneIntervals((GeneFeature)gf);
                    geneIntervals[gf.Name] = gi;
                }
            }
        }

        /// <summary>
        /// Create a file where each line contains the alternative mappings that have the same
        /// sequence in genome when considering any allowed mismatches in bowtie.
        /// Compromises by not handling spliced multiread - if they align to non-annotated regions in real genome,
        /// they will be be missed in annotation.
        /// 
        /// First steps are for a readLen of 37bp:
        /// mono --gc=sgen SB.exe dump hg19_sUCSC 37 1 0 3 Splices Fqfile   # uses Annotations_sUCSC_37bp.txt
        /// bowtie --phred64-quals -a -v 3 -M 100 --best hg19 Fqfile Mapfile
        /// mono --gc=sgen SB.exe translatemapfile 37 hg19_sUCSC Mapfile
        /// </summary>
        /// <param name="fqFile">original fq file of synthetic transcript reads from the dump command</param>
        /// <param name="mapFile">bowtie output file - order has to be as in fqFile - use single threaded bowtie!</param>
        /// <param name="hmapFile">outfile for multimapping remapping data</param>
        /// <param name="remainFqFile">outfile for reads that did not have a mapping to the correct location as given by readId</param>
        public void TranslateMapFile(string fqFile, string mapFile, string hmapFile, string remainFqFile)
        {
            HashSet<string> usedFirstGroups = new HashSet<string>();
            string line;
            string currentReadId = null;
            string currentReadName = "";
            int currentNAltMappings = 0;
            bool someMappingMatchesReadIdGenename = false;
            int n = 0, dupl = 0, nNoCorrectMapping = 0, nTooManyAltMappings = 0;
            List<string> mapGroup = new List<string>();
            Console.WriteLine("Translating from " + mapFile + " to " + hmapFile);
            StreamWriter hmapWriter = new StreamWriter(hmapFile);
            StreamWriter remainWriter = new StreamWriter(remainFqFile);
            hmapWriter.WriteLine("#Each line represents a multiread mapping: A set of identical seq stretches in the genome under given mapping settings.");
            hmapWriter.WriteLine("#Each mapping is: chromosome, strand, start position (5') on chromosome");
            List<string> fqLines = new List<string>();
            using (StreamReader fqReader = new StreamReader(fqFile))
            {
                using (StreamReader reader = new StreamReader(mapFile))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (++n % 10000000 == 0)
                            Console.Write(n + "..");
                        string[] fields = line.Split('\t');
                        string newReadId = fields[0];
                        if (currentReadId != null && newReadId != currentReadId)
                        {
                            while (true)
                            {
                                fqLines.Clear();
                                for (int i = 0; i < 4; i++)
                                    fqLines.Add(fqReader.ReadLine());
                                if (fqLines[0].Substring(1).Equals(currentReadId)) break;
                                foreach (string ql in fqLines)
                                    remainWriter.WriteLine(ql);
                            }
                            if (!someMappingMatchesReadIdGenename)
                            {
                                nNoCorrectMapping++;
                                if (currentNAltMappings >= 10 && mapGroup.Count == 1)
                                    nTooManyAltMappings++;
                                else
                                    foreach (string ql in fqLines)
                                        remainWriter.WriteLine(ql);
                            }
                            else if (mapGroup.Count > 1)
                            {
                                mapGroup.Sort();
                                string mapGroup0 = mapGroup[0].Replace("%", "");
                                if (usedFirstGroups.Contains(mapGroup0))
                                    dupl++;
                                else
                                {
                                    usedFirstGroups.Add(mapGroup0);
                                    hmapWriter.WriteLine(string.Join("\t", mapGroup.ToArray()));
                                }
                            }
                            mapGroup.Clear();
                            someMappingMatchesReadIdGenename = false;
                            currentReadId = null;
                        }
                        if (currentReadId == null)
                        {
                            currentReadId = newReadId;
                            Match m = Regex.Match(currentReadId, "Gene=(.+):Chr=");
                            currentNAltMappings = int.Parse(fields[6]);
                            currentReadName = m.Groups[1].Value;
                        }
                        char hitStrand = fields[1][0];
                        string hitChr = fields[2].Replace("chr", "");
                        int hitPos = int.Parse(fields[3]);
                        int hitLen = fields[4].Length;
                        string sameGeneAgain = "";
                        if (geneIntervals[currentReadName].Contains(hitChr, hitStrand, hitPos, hitLen))
                        {
                            if (someMappingMatchesReadIdGenename)
                                sameGeneAgain = "%";
                            someMappingMatchesReadIdGenename = true;
                        }
                        mapGroup.Add(string.Format("{1},{2},{3}{0}", sameGeneAgain, hitChr, hitStrand, hitPos));
                    }
                    if (!someMappingMatchesReadIdGenename)
                    {
                        nNoCorrectMapping++;
                        if (currentNAltMappings >= 10 && mapGroup.Count == 1)
                            nTooManyAltMappings++;
                        else
                            foreach (string ql in fqLines)
                                remainWriter.WriteLine(ql);
                    }
                    else if (mapGroup.Count > 1)
                    {
                        mapGroup.Sort();
                        string mapGroup0 = mapGroup[0].Replace("%", "");
                        if (usedFirstGroups.Contains(mapGroup0))
                            dupl++;
                        else
                        {
                            usedFirstGroups.Add(mapGroup0);
                            hmapWriter.WriteLine(string.Join("\t", mapGroup.ToArray()));
                        }
                    }
                    Console.WriteLine(dupl + " duplicated entries (usually due to different splices with small overhangs of same sequence) were replaced with one each.");
                    Console.WriteLine(nTooManyAltMappings + " had more than maximum alignments and no correct. They are omitted.");
                    Console.WriteLine(nNoCorrectMapping + " reads had less than maximum and no alignment to the correct position and are written to " + remainFqFile);
                }
            }
            hmapWriter.Close();
            remainWriter.Close();
        }

    }
}
