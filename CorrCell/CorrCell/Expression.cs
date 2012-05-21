using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CorrCell
{
    /// <summary>
    /// Represents expression values of transcripts for a number of cells
    /// </summary>
    public class Expression
    {
        private string[] cellNames;
        public string GetCellName(int cellIdx)
        {
            return cellNames[cellIdx];
        }

        private string[] geneNames;
        public string GetGeneName(int geneIdx)
        {
            return geneNames[geneIdx];
        }

        private List<int[]> data = new List<int[]>();

        /// <summary>
        /// Number of genes in data the data set
        /// </summary>
        public int GeneCount { get { return data.Count; } }
        /// <summary>
        /// Number of cells in the data set
        /// </summary>
        public int CellCount { get { return data[0].Length; } }

        /// <summary>
        /// Fetch the expression value for a specific gene and cell
        /// </summary>
        /// <param name="geneIdx"></param>
        /// <param name="cellIdx"></param>
        /// <returns></returns>
        public int GetValue(int geneIdx, int cellIdx)
        {
            return data[geneIdx][cellIdx];
        }

        /// <summary>
        /// Get the average count for a specific gene
        /// </summary>
        /// <param name="geneIdx"></param>
        /// <returns></returns>
        public double GeneMean(int geneIdx)
        {
            int n = 0;
            double sum = 0.0;
            foreach (int value in data[geneIdx])
            {
                n++;
                sum += value;
            }
            return sum / n;
        }

        /// <summary>
        /// Get the average count for a specific cell
        /// </summary>
        /// <param name="cellIdx"></param>
        /// <returns></returns>
        public double CellMean(int cellIdx)
        {
            int n = 0;
            double sum = 0.0;
            foreach (int value in IterCellValues(cellIdx))
            {
                n++;
                sum += value;
            }
            return sum / n;
        }

        /// <summary>
        /// Get the counts for all cells of a specific gene
        /// </summary>
        /// <param name="geneIdx"></param>
        /// <returns></returns>
        public int[] GetGeneValues(int geneIdx)
        {
            return data[geneIdx];
        }

        /// <summary>
        /// Iterate the counts for all genes of a specific cell
        /// </summary>
        /// <param name="cellIdx"></param>
        /// <returns></returns>
        public IEnumerable<int> IterCellValues(int cellIdx)
        {
            for (int row = 0; row < CellCount; row++)
                yield return data[row][cellIdx];
        }

        /// <summary>
        /// Iterate all counts in the whole table
        /// </summary>
        /// <param name="includeZeroGenes">If false, will skip genes with no expression</param>
        /// <returns></returns>
        public IEnumerable<int> IterValues(bool includeZeroGenes)
        {
            for (int geneIdx = 0; geneIdx < GeneCount; geneIdx++)
                if (includeZeroGenes || GeneMean(geneIdx) > 0)
                    foreach (int value in data[geneIdx])
                        yield return value;
        }

        /// <summary>
        /// Init from a expression.tab STRT pipeline output file
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public Expression(string file)
        {
            bool dataReached = false;
            int firstDataCol = 0;
            List<string> genes = new List<string>();
            using (StreamReader reader = new StreamReader(file))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!dataReached)
                    {
                        int p = line.IndexOf("Sample:\t");
                        if (p > -1)
                        {
                            cellNames = line.Substring(p + 7).Trim().Split('\t');
                            firstDataCol = line.Substring(0, p).Split('\t').Length;
                            Console.WriteLine("Firstdatacol:" + firstDataCol.ToString());
                        }
                        else if (line.StartsWith("Feature\t"))
                            dataReached = true;
                    }
                    else
                    {
                        if (line.StartsWith("RNA_SPIKE") || line.StartsWith("r_"))
                            continue;
                        string[] fields = line.Split('\t');
                        int nDataFields = fields.Length - firstDataCol;
                        if (nDataFields != cellNames.Length)
                            throw new IOException("File format error - Wrong number of data columns:\n" + line);
                        int[] geneData = new int[nDataFields];
                        for (int col = firstDataCol; col < fields.Length; col++)
                            geneData[col - firstDataCol] = int.Parse(fields[col]);
                        data.Add(geneData);
                        genes.Add(fields[0]);
                    }
                }
            }
            geneNames = genes.ToArray();            
        }

    }
}
