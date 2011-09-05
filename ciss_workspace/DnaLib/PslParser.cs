using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Linnarsson.Dna
{
    public struct PslBlock
    {
        private int m_Size;
        public int Size
        {
            get { return m_Size; }
            set { m_Size = value; }
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
        public PslBlock(int size, int qStart, int tStart)
        {
            this.m_Size = size;
            this.m_QStart = qStart;
            this.m_TStart = tStart;
        }	
    }

    public class PslHit
    {
        private int m_Matches;
        public int Matches
        {
            get { return m_Matches; }
            set { m_Matches = value; }
        }
        private int m_MisMatches;
        public int MisMatches
        {
            get { return m_MisMatches; }
            set { m_MisMatches = value; }
        }
        private int m_RepMatches;
        public int RepMatches
        {
            get { return m_RepMatches; }
            set { m_RepMatches = value; }
        }
        private int m_nCount;
        public int NCount
        {
            get { return m_nCount; }
            set { m_nCount = value; }
        }
        private int m_QNumInsert;
        public int QNumInsert
        {
            get { return m_QNumInsert; }
            set { m_QNumInsert = value; }
        }
        private int m_QBaseInsert;
        public int QBaseInsert
        {
            get { return m_QBaseInsert; }
            set { m_QBaseInsert = value; }
        }
        private int m_TNumInsert;
        public int TNumInsert
        {
            get { return m_TNumInsert; }
            set { m_TNumInsert = value; }
        }
        private int m_TBaseInsert;
        public int TBaseInsert
        {
            get { return m_TBaseInsert; }
            set { m_TBaseInsert = value; }
        }
        private char m_Strand;
        public char Strand
        {
            get { return m_Strand; }
            set { m_Strand = value; }
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
        private string m_TName;
        public string TName
        {
            get { return m_TName; }
            set { m_TName = value; }
        }
        private int m_TSize;
        public int TSize
        {
            get { return m_TSize; }
            set { m_TSize = value; }
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
        private int m_BlockCount;
        public int BlockCount
        {
            get { return m_BlockCount; }
            set { m_BlockCount = value; }
        }
        private string m_BlockSizes;
        public string BlockSizes
        {
            get { return m_BlockSizes; }
            set { m_BlockSizes = value; }
        }
        private string m_QStarts;
        public string QStarts
        {
            get { return m_QStarts; }
            set { m_QStarts = value; }
        }
        private string m_TStarts;
        public string TStarts
        {
            get { return m_TStarts; }
            set { m_TStarts = value; }
        }
        private PslBlock[] m_PslBlocks;
    	public PslBlock[] PslBlocks
	    {
		    get { return m_PslBlocks;}
    		set { m_PslBlocks = value;}
	    }
	
	    public PslHit(int matches, int misMatches, int repMatches, int nCount, int qNumInsert,
                      int qBaseInsert, int tNumInsert, int tBaseInsert, char strand,
                      string qName, int qSize, int qStart, int qEnd, string tName, int tSize,
                      int tStart, int tEnd, int blockCount,
                      string blockSizes, string qStarts, string tStarts)
        {
            this.m_Matches = matches;
            this.m_MisMatches = misMatches;
            this.m_RepMatches = repMatches;
            this.m_nCount = nCount;
            this.m_QNumInsert = qNumInsert;
            this.m_QBaseInsert = qBaseInsert;
            this.m_TNumInsert = tNumInsert;
            this.m_TBaseInsert = tBaseInsert;
            this.m_Strand = strand;
            this.m_QName = qName;
            this.m_QSize = qSize;
            this.m_QStart = qStart;
            this.m_QEnd = qEnd;
            this.m_TName = tName;
            this.m_TSize = tSize;
            this.m_TStart = tStart;
            this.m_TEnd = tEnd;
            this.m_BlockCount = blockCount;
            this.m_BlockSizes = BlockSizes;
            this.m_QStarts = qStarts;
            this.m_TStarts = tStarts;
            string[] ss = blockSizes.Split(',');
            string[] qq = qStarts.Split(',');
            string[] tt = tStarts.Split(',');
            this.m_PslBlocks = new PslBlock[ss.Length];
            for (int i = 0; i < ss.Length - 1; i++)
            {
                this.m_PslBlocks[i] = new PslBlock(int.Parse(ss[i]), int.Parse(qq[i]), int.Parse(tt[i]));
            }              
        }
        public static PslHit FromPslLine(string line)
        {
            string[] f = line.Split('\t');
            if (f.Length < 21)
                return null;
            return new PslHit(int.Parse(f[0]), int.Parse(f[1]), int.Parse(f[2]), int.Parse(f[3]),
                              int.Parse(f[4]), int.Parse(f[5]), int.Parse(f[6]), int.Parse(f[7]),
                              f[8][0], f[9], int.Parse(f[10]), int.Parse(f[11]), int.Parse(f[12]), 
                              f[13], int.Parse(f[14]), int.Parse(f[15]), int.Parse(f[16]), 
                              int.Parse(f[17]), f[18], f[19], f[20]);
        }
    }

    public class PslParser
    {
        private List<PslHit> m_Hits = new List<PslHit>();
        public List<PslHit> Hits
        {
            get { return m_Hits; }
            set { m_Hits = value; }
        }
        private Dictionary<string, List<PslHit>> m_HitsByQuery = new Dictionary<string,List<PslHit>>();
        public Dictionary<string, List<PslHit>> HitsByQuery
        {
            get { return m_HitsByQuery; }
            set { m_HitsByQuery = value; }
        }
	
        public PslParser(string pslFile)
        {
            StreamReader sr = new StreamReader(pslFile);
            for (int i = 0; i < 5; i++)
                sr.ReadLine(); // Read past header lines
            string line = sr.ReadLine();
            while (line != null)
            {
                PslHit hit = PslHit.FromPslLine(line);
                if (hit != null)
                {
                    m_Hits.Add(hit);
                    if (!m_HitsByQuery.ContainsKey(hit.QName))
                        m_HitsByQuery[hit.QName] = new List<PslHit>();
                    m_HitsByQuery[hit.QName].Add(hit);
                }
                line = sr.ReadLine();
            }
            sr.Close();
        }

    }
}
