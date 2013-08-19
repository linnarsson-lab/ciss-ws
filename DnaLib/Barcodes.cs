using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    public class BarcodeFileException : ApplicationException
    {
        public BarcodeFileException(string msg)
            : base(msg)
        { }
    }
    
	public abstract class Barcodes
	{
        private string[] m_Seqs;
        public string[] Seqs { 
            get { return m_Seqs; }
            protected set {
                m_Seqs = value;
                m_BarcodeLen = m_Seqs[0].Length;
                MakeBcSeqToBcIdxMap();
            }
        }
        public int Count { get { return m_Seqs.Length; } }

        protected int m_BarcodePos = 0;
        public int BarcodePos { get { return m_BarcodePos; } }
        private int m_BarcodeLen;
        public int BarcodeLen { get { return m_BarcodeLen; } }
        public int BarcodeEndPos { get { return m_BarcodePos + m_BarcodeLen; } }

        protected string[] m_WellIds = null;
        protected string[] m_SpeciesByWell = null;
        public string[] SpeciesByWell { get { return m_SpeciesByWell; } }
        public static int MaxCount { get { return 96; } }

        private Dictionary<string, string[]> AnnotationsByWell = new Dictionary<string, string[]>();

        protected string m_Name;
        public string Name { get { return m_Name; } }

        protected int m_UMIPos = 0;
        public int UMIPos { get { return m_UMIPos; } }
        protected int m_UMILen = 0;
        public int UMILen { get { return m_UMILen; } }
        public int UMIEndPos { get { return m_UMIPos + m_UMILen; } }
        public int UMICount { get { return 1 << (2 * m_UMILen); } }
        public bool HasUMIs { get { return m_UMILen > 0; } }

        protected int m_PrefixRead2 = 0;
        public int PrefixRead2 { get { return m_PrefixRead2; } }
        protected int m_PrefixRead3 = 0;
        public int PrefixRead3 { get { return m_PrefixRead3; } }
        public bool NeedRead2Or3 { get { return m_PrefixRead2 > 0 || m_PrefixRead3 > 0; } }

        protected int m_InsertStart = 0;
        public int InsertStart { get { return m_InsertStart; } }

        public int BarcodeFieldLen { get { return (m_UMILen > 0)? (1 + m_UMILen + m_BarcodeLen) : m_BarcodeLen; } }

        /// <summary>
        /// Set to true to allow extraction step to correct single base substitutions in barcodes.
        /// </summary>
        public bool AllowSingleMutations = false;

        protected Dictionary<string, int> bcSeqToBcIdxMap;

        public int NOBARIdx = 0;
        public static readonly string NOBARCODE = "NOBAR";

        protected string m_TSSeq = "GGG";
        public string TSSeq { get { return m_TSSeq; } }
        protected char m_TSTrimNt = 'G';
        public char TSTrimNt { get { return m_TSTrimNt; } }

        public Barcodes(string bcSetName, string[] seqs)
        {
            m_Name = bcSetName;
            Seqs = seqs;
        }

        public virtual void MakeBcSeqToBcIdxMap()
        {
            bcSeqToBcIdxMap = new Dictionary<string, int>();
            for (int bcIdx = 0; bcIdx < Count; bcIdx++)
            {
                string bcSeq = m_Seqs[bcIdx];
                bcSeqToBcIdxMap[bcSeq] = bcIdx;
                if (AllowSingleMutations)
                {
                    for (int p = 0; p < BarcodeLen; p++)
                    {
                        foreach (char subNt in new char[] { 'A', 'C', 'G', 'T' })
                        {
                            if (bcSeq[p] != subNt)
                            {
                                char[] subS = bcSeq.ToCharArray();
                                subS[p] = subNt;
                                bcSeqToBcIdxMap[new string(subS)] = bcIdx;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extract the barcode from the read, check that a required TS-'GGG' is there, and
        /// remove any trailing 'G':s, when applicable
        /// </summary>
        /// <param name="read">the full read</param>
        /// <param name="maxTrimExtraGs">max extra 'G':s to remove</param>
        /// <param name="bcIdx">the detected barcode index, or -1 on failure</param>
        /// <param name="insertPos">the position of the actual insert, after any GGG...</param>
        /// <returns>true on successful detection of barcodes (and GGG)</returns>
        public bool VerifyBarcodeAndTS(string read, int maxTrimExtraGs, out int bcIdx, out int insertPos)
        {
            bcIdx = -1;
            insertPos = m_InsertStart + TSSeq.Length;
            if (bcSeqToBcIdxMap.TryGetValue(read.Substring(BarcodePos, BarcodeLen), out bcIdx))
            {
                if (TSSeq != null & read.Substring(m_InsertStart, TSSeq.Length) == TSSeq)
                {
                    while (maxTrimExtraGs > 0 && read.Length > insertPos && read[insertPos] == m_TSTrimNt)
                    {
                        insertPos++;
                        maxTrimExtraGs--;
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Find the position after UMI, barcode and any GGG-triple, or specified InsertStart, whichever is highest
        /// </summary>
        /// <returns>Positions where actual sequence should start</returns>
        public virtual int GetInsertStartPos()
        {
            return Math.Max(m_BarcodePos + m_BarcodeLen + m_TSSeq.Length, Math.Max(m_UMIPos + m_UMILen, m_InsertStart));
        }

        /// <summary>
        /// Strip the barcode and UMI from the ReadId
        /// </summary>
        /// <param name="readId">ReadId from STRT extracted FastQ file</param>
        /// <param name="bcIdx">barcode as an index</param>
        /// <param name="UMIIdx">UMI as an index</param>
        /// <returns>ReadId stripped from barcode/UMI parts</returns>
        public string StripBarcodesFromReadId(string readId, out int bcIdx, out int UMIIdx)
        {
            bcIdx = bcSeqToBcIdxMap[readId.Substring(readId.Length - m_BarcodeLen)];
            UMIIdx = 0;
            int p = readId.Length - BarcodeFieldLen;
            for (int i = 0; i < m_UMILen; i++)
            {
                UMIIdx = (UMIIdx << 2) | ("ACGT".IndexOf(readId[p++]));
            }
            return readId.Substring(0, readId.Length - BarcodeFieldLen - 1);
        }

        public string MakeUMISeq(int UMIIdx)
        {
            char[] UMISeq = new char[m_UMILen];
            int p = m_UMILen - 1;
            for (int i = 0; i < m_UMILen; i++)
            {
                UMISeq[p--] = "ACGT"[UMIIdx & 3];
                UMIIdx = UMIIdx >> 2;
            }
            return new string(UMISeq);
        }

        public static string[] GetAllBarcodeSetNames()
        {
            string[] allBarcodeSetNames = PathHandler.GetAllCustomBarcodeSetNames();
            Array.Resize(ref allBarcodeSetNames, allBarcodeSetNames.Length + 6);
            Array.Copy(new string[] { "v1", "v2", "v3", "PE_8", "no", "Lin8" }, 0, allBarcodeSetNames, allBarcodeSetNames.Length - 6, 6);
            return allBarcodeSetNames;
        }

        public static Barcodes GetBarcodes(string barcodeSetName)
        {
            Barcodes bc = null;
            switch (barcodeSetName.ToLower())
            {
                case "v1":
                    bc = new STRTv1Barcodes();
                    break;
                case "v2":
                    bc = new STRTv2Barcodes();
                    break;
                case "pe_8":
                    bc = new PE_8Barcodes();
                    break;
                case "v3":
                    bc = new STRTv3Barcodes();
                    break;
                case "lin8":
                    bc = new LineageBarcodes();
                    break;
                case "no":
                    bc = new NoBarcodes();
                    break;
                default:
                    bc = new CustomBarcodes(barcodeSetName);
                    break;
            }
            return bc;
        }

        public void SetupPlate()
        {
            m_WellIds = new string[96];
            for (int wellIdx = 0; wellIdx < 96; wellIdx++)
            {
                char first = "ABCDEFGH"[wellIdx % 8];
                int second = 1 + (int)wellIdx / 8;
                string wellId = string.Format("{0}{1:00}", first, second);
                m_WellIds[wellIdx] = wellId;
            }
        }

        private bool GenomeMatchesWell(StrtGenome genome, int bcIdx, bool strict)
        {
            if (m_SpeciesByWell == null) return true;
            string speciesId = m_SpeciesByWell[bcIdx].ToLower();
            return (speciesId == "empty" && !strict)
                   || speciesId.StartsWith(genome.Abbrev.ToLower()) || speciesId.StartsWith(genome.Name.ToLower())
                   || speciesId.StartsWith(genome.LatinName.ToLower());
        }

        public bool HasSampleLayout()
        {
            return m_SpeciesByWell != null;
        }
        public int[] GenomeAndEmptyBarcodeIndexes(StrtGenome genome)
        {
            return GenomeBarcodeIndexes(genome, false);
        }
        /// <summary>
        /// Get the barcode indices of the specified genome (and optionally empty wells) given the layout file.
        /// </summary>
        /// <param name="genome">Defines species to pick wells for</param>
        /// <param name="strict">If true, only species wells are taken. If false, empty wells are included</param>
        /// <returns>All barcode indexes if no layout was defined</returns>
        public int[] GenomeBarcodeIndexes(StrtGenome genome, bool strict)
        {
            List<int> indexes = new List<int>();
            for (int idx = 0; idx < m_Seqs.Length; idx++)
                if (GenomeMatchesWell(genome, idx, strict))
                    indexes.Add(idx);
            return indexes.ToArray();
        }
        /// <summary>
        /// </summary>
        /// <returns>Barcode indexes of wells that were defined as empty in layout, or empty array if no layout was given</returns>
        public int[] EmptyBarcodeIndexes()
        {
            if (m_SpeciesByWell == null) return new int[] {};
            List<int> indexes = new List<int>();
            for (int i = 0; i < m_SpeciesByWell.Length; i++)
                if (m_SpeciesByWell[i].ToLower() == "empty") indexes.Add(i);
            return indexes.ToArray();
        }

        public string GetWellId(int wellIdx)
        {
            return m_WellIds[wellIdx];
        }

        public List<string> GetAnnotationTitles()
        {
            return AnnotationsByWell.Keys.ToList();
        }
        public string GetAnnotation(string annotationTitle, int wellIdx)
        {
            return AnnotationsByWell[annotationTitle][wellIdx];
        }

        public void SetSampleLayout(PlateLayout sampleLayout)
        {
            AnnotationsByWell.Clear();
            m_SpeciesByWell = new string[m_WellIds.Length];
            if (m_WellIds.Length != sampleLayout.Length)
                throw new SampleLayoutFileException("Number of lines does not match barcode set " + this.Name + " in file " + sampleLayout.Filename);
            foreach (string annotation in sampleLayout.GetAnnotations())
                AnnotationsByWell[annotation] = new string[m_WellIds.Length];
            foreach (KeyValuePair<string, string> pair in sampleLayout.SpeciesIdBySampleId)
            {
                string sampleId = pair.Key;
                int wellIdx = Array.FindIndex(m_WellIds, (id) => id == sampleId);
                if (wellIdx == -1)
                    throw new SampleLayoutFileException("SampleId " + sampleId + " is not in barcode set " + this.Name + " in file " + sampleLayout.Filename);
                m_SpeciesByWell[wellIdx] = pair.Value;
                foreach (string annotation in sampleLayout.GetAnnotations())
                    AnnotationsByWell[annotation][wellIdx] = sampleLayout.GetSampleAnnotation(annotation, sampleId);
            }
        }

		#region Paired-end
		public static string[] PE_8 = new string[]
		{
			"TTTAGG",
			"AGCGAG",
			"ATCAAC",
			"CCGCTA",
			"GGGTTT",
			"CATGAT",
			"GAATTA",
			"ACACCC"
		};

		#endregion

        #region STRT 1.0
        public static string[] STRT_v1 = new string[]
		{
			"CAGAA",
			"CATAC",
			"CAAAG",
			"CACAT",
			"CATCA",
			"CAGCC",
			"CACCG",
			"CAACT",
			"CAAGA",
			"CACGC",
			"CATGT",
			"CACTA",
			"CAATC",
			"CATTG",
			"CAGTT",
			"CCTAA",
			"CCGAC",
			"CCAAT",
			"CCGCA",
			"CCTCC",
			"CCACG",
			"CCAGC",
			"CCTGG",
			"CCGGT",
			"CCATA",
			"CCGTG",
			"CCTTT",
			"CGAAA",
			"CGCAC",
			"CGGAG",
			"CGTAT",
			"CGCCA",
			"CGACC",
			"CGTCG",
			"CGGCT",
			"CGGGA",
			"CGTGC",
			"CGAGG",
			"CGCGT",
			"CGTTA",
			"CGGTC",
			"CGCTG",
			"CGATT",
			"CTCAA",
			"CTAAC",
			"CTTAG",
			"CTGAT",
			"CTACA",
			"CTGCG",
			"CTTCT",
			"CTTGA",
			"CTGGC",
			"CTCGG",
			"CTAGT",
			"CTGTA",
			"CTTTC",
			"CTATG",
			"CTCTT",
			"GACAA",
			"GAAAC",
			"GATAG",
			"GAGAT",
			"GAACA",
			"GAGCG",
			"GATCT",
			"GATGA",
			"GAGGC",
			"GACGG",
			"GAAGT",
			"GAGTA",
			"GATTC",
			"GAATG",
			"GACTT",
			"GCAAA",
			"GCCAC",
			"GCGAG",
			"GCTAT",
			"GCACC",
			"GCTCG",
			"GCGCT",
			"GCGGA",
			"GCTGC",
			"GCAGG",
			"GCCGT",
			"GCTTA",
			"GCGTC",
			"GCCTG",
			"GCATT",
			"GGTAA",
			"GGCAG",
			"GGAAT",
			"GGTCC",
			"GGACG",
			"GGCCT",
			"GGCGA",
			"GGAGC",
		};
		#endregion

        #region STRT 2.0
        public static string[] STRT_v2 = new string[]
		{
            "TTTAGG",
            "ATTCCA",
            "GCTCAA",
            "CATCCC",
            "TTGGAC",
            "CTGTGT",
            "GGACAT",
            "CAAAGT",
            "AAGCGG",
            "AATAAA",
            "GAGGAG",
            "GGTACA",
            "AGCGAG",
            "GTCGGT",
            "ATTTGC",
            "AGGACT",
            "GCCCTC",
            "TCGTAA",
            "CCAGAC",
            "TATGTA",
            "ACAATA",
            "ATGCTT",
            "AGTTTA",
            "CACAAG",
            "ATCAAC",
            "TAGTCG",
            "TAGAGA",
            "GTCCCG",
            "TACTTC",
            "AAAGTT",
            "TAAGGG",
            "GTTGCC",
            "AAGTAC",
            "GATCTT",
            "TTAACT",
            "GCGAAT",
            "CCGCTA",
            "TGAAGC",
            "ATACAG",
            "CTTCTG",
            "GAGATC",
            "CCGACG",
            "CTCCAT",
            "AAAACG",
            "TAGCAT",
            "TCGGGT",
            "GTGGTA",
            "CCTAGA",
            "GGGTTT",
            "ATGGCG",
            "TTCATA",
            "AACGCC",
            "GGCTGC",
            "GCTGTG",
            "AGATGG",
            "GTAATG",
            "AGGGTC",
            "ATCTCT",
            "GCCTAG",
            "TCAAAG",
            "CATGAT",
            "TGTGCG",
            "GCAGGA",
            "TCTACC",
            "AGTCGT",
            "CGTGGC",
            "GCGTCC",
            "GAACGC",
            "ACTTAT",
            "TGGATG",
            "TATTGT",
            "ACGTTG",
            "GAATTA",
            "CCATCT",
            "TGATCA",
            "CGTATT",
            "CGGCAG",
            "GACACT",
            "TTCCGC",
            "CTCGCA",
            "GTATAC",
            "TGTCAC",
            "TGCGGA",
            "ACGAGC",
            "ACACCC",
            "CGCTTG",
            "TGCAAT",
            "CAACAA",
            "CTGAAA",
            "AACCTA",
            "ACCTGA",
            "TCACTT",
            "GGGCGA",
            "CGCACC",
            "CGAGTA",
            "CCTTTC"
		};
        #endregion

        #region NO_BARCODES
        public static string[] NO_BARCODES = new string[] { Barcodes.NOBARCODE };
        #endregion
    }

    public class STRTv1Barcodes : Barcodes
    {
        public STRTv1Barcodes() : base("v1", Barcodes.STRT_v1)
        {
            SetupPlate();
        }
    }

    public class PE_8Barcodes : Barcodes
    {
        public PE_8Barcodes() : base("PE_8", Barcodes.PE_8)
        {
            this.m_WellIds = new string[] { "S1", "S2", "S3", "S4", "S5", "S6", "S7", "S8" }; 
			this.m_TSSeq = "";
            this.m_TSTrimNt = ' ';
        }
    }

	public class LineageBarcodes : Barcodes
	{
		public LineageBarcodes() : base("Lin8", Barcodes.PE_8)
		{
			this.m_TSSeq = "TTAA";
            this.m_TSTrimNt = ' ';
        }
	}
	
	public class STRTv2Barcodes : Barcodes
    {
        public STRTv2Barcodes() : base("v2", Barcodes.STRT_v2)
        {
            SetupPlate();
        }
    }

    public class STRTv3Barcodes : Barcodes
    {
        public STRTv3Barcodes() : base("v3", Barcodes.STRT_v2)
        {
            this.m_UMIPos = 0;
            this.m_UMILen = 4;
            this.m_BarcodePos = 4;
            SetupPlate();
        }
    }

    public class NoBarcodes : Barcodes
    {
        public NoBarcodes() : base("No", NO_BARCODES)
        {
            this.m_TSSeq = "";
            this.m_TSTrimNt = ' ';
            this.m_WellIds = new string[] { "Sample1" };
        }
        public override void MakeBcSeqToBcIdxMap()
        {
            bcSeqToBcIdxMap = new Dictionary<string, int>();
            bcSeqToBcIdxMap[""] = 0;
        }
    }

    public class CustomBarcodes : Barcodes
    {
        public CustomBarcodes(string barcodeSetName) : base(barcodeSetName, new string[] {"NOBAR"})
        {
            m_TSSeq = "";
            m_TSTrimNt = ' ';
            string path = PathHandler.MakeBarcodeFilePath(barcodeSetName);
            if (!File.Exists(path))
                throw new FileNotFoundException("ERROR: Can not find barcode file " + path);
            List<string> sampleIds = new List<string>();
            List<string> barcodes = new List<string>();
            int bcSeqLen = 0;
            using (StreamReader reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine().Trim()).StartsWith("#"))
                {
                    line = line.Replace(" ", "");
                    if (line.StartsWith("#remove=") && line.Length > 8)
                        m_TSSeq = line.Substring(8);
                    else if (line.StartsWith("#trim=") & line.Length > 6)
                        m_TSTrimNt = line[6];
                    else if (line.StartsWith("#umipos="))
                    {
                        m_UMIPos = int.Parse(line.Substring(8));
                    }
                    else if (line.StartsWith("#umilen="))
                    {
                        m_UMILen = int.Parse(line.Substring(8));
                    }
                    else if (line.StartsWith("#barcodepos="))
                        m_BarcodePos = int.Parse(line.Substring(12));
                    else if (line.StartsWith("#insertpos="))
                        m_InsertStart = int.Parse(line.Substring(11));
                    else if (line.StartsWith("#allowsinglemutations"))
                        AllowSingleMutations = true;
                    else if (line.StartsWith("#prefixread2="))
                        m_PrefixRead2 = int.Parse(line.Substring(13));
                    else if (line.StartsWith("#prefixread3="))
                        m_PrefixRead3 = int.Parse(line.Substring(13));
                }
                while (line != null)
                {
                    line = line.Trim();
                    if (line.Length > 0 && !line.StartsWith("#"))
                    {
                        string[] fields = line.Split('\t');
                        if (fields.Length == 1)
                            fields = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (fields.Length != 2)
                            throw new BarcodeFileException("ERROR: Barcode file definition lines should be SampleId TAB Barcode:" + line);
                        string sampleId = fields[0];
                        string bc = fields[1];
                        if (bcSeqLen == 0) bcSeqLen = bc.Length;
                        else if (bcSeqLen != bc.Length)
                            throw new BarcodeFileException("ERROR: Barcodes have different lengths: " + path);
                        if (sampleIds.Contains(sampleId))
                            throw new BarcodeFileException("ERROR: SampledIds in barcode file must be unique: " + sampleId);
                        sampleIds.Add(sampleId);
                        barcodes.Add(bc);
                    }
                    line = reader.ReadLine();
                }
            }
            m_WellIds = sampleIds.ToArray();
            Seqs = barcodes.ToArray();
        }
    }

}
