using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class AnnotType
    {
        public static readonly int NOHIT = -1;
        public static readonly int EXON = 0;
        public static readonly int CTRL = 1;
        public static readonly int INTR = 2;
        public static readonly int USTR = 3;
        public static readonly int DSTR = 4;
        public static readonly int SPLC = 5;
        public static readonly int REPT = 6;
        public static readonly int SenseCount = 7;

        public static readonly int AEXON = SenseCount + 0;
        public static readonly int ACTRL = SenseCount + 1;
        public static readonly int AINTR = SenseCount + 2;
        public static readonly int AUSTR = SenseCount + 3;
        public static readonly int ADSTR = SenseCount + 4;
        public static readonly int ASPLC = SenseCount + 5;
        public static readonly int AREPT = SenseCount + 6;

        public static readonly int Count = SenseCount * 2;

        /// <summary>
        /// Check if the annotType is a transcript (exon or splice) under the setting for directional read
        /// </summary>
        /// <param name="annotType"></param>
        /// <returns></returns>
        public static bool IsTranscript(int annotType)
        {
            return annotType == EXON || annotType == SPLC || annotType == CTRL
                   || (!Props.props.DirectionalReads && (annotType == AEXON || annotType == ASPLC || annotType == ACTRL));
        }

        public static int MakeAntisense(int senseAnnotType)
        {
            return SenseCount + senseAnnotType;
        }

        private static readonly Dictionary<int, string> fullNames = new Dictionary<int,string>()
            { {EXON, "exon"}, {INTR, "intron"}, {USTR, "upstream"}, {DSTR, "downstream"}, {SPLC, "splice"}, {REPT, "repeat"},
              {AEXON, "anti-exon"}, {AINTR, "anti-intron"}, {AUSTR, "anti-upstream"}, {ADSTR, "anti-downstream"},
              {ASPLC, "anti-splice"}, {AREPT, "anti-repeat"}, {CTRL, "spike"}, {ACTRL, "anti-spike"} };

        public static int[] GetGeneTypes()
        {
            return new int[] { CTRL, EXON, SPLC, INTR, USTR, DSTR, ACTRL, AEXON, ASPLC, AINTR, AUSTR, ADSTR };
        }
        public static int[] GetSenseTypes()
        {
            return new int[] { CTRL, EXON, SPLC, INTR, USTR, DSTR };
        }

        public static int[] GetTypes()
        {
            return Props.props.DirectionalReads ?
                new int[] { CTRL, EXON, SPLC, INTR, USTR, DSTR, REPT, ACTRL, AEXON, ASPLC, AINTR, AUSTR, ADSTR } :
                new int[] { CTRL, EXON, SPLC, INTR, USTR, DSTR, REPT };
        }

        public static string GetName(int type)
        {
            if (type == -1) return "NOHIT";
            return fullNames[type];
        }
    }
}
