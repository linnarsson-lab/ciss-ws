﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class AnnotType
    {
        public static readonly int NOHIT = -1;
        public static readonly int EXON = 0;
        public static readonly int INTR = 1;
        public static readonly int USTR = 2;
        public static readonly int DSTR = 3;
        public static readonly int SPLC = 4;
        public static readonly int REPT = 5;
        public static readonly int SenseCount = 6;

        public static readonly int AEXON = SenseCount + 0;
        public static readonly int AINTR = SenseCount + 1;
        public static readonly int AUSTR = SenseCount + 2;
        public static readonly int ADSTR = SenseCount + 3;
        public static readonly int ASPLC = SenseCount + 4;
        public static readonly int AREPT = SenseCount + 5;

        public static readonly int Count = SenseCount * 2;

        public static bool DirectionalReads = true;
        public static bool IsTranscript(int annotType)
        {
            return annotType == EXON || annotType == SPLC
                   || (!DirectionalReads && (annotType == AEXON || annotType == ASPLC));
        }

        public static int MakeAntisense(int senseAnnotType)
        {
            return SenseCount + senseAnnotType;
        }

        private static readonly string[] ordered = 
            new string[] { "EXON", "INTR", "USTR", "DSTR", "SPLC", "REPT",
                           "AEXON", "AINTR", "AUSTR", "ADSTR", "ASPLC", "AREPT" };

        public static int[] GetGeneTypes()
        {
            return new int[] { EXON, INTR, USTR, DSTR, SPLC, 
                               AEXON, AINTR, AUSTR, ADSTR, ASPLC };
        }
        public static int[] GetSenseTypes()
        {
            return new int[] { EXON, INTR, USTR, DSTR, SPLC };
        }

        public static string GetName(int type)
        {
            if (type == -1) return "NOHIT";
            return ordered[type];
        }
    }
}