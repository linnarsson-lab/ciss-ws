using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Mathematics;

namespace Linnarsson.Dna
{
    public class RedundantExonHitMapper
    {
        private Dictionary<long, List<Pair<int, FtInterval>>> altMap;
        private Dictionary<string, int> chrCodes;

        public RedundantExonHitMapper(string redundantDataFile, Dictionary<string, GeneFeature> geneFeatures)
        {
            chrCodes = new Dictionary<string, int>();
            altMap = new Dictionary<long, List<Pair<int, FtInterval>>>();
            StreamReader reader = new StreamReader(redundantDataFile);
            string line = reader.ReadLine();
            while (line != null)
            {  // chrid,chrbowtiehitpos,strand \t gene1name,gene1trmidhitpos \t gene2name,gene2trmidhitpos ...
                string[] data = line.Trim().Split('\t');
                string[] chrData = data[0].Split(',');
                List<Pair<int, FtInterval>> redundantPairs = new List<Pair<int, FtInterval>>();
                for (int i = 1; i < data.Length; i++)
                {
                    string[] dataPair = data[i].Split(',');
                    string gfName = dataPair[0];
                    GeneFeature gf = geneFeatures[gfName];
                    int gfTrMidHitPos = int.Parse(dataPair[1]);
                    int gfChrPos = gf.GetChrPos(gfTrMidHitPos);
                    FtInterval ivl = new FtInterval(0, 0, gf.MarkAltExonHit, gfChrPos);
                    redundantPairs.Add(new Pair<int, FtInterval>(0, ivl));
                }
                string chrId = chrData[0];
                if (!chrCodes.ContainsKey(chrId))
                    chrCodes[chrId] = chrCodes.Count;
                long codedPos = CodeChrPos(chrData[0], int.Parse(chrData[1]), chrData[2][0]);
                altMap[codedPos] = redundantPairs;
                line = reader.ReadLine();
            }
            reader.Close();
        }

        private long CodeChrPos(string chrId, int hitMidPos, char strand)
        {
            int codedStrand = (strand == '+') ? 0 : 1;
            int chrCode = chrCodes[chrId];
            return (hitMidPos << 8) + (chrCode << 1) + codedStrand;
        }

        public List<Pair<int, FtInterval>> GetRedundantMappings(string chrId, int hitMidPos, char strand)
        {
            long codedPos = CodeChrPos(chrId, hitMidPos, strand);
            return altMap[codedPos];
        }

        public static RedundantExonHitMapper GetRedundantHitMapper(StrtGenome strtGenome, int averageReadLen, 
                                                                   Dictionary<string, GeneFeature> geneFeatures)
        {
            string redundancyPath = PathHandler.GetRedundancyPath(strtGenome, averageReadLen);
            if (File.Exists(redundancyPath))
                return new RedundantExonHitMapper(redundancyPath, geneFeatures);
            return null;
        }
    }
}
