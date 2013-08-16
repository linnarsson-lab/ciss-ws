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
        protected string[] m_Seqs;
        public string[] Seqs { get { return m_Seqs; } }
        public int Count { get { return m_FirstNegativeBcIdx; } }
        public int AllCount { get { return m_Seqs.Length; } } // Includes negative barcodes
        protected int m_BarcodePos = 0;
        public int BarcodePos { get { return m_BarcodePos; } }
        public int BarcodeEndPos { get { return m_BarcodePos + BarcodeLen; } }

        protected string[] m_WellIds = null;
        protected string[] m_SpeciesByWell = null;
        public string[] SpeciesByWell { get { return m_SpeciesByWell; } }
        public static int MaxCount { get { return 96; } }

        private Dictionary<string, string[]> AnnotationsByWell = new Dictionary<string, string[]>();

        protected int m_BarcodeLen;
        public int BarcodeLen { get { return m_BarcodeLen; } }

        protected string m_Name;
        public string Name { get { return m_Name; } }

        protected int m_FirstNegativeBcIdx;
        public int FirstNegativeBcIdx { get { return m_FirstNegativeBcIdx; } }

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

        public int InsertStart { get { return m_InsertStart; } }
        protected int m_InsertStart = 0;

        public int BarcodeFieldLen { get { return (UMILen > 0)? (1 + UMILen + BarcodeLen) : BarcodeLen; } }

        /// <summary>
        /// Set to true to allow extraction step to correct single base substitutions in barcodes.
        /// </summary>
        public bool AllowSingleMutations = false;
        private Dictionary<string, int> barcodeToIdx;
        public int NOBARIdx = 0;
        public static readonly string NOBARCODE = "NOBAR";
        public int GetBcIdx(string barcode)
        {
            if (barcodeToIdx == null)
            {
                barcodeToIdx = new Dictionary<string, int>();
                for (int bcIdx = 0; bcIdx < m_Seqs.Length; bcIdx++)
                    barcodeToIdx[m_Seqs[bcIdx]] = bcIdx;
                barcodeToIdx[NOBARCODE] = 0;
            }
            return barcodeToIdx[barcode];
        }

        protected string m_TSSeq = "GGG";
        public string TSSeq { get { return m_TSSeq; } }
        protected char m_TSTrimNt = 'G';
        public char TSTrimNt { get { return m_TSTrimNt; } }

        public virtual Dictionary<string, int> GetBcWTSSeqToBcIdxMap()
        {
            Dictionary<string, int> bcWTSSeqToBcIdxMap = new Dictionary<string, int>();
            for (int bcIdx = 0; bcIdx < Count; bcIdx++)
            {
                string bcWTSSeq = m_Seqs[bcIdx] + m_TSSeq;
                bcWTSSeqToBcIdxMap[bcWTSSeq] = bcIdx;
                if (AllowSingleMutations)
                {
                    for (int p = 0; p < BarcodeLen; p++)
                    {
                        foreach (char subNt in new char[] { 'A', 'C', 'G', 'T' })
                        {
                            if (bcWTSSeq[p] != subNt)
                            {
                                char[] subS = bcWTSSeq.ToCharArray();
                                subS[p] = subNt;
                                bcWTSSeqToBcIdxMap[new string(subS)] = bcIdx;
                            }
                        }
                    }
                }
            }
            return bcWTSSeqToBcIdxMap;
        }

        public virtual string[] GetBarcodesWithTSSeq()
        {
            string[] barcodesWithTSSeq = new string[Count];
            for (int i = 0; i < barcodesWithTSSeq.Length; i++)
            {
                barcodesWithTSSeq[i] = m_Seqs[i] + m_TSSeq;
            }
            return barcodesWithTSSeq;
        }

        public virtual int GetLengthOfBarcodesWithTSSeq()
        {
            return BarcodeLen + m_TSSeq.Length;
        }
        /// <summary>
        /// Find the position after UMI, barcode and any GGG-triple, or specified InsertStart, whichever is highest
        /// </summary>
        /// <returns>Positions where actual sequence should start</returns>
        public virtual int GetInsertStartPos()
        {
            return Math.Max(m_BarcodePos + GetLengthOfBarcodesWithTSSeq(), Math.Max(m_UMIPos + m_UMILen, m_InsertStart));
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
            bcIdx = GetBcIdx(readId.Substring(readId.Length - m_BarcodeLen));
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
        public static int STRT_v1_FirstNegBarcodeIndex = 96;
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
			// negative barcodes
			/*
 			"GGTGG",
            "GGATA",
			"GGCTC",
			"GGGTG",
			"GGTTT",
			"GTGAA",
			"GTTAC",
			"GTAAG",
			"GTCAT",
			"GTTCA",
			"GTGCC",
			"GTCCG",
			"GTACT",
			"GTAGA",
			"GTCGC",
			"GTTGT",
			"GTCTA",
			"GTATC",
			"GTTTG",
			"GTGTT"
            */
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
        public STRTv1Barcodes()
        {
            this.m_Seqs = Barcodes.STRT_v1;
            this.m_BarcodeLen = 5;
            this.m_FirstNegativeBcIdx = Barcodes.STRT_v1_FirstNegBarcodeIndex;
            this.m_Name = "v1";
            SetupPlate();
        }
    }

    public class PE_8Barcodes : Barcodes
    {
        public PE_8Barcodes()
        {
            this.m_Seqs = Barcodes.PE_8;
            this.m_BarcodeLen = 6;
            this.m_FirstNegativeBcIdx = m_Seqs.Length;
            this.m_Name = "PE_8";
            this.m_WellIds = new string[] { "S1", "S2", "S3", "S4", "S5", "S6", "S7", "S8" }; 
			this.m_TSSeq = "";
            this.m_TSTrimNt = ' ';
        }
    }

	public class LineageBarcodes : Barcodes
	{
		public LineageBarcodes()
		{
			this.m_Seqs = Barcodes.PE_8;
			this.m_BarcodeLen = 6;
			this.m_FirstNegativeBcIdx = m_Seqs.Length;
			this.m_Name = "Lin8";
			this.m_TSSeq = "TTAA";
            this.m_TSTrimNt = ' ';
        }
	}
	
	public class STRTv2Barcodes : Barcodes
    {
        public STRTv2Barcodes()
        {
            this.m_Seqs = Barcodes.STRT_v2;
            this.m_BarcodeLen = 6;
            this.m_FirstNegativeBcIdx = m_Seqs.Length;
            this.m_Name = "v2";
            SetupPlate();
        }
    }

    public class STRTv3Barcodes : Barcodes
    {
        public STRTv3Barcodes()
        {
            this.m_Seqs = Barcodes.STRT_v2;
            this.m_BarcodeLen = 6;
            this.m_FirstNegativeBcIdx = m_Seqs.Length;
            this.m_Name = "v3";
            this.m_UMIPos = 0;
            this.m_UMILen = 4;
            this.m_BarcodePos = 4;
            SetupPlate();
        }
    }

    public class NoBarcodes : Barcodes
    {
        public NoBarcodes()
        {
            this.m_Seqs = Barcodes.NO_BARCODES;
            this.m_BarcodeLen = 5;
            this.m_FirstNegativeBcIdx = m_Seqs.Length;
            this.m_Name = "No";
            this.m_TSSeq = "";
            this.m_TSTrimNt = ' ';
            this.m_WellIds = new string[] { "Sample1" };
        }
        public override Dictionary<string, int> GetBcWTSSeqToBcIdxMap()
        {
            Dictionary<string, int> b2Idx = new Dictionary<string, int>();
            b2Idx[""] = 0;
            return b2Idx;
        }
        public override string[] GetBarcodesWithTSSeq()
        {
            return new string[] { "" };
        }
        public override int GetLengthOfBarcodesWithTSSeq()
        {
            return 0;
        }
    }

    public class CustomBarcodes : Barcodes
    {
        public CustomBarcodes(string barcodeSetName)
        {
            m_Name = barcodeSetName;
            m_BarcodeLen = 0;
            m_TSSeq = "";
            m_TSTrimNt = ' ';
            string path = PathHandler.MakeBarcodeFilePath(barcodeSetName);
            if (!File.Exists(path))
                throw new FileNotFoundException("ERROR: Can not find barcode file " + path);
            List<string> sampleIds = new List<string>();
            List<string> barcodes = new List<string>();
            using (StreamReader reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine().Trim()).StartsWith("#"))
                {
                    line = line.Replace(" ", "").ToLower();
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
                        if (m_BarcodeLen == 0) m_BarcodeLen = bc.Length;
                        else if (m_BarcodeLen != bc.Length)
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
            m_Seqs = barcodes.ToArray();
            m_FirstNegativeBcIdx = m_Seqs.Length;
        }
    }

}
