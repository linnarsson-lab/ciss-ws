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
    
    /// <summary>
    /// Handler of barcode set definitions, as well as cell specific annotations from any layout file
    /// </summary>
	public abstract class Barcodes
	{
        private string[] m_Seqs;
        public string[] Seqs { 
            get { return m_Seqs; }
            protected set {
                m_Seqs = value;
                if (m_BarcodeLen == 0)
                    m_BarcodeLen = m_Seqs[0].Length;
                MakeUsedBcSeqToBcIdxMap();
            }
        }
        public int Count { get { return m_Seqs.Length; } }

        protected int m_InsertRead = 1;
        public int InsertRead { get { return m_InsertRead; } protected set { m_InsertRead = value; } }
        protected int m_BarcodeRead = 2;
        public int BarcodeRead { get { return m_BarcodeRead; } protected set { m_BarcodeRead = value; } }
        protected int m_Barcode2Read = 0;
        public int Barcode2Read { get { return m_Barcode2Read; } protected set { m_Barcode2Read = value; } }
        protected int m_UMIRead = 1;
        public int UMIRead { get { return m_UMIRead; } protected set { m_UMIRead = value; } }
        protected bool m_KeepRead1 = false;
        public bool KeepRead1 { get { return m_KeepRead1; } protected set { m_KeepRead1 = value; } }
        protected bool m_KeepRead2 = false;
        public bool KeepRead2 { get { return m_KeepRead2; } protected set { m_KeepRead2 = value; } }
        protected bool m_KeepRead3 = false;
        public bool KeepRead3 { get { return m_KeepRead3; } protected set { m_KeepRead3 = value; } }
        public bool NeedRead(int readno)
        {
            if (readno == 1) return (InsertRead == 1 || BarcodeRead == 1 || Barcode2Read == 1 || UMIRead == 1 || KeepRead1);
            if (readno == 2) return (InsertRead == 2 || BarcodeRead == 2 || Barcode2Read == 2 || UMIRead == 2 || KeepRead2);
            if (readno == 3) return (InsertRead == 3 || BarcodeRead == 3 || Barcode2Read == 3 || UMIRead == 3 || KeepRead3);
            return false;
        }

        protected int BackOffsetToAfterReadId = -1;
        protected int BackOffsetToUMIEndPos = 0;

        protected int m_BarcodePos = 0;
        public int BarcodePos { get { return m_BarcodePos; } }
        protected int m_BarcodeLen = 0;
        public int BarcodeLen { get { return m_BarcodeLen; } }
        protected int m_Barcode2Pos = 0;
        public int Barcode2Pos { get { return m_Barcode2Pos; } }
        protected int m_Barcode2Len = 0;
        public int Barcode2Len { get { return m_Barcode2Len; } }
        public bool UseNoBarcodes;
        public bool IncludeNonPF = false;
        public static readonly string NOBARCODE = "NOBAR";

        protected int m_MaxTotalLen = 0;
        public int VerifyTotalLen(int len)
        {
            if (m_MaxTotalLen > 0)
                return Math.Min(m_MaxTotalLen, len);
            return len;
        }

        protected string[] m_WellIds = null;
        protected string[] m_SpeciesByWell = null;
        public string[] SpeciesByWell { get { return m_SpeciesByWell; } }
        public string[] WellIds { get { return m_WellIds; } }

        private Dictionary<string, string[]> AnnotationsByWell = new Dictionary<string, string[]>();

        protected string m_Name;
        public string Name { get { return m_Name; } }

        protected int m_UMIPos = 0;
        public int UMIPos { get { return m_UMIPos; } protected set { m_UMIPos = value; } }
        protected int m_UMILen = 0;
        public int UMILen { get { return m_UMILen; } protected set { m_UMILen = value; m_UMIFieldLen = value; } }
        /// <summary>
        /// Same as UMILen if UMIMask is not used, otherwise less
        /// </summary>
        protected int m_UMIFieldLen = 0;
        /// <summary>
        /// Pattern 'NNN-NN' where unused nts in UMI are indicated with a '-'
        /// </summary>
        protected string m_UMIMask = null;
        public int UMICount { get { return 1 << (2 * m_UMILen); } }
        public bool HasUMIs { get { return m_UMILen > 0; } }
        /// <summary>
        /// Iterate the positions of the read which have valid UMI bases
        /// </summary>
        /// <returns></returns>
        public IEnumerable<int> ReadUMIPositions()
        {
            int p = 0;
            for (int i = m_UMIPos; i < m_UMIPos + m_UMIFieldLen; i++)
                if (m_UMIMask == null || m_UMIMask[p++] != '-')
                    yield return i;
        }

        protected int m_PrefixRead2 = 0;
        public int PrefixRead2 { get { return m_PrefixRead2; } }
        protected int m_PrefixRead3 = 0;
        public int PrefixRead3 { get { return m_PrefixRead3; } }
        public bool NeedRead2Or3 { get { return m_PrefixRead2 > 0 || m_PrefixRead3 > 0; } }
        public char HighestNeededReadNo {
            get {

                int newMax = Math.Max(InsertRead, Math.Max(UMIRead, Math.Max(BarcodeRead, Barcode2Read)));
                int oldMax =(m_PrefixRead3 > 0) ? 3 : (m_PrefixRead2 > 0) ? 2 : 1;
                return Math.Max(oldMax, newMax).ToString()[0];
            }
        }

        /// <summary>
        /// InsertStart, may begin with TSS G:s
        /// </summary>
        public int InsertOrGGGPos { get; protected set; }

        /// <summary>
        /// Set to true to allow extraction step to correct single base substitutions in barcodes.
        /// </summary>
        public bool AllowSingleMutations = false;

        /// <summary>
        /// Maps all barcodes to the numerical index. If single mutations allowed, these keys are also included.
        /// </summary>
        protected Dictionary<string, int> usedBcSeqToBcIdxMap;

        protected string m_TSSeq = "GGG";
        public string TSSeq { get { return m_TSSeq; } }
        protected char m_TSTrimNt = 'G';
        public char TSTrimNt { get { return m_TSTrimNt; } }

        public Barcodes(string bcSetName)
        {
            UseNoBarcodes = false;
            m_Name = bcSetName;
        }
        public Barcodes(string bcSetName, string[] seqs)
        {
            UseNoBarcodes = false;
            m_Name = bcSetName;
            Seqs = seqs;
        }

        protected virtual void MakeUsedBcSeqToBcIdxMap()
        {
            usedBcSeqToBcIdxMap = new Dictionary<string, int>();
            for (int bcIdx = 0; bcIdx < Count; bcIdx++)
            {
                if (usedBcSeqToBcIdxMap.ContainsKey(m_Seqs[bcIdx]))
                    Console.WriteLine("ERROR: Identical barcodes (" + m_Seqs[bcIdx] + ") found in barcode set " + m_Name + "!");
				if (! IsUnused(bcIdx))
	                usedBcSeqToBcIdxMap[m_Seqs[bcIdx]] = bcIdx;
            }
            if (AllowSingleMutations)
                AddSingleMutations();
        }

        private void AddSingleMutations()
        {
            for (int bcIdx = 0; bcIdx < Count; bcIdx++)
            {
                if (m_Seqs[bcIdx] == NOBARCODE || IsUnused(bcIdx)) continue;
                foreach (string singleMutSeq in GenerateAllSingleMutations(m_Seqs[bcIdx]))
                {
                    if (m_Seqs.Contains(singleMutSeq))
                    {
                        Console.WriteLine("INFO: Avoiding singleMutation barcode " + singleMutSeq + " that matches a non-mutated barcode.");
                        continue;
                    }
                    int bcValue = usedBcSeqToBcIdxMap.ContainsKey(singleMutSeq) ? -1 : bcIdx;
                    usedBcSeqToBcIdxMap[singleMutSeq] = bcValue;
                }
            } // Remove cases where two singleMutations have the same sequence
            int nDupMut = 0;
            foreach (string bcs in usedBcSeqToBcIdxMap.Keys.ToArray())
                if (usedBcSeqToBcIdxMap[bcs] == -1)
                {
                    nDupMut++;
                    usedBcSeqToBcIdxMap.Remove(bcs);
                }
            if (nDupMut > 0)
                Console.WriteLine("INFO: Avoiding " + nDupMut + " singleMutation barcodes that match another singleMutation barcode.");
            Console.WriteLine("INFO: AllowSingleMutations generated " + usedBcSeqToBcIdxMap.Count + " index sequences to match.");
        }

        private IEnumerable<string> GenerateAllSingleMutations(string bcSeq)
        {
            for (int p = 0; p < BarcodeLen; p++)
            {
                foreach (char subNt in new char[] { 'A', 'C', 'G', 'T' })
                {
                    if (bcSeq[p] != subNt)
                    {
                        char[] subS = bcSeq.ToCharArray();
                        subS[p] = subNt;
                        yield return new string(subS);
                    }
                }
            }
        }

        /// <summary>
        /// Convert a barcode sequence (maybe concat of Idx1 + Idx2) to a numeric barcode index.
        /// Return -1 on no match, and 0 if no barcodes are in use.
        /// </summary>
        /// <param name="barcodeSeq"></param>
        /// <returns></returns>
        public int ExtractBcIdx(string barcodeSeq)
        {
			if (UseNoBarcodes)
				return 0;
            int bcIdx;
            if (usedBcSeqToBcIdxMap.TryGetValue(barcodeSeq, out bcIdx))
                return bcIdx;
            return -1;
        }

        public bool VerifyTS(string read, int maxTrimExtraGs, out int actualInsertPos)
        {
            actualInsertPos = InsertOrGGGPos + TSSeq.Length;
            if (read.Substring(InsertOrGGGPos, TSSeq.Length) != TSSeq)
                return false;
            while (maxTrimExtraGs > 0 && read.Length > actualInsertPos && read[actualInsertPos] == m_TSTrimNt)
            {
                actualInsertPos++;
                maxTrimExtraGs--;
            }
            return true;
        }

        /// <summary>
        /// Extract the barcode from the read, check that a required TS-'GGG' is there, and
        /// remove any trailing 'G':s, when applicable
        /// </summary>
        /// <param name="read">the full read (may be prefixed with index and/or PE read as specified by barcode set</param>
        /// <param name="maxTrimExtraGs">max extra 'G':s to remove</param>
        /// <param name="bcIdx">the detected barcode index, or -1 on failure</param>
        /// <param name="actualInsertPos">the position of the actual insert, after any GGG...</param>
        /// <returns>ReadStatus.VALID on successful detection of barcodes (and GGG)</returns>
        public int VerifyBarcodeAndTS(string read, int maxTrimExtraGs, out int bcIdx, out int actualInsertPos)
        {
            actualInsertPos = InsertOrGGGPos + TSSeq.Length;
            if (UseNoBarcodes)
                bcIdx = 0;
            else
            {
                if (!usedBcSeqToBcIdxMap.TryGetValue(read.Substring(BarcodePos, BarcodeLen), out bcIdx))
                {
                    bcIdx = -1;
                    return ReadStatus.NO_BC_OTHER;
                }
            }
            if (read.Substring(InsertOrGGGPos, TSSeq.Length) != TSSeq)
                return ReadStatus.NO_TSSEQ_OTHER;
            while (maxTrimExtraGs > 0 && read.Length > actualInsertPos && read[actualInsertPos] == m_TSTrimNt)
            {
                actualInsertPos++;
                maxTrimExtraGs--;
            }
            return ReadStatus.VALID;
        }

/*
        public virtual string GetUMIAndStripBcFromReadId(string readId, out int UMIIdx)
        {
            int p = (readId[readId.Length - m_BarcodeLen - 1] == '.') ? (readId.Length - BarcodeFieldLen) : (readId.Length - UMILen);
            UMIIdx = 0;
            for (int i = 0; i < UMILen; i++)
            {
                UMIIdx = (UMIIdx << 2) | ("ACGT".IndexOf(readId[p++]));
            }
            return readId.Substring(0, readId.Length - BarcodeFieldLen - 1);
        }
*/

        public string MakeUMISeq(int UMIIdx)
        {
            char[] UMISeq = new char[UMILen];
            int p = UMILen - 1;
            for (int i = 0; i < UMILen; i++)
            {
                UMISeq[p--] = "ACGT"[UMIIdx & 3];
                UMIIdx = UMIIdx >> 2;
            }
            return new string(UMISeq);
        }

        /// <summary>
        /// All names are converted to lower case
        /// </summary>
        /// <returns></returns>
        public static string[] GetAllKnownBarcodeSetNames()
        {
            string[] allBarcodeSetNames = PathHandler.GetAllCustomBarcodeSetNames();
            Array.Resize(ref allBarcodeSetNames, allBarcodeSetNames.Length + 6);
            Array.Copy(new string[] { "v1", "v2", "v3", "pe_8", "no", "lin8" }, 0, allBarcodeSetNames, allBarcodeSetNames.Length - 6, 6);
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
			if (speciesId == "unused")
				return false;
			if (speciesId == "empty" && !strict)
				return true;
            return speciesId.StartsWith(genome.Abbrev.ToLower()) || speciesId.StartsWith(genome.Name.ToLower())
                   || speciesId.StartsWith(genome.LatinName.ToLower());
        }

		public bool IsUnused(int bcIdx)
		{
			return m_SpeciesByWell != null && m_SpeciesByWell[bcIdx] == "unused";
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

        /// <summary>
        /// Return the wellid, e.g. "A01"
        /// </summary>
        /// <param name="wellIdx"></param>
        /// <returns></returns>
        public string GetWellId(int wellIdx)
        {
            return m_WellIds[wellIdx];
        }

        public int GetBcIdxFromWellId(string wellId)
        {
            return Array.IndexOf(m_WellIds, wellId);
        }
        public int GetBcIdxFromBarcode(string barcode)
        {
            return Array.IndexOf(m_Seqs, barcode);
        }

        /// <summary>
        /// Get the bcIdx on which a map file should be analyzed. This is not same as
        /// the input BcIdx if remappings are defined in the 'Merge' column of the PlateLayout.
        /// Merge keys may be either
        ///    a) the wellId (the merging well may be empty in 'Merge' column),
        /// or b) a subsample name. The merging well will be the one with the first occurence of the subsample name. 
        /// </summary>
        /// <param name="bcIdx">bcIdx taken from (.map) filename</param>
        /// <returns>bcIdx that the data should be merged into</returns>
        public int GetWellIdxFromBcIdx(int bcIdx)
        {
            int mergeBcIdx = bcIdx;
            if (AnnotationsByWell.ContainsKey("Merge"))
            {
                string mergeSampleId = AnnotationsByWell["Merge"][bcIdx];
                if (mergeSampleId != "")
                {
                    // First try direct reference to another well, e.g. 'A01'
                    mergeBcIdx = Array.FindIndex(m_WellIds, (id) => id == mergeSampleId);
                    if (mergeBcIdx == -1) // Then try reference to a merge subsample name, e.g. 'Sample1'. The first well of that name will be used
                        mergeBcIdx = Array.FindIndex(AnnotationsByWell["Merge"], (id) => id == mergeSampleId);
                    if (mergeBcIdx == -1) // First occurence of this reference - point to itself
                        mergeBcIdx = bcIdx;
                }
            }
            return mergeBcIdx;
        }

        /// <summary>
        /// Return a well id, like 'A02', or if this well is merged (by layout 'Merge' column) into another well, return parenthesized well id
        /// </summary>
        /// <param name="bcIdx"></param>
        /// <returns></returns>
        public string GetWellSymbolFromBcIdx(int bcIdx)
        {
            int wellIdx = GetWellIdxFromBcIdx(bcIdx);
            string wellId = GetWellId(wellIdx);
            return (wellIdx != bcIdx) ? "(" + wellId + ")" : wellId;
        }

        /// <summary>
        /// Check if a particular well annotation exists. Case insensitive
        /// </summary>
        /// <param name="annotationTitle"></param>
        /// <returns></returns>
        public string TestAnnotation(string annotationTitle)
        {
            if (AnnotationsByWell.ContainsKey(annotationTitle))
                return annotationTitle;
            if (AnnotationsByWell.ContainsKey(annotationTitle.ToLower()))
                return annotationTitle.ToLower();
            if (AnnotationsByWell.ContainsKey(annotationTitle.ToUpper()))
                return annotationTitle.ToUpper();
            return null;
        }
        /// <summary>
        /// Return the names of all well annotations as a list.
        /// </summary>
        /// <returns></returns>
        public List<string> GetAnnotationTitles()
        {
            return AnnotationsByWell.Keys.ToList();
        }
        public string GetAnnotation(string annotationTitle, int wellIdx)
        {
            return AnnotationsByWell[annotationTitle][wellIdx];
        }

        /// <summary>
        /// Read plate layout from DB if props.InsertCellDBData == true, else or on failure read from layoutpath.
        /// Return value is species/build name(s) as defined by layout.
        /// </summary>
        /// <param name="plateid"></param>
        /// <param name="layoutpath"></param>
        /// <returns>Ids of all the species/builds that are in the layout</returns>
        public string[] ParsePlateLayout(string plateid, string layoutpath)
        {
            PlateLayout plateLayout = PlateLayout.GetPlateLayout(plateid, layoutpath);
            if (plateLayout == null) return new string[] {};
            AnnotationsByWell.Clear();
            m_SpeciesByWell = new string[m_WellIds.Length];
			for (int i = 0; i < m_SpeciesByWell.Length; i++)
				m_SpeciesByWell[i] = "unused"; // "empty";
            if (m_WellIds.Length < plateLayout.Length)
            {
                string xtra = (plateLayout.Filename == "C1 database") ? "It may help to reload plate with C1SeqPlateLoader.exe" : "";
                throw new SampleLayoutFileException("There are more wells in layout " + plateLayout.Filename + " (" + plateLayout.Length +
                                                    ") then defined in barcode set " + this.Name + "(" + this.Count + ") " + xtra);
            }
            foreach (string annotation in plateLayout.GetAnnotations())
                AnnotationsByWell[annotation] = new string[m_WellIds.Length];
            foreach (KeyValuePair<string, string> pair in plateLayout.SpeciesIdBySampleId)
            {
                string sampleId = pair.Key;
                int wellIdx = Array.FindIndex(m_WellIds, (id) => id == sampleId);
                if (wellIdx == -1)
                    throw new SampleLayoutFileException("SampleId " + sampleId + " is not in barcode set " + this.Name + " in file " + plateLayout.Filename);
                m_SpeciesByWell[wellIdx] = pair.Value;
				foreach (string annotation in plateLayout.GetAnnotations()) {
					string annot = plateLayout.GetSampleAnnotation(annotation, sampleId);
					if (annot == "")
						annot = "-";
					AnnotationsByWell[annotation][wellIdx] = annot;
				}
            }
            return plateLayout.BuildIds;
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
            this.UMIPos = 0;
            this.UMILen = 4;
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
            this.m_WellIds = new string[] { "Sample" };
            UseNoBarcodes = true;
        }
        protected override void MakeUsedBcSeqToBcIdxMap()
        {
            usedBcSeqToBcIdxMap = new Dictionary<string, int>();
            usedBcSeqToBcIdxMap[""] = 0;
        }

/*        public override string GetUMIAndStripBcFromReadId(string readId, out int UMIIdx)
        {
            UMIIdx = 0;
            if (BackOffsetToAfterReadId == -1)
            {
                int sp = readId.LastIndexOf('_');
                BackOffsetToAfterReadId = readId.Length - ((sp > -1) ? sp : 0);
                int dp = readId.LastIndexOf('.');
                BackOffsetToUMIEndPos = readId.Length - ((dp > -1)? dp : 0);
            }
            for (int p = readId.Length - BackOffsetToAfterReadId + 1; p < readId.Length - BackOffsetToUMIEndPos; p++)
            {
                UMIIdx = (UMIIdx << 2) | ("ACGT".IndexOf(readId[p]));
            }
            return readId.Substring(0, readId.Length - BackOffsetToAfterReadId);
        }*/
    }

    public class NoUMIsNoBarcodes : NoBarcodes
    {
/*        public override string GetUMIAndStripBcFromReadId(string readId, out int UMIIdx0)
        {
            UMIIdx0 = 0;
            if (BackOffsetToAfterReadId == -1)
            {
                int sp = readId.LastIndexOf('_');
                BackOffsetToAfterReadId = readId.Length - ((sp > -1) ? sp : 0);
            }
            return readId.Substring(0, readId.Length - BackOffsetToAfterReadId);
        }*/
    }

    public class CustomBarcodes : Barcodes
    {
        public CustomBarcodes(string barcodeSetName) : base(barcodeSetName)
        {
            m_TSSeq = "";
            m_TSTrimNt = ' ';
            string path = PathHandler.MakeBarcodeFilePath(barcodeSetName);
            if (!File.Exists(path))
                throw new BarcodeFileException("ERROR: Can not find barcode file " + path + " Please correct in Sanger DB or make this file.");
            using (StreamReader reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (!line.StartsWith("#")) break;
                    line = line.Replace(" ", "");
                    if (line.StartsWith("#remove=") && line.Length > 8) m_TSSeq = line.Substring(8);
                    else if (line.StartsWith("#trim=") & line.Length > 6) m_TSTrimNt = line[6];
                    else if (line.StartsWith("#keepread1")) KeepRead1 = true;
                    else if (line.StartsWith("#keepread2")) KeepRead2 = true;
                    else if (line.StartsWith("#keepread3")) KeepRead3 = true;
                    else if (line.StartsWith("#indexread=")) BarcodeRead = int.Parse(line.Substring(11));
                    else if (line.StartsWith("#indexstart=")) m_BarcodePos = int.Parse(line.Substring(12));
                    else if (line.StartsWith("#indexlen=")) m_BarcodeLen = int.Parse(line.Substring(10));
                    else if (line.StartsWith("#index2read=")) Barcode2Read = int.Parse(line.Substring(12));
                    else if (line.StartsWith("#index2start=")) m_Barcode2Pos = int.Parse(line.Substring(13));
                    else if (line.StartsWith("#index2len=")) m_Barcode2Len = int.Parse(line.Substring(11));
                    else if (line.StartsWith("#insertread=")) InsertRead = int.Parse(line.Substring(12));
                    else if (line.StartsWith("#insertstart=")) InsertOrGGGPos = int.Parse(line.Substring(13));
                    else if (line.StartsWith("#umiread=")) UMIRead = int.Parse(line.Substring(9));
                    else if (line.StartsWith("#umistart=")) UMIPos = int.Parse(line.Substring(10));
                    else if (line.StartsWith("#umilen=") && m_UMIMask == null) UMILen = int.Parse(line.Substring(8));
                    else if (line.StartsWith("#umimask="))
                    {
                        m_UMIMask = line.Substring(9);
                        m_UMIFieldLen = m_UMIMask.Length;
                        m_UMILen = m_UMIMask.Count(c => c != '-');
                    }
                    else if (line.StartsWith("#allowsinglemutations")) AllowSingleMutations = true;
                    else if (line.StartsWith("#nobarcode")) UseNoBarcodes = true;
                    else if (line.StartsWith("#noindex")) UseNoBarcodes = true;
                    else if (line.StartsWith("#includenonpf")) IncludeNonPF = true;
                    else if (line.StartsWith("#prefixread2=")) m_PrefixRead2 = int.Parse(line.Substring(13));
                    else if (line.StartsWith("#prefixread3=")) m_PrefixRead3 = int.Parse(line.Substring(13));
                    else if (line.StartsWith("#truncateat=")) m_MaxTotalLen = int.Parse(line.Substring(12));
                }
                if (UseNoBarcodes)
                    m_WellIds = new string[] { "Sample" };
                else
                    ReadWellsAndBarcodes(path, reader, line);
            }
        }

        private void ReadWellsAndBarcodes(string path, StreamReader reader, string nextLine)
        {
            if (nextLine == null)
                throw new BarcodeFileException("ERROR: Barcode file must define at least one well/barcode: " + path);
            int bcSeqLen = 0;
            List<string> sampleIds = new List<string>();
            List<string> barcodes = new List<string>();
            while (nextLine != null)
            {
                nextLine = nextLine.Trim();
                if (nextLine.Length > 0 && !nextLine.StartsWith("#"))
                {
                    string[] fields = nextLine.Split('\t');
                    if (fields.Length == 1)
                        fields = nextLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length != 2)
                        throw new BarcodeFileException("ERROR: Barcode file definition lines should be SampleId TAB Barcode:" + nextLine +
                                                       " Please correct: " + path);
                    string sampleId = fields[0];
                    string bc = fields[1];
                    if (bcSeqLen == 0) bcSeqLen = bc.Length;
                    else if (bcSeqLen != bc.Length)
                        throw new BarcodeFileException("ERROR: Barcodes have different lengths. Please correct: " + path);
                    if (sampleIds.Contains(sampleId))
                        throw new BarcodeFileException("ERROR: Duplicated SampleId in barcode file: " + sampleId + " Please correct: " + path);
                    sampleIds.Add(sampleId);
                    barcodes.Add(bc);
                }
                nextLine = reader.ReadLine();
            }
            m_WellIds = sampleIds.ToArray();
            Seqs = barcodes.ToArray();
        }
    }

}
