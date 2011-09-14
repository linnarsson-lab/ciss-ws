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
    
    public class BarcodeMapper
    {
        public Dictionary<string, int> barcodeToIdx = new Dictionary<string, int>();
        public int NOBARIdx = 0;

        public BarcodeMapper(Barcodes barcodes)
        {
            for (int idx = 0; idx < barcodes.Count; idx++)
                barcodeToIdx[barcodes.Seqs[idx]] = idx;
            barcodeToIdx[Barcodes.NOBARCODE] = 0;
        }
        public int GetBarcodeIdx(string barcode)
        {
            return barcodeToIdx[barcode];
        }
    }

	public abstract class Barcodes
	{
        protected string[] m_Seqs;
        public string[] Seqs { get { return m_Seqs; } }
        public int Count { get { return m_FirstNegBarcodeIndex; } }
        public int AllCount { get { return m_Seqs.Length; } } // Includes negative barcodes
        protected int m_BarcodePos = 0;
        public int BarcodePos { get { return m_BarcodePos; } }
        protected string[] m_WellIds = null;
        protected string[] m_SpeciesByWell = null;
        public string[] SpeciesByWell { get { return m_SpeciesByWell; } }
        public static int MaxCount { get { return 96; } }

        private Dictionary<string, string[]> AnnotationsByWell = new Dictionary<string, string[]>();

        protected int m_SeqLength;
        public int SeqLength { get { return m_SeqLength; } }

        protected string m_Name;
        public string Name { get { return m_Name; } }

        protected int m_FirstNegBarcodeIndex;
        public int FirstNegBarcodeIndex { get { return m_FirstNegBarcodeIndex; } }

        protected bool m_HasRandomBarcodes = false;
        public bool HasRandomBarcodes { get { return m_HasRandomBarcodes; } }
        protected int m_RandomTagPos = 0;
        public int RandomTagPos { get { return m_RandomTagPos; } }
        protected int m_RandomTagLen = 0;
        public int RandomTagLen { get { return m_RandomTagLen; } }

        public static readonly string NOBARCODE = "NOBAR";

        protected string m_TSSeq = "GGG";
        public string TSSeq { get { return m_TSSeq; } }
        protected char m_TSTrimNt = 'G';
        public char TSTrimNt { get { return m_TSTrimNt; } }

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
            return SeqLength + m_TSSeq.Length;
        }
        public virtual int GetInsertStartPos()
        {
            return Math.Max(m_BarcodePos + GetLengthOfBarcodesWithTSSeq(), m_RandomTagPos + m_RandomTagLen); 
        }

        public static Barcodes GetBarcodes(string barcodeSet)
        {
            string lbc = barcodeSet.ToLower();
			if (lbc.Contains("v1"))
				return new STRTv1Barcodes();
			else if (lbc.Contains("v2"))
				return new STRTv2Barcodes();
			else if (lbc == "pe_8")
				return new PE_8Barcodes();
			else if (lbc.Contains("v3"))
				return new STRTv3Barcodes();
			else if (lbc.Contains("lin8"))
				return new LineageBarcodes();
			else if (lbc.Contains("no"))
				return new NoBarcodes();
            CustomBarcodes bc = new CustomBarcodes();
            bc.ReadBarcodesFromFile(barcodeSet);
            return bc;
        }

        public Barcodes()
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

        private bool GenomeMatchesWell(StrtGenome genome, int barcodeIdx, bool strict)
        {
            if (m_SpeciesByWell == null) return true;
            string speciesId = m_SpeciesByWell[barcodeIdx].ToLower();
            return (speciesId == "empty" && !strict)
                   || speciesId.StartsWith(genome.Abbrev.ToLower()) || speciesId.StartsWith(genome.Name.ToLower())
                   || speciesId.StartsWith(genome.LatinName.ToLower());
        }

        public bool HasSampleLayout()
        {
            return m_SpeciesByWell != null;
        }
        public int[] GenomeBarcodeIndexes(StrtGenome genome)
        {
            return GenomeBarcodeIndexes(genome, false);
        }
        /// <summary>
        /// </summary>
        /// <param name="genome">Defines species to pick wells for</param>
        /// <param name="strict">If true, only species wells are taken. If false, empty wells are included</param>
        /// <returns></returns>
        public int[] GenomeBarcodeIndexes(StrtGenome genome, bool strict)
        {
            List<int> indexes = new List<int>();
            for (int idx = 0; idx < m_Seqs.Length; idx++)
                if (GenomeMatchesWell(genome, idx, strict))
                    indexes.Add(idx);
            return indexes.ToArray();
        }
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
        public STRTv1Barcodes() : base()
        {
            this.m_Seqs = Barcodes.STRT_v1;
            this.m_SeqLength = 5;
            this.m_FirstNegBarcodeIndex = Barcodes.STRT_v1_FirstNegBarcodeIndex;
            this.m_Name = "v1";
        }
    }

    public class PE_8Barcodes : Barcodes
    {
        public PE_8Barcodes()
        {
            this.m_Seqs = Barcodes.PE_8;
            this.m_SeqLength = 6;
            this.m_FirstNegBarcodeIndex = m_Seqs.Length;
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
			this.m_SeqLength = 6;
			this.m_FirstNegBarcodeIndex = m_Seqs.Length;
			this.m_Name = "Lin8";
			this.m_TSSeq = "TTAA";
            this.m_TSTrimNt = ' ';
        }
	}
	
	public class STRTv2Barcodes : Barcodes
    {
        public STRTv2Barcodes() : base()
        {
            this.m_Seqs = Barcodes.STRT_v2;
            this.m_SeqLength = 6;
            this.m_FirstNegBarcodeIndex = m_Seqs.Length;
            this.m_Name = "v2";
        }
    }

    public class STRTv3Barcodes : Barcodes
    {
        public STRTv3Barcodes() : base()
        {
            this.m_Seqs = Barcodes.STRT_v2;
            this.m_SeqLength = 6;
            this.m_FirstNegBarcodeIndex = m_Seqs.Length;
            this.m_Name = "v3";
            this.m_HasRandomBarcodes = true;
            this.m_RandomTagPos = 0;
            this.m_RandomTagLen = 4;
            this.m_BarcodePos = 4;
        }
    }

    public class NoBarcodes : Barcodes
    {
        public NoBarcodes()
        {
            this.m_Seqs = Barcodes.NO_BARCODES;
            this.m_SeqLength = 5;
            this.m_FirstNegBarcodeIndex = m_Seqs.Length;
            this.m_Name = "No";
            this.m_TSSeq = "";
            this.m_TSTrimNt = ' ';
            this.m_WellIds = new string[] { "Sample1" };
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
        public CustomBarcodes()
        {
        }

        public void ReadBarcodesFromFile(string bcSetName)
        {
            m_Name = bcSetName;
            m_SeqLength = 0;
            m_TSSeq = "";
            m_TSTrimNt = ' ';
            string path = PathHandler.GetBarcodeFilePath(bcSetName);
            if (!File.Exists(path))
                throw new FileNotFoundException("ERROR: Can not find barcode file " + path);
            StreamReader reader = path.OpenRead();
            string line = reader.ReadLine();
            while (line.StartsWith("#"))
            {
                line = line.Trim().Replace(" ", "");
                if (line.StartsWith("#remove="))
                    m_TSSeq = line.Substring(8);
                else if (line.StartsWith("#trim="))
                    m_TSTrimNt = line[6];
                else if (line.StartsWith("#randomtagpos="))
                {
                    m_HasRandomBarcodes = true;
                    m_RandomTagLen = int.Parse(line.Substring(14));
                }
                else if (line.StartsWith("#randomtaglen="))
                {
                    m_RandomTagLen = int.Parse(line.Substring(14));
                }
                else if (line.StartsWith("#barcodepos="))
                    m_BarcodePos = int.Parse(line.Substring(12));
                line = reader.ReadLine();
            }
            List<string> sampleIds = new List<string>();
            List<string> barcodes = new List<string>();
            while (line != null)
            {
                line = line.Trim();
                if (line.Length > 0 && !line.StartsWith("#"))
                {
                    string[] fields = line.Split('\t');
                    if (fields.Length != 2)
                        throw new BarcodeFileException("ERROR: Barcode file definition lines should be SampleId TAB Barcode:" + line);
                    string sampleId = fields[0];
                    string bc = fields[1];
                    if (m_SeqLength == 0) m_SeqLength = bc.Length;
                    else if (m_SeqLength != bc.Length)
                        throw new BarcodeFileException("ERROR: Barcodes have different lengths: " + path);
                    sampleIds.Add(sampleId);
                    barcodes.Add(bc);
                }
                line = reader.ReadLine();
            }
            reader.Close();
            m_WellIds = sampleIds.ToArray();
            m_Seqs = barcodes.ToArray();
            m_FirstNegBarcodeIndex = m_Seqs.Length;
            Console.WriteLine("{0} custom {1}-mer barcodes imported from {2}.", Seqs.Length, SeqLength, path);
        }
    }

}
