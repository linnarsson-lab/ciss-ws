using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Mathematics;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    /// <summary>
    /// Analyzes length distribution, color (base) balance and quality profile of reads.
    /// </summary>
    public class ExtractionQuality
    {
        private int[,] colorBalance;
        private int[,] lengthDistro; // [0,x] is before, [1,x] is after trimming
        private DescriptiveStatistics[] qprofile;

        public ExtractionQuality(int maxReadLength)
        {
            colorBalance = new int[4, maxReadLength];
            lengthDistro = new int[2, maxReadLength];
            qprofile = new DescriptiveStatistics[maxReadLength];
			for (int i = 0; i < qprofile.Length; i++)
			{
				qprofile[i] = new DescriptiveStatistics();
			}
        }

        public void Add(string seq, byte[] qualities)
        {
            // Calculate quality profile & color balance
            for (int i = 0; i < qualities.Length; i++)
            {
                qprofile[i].Add(FastQRecord.QualityToProbability(qualities[i]));
                int k = "ACGT".IndexOf(seq[i]);
                if (k >= 0) colorBalance[k, i]++;
            }
            lengthDistro[0, seq.Length]++;
        }

        public void AddTrimmedLength(int insertLength)
        {
            lengthDistro[1, insertLength]++;
        }

        public void Write(LaneInfo extrInfo)
        {
            // Dump quality profile & color balance
            string outputPathHead = Path.Combine(extrInfo.laneExtractionFolder, "extraction_");
            WriteQualityProfile(qprofile, extrInfo.PFReadFilePath, outputPathHead + "quality.txt");
            WriteColorBalance(colorBalance, extrInfo.PFReadFilePath, outputPathHead + "colors.txt");
            WriteLengthDistribution(lengthDistro, extrInfo.PFReadFilePath, outputPathHead + "lengths.txt");
        }

        private static void WriteLengthDistribution(int[,] lengthDistro, string referredFile, string outputPath)
        {
            using (StreamWriter lenFile = new StreamWriter(outputPath))
            {
                lenFile.WriteLine("Length distribution for {0}\tAll reads\tBarcoded_trimmed", referredFile);
                for (int i = 0; i < lengthDistro.GetLength(1); i++)
                {
                    lenFile.WriteLine(i.ToString() + "\t" + lengthDistro[0, i] + "\t" + lengthDistro[1, i]);
                }
                lenFile.WriteLine();
            }
        }

        private static void WriteColorBalance(int[,] colorBalance, string referredFile, string outputPath)
        {
            using (StreamWriter cFile = new StreamWriter(outputPath))
            {
                cFile.WriteLine("Color balance for {0} (all reads):\tA\tC\tG\tT", referredFile);
                for (int i = 0; i < colorBalance.GetLength(1); i++)
                {
                    cFile.WriteLine(i.ToString() + "\t" + colorBalance[0, i] + "\t" + colorBalance[1, i] + "\t" + colorBalance[2, i] + "\t" + colorBalance[3, i]);
                }
                cFile.WriteLine();
            }
        }

        private static void WriteQualityProfile(DescriptiveStatistics[] qprofile, string referredFile, string outputPath)
        {
            using (StreamWriter qFile = new StreamWriter(outputPath))
            {
                qFile.WriteLine("Quality profile for {0} (all reads):", referredFile);
                for (int i = 0; i < qprofile.Length; i++)
                {
                    qFile.WriteLine("{0}\t{1}", i, qprofile[i].Mean());
                }
                qFile.WriteLine();
            }
        }

    }
}
