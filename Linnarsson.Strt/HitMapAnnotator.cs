using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    [Serializable]
    public class GeneData
    {
        private string m_Name;
        public string Name { get { return m_Name; } }
        private char m_SenseStrandOnChr;
        public char Strand { get { return m_SenseStrandOnChr; } }
        private string m_Chr;
        public string Chr { get { return m_Chr; } }

        [NonSerialized]
        private int[,] HitsByBarcodeAnnotType;
        private int[,] AltGeneHitsByBarcodeAnnotType;
        public Dictionary<int, int> HitsBySpliceExonIdCode;

        public GeneData(string name, char senseStrandOnChr, string chr)
        {
            m_Name = name;
            m_SenseStrandOnChr = senseStrandOnChr;
            m_Chr = chr;
        }

        public string ToSbaFileLine()
        {
            return "@" + m_Name + "\t" + m_Chr + "\t" + m_SenseStrandOnChr;
        }
        public static GeneData FromSbaFileLine(string line)
        {
            string[] fields = line.Split('\t');
            return new GeneData(fields[0].Substring(1), fields[2][0], fields[1]);
        }

        public void Init(int nBarcodes)
        {
            HitsByBarcodeAnnotType = new int[nBarcodes, AnnotType.Count];
            AltGeneHitsByBarcodeAnnotType = new int[nBarcodes, AnnotType.Count];
            HitsBySpliceExonIdCode = new Dictionary<int, int>();
        }
        public void AddHit(int nHits, char chrHitStrand, int trSenseAnnotType, int bcIdx, int exonIdCode, bool hitToAlternativeGenes)
        {
            int annotType = (chrHitStrand == m_SenseStrandOnChr)? trSenseAnnotType : (trSenseAnnotType + AnnotType.SenseCount);
            HitsByBarcodeAnnotType[bcIdx, annotType] += nHits;
            if (hitToAlternativeGenes)
                AltGeneHitsByBarcodeAnnotType[bcIdx, annotType] += nHits;
            if (!HitsBySpliceExonIdCode.ContainsKey(exonIdCode))
                HitsBySpliceExonIdCode[exonIdCode] = nHits;
            else
                HitsBySpliceExonIdCode[exonIdCode] += nHits;
        }

        public int TotalHits()
        {
            int n = 0;
            for (int bcIdx = 0; bcIdx < HitsByBarcodeAnnotType.GetLength(0); bcIdx++)
                for (int annotType = 0; annotType < HitsByBarcodeAnnotType.GetLength(1); annotType++)
                    n += HitsByBarcodeAnnotType[bcIdx, annotType];
            return n;
        }
        public int TotalHitsOfAnnotTypes(int[] annotTypes)
        {
            int n = 0;
            for (int bcIdx = 0; bcIdx < HitsByBarcodeAnnotType.GetLength(0); bcIdx++)
                foreach (int annotType in annotTypes)
                    n += HitsByBarcodeAnnotType[bcIdx, annotType];
            return n;
        }
        public IEnumerable<int> HitsByBcIdxOfAnnotTypes(int[] annotTypes)
        {
            for (int bcIdx = 0; bcIdx < HitsByBarcodeAnnotType.GetLength(0); bcIdx++)
            {
                int n = 0;
                foreach (int annotType in annotTypes)
                    n += HitsByBarcodeAnnotType[bcIdx, annotType];
                yield return n;
            }
            yield break;
        }
        public int TotalHitsByBcIdx(int bcIdx)
        {
            int n = 0;
            for (int annotType = 0; annotType < HitsByBarcodeAnnotType.GetLength(1); annotType++)
                n += HitsByBarcodeAnnotType[bcIdx, annotType];
            return n;
        }

        public static int CodeExonIds(List<int> exonIds)
        {
            int exonIdCode = 0;
            foreach (int exonId in exonIds)
                exonIdCode = (exonIdCode << 8) | exonId;
            return exonIdCode;
        }
        public static string DecodeExonIds(int exonIdCode)
        {
            string exonIdString = (exonIdCode & 255).ToString();
            while ((exonIdCode >>= 8) > 0)
                exonIdString = (exonIdCode & 255).ToString() + "-" + exonIdString;
            return exonIdString;
        }

    }


    [Serializable]
    public class HitMapping
    {
        public GeneData geneData;
        private int typeAndPos;
        private int exonIdCode = 0;

        public HitMapping(GeneData geneData, int trSenseAnnotType, int trPos, List<int> exonIds)
        {
            this.geneData = geneData;
            typeAndPos = (trSenseAnnotType << 26) + trPos;
            exonIdCode = GeneData.CodeExonIds(exonIds);
        }
        public HitMapping(GeneData geneData, int typeAndPos, int exonIdCode)
        {
            this.geneData = geneData;
            this.typeAndPos = typeAndPos;
            this.exonIdCode = exonIdCode;
        }

        public void AddHit(int nHits, char chrHitStrand, int bcIdx, bool hitToAlternativeGenes)
        {
            geneData.AddHit(nHits, chrHitStrand, typeAndPos >> 26, bcIdx, exonIdCode, hitToAlternativeGenes);
        } 

        public string ToSbaFileField()
        {
            return geneData.Name + "," + typeAndPos + "," + exonIdCode;
        }
        public static HitMapping FromSbaFileField(string field, Dictionary<string, GeneData> geneDataMap)
        {
            string[] hmfields = field.Split(',');
            string geneName = hmfields[0];
            GeneData gd = geneDataMap[geneName];
            return new HitMapping(gd, int.Parse(hmfields[1]), int.Parse(hmfields[2]));
        }
    }


    [Serializable]
    public class HitMappingGroup
    {
        public List<HitMapping> HitMappings = new List<HitMapping>();

        [NonSerialized]
        public static int nRandomTags;
        [NonSerialized]
        private BitArray randomTagIdxUsed;
        [NonSerialized]
        public bool hasHitsToAlternativeGenes = false;

        public void SetupRandomTagFiltering()
        {
            randomTagIdxUsed = (nRandomTags > 0)? new BitArray(nRandomTags * 2, false) : null;
        }
        public void ChangeBarcode(int bcIdx)
        {
            if (randomTagIdxUsed != null)
                randomTagIdxUsed.SetAll(false);
        }
        public void DecideAlternativeGenes()
        {
            GeneData gd0 = HitMappings[0].geneData;
            foreach (HitMapping hm in HitMappings)
                if (hm.geneData != gd0)
                {
                    hasHitsToAlternativeGenes = true;
                    return;
                }
        }

        public int GetNumUsedRandomTags(char chrStrand)
        {
            int n = 0;
            if (randomTagIdxUsed != null)
            {
                int startTag = (chrStrand == '+') ? 0 : nRandomTags;
                for (int i = startTag; i < startTag + nRandomTags; i++)
                    if (randomTagIdxUsed[i]) n++;
            }
            return n;
        }

        public void AddMapping(HitMapping hm)
        {
            HitMappings.Add(hm);
        }

        public bool AddHitIfNewMol(int nHits, char chrHitStrand, int bcIdx, int randomBcIdx)
        {
            if (randomTagIdxUsed != null)
            {
                int tagIdx = randomBcIdx + ((chrHitStrand == '+') ? 0 : nRandomTags);
                if (randomTagIdxUsed[tagIdx] == true)
                    return false;
                randomTagIdxUsed[tagIdx] = true;
            }
            foreach (HitMapping hm in HitMappings)
                hm.AddHit(nHits, chrHitStrand, bcIdx, hasHitsToAlternativeGenes);
            return true;
        }
    }


    public class HitMapAnnotator
    {
        private Dictionary<string, long> chrToCode = new Dictionary<string, long>();
        private Barcodes barcodes;
        private int[] nReadsPerRandomBcIdx, nPositionsPerRandomBcCount;

        private Dictionary<long, HitMappingGroup> hitMappingGroups = new Dictionary<long, HitMappingGroup>();
        private Dictionary<string, GeneData> geneDatas = new Dictionary<string, GeneData>(30000);

        int nTot, nPassedRF, nMapped, nMissingHitMapping;
        List<string> mapFiles = new List<string>();

        public HitMapAnnotator()
        {
        }

        private void clearHitMap()
        {
            chrToCode.Clear();
            hitMappingGroups.Clear();
            geneDatas.Clear();
        }

        private long CodeHitPos(string hitChr, long hitPos)
        {
            // REPLACE WITH: return chrToCode[hitChr.Substring(3)] | hitPos; WHEN ANNOTATIONS FILES REBUILT WITH "chr" also on JUNCTIONID
            if (hitChr.StartsWith("chr")) hitChr = hitChr.Substring(3);
            //long strandCode = (strand == '+') ? 0 : (1L << 53);
            return chrToCode[hitChr] | hitPos;
        }

        private void AddCodedChr(string hitChr, long code)
        {
            if (hitChr.StartsWith("chr")) hitChr = hitChr.Substring(3);
            chrToCode[hitChr] = code;
        }
        private void AssertCodedChr(string hitChr)
        {
            // REPLACE WITH: hitChr = hitChr.Substring(3); WHEN ANNOTATIONS FILES REBUILT WITH "chr" also on JUNCTIONID
            if (hitChr.StartsWith("chr")) hitChr = hitChr.Substring(3);
            if (!chrToCode.ContainsKey(hitChr))
            {
                chrToCode[hitChr] = (long)chrToCode.Count << 54;
            }
        }

        public void Serialize(string binaryFile)
        {
            FileStream fs = new FileStream(binaryFile, FileMode.Create);
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(fs, geneDatas);
            bf.Serialize(fs, hitMappingGroups);
            fs.Close();
        }

        public void WriteHitMapToSbaFile(string outputFile)
        {
            using (StreamWriter writer = new StreamWriter(outputFile))
            {
                foreach (string chr in chrToCode.Keys)
                    writer.WriteLine("chr" + chr + "\t" + chrToCode[chr]);
                foreach (GeneData gd in geneDatas.Values)
                    writer.WriteLine(gd.ToSbaFileLine());
                foreach (KeyValuePair<long, HitMappingGroup> item in hitMappingGroups)
                {
                    writer.Write(item.Key);
                    foreach (HitMapping m in item.Value.HitMappings)
                        writer.Write("\t" + m.ToSbaFileField());
                    writer.WriteLine();
                }
            }
            Console.WriteLine("Wrote " + geneDatas.Count + " genes and " + hitMappingGroups.Count + " mappings to " + outputFile);
        }

        public void InitHitMapFromFile(string file)
        {
            Console.Write("Reading annotations from " + file + " ");
            if (file.EndsWith(".map"))
                InitHitMapFromReadMapFile(file);
            else if (file.EndsWith(".sba"))
                InitHitMapFromSbaFile(file);
            else
                InitHitMapFromSerialized(file);
            foreach (HitMappingGroup hmg in hitMappingGroups.Values)
                hmg.DecideAlternativeGenes();
            Console.WriteLine("\n...read " + hitMappingGroups.Count + " hit mapping groups for "
                              + geneDatas.Count + " genes from " + file);
        }

        public void InitHitMapFromReadMapFile(string mapFile)
        {
            clearHitMap();
            string line;
            int n = 0;
            using (StreamReader reader = new StreamReader(mapFile))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    if (++n % 10000000 == 0)
                        Console.Write(n + "..");
                    string[] fields = line.Split('\t');
                    string hitChr = fields[2];
                    char strand = fields[1][0];
                    long hitPos = int.Parse(fields[3]);
                    Match m = Regex.Match(fields[0], "Gene=(.+):Chr=(.+)([-+]):Pos=([0-9]+):TrPos=([0-9]+):Exon=(.+)");
                    string realGeneName = m.Groups[1].Value;
                    string realChr = m.Groups[2].Value;
                    char realStrand = m.Groups[3].Value[0];
                    int realChrPos = int.Parse(m.Groups[4].Value);
                    int realTrPos = int.Parse(m.Groups[5].Value);
                    string exonIdString = m.Groups[6].Value;
                    List<int> exonIds = new List<int>();
                    foreach (string x in exonIdString.Split('-'))
                        exonIds.Add(int.Parse(x) + 1); // Change exon index to 1-based.
                    AssertCodedChr(hitChr);
                    long codedHitPos = CodeHitPos(hitChr, hitPos);
                    int trSenseAnnotType = (exonIds.Count > 1) ? AnnotType.SPLC : AnnotType.EXON;
                    GeneData gd;
                    if (!geneDatas.TryGetValue(realGeneName, out gd))
                    {
                        gd = new GeneData(realGeneName, realStrand, realChr);
                        geneDatas[realGeneName] = gd;
                    }
                    HitMapping hm = new HitMapping(gd, trSenseAnnotType, realTrPos, exonIds);
                    if (!hitMappingGroups.ContainsKey(codedHitPos))
                        hitMappingGroups[codedHitPos] = new HitMappingGroup();
                    hitMappingGroups[codedHitPos].AddMapping(hm);
                }
            }
        }

        public void InitHitMapFromSbaFile(string sbaFile)
        {
            long fileStep = new FileInfo(sbaFile).Length / 10;
            long filePos = fileStep;
            clearHitMap();
            string line;
            using (StreamReader reader = new StreamReader(sbaFile))
            {
                line = reader.ReadLine();
                while (line.StartsWith("chr"))
                {
                    string[] chrFields = line.Split('\t');
                    AddCodedChr(chrFields[0], long.Parse(chrFields[1]));
                    line = reader.ReadLine();
                }
                while (line[0] == '@')
                {
                    GeneData gd = GeneData.FromSbaFileLine(line);
                    geneDatas[gd.Name] = gd;
                    line = reader.ReadLine();
                }
                while (line != null)
                {
                    string[] fields = line.Split('\t');
                    long codedHitPos = long.Parse(fields[0]);
                    HitMappingGroup hmg = new HitMappingGroup();
                    for (int i = 1; i < fields.Length; i++)
                    {
                        hmg.AddMapping(HitMapping.FromSbaFileField(fields[i], geneDatas));
                    }
                    hitMappingGroups[codedHitPos] = hmg;
                    line = reader.ReadLine();
                    if (reader.BaseStream.Position > filePos)
                    {
                        filePos += fileStep;
                        Console.Write(".");
                    }
                }
            }
        }

        public void InitHitMapFromSerialized(string binaryFile)
        {
            FileStream fs = new FileStream(binaryFile, FileMode.Open);
            BinaryFormatter bf = new BinaryFormatter();
            geneDatas = (Dictionary<string, GeneData>)bf.Deserialize(fs);
            hitMappingGroups = (Dictionary<long, HitMappingGroup>)bf.Deserialize(fs);
            fs.Close();
        }

        public void InitAnalysis(Barcodes barcodes)
        {
            this.barcodes = barcodes;
            HitMappingGroup.nRandomTags = barcodes.RandomBarcodeCount;
            foreach (HitMappingGroup hmg in hitMappingGroups.Values)
                hmg.SetupRandomTagFiltering();
            int nBarcodes = barcodes.Count;
            foreach (GeneData gd in geneDatas.Values)
                gd.Init(nBarcodes);
            nTot = 0; nPassedRF = 0; nMapped = 0; nMissingHitMapping = 0;
            mapFiles.Clear();
            nReadsPerRandomBcIdx = new int[Math.Max(1, barcodes.RandomBarcodeCount)];
            nPositionsPerRandomBcCount = new int[barcodes.RandomBarcodeCount];
        }

        /// <summary>
        /// map file names expected to follow the "X_Run..." pattern where X = bcIdx
        /// </summary>
        /// <param name="mapfileNames"></param>
        public void AnnotatateMapFiles(string folder, string[] mapfileNames)
        {
            Array.Sort(mapfileNames);
            int currentBcIdx = int.Parse(mapfileNames[0].Split('_')[0]);
            foreach (string filename in mapfileNames)
            {
                int bcIdx = int.Parse(filename.Split('_')[0]);
                if (bcIdx != currentBcIdx)
                {
                    foreach (HitMappingGroup hmg in hitMappingGroups.Values)
                    {
                        nPositionsPerRandomBcCount[hmg.GetNumUsedRandomTags('+')]++;
                        nPositionsPerRandomBcCount[hmg.GetNumUsedRandomTags('-')]++;
                    }
                }
                AnnotateMapFile(Path.Combine(folder, filename), bcIdx);
            }
        }

        public void AnnotateMapFile(string mapFile, int bcIdx)
        {
            foreach (HitMappingGroup hmg in hitMappingGroups.Values)
                hmg.ChangeBarcode(bcIdx);
            int nTotThisFile = 0, nPassedRFThisFile = 0, nMappedThisFile = 0;
            MapFile bmf = MapFile.GetMapFile(mapFile, 1, barcodes);
            foreach (MultiReadMappings mrm in bmf.SingleMappings(mapFile))
            {
                if (bcIdx != mrm.BarcodeIdx)
                    throw new FormatException("Each map file has to have the same bcIdx for all reads! Error: " + mapFile);
                nTotThisFile++;
                char strand = mrm[0].Strand;
                int hitPos = mrm[0].Position;
                nReadsPerRandomBcIdx[mrm.RandomBcIdx]++;
                /*if (hitMappingGroups.TryGetValue(CodeHitPos(mrm[0].Chr, hitPos, strand), out hmg))
                {
                    if (hmg.AddHitIfNewMol(1, strand, mrm.BarcodeIdx, mrm.RandomBcIdx))
                        nPassedRFThisFile++;
                    nMappedThisFile++;
                }*/
                try // Needed to handle missing chromosomes if incomplete annotation files are used
                {
                    if (hitMappingGroups[CodeHitPos(mrm[0].Chr, hitPos)].AddHitIfNewMol(1, strand, mrm.BarcodeIdx, mrm.RandomBcIdx))
                        nPassedRFThisFile++;
                    nMappedThisFile++;
                }
                catch (KeyNotFoundException)
                {
                    nMissingHitMapping++;
                    if (nMissingHitMapping <= 20)
                    {
                        Console.WriteLine("Missing hit mapping for hit at chr:" + mrm[0].Chr + " pos:" + hitPos);
                        if (nMissingHitMapping == 20)
                            Console.WriteLine("...skipping output after 20 missing hit mappings.");
                    }
                }
                if (nTotThisFile % 100000 == 0)
                    Console.WriteLine("Tot:" + nTotThisFile + " PassedRF:" + nPassedRFThisFile +
                                      " Mapped:" + nMappedThisFile + " MissingHitMapping:" + nMissingHitMapping);
            }
            Console.WriteLine(nTotThisFile + " reads in " + mapFile);
            Console.WriteLine(nPassedRFThisFile + " passed random tag filter and "
                              + nMappedThisFile + " of these mapped to at least one transcript");
            Console.WriteLine("\nSense-Antisense distribution:");
            for (int senseAnnotType = 0; senseAnnotType < AnnotType.SenseCount; senseAnnotType++)
            {
                int antiAnnotType = AnnotType.MakeAntisense(senseAnnotType);
                Console.WriteLine(AnnotType.GetName(senseAnnotType) + "\t" + GetTotHitsByAnnotType(senseAnnotType) + "\t" +
                                  AnnotType.GetName(antiAnnotType) + "\t" + GetTotHitsByAnnotType(antiAnnotType));
            }
            nTot += nTotThisFile; nPassedRF += nPassedRFThisFile; nMapped += nMappedThisFile;
            mapFiles.Add(mapFile);
        }

        public void WriteSummary()
        {
            string summaryFile = "summary.txt";
            Console.WriteLine("Reporting to " + summaryFile);
            StreamWriter writer = new StreamWriter(summaryFile);
            writer.WriteLine("Totally " + nTot + " reads in " + mapFiles.Count + " map files");
            writer.WriteLine(nPassedRF + " passed random tag filter and " + nMapped + " of these mapped to at least one transcript");
            writer.WriteLine("\nSense-Antisense distribution:");
            for (int senseAnnotType = 0; senseAnnotType < AnnotType.SenseCount; senseAnnotType++)
            {
                int antiAnnotType = AnnotType.MakeAntisense(senseAnnotType);
                writer.WriteLine(AnnotType.GetName(senseAnnotType) + "\t" + GetTotHitsByAnnotType(senseAnnotType) + "\t" +
                                  AnnotType.GetName(antiAnnotType) + "\t" + GetTotHitsByAnnotType(antiAnnotType));
            }
            if (barcodes.HasRandomBarcodes)
            {
                writer.WriteLine("\nHit counts by random tags (all mapped reads):");
                for (int i = 0; i < nReadsPerRandomBcIdx.Length; i++)
                    writer.WriteLine(barcodes.MakeRandomTag(i) + "\t" + nReadsPerRandomBcIdx[i]);
                writer.WriteLine("\nDistribution of hit positions over used random tag counts (positions within exons only):");
                for (int i = 0; i < nPositionsPerRandomBcCount.Length; i++)
                    writer.WriteLine(i + "\t" + nPositionsPerRandomBcCount[i]);
            }
        }

        public int GetTotHitsByAnnotType(int annotType)
        {
            int n = 0;
            int[] annotTypes = new int[] { annotType };
            foreach (GeneData gd in geneDatas.Values)
                n += gd.TotalHitsOfAnnotTypes(annotTypes);
            return n;
        }

        public void WriteRawCounts(string outputFile)
        {
            StreamWriter writer = new StreamWriter(outputFile);
            writer.Write("Gene\tChr\tTotTrHits");
            foreach (string bcSeq in barcodes.Seqs)
                writer.Write("\t" + bcSeq);
            writer.WriteLine();
            int[] trAnnotTypes = new int[] { AnnotType.EXON, AnnotType.SPLC };
            foreach (GeneData gd in geneDatas.Values)
            {
                writer.Write(gd.Name + "\t" + gd.Chr + "\t" + gd.TotalHitsOfAnnotTypes(trAnnotTypes));
                foreach (int n in gd.HitsByBcIdxOfAnnotTypes(trAnnotTypes))
                    writer.Write("\t" + n);
                writer.WriteLine();
            }
        }

    }
}
