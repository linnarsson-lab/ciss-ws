﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using System.IO;

namespace ErrorCorrection_console
{
    public class ErrorHandler
    {
        public string refSeqFilepath { get; set; }
        public string errorFilepath { get; set; }
        public string errorCorrectedFilepath { get; set; }
        public ErrorHandler()
        { 

        }
        public void ErrorCorrection()
        {
            if (!File.Exists(refSeqFilepath))
            {
                Console.WriteLine("Reference .fq file not found at " + refSeqFilepath);
                return;
            }

            var output = (Path.Combine(Path.GetDirectoryName(refSeqFilepath), Path.GetFileNameWithoutExtension(refSeqFilepath) + "_errorCorrected.fq")).OpenWrite();


            string[] lines = System.IO.File.ReadAllLines(refSeqFilepath);
            //string[] seqLines = new string[lines.Length / 4];
            string[] headerLines = new string[lines.Length / 4];
            ShortDnaSequence seq = new ShortDnaSequence();
            ShortDnaSequence Array32bp = new ShortDnaSequence();


            int seqCount = 0;
            ulong score = 0;
            //ulong de = 0;

            for (int i = 0; i < lines.Length; i = i + 4)
            {
                headerLines[seqCount] = lines[i];
                string errorScore = lines[i + 3];
                string[] HLItems = lines[i].Split('\t');
                int readCount = Convert.ToInt32(HLItems[1]);
                string resultLine = "";
                Dictionary<ulong, string> d = new Dictionary<ulong, string>();
                Dictionary<ulong, int> d1 = new Dictionary<ulong, int>();
                Dictionary<ulong, int> d2 = new Dictionary<ulong, int>();
                Dictionary<ulong, int> d3 = new Dictionary<ulong, int>();
                Dictionary<ulong, int> d4 = new Dictionary<ulong, int>();
                for (int ij = 0; ij < (readCount * 4); ij = ij + 4)
                {

                    seq = new ShortDnaSequence(lines[i + 1]);
                    //MessageBox.Show(seq.ToString());
                    int seqfrag = 0;
                    int deCount = 0;
                    //for (int j = 0; j < 4; j++)
                    //{
                    //for dictionary d1*********************
                    Array32bp = (ShortDnaSequence)seq.SubSequence(seqfrag, 32);
                    seqfrag = seqfrag + 32;
                    score = Array32bp.ToIndex();
                    if (!d.ContainsKey(score)) d.Add(score, Array32bp.ToString());

                    if (d1.ContainsKey(score) == true)
                    {
                        d1.TryGetValue(score, out deCount);
                        deCount++;
                        d1.Remove(score);
                        d1.Add(score, deCount);
                        deCount = 0;

                    }
                    else d1.Add(score, deCount + 1);

                    //for dictionary d2*********************
                    Array32bp = (ShortDnaSequence)seq.SubSequence(seqfrag, 32);
                    seqfrag = seqfrag + 32;
                    score = Array32bp.ToIndex();
                    if (!d.ContainsKey(score)) d.Add(score, Array32bp.ToString());
                    if (d2.ContainsKey(score) == true)
                    {
                        d2.TryGetValue(score, out deCount);
                        deCount++;
                        d2.Remove(score);
                        d2.Add(score, deCount);
                        deCount = 0;

                    }
                    else d2.Add(score, deCount + 1);

                    Array32bp = (ShortDnaSequence)seq.SubSequence(seqfrag, 32);
                    seqfrag = seqfrag + 32;
                    score = Array32bp.ToIndex();
                    if (!d.ContainsKey(score)) d.Add(score, Array32bp.ToString());
                    if (d3.ContainsKey(score) == true)
                    {
                        d3.TryGetValue(score, out deCount);
                        deCount++;
                        d3.Remove(score);
                        d3.Add(score, deCount);
                        deCount = 0;

                    }
                    else d3.Add(score, deCount + 1);

                    Array32bp = (ShortDnaSequence)seq.SubSequence(seqfrag, 32);
                    //seqfrag = seqfrag + 32;
                    score = Array32bp.ToIndex();
                    if (!d.ContainsKey(score)) d.Add(score, Array32bp.ToString());
                    if (d4.ContainsKey(score) == true)
                    {
                        d4.TryGetValue(score, out deCount);
                        deCount++;
                        d4.Remove(score);
                        d4.Add(score, deCount);
                        deCount = 0;

                    }
                    else d4.Add(score, deCount + 1);

                    //}
                    //MessageBox.Show("value of i=" + i);
                    i = i + 4;
                }
                //output1.WriteLine(d1.Values);
                //output1.Close();
                //var sortedD1 = (from entry in d1 orderby entry.Value ascending select entry).ToDictionary(pair => pair.Key, pair => pair.Value);
                var sortedd1 = d1.OrderByDescending(x1 => x1.Value);
                var sortedd2 = d2.OrderByDescending(x2 => x2.Value);
                var sortedd3 = d3.OrderByDescending(x3 => x3.Value);
                var sortedd4 = d4.OrderByDescending(x4 => x4.Value);
                string tempseq = "";
                var seq1 = sortedd1.ElementAt(0);
                d.TryGetValue(seq1.Key, out tempseq);
                resultLine = resultLine + tempseq;
                tempseq = "";
                var seq2 = sortedd2.ElementAt(0);
                d.TryGetValue(seq2.Key, out tempseq);
                resultLine = resultLine + tempseq;
                tempseq = "";
                var seq3 = sortedd3.ElementAt(0);
                d.TryGetValue(seq3.Key, out tempseq);
                resultLine = resultLine + tempseq;
                tempseq = "";
                var seq4 = sortedd4.ElementAt(0);
                d.TryGetValue(seq4.Key, out tempseq);
                resultLine = resultLine + tempseq;
                tempseq = "";
                //MessageBox.Show(resultLine);
                //foreach (var item in sortedd1)
                //{
                //    MessageBox.Show("key=" + (item.Key).ToString());
                //    MessageBox.Show("value=" + (item.Value).ToString());
                //} 

                output.WriteLine(headerLines[seqCount]);
                output.WriteLine(resultLine);
                output.WriteLine("+");
                output.WriteLine(errorScore);
                seqCount++;
                i = i - 4;
            }
            output.Close();
           // var output =                 (Path.Combine(Path.GetDirectoryName(refSeqFilepath), Path.GetFileNameWithoutExtension(refSeqFilepath) + "_errorCorrected.fq")).OpenWrite();
            //FastQFile fq = FastQFile.Load((Path.Combine(Path.GetDirectoryName(refSeqFilepath), Path.GetFileNameWithoutExtension(refSeqFilepath) + "_errorCorrected.fq")), 64);
            FastQFile fq = FastQFile.Load(errorCorrectedFilepath, 64);
            var output1 = (Path.Combine(Path.GetDirectoryName(refSeqFilepath), Path.GetFileNameWithoutExtension(refSeqFilepath) + "_errorReport.txt")).OpenWrite();
            output1.WriteLine("Header" + "\t" + "readNo" + "\t" + "PositionAt" + "\t" + "From" + "\t" + "To" + "\t" + "ErrorReport" + "\t" + "Total no of reads");
            string[] Errorlines = System.IO.File.ReadAllLines(errorFilepath);
            //string[] ErrorCorrlines = System.IO.File.ReadAllLines(errorCorrectedFilepath);

            string errorReport = "";
            //for (int j = 0; j < ErrorCorrlines.Length; j++)
            //{
            //   FastQRecord fr = fq.Records[j];    
            //}
            for (int i = 0; i < Errorlines.Length; i++)
            {
                string[] errorHead = Errorlines[i].Split('\t');
                string[] errorHead2;
                int fqRecord = -1;

                do
                {
                    fqRecord++;
                    errorHead2 = fq.Records[fqRecord].Header.Split('\t');
                } while (errorHead[0] != errorHead2[0]);
                //MessageBox.Show(errorHead[0] + "and " + errorHead2[0]);
                ShortDnaSequence errorseq = new ShortDnaSequence(fq.Records[fqRecord].Sequence);
                char charPosition1 = errorseq.GetNucleotide(Convert.ToInt64(errorHead[2]) - 1);
                char charPosition2 = Convert.ToChar(errorHead[3]);
                if (charPosition1 == charPosition2)
                {
                    errorReport = "Error Corrected";
                }
                else errorReport = "Error not corrected";
                output1.WriteLine(Errorlines[i].ToString() + "\t" /*+ charPosition1 + "\t" + charPosition2 + "\t"*/ + errorReport + "\t" + errorHead2[1]);
                errorHead2 = null;

            }
            output1.Close();
            Console.WriteLine("End of Run!");

            
        }
    }
    

}
