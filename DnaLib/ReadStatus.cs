﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class ReadStatus
    {
        public readonly static int VALID = 0;
        public readonly static int LENGTH_ERROR = 1;
        public readonly static int SEQ_QUALITY_ERROR = 2;
        public readonly static int COMPLEXITY_ERROR = 3;
        public readonly static int P1_IN_READ = 4;
        public readonly static int N_IN_RANDOM_TAG = 5;
        public readonly static int LOW_QUALITY_IN_RANDOM_TAG = 6;
        public readonly static int NEGATIVE_BARCODE_ERROR = 7;
        public readonly static int NO_BC_CGACT25 = 8;
        public readonly static int NO_BC_NNNA25 = 9;
        public readonly static int NO_BC_P1 = 10;
        public readonly static int NO_BC_TN5 = 11;
        public readonly static int NO_BC_INTERNAL_T20 = 12;
        public readonly static int BARCODE_ERROR = 13;
        public readonly static int TSSEQ_MISSING = 14;
        public readonly static int TOO_SHORT_INSERT = 15;
        public readonly static int FORBIDDEN_INTERNAL_SEQ = 16;
        public readonly static int TOO_LONG_TRAILING_PRIMER_SEQ = 17;
        public readonly static int Length = 18;
        public readonly static string[] categories = new string[] { "VALID", "TOO_LONG_pA_pN_TAIL", "SEQ_QUALITY_ERROR",
                                                                    "COMPLEXITY_ERROR",  "P1-PRIMER_IN_READ", "N_IN_RANDOM_TAG",
                                                                    "LOW_QUALITY_IN_RANDOM_TAG","NEGATIVE_BARCODE_ERROR",
                                                                    "NO_BARCODE-CGACT25", "NO_BARCODE-NNNA25", "NO_BARCODE-HAS-P1-PRIMER",
                                                                    "NO_BARCODE-HAS_TN5_MOSAIC_END", "NO_BARCODE-INTERNAL-T20",
                                                                    "NO_VALID_BARCODE", "TSSEQ_MISSING", "TOO_SHORT_INSERT",
                                                                    "FORBIDDEN_INTERNAL_SEQ", "TOO_LONG_TRAILING_PRIMER_SEQ" };
        public static int Parse(string category)
        {
            return Array.FindIndex(categories, (c) => category.Equals(c, StringComparison.CurrentCultureIgnoreCase));
        }
    }

}
