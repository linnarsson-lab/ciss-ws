using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Linnarsson.Dna
{
    public class ChainSegment
    {
        private int m_Size;
        public int Size
        {
            get { return m_Size; }
            set { m_Size = value; }
        }
        private int m_Dt;
        public int Dt
        {
            get { return m_Dt; }
            set { m_Dt = value; }
        }
        private int m_Dq;
        public int Dq
        {
            get { return m_Dq; }
            set { m_Dq = value; }
        }
        private int m_QStart;
        public int QStart
        {
            get { return m_QStart; }
            set { m_QStart = value; }
        }
        private int m_TStart;
        public int TStart
        {
            get { return m_TStart; }
            set { m_TStart = value; }
        }
        public ChainSegment(int size, int dt, int dq)
        {
            this.m_Size = size;
            this.m_Dt = dt;
            this.m_Dq = dq;
        }
        public ChainSegment(int size)
        {
            this.m_Size = size;
            this.m_Dt = this.m_Dq = 0;
        }

        public void SetStarts(ref int qStart, ref int tStart)
        {
            this.m_QStart = qStart;
            this.m_TStart = tStart;
            qStart += this.m_Size + this.Dq;
            tStart += this.m_Size + this.Dt;
        }
    }

    public class Chain
    {
        private long m_Score;
	    public long Score
	    {
		    get { return m_Score;}
		    set { m_Score = value;}
	    }
        private string m_TName;
	    public string TName
	    {
		    get { return m_TName;}
		    set { m_TName = value;}
	    }
        private int m_TSize;
        public int TSize
        {
            get { return m_TSize; }
            set { m_TSize = value; }
        }
        private char m_TStrand;
        public char TStrand
        {
            get { return m_TStrand; }
            set { m_TStrand = value; }
        }
        private int m_TStart;
        public int TStart
        {
            get { return m_TStart; }
            set { m_TStart = value; }
        }
        private int m_TEnd;
        public int TEnd
        {
            get { return m_TEnd; }
            set { m_TEnd = value; }
        }
        private string m_QName;
        public string QName
        {
            get { return m_QName; }
            set { m_QName = value; }
        }
        private int m_QSize;
        public int QSize
        {
            get { return m_QSize; }
            set { m_QSize = value; }
        }
        private char m_QStrand;
        public char QStrand
        {
            get { return m_QStrand; }
            set { m_QStrand = value; }
        }
	
        private int m_QStart;
        public int QStart
        {
            get { return m_QStart; }
            set { m_QStart = value; }
        }
        private int m_QEnd;
        public int QEnd
        {
            get { return m_QEnd; }
            set { m_QEnd = value; }
        }
        private string m_Id;
        public string Id
        {
            get { return m_Id; }
            set { m_Id = value; }
        }
        private List<ChainSegment> m_ChainSegments;
        public List<ChainSegment> ChainSegments
        {
            get { return m_ChainSegments; }
            set { m_ChainSegments = value; }
        }
        public Chain(long score, string tName, int tSize, char tStrand, int tStart, int tEnd,
                     string qName, int qSize, char qStrand, int qStart, int qEnd, string id,
                     List<ChainSegment> chainSegments)
        {
            this.Score = score;
            this.m_TName = tName;
            this.m_TSize = tSize;
            this.m_TStrand = tStrand;
            this.m_TStart = TStart;
            this.m_TEnd = tEnd;
            this.m_QName = qName;
            this.m_QSize = QSize;
            this.m_QStrand = QStrand;
            this.m_QStart = QStart;
            this.m_QEnd = qEnd;
            this.m_Id = id;
            this.m_ChainSegments = chainSegments;
        }

        public static Chain FromFile(StreamReader sr)
        {
            string line;
            do
            {
                line = sr.ReadLine();
                if (line == null) return null;
            }
            while (!line.StartsWith("chain"));
            string[] h = line.Split();
            int tStart = int.Parse(h[5]);
            int qStart = int.Parse(h[10]);
            List<ChainSegment> chainSegments = new List<ChainSegment>();
            bool lastSegment = false;
            do
            {
                line = sr.ReadLine();
                string[] f = line.Trim().Split('\t');
                ChainSegment cs;
                if (f.Length == 3)
                    cs = new ChainSegment(int.Parse(f[0]), int.Parse(f[1]), int.Parse(f[2]));
                else
                {
                    cs = new ChainSegment(int.Parse(f[0]));
                    lastSegment = true;
                }
                chainSegments.Add(cs);
            }
            while (!lastSegment);
            Chain chain = new Chain(long.Parse(h[1]),
                                    h[2], int.Parse(h[3]), h[4][0], int.Parse(h[5]), int.Parse(h[6]),
                                    h[7], int.Parse(h[8]), h[9][0], int.Parse(h[10]), int.Parse(h[11]),
                                    h[12], chainSegments);
            chain.SetStarts();
            return chain;
        }

        public void SetStarts()
        {
            int runningQStart = this.m_QStart;
            int runningTStart = this.m_TStart;
            foreach (ChainSegment cs in this.m_ChainSegments)
                cs.SetStarts(ref runningQStart, ref runningTStart);
        }

        public void Reverse(int tLength, int qLength)
        {
            this.m_QStart = qLength - this.m_QStart;
            this.m_QEnd = qLength - this.m_QEnd;
            this.m_TStart = tLength - this.m_TStart;
            this.m_TEnd = tLength - this.m_TEnd;
            this.m_QStrand = (this.m_QStrand == '-') ? '+' : '-';
            this.m_TStrand = (this.m_TStrand == '-') ? '+' : '-';
            m_ChainSegments.Reverse();
            for (int i = 0; i < m_ChainSegments.Count - 1; i++)
            {
                m_ChainSegments[i].Dq = m_ChainSegments[i + 1].Dq;
                m_ChainSegments[i].Dt = m_ChainSegments[i + 1].Dt;
            }
            m_ChainSegments[m_ChainSegments.Count - 1].Dq = 0;
            m_ChainSegments[m_ChainSegments.Count - 1].Dt = 0;
            SetStarts();
        }

    }

    public class ChainParser
    {
        public List<Chain> chains;
        public ChainParser(string chainFile)
        {
            StreamReader sr = new StreamReader(chainFile);
            chains = new List<Chain>();
            Chain c = Chain.FromFile(sr);
            while (c != null)
            {
                chains.Add(c);
                c = Chain.FromFile(sr);
            }
        }

        public List<Chain> GetMatchingChains(string tName, string qName)
        {
            List<Chain> matchingChains = new List<Chain>();
            foreach (Chain c in chains)
            {
                if (c.TName == tName && c.QName == qName)
                    matchingChains.Add(c);
            }
            return matchingChains;
        }

        public static void MakeTargetForward(List<Chain> chs, int tLength, int qLength)
        {
            foreach (Chain c in chs)
                if (c.TStrand == '-')
                    c.Reverse(tLength, qLength);
        }

        public static void OrderOnTarget(List<Chain> chs)
        {
            List<Chain> orderedChain = new List<Chain>();
            foreach (Chain c in chs)
            {
                if (c.TStrand == '-')
                    throw new InvalidDataException("Target chains are not all forward oriented.");
                int tStart = c.TStart;
                int p = 0;
                for (; p < orderedChain.Count; p++)
                    if (orderedChain[p].TStart > tStart)
                        break;
                orderedChain.Insert(p, c);
            }
            chs = orderedChain;
        }

        /// <summary>
        /// Combines sequence from aligned tSeq and qSeq into one consensus sequence.
        /// Any gaps are filled with sequence from tSeq, except when qSeq is alone making
        /// an insert compared with tSeq, or when the insert in tSeq is very small and
        /// corresponding non-aligned insert in qSeq is large.
        /// Annotates inserts and SNPs on Sequence.
        /// </summary>
        /// <param name="tSeq"></param>
        /// <param name="tStart"></param>
        /// <param name="tEnd"></param>
        /// <param name="qSeq"></param>
        /// <returns></returns>
		public GenbankFile GetCombinedInclusiveSequence(DnaSequence tSeq, int tStart, int tEnd, DnaSequence qSeq)
        {
            string tName = tSeq.GenbankHeader.Accession;
            string qName = qSeq.GenbankHeader.Accession;
            List<Chain> chs = GetMatchingChains(tName, qName);
            if (chs.Count == 0)
                return null;
            int tLength = (int)tSeq.Count;
            int qLength = (int)qSeq.Count;
            MakeTargetForward(chs, tLength, qLength);
            OrderOnTarget(chs);
            int i = 0;
            for (; i < chs.Count; i++)
                if (tStart >= chs[i].TStart) break;
            if (i == chs.Count)
                throw new InvalidDataException("No alignment that fits within region.");

			GenbankFile gbf = new GenbankFile();
			DnaSequence newSeq = new DnaSequence();
			gbf.Records.Add(new GenbankRecord());
			gbf.Records[0].Sequence = newSeq;
			while (tStart < tEnd)
            {
                if (tStart > chs[i].TEnd)
                    continue;
                if (tStart < chs[i].TStart)
                { // For gaps between chains, take sequence from qSeq
                    newSeq.Append(tSeq.SubSequence(tStart, chs[i].TStart - tStart));
                    GenbankFeature f = MakeInsertAnnotation(tStart + 1, chs[i].TStart, tName);
                    gbf.Records[0].Features.Add(f);
                    tStart = chs[i].TStart;
                }
                Chain c = chs[i];
				DnaSequence qSubSeq;
                int subLen = c.QEnd - c.QStart;
                if (c.QStrand == '+')
                    qSubSeq = qSeq.SubSequence(c.QStart, subLen);
                else
                {
                    qSubSeq = qSeq.SubSequence(qSeq.Count - c.QEnd, subLen);
                    qSubSeq.RevComp();
                }
                foreach (ChainSegment cs in c.ChainSegments)
                {
                    if (tStart > (cs.TStart + cs.Size + cs.Dt))
                        continue;
                    int qPos = cs.QStart - c.QStart;
                    int segEnd = Math.Min(tEnd, c.TEnd);
                    while (tStart < segEnd) // Add the aligned segment sequence
                    {
                        char tNt = tSeq.GetNucleotide(tStart);
                        newSeq.Append(tNt);
                        if (tNt != qSeq[qPos])
                        {
                            GenbankFeature fsnp = MakeTargetSNPAnnotation(tStart, qSeq.GetNucleotide(qPos));
							gbf.Records[0].Features.Add(fsnp);
                        }
                        qPos++;
                        tStart++;
                    }
                    if (tStart < segEnd)
                    { // Add selected sequence from the gap between segments
                        if (cs.Dt < 10 && cs.Dq > 50)
                        { // Use query gap sequence
                            newSeq.Append(qSubSeq.SubSequence(qPos, cs.Dq));
                            GenbankFeature fq = MakeInsertAnnotation(qPos + c.QStart + 1, qPos + c.QStart + cs.Dq, qName);
							gbf.Records[0].Features.Add(fq);
                            qPos += cs.Dq;
                        }
                        else if (cs.Dt > 0)
                        { // Use Target gap sequence
                            newSeq.Append(tSeq.SubSequence(tStart, cs.Dt));
                            GenbankFeature ft = MakeInsertAnnotation(tStart + 1, tStart + cs.Dt, tName);
							gbf.Records[0].Features.Add(ft);
                            tStart += cs.Dt;
                        }
                    }
                    if (tStart >= tEnd)
                        break;
                }
            }
            return newSeq;
        }

        private GenbankFeature MakeTargetSNPAnnotation(int tStart, char altNt)
        {
            GenbankQualifier q = new GenbankQualifier();
            q.Name = "replace";
            q.Value = altNt.ToString();
            GenbankFeature f = new GenbankFeature();
            f.Name = "variation";
            f.Location = string.Format("{0}", tStart + 1);
            f.Qualifiers = new List<GenbankQualifier>();
            f.Qualifiers.Add(q);
            return f;
        }


        private GenbankFeature MakeInsertAnnotation(int start, int end, string seqName)
        {
            GenbankQualifier q = new GenbankQualifier();
            q.Name = "note";
            q.Value = string.Format("From {0}", seqName);
            GenbankFeature f = new GenbankFeature();
            f.Name = "misc_feature";
            f.Location = string.Format("{0}..{1}", start + 1, end);
            f.Qualifiers = new List<GenbankQualifier>();
            f.Qualifiers.Add(q);
            return f;
        }
    }
}
