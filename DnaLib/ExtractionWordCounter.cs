using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics;

namespace Linnarsson.Dna
{
    public struct FrequentWord
    {
        public int count;
        public double fractionOfReads;
        public string seq;
        public FrequentWord(int c, string s, double frac)
        {
            count = c;
            seq = s;
            fractionOfReads = frac;
        }
        public override string ToString()
        {
            return string.Format("{0,12:0} {1,9:0.00000} {2}", count, fractionOfReads, seq);
        }

        public static string Header = "      #Reads ReadsFrac Word\n";
    }

    /// <summary>
    /// Analyzes frequent subsequences in the Illumina reads, for detection of artefacts and contamination.
    /// </summary>
    public class ExtractionWordCounter
    {
        private int[] counts;
        private int wordLength;
        private ulong mask;
        private int nReads;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wordLength">Length of subsequences to analyse for frequencies</param>
        public ExtractionWordCounter(int wordLength)
        {
            this.wordLength = wordLength;
            ulong wordRange = 1U << (2 * wordLength);
            mask = wordRange - 1;
            counts = new int[wordRange];
            nReads = 0;
        }

        /// <summary>
        /// Add a read to the statistic
        /// </summary>
        /// <param name="readSeq">Read sequence</param>
        public void AddRead(string readSeq)
        {
            nReads++;
            foreach (ulong hash in IterateUniqueHashes(readSeq))
                counts[hash]++;
        }

        private IEnumerable<ulong> IterateUniqueHashes(string seq)
        {
            Dictionary<ulong, bool> unique = new Dictionary<ulong, bool>();
            foreach (ulong hash in IterateHashes(seq))
                if (!unique.ContainsKey(hash))
                {
                    unique[hash] = true;
                    yield return hash;
                }
        }

        private IEnumerable<ulong> IterateHashes(string seq)
        {
            if (seq.Length < wordLength)
                yield break;
            ulong hash = 0;
            for (int i = 0; i < seq.Length; i++)
            {
                hash = hash << 2;
                char c = seq[i];
                uint code = (c == 'A') ? 0U : (c == 'C') ? 1U : (c == 'G') ? 2U : 3U;
                hash = (hash & mask) | code;
                if (i >= wordLength)
                    yield return hash;
            }
        }

        /// <summary>
        /// Generate output from the the added reads.
        /// Frequent subsequences that overlap by all but one base are
        /// grouped and aligned in the output.
        /// </summary>
        /// <param name="topLength">Number of subsequences to output</param>
        /// <returns>A string with multiple-line output to write to a file</returns>
        public string GroupsToString(int topLength)
        {
            FrequentWord[] fWords = GetFrequentWords(topLength);
            List<List<FrequentWord>> groups = GetOverlappingGroups(fWords);
            StringBuilder sb = new StringBuilder();
            sb.Append(FrequentWord.Header);
            foreach (List<FrequentWord> group in groups)
            {
                if (group.Count > 1)
                {
                    sb.Append("\nConsensus              " + group[0].seq);
                    for (int i = 1; i < group.Count; i++)
                        sb.Append(group[i].seq[group[i].seq.Length - 1]);
                    sb.Append('\n');
                }
                foreach (FrequentWord fWord in GetGroupWords(group))
                    sb.Append(fWord.ToString() + "\n");
                if (group.Count > 1)
                    sb.Append('\n');
            }
            return sb.ToString();
        }

        private static List<FrequentWord> GetGroupWords(List<FrequentWord> group)
        {
            List<FrequentWord> groupWords = new List<FrequentWord>();
            int indent = 0;
            foreach (FrequentWord fw in group)
            {
                FrequentWord indentFw = new FrequentWord(fw.count, new String(' ', indent++) + fw.seq, fw.fractionOfReads);
                groupWords.Add(indentFw);
            }
            return groupWords;
        }

        private List<List<FrequentWord>> GetOverlappingGroups(FrequentWord[] fWords)
        {
            List<List<FrequentWord>> groups = new List<List<FrequentWord>>();
            foreach (FrequentWord fWord in fWords)
            {
                List<FrequentWord> newGroup = new FrequentWord[] { fWord }.ToList();
                groups.Add(newGroup);
            } 
            bool change = true;
            while (change)
            {
                change = false;
                for (int i = 0; i < groups.Count - 1; i++)
                {
                    List<FrequentWord> groupI = groups[i];
                    string firstSeq = groupI[0].seq;
                    string lastSeq = groupI[groupI.Count - 1].seq;
                    for (int j = i + 1; j < groups.Count; j++)
                    {
                        List<FrequentWord> groupJ = groups[j];
                        if (groupJ[0].seq.StartsWith(lastSeq.Substring(1)))
                        {
                            groupJ.InsertRange(0, groupI);
                            groups.RemoveAt(i);
                            i--;
                            change = true;
                            break;
                        }
                        else if (firstSeq.StartsWith(groupJ[groupJ.Count - 1].seq.Substring(1)))
                        {
                            groupJ.AddRange(groupI);
                            groups.RemoveAt(i);
                            i--;
                            change = true;
                            break;
                        }
                    }
                }
                if (change)
                    break;
            }
            return groups;
        }

        private FrequentWord[] GetFrequentWords(int topLength)
        {
            List<ulong> maxHashes = new List<ulong>();
            List<int> maxCounts = new List<int>();
            for (int i = 0; i < topLength; i++)
            {
                int maxCount = 0;
                ulong maxH = 0;
                for (ulong h = 0; h < (ulong)counts.Length; h++)
                    if (counts[h] > maxCount && !maxHashes.Contains(h))
                    {
                        maxCount = counts[h];
                        maxH = h;
                    }
                if (maxCount > 0)
                {
                    maxHashes.Add(maxH);
                    maxCounts.Add(maxCount);
                    counts[maxH] = 0;
                }
            }
            FrequentWord[] result = new FrequentWord[maxHashes.Count];
            for (int i = 0; i < maxHashes.Count; i++)
            {
                char[] cs = new char[wordLength];
                ulong hash = maxHashes[i];
                for (int p = wordLength - 1; p >= 0; p--)
                {
                    uint code = (uint)(hash & 3);
                    hash = hash >> 2;
                    char c = (code == 0U) ? 'A' : (code == 1U) ? 'C' : (code == 2U) ? 'G' : 'T';
                    cs[p] = c;
                }
                result[i] = new FrequentWord(maxCounts[i], new string(cs), maxCounts[i]/(double)nReads);
            }
            return result;
        }
    }
}
