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

        public void Add(FastQRecord rec)
        {
            // Calculate quality profile & color balance
            byte[] qualities = rec.Qualities;
            for (int i = 0; i < qualities.Length; i++)
            {
                qprofile[i].Add(FastQRecord.QualityToProbability(qualities[i]));
                int k = "ACGT".IndexOf(rec.Sequence[i]);
                if (k >= 0) colorBalance[k, i]++;
            }
            lengthDistro[0, rec.Sequence.Length]++;
        }

        public void AddTrimmedLength(int insertLength)
        {
            lengthDistro[1, insertLength]++;
        }

        public void Write(LaneInfo extrInfo)
        {
            // Dump quality profile & color balance
            string outputPathHead = Path.Combine(extrInfo.extractedFileFolder, "extraction_");
            WriteQualityProfile(qprofile, extrInfo.readFilePath, outputPathHead + "quality.txt");
            WriteColorBalance(colorBalance, extrInfo.readFilePath, outputPathHead + "colors.txt");
            WriteLengthDistribution(lengthDistro, extrInfo.readFilePath, outputPathHead + "lengths.txt");
        }

        private static void WriteLengthDistribution(int[,] lengthDistro, string referredFile, string outputPath)
        {
            var lenFile = outputPath.OpenWrite();
            lenFile.WriteLine("Length distribution for " + referredFile + "\tAll reads\tBarcoded_trimmed");
            for (int i = 0; i < lengthDistro.GetLength(1); i++)
            {
                lenFile.WriteLine(i.ToString() + "\t" + lengthDistro[0, i] + "\t" + lengthDistro[1, i]);
            }
            lenFile.WriteLine();
            lenFile.Close();
        }

        private static void WriteColorBalance(int[,] colorBalance, string referredFile, string outputPath)
        {
            var cFile = outputPath.OpenWrite();
            cFile.WriteLine("Color balance for " + referredFile + " (all reads):\tA\tC\tG\tT");
            for (int i = 0; i < colorBalance.GetLength(1); i++)
            {
                cFile.WriteLine(i.ToString() + "\t" + colorBalance[0, i] + "\t" + colorBalance[1, i] + "\t" + colorBalance[2, i] + "\t" + colorBalance[3, i]);
            }
            cFile.WriteLine();
            cFile.Close();
        }

        private static void WriteQualityProfile(DescriptiveStatistics[] qprofile, string referredFile, string outputPath)
        {
            var qFile = outputPath.OpenWrite();
            qFile.WriteLine("Quality profile for " + referredFile + " (all reads):");
            for (int i = 0; i < qprofile.Length; i++)
            {
                qFile.WriteLine(i.ToString() + "\t" + qprofile[i].Mean().ToString());
            }
            qFile.WriteLine();
            qFile.Close();
        }

    }
}
