using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class ReadStatus
    {
        public readonly static int VALID = 0;
        public readonly static int SEQ_QUALITY_ERROR = 1;
        public readonly static int NO_BC_CGACT25 = 2;
        public readonly static int NO_BC_INTERNAL_A20 = 3;
        public readonly static int NO_BC_P1 = 4;
        public readonly static int NO_BC_TN5 = 5;
        public readonly static int NO_BC_INTERNAL_T20 = 6;
        public readonly static int NO_BC_PHIX = 7;
        public readonly static int NO_BC_RNA45S = 8;
        public readonly static int NO_BC_OTHER = 9;
        public readonly static int NEGATIVE_BARCODE_ERROR = 10;
        public readonly static int COMPLEXITY_ERROR = 11;
        public readonly static int TOO_LONG_TRAILING_PRIMER_SEQ = 12;
        public readonly static int TOO_LONG_TRAILING_pApN = 13;
        public readonly static int TOO_SHORT_INSERT = 14;
        public readonly static int TSSEQ_MISSING_P1 = 15;
        public readonly static int TSSEQ_MISSING_TN5 = 16;
        public readonly static int TSSEQ_MISSING_INTERNAL_T20 = 17;
        public readonly static int TSSEQ_MISSING_INTERNAL_A20 = 18;
        public readonly static int TSSEQ_MISSING_OTHER = 19;
        public readonly static int N_IN_UMI = 20;
        public readonly static int LOW_QUALITY_IN_UMI = 21;
        public readonly static int FORBIDDEN_INTERNAL_SEQ = 22;
        public readonly static int P1_IN_READ = 23;
        public readonly static int Count = 24;

        private static int[] UMIStatuses = { N_IN_UMI, LOW_QUALITY_IN_UMI };
        private static int[] NoBcStatuses = { SEQ_QUALITY_ERROR, NO_BC_CGACT25, NO_BC_INTERNAL_A20, NO_BC_P1, NO_BC_TN5,
                                              NO_BC_INTERNAL_T20, NO_BC_PHIX, NO_BC_RNA45S, NO_BC_OTHER, NEGATIVE_BARCODE_ERROR };

        private readonly static string[] categories = new string[]
              { "VALID", "SEQ_QUALITY_ERROR",
                "NO_BARCODE-CONTAINS_CGACT25", "NO_BARCODE-CONTAINS_A(20)", "NO_BARCODE-CONTAINS_P1",
                "NO_BARCODE-CONTAINS_TN5", "NO_BARCODE-CONTAINS_T(20)", "NO_BARCODE-PHIX_SUBSEQ",
                "NO_BARCODE-RNA45S", "NO_BARCODE-UNIDENTIFIED", "NEGATIVE_BARCODE_ERROR", 
                "COMPLEXITY_ERROR", "TOO_LONG_TRAILING_PRIMER_SEQ", "TOO_LONG_pA_pN_TAIL", "TOO_SHORT_INSERT",
                "TSSEQ_MISSING-CONTAINS_P1", "TSSEQ_MISSING-CONTAINS_TN5",
                "TSSEQ_MISSING-CONTAINS_T(20)", "TSSEQ_MISSING-CONTAINS_A(20)", "TSSEQ_MISSING",
                "N_IN_UMI", "LOW_QUALITY_IN_UMI", 
                "FORBIDDEN_INTERNAL_SEQ", "P1_PRIMER_IN_INSERT" };

        private readonly static Dictionary<string, int> oldCategories = new Dictionary<string,int>() 
                { {"N_IN_RANDOM_TAG", N_IN_UMI}, {"LOW_QUALITY_IN_RANDOM_TAG", LOW_QUALITY_IN_UMI}, 
                  {"SAL1-T25_IN_READ", NO_BC_OTHER}, {"NO_BARCODE-CONTAINS_NNNA25", NO_BC_INTERNAL_A20 },
                  {"NEGATIVE_BARCODE_ERROR", NO_BC_OTHER}, {"NO_BARCODE-CGACT25", NO_BC_CGACT25}, {"NO_BARCODE-NNNA25", NO_BC_INTERNAL_A20},
                  {"NO_BARCODE-SAL1-T25", NO_BC_OTHER}, {"NO_BARCODE-INTERNAL-T20", NO_BC_INTERNAL_T20},
                  {"NO_BARCODE-SOLEXA-ADP2_CONTAINING", NO_BC_OTHER}, {"NO_VALID_BARCODE-UNCHARACTERIZED", NO_BC_OTHER},
                  {"NO_BARCODE-CONTAINS_TN5_MOSAIC_END", NO_BC_TN5}, {"TSSEQ_MISSING-CONTAINS_TN5_MOSAIC_END", TSSEQ_MISSING_TN5},
                  {"TSSEQ_MISSING-CONTAINS_P1_PRIMER", TSSEQ_MISSING_P1}, {"NO_BARCODE-CONTAINS_P1_PRIMER", NO_BC_P1} };

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
            int readStatus = Array.FindIndex(categories, (c) => category.Equals(c, StringComparison.CurrentCultureIgnoreCase));
            if (readStatus == -1)
                oldCategories.TryGetValue(category, out readStatus);
            return readStatus;
        }
    }

}
