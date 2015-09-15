using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class ReadStatus
    {
        private static int ri = 0;
        public readonly static int VALID = ri++;
        public readonly static int MIXIN_SAMPLE_BC = ri++;
        public readonly static int UNKNOWN_BC = ri++;
        public readonly static int NO_BC_P1 = ri++;
        public readonly static int NO_BC_TN5 = ri++;
        public readonly static int NO_BC_MANY_A = ri++;
        public readonly static int NO_BC_MANY_C = ri++;
        public readonly static int NO_BC_MANY_G = ri++;
        public readonly static int NO_BC_MANY_T = ri++;
        public readonly static int NO_BC_PHIX = ri++;
        public readonly static int NO_BC_RNA45S = ri++;
        public readonly static int NO_BC_OTHER = ri++;
        public readonly static int SEQ_QUALITY_ERROR = ri++;
        public readonly static int COMPLEXITY_ERROR = ri++;
        public readonly static int TOO_LONG_TRAILING_PRIMER_SEQ = ri++;
        public readonly static int TOO_LONG_TRAILING_pApN = ri++;
        public readonly static int TOO_SHORT_INSERT = ri++;
        public readonly static int NO_TSSEQ_P1 = ri++;
        public readonly static int NO_TSSEQ_TN5 = ri++;
        public readonly static int NO_TSSEQ_MANY_A = ri++;
        public readonly static int NO_TSSEQ_MANY_C = ri++;
        public readonly static int NO_TSSEQ_MANY_G = ri++;
        public readonly static int NO_TSSEQ_MANY_T = ri++;
        public readonly static int NO_TSSEQ_OTHER = ri++;
        public readonly static int N_IN_UMI = ri++;
        public readonly static int LOW_QUALITY_IN_UMI = ri++;
        public readonly static int FORBIDDEN_INTERNAL_SEQ = ri++;
        public readonly static int P1_IN_READ = ri++;
        public readonly static int Count = ri;

        private static int[] UMIStatuses = { N_IN_UMI, LOW_QUALITY_IN_UMI };
        private static int[] NoBcStatuses = { MIXIN_SAMPLE_BC, UNKNOWN_BC, SEQ_QUALITY_ERROR, 
                                              NO_BC_MANY_A, NO_BC_MANY_C, NO_BC_MANY_G, NO_BC_MANY_T, NO_BC_P1, NO_BC_TN5,
                                              NO_BC_PHIX, NO_BC_RNA45S, NO_BC_OTHER };

        private readonly static Dictionary<int, string> categories = new Dictionary<int,string>()
        { { VALID, "VALID" }, {MIXIN_SAMPLE_BC, "MIXIN_SAMPLE"}, {UNKNOWN_BC, "UNKNOWN_BARCODE"}, 
          {SEQ_QUALITY_ERROR, "SEQ_QUALITY_ERROR"},
          {NO_BC_MANY_A, "NO_BARCODE-A_RICH"}, {NO_BC_MANY_C, "NO_BARCODE-C_RICH"},
          {NO_BC_MANY_G, "NO_BARCODE-G_RICH"}, {NO_BC_MANY_T, "NO_BARCODE-T_RICH"},
          {NO_BC_P1, "NO_BARCODE-CONTAINS_P1"}, {NO_BC_TN5,  "NO_BARCODE-CONTAINS_TN5"}, {NO_BC_PHIX, "NO_BARCODE-PHIX_SUBSEQ"},
          {NO_BC_RNA45S, "NO_BARCODE-RNA45S"}, {NO_BC_OTHER, "NO_BARCODE-UNIDENTIFIED"},
          {COMPLEXITY_ERROR,  "COMPLEXITY_ERROR"}, {TOO_LONG_TRAILING_PRIMER_SEQ, "TOO_LONG_TRAILING_PRIMER_SEQ"},
          {TOO_LONG_TRAILING_pApN, "TOO_LONG_pA_pN_TAIL"}, {TOO_SHORT_INSERT, "TOO_SHORT_INSERT"},
          {NO_TSSEQ_MANY_A, "NO_TSSEQ-A_RICH"}, {NO_TSSEQ_MANY_C, "NO_TSSEQ-C_RICH"}, 
          {NO_TSSEQ_MANY_G, "NO_TSSEQ-G_RICH"}, {NO_TSSEQ_MANY_T, "NO_TSSEQ-T_RICH"},
          {NO_TSSEQ_P1, "NO_TSSEQ-CONTAINS_P1"}, {NO_TSSEQ_TN5, "NO_TSSEQ-CONTAINS_TN5"},
          {NO_TSSEQ_OTHER, "NO_TSSEQ_UNIDENTIFIED"},
          {N_IN_UMI, "N_IN_UMI"}, {LOW_QUALITY_IN_UMI, "LOW_QUALITY_IN_UMI"},
          {FORBIDDEN_INTERNAL_SEQ, "FORBIDDEN_SEQ_IN_INSERT"}, {P1_IN_READ, "P1_PRIMER_IN_INSERT"} };

        private readonly static Dictionary<string, int> oldCategories = new Dictionary<string,int>() 
                { {"N_IN_RANDOM_TAG", N_IN_UMI}, {"LOW_QUALITY_IN_RANDOM_TAG", LOW_QUALITY_IN_UMI}, 
                  {"SAL1-T25_IN_READ", NO_BC_OTHER}, {"NO_BARCODE-CONTAINS_NNNA25", NO_BC_MANY_A },
                  {"NEGATIVE_BARCODE_ERROR", NO_BC_OTHER}, {"NO_BARCODE-CGACT25", NO_BC_MANY_C}, {"NO_BARCODE-NNNA25", NO_BC_MANY_A},
                  {"NO_BARCODE-SAL1-T25", NO_BC_OTHER}, {"NO_BARCODE-INTERNAL-T20", NO_BC_MANY_T},
                  {"NO_BARCODE-SOLEXA-ADP2_CONTAINING", NO_BC_OTHER}, {"NO_VALID_BARCODE-UNCHARACTERIZED", NO_BC_OTHER},
                  {"NO_BARCODE-CONTAINS_TN5_MOSAIC_END", NO_BC_TN5}, {"TSSEQ_MISSING-CONTAINS_TN5_MOSAIC_END", NO_TSSEQ_TN5},
                  {"TSSEQ_MISSING-CONTAINS_P1_PRIMER", NO_TSSEQ_P1}, {"NO_BARCODE-CONTAINS_P1_PRIMER", NO_BC_P1},
                  {"NO_BARCODE-CONTAINS_A(20)", NO_BC_MANY_A}, {"NO_BARCODE-CONTAINS_T(20)", NO_BC_MANY_T}, 
                  {"NO_BARCODE-CONTAINS_CGACT25", NO_BC_MANY_T}, {"NO_TSSEQ", NO_TSSEQ_OTHER}, 
                  {"TSSEQ_MISSING-CONTAINS_TN5", NO_TSSEQ_TN5}, {"TSSEQ_MISSING-CONTAINS_P1", NO_TSSEQ_P1},
                  {"TSSEQ_MISSING-CONTAINS_A(20)", NO_TSSEQ_MANY_A}, {"TSSEQ_MISSING-CONTAINS_T(20)", NO_TSSEQ_MANY_T} };

        public static string GetName(int readStatus)
        {
            return categories[readStatus];
        }
        public static bool IsUMICategory(int readStatus)
        {
            return UMIStatuses.Contains(readStatus);
        }
        public static bool IsBarcodedCategory(int readStatus)
        {
            return !NoBcStatuses.Contains(readStatus);
        }
        public static int Parse(string category)
        {
            int readStatus = ReadStatus.VALID;
            try
            {
                readStatus = categories.First(p => p.Value.Equals(category, StringComparison.CurrentCultureIgnoreCase)).Key;
            }
            catch (Exception)
            {
                oldCategories.TryGetValue(category, out readStatus);
            }
            return readStatus;
        }
    }

}
