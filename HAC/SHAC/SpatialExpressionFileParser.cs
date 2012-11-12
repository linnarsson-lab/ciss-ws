using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HAC;

namespace SHAC
{
    public class FileFormatException : Exception
    {
        public FileFormatException() : base("The format of the input file is illegal.")
        { }
 
        public FileFormatException(string message) : base(message)
        { }
    }

    public class SpatialExpressionFileParser
    {
        /// <summary>
        /// The input file is expected to conform to:
        /// (title)   tab SampleA_ID tab SampleB_ID ...
        /// (xheader) tab  A_XPOS    tab   B_XPOS   ...
        /// (yheader) tab  A_YPOS    tab   B_YPOS   ...
        /// GENE1_ID  tab A1_VALUE   tab  B1_VALUE  ...
        /// GENE2_ID ...
        /// 
        /// </summary>
        /// <param name="exprFile">path to expression data file</param>
        /// <param name="neighborhood">one of 'queen' and 'rook'</param>
        /// <returns></returns>
        public static Element[] Parse(string exprFile, Neighborhood neighborhood)
        {
            int nSamples;
            int[] xPostitions, yPositions;
            Element[] elements;
            List<string> geneIds = new List<string>();
            using (StreamReader reader = new StreamReader(exprFile))
            {
                int n = 1;
                try
                {
                    string line = reader.ReadLine();
                    string[] fields = line.Split('\t');
                    nSamples = fields.Length - 1;
                    string[] sampleIds = new string[nSamples];
                    elements = new Element[nSamples];
                    for (int i = 0; i < nSamples; i++)
                        elements[i] = new Element(fields[i + 1]);
                    n++;
                    xPostitions = ReadInts(fields, 1, nSamples, reader.ReadLine());
                    if (xPostitions.Length != nSamples)
                        throw new FileFormatException("Wrong number of fields on X coordinate line of input file.");
                    n++;
                    yPositions = ReadInts(fields, 1, nSamples, reader.ReadLine());
                    if (yPositions.Length != nSamples)
                        throw new FileFormatException("Wrong number of fields on Y coordinate line of input file.");
                    while ((line = reader.ReadLine()) != null)
                    {
                        n++;
                        fields = line.Split('\t');
                        if (fields.Length != nSamples + 1)
                            throw new FileFormatException("Wrong number of fields on line " + n.ToString() + " of input file.");
                        geneIds.Add(fields[0]);
                        for (int i = 0; i < nSamples; i++)
                            elements[i].AddDataPoint(double.Parse(fields[i + 1]));
                    }
                }
                catch (Exception e)
                {
                    throw new FileFormatException("Error parsing line " + n.ToString() +
                                                  " of input file.\nException details:" + e.ToString());
                }
            }
            neighborhood.Setup(elements, xPostitions, yPositions);
            return elements;
        }

        private static int[] ReadInts(string[] fields, int startIdx, int inclusiveEndIdx, string xLine)
        {
            int[] data = new int[1 + inclusiveEndIdx - startIdx];
            fields = xLine.Split('\t');
            int dataIdx = 0;
            for (int fieldIdx = startIdx; fieldIdx <= inclusiveEndIdx; fieldIdx++)
                data[dataIdx++] = int.Parse(fields[fieldIdx]);
            return data;
        }
    }
}
