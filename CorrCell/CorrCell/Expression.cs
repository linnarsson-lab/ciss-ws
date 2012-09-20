using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Linnarsson.Mathematics;

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

        /// <summary>
        /// Lower case of gene names
        /// </summary>
        private string[] geneNames;
        public string GetGeneName(int geneIdx)
        {
            return geneNames[geneIdx];
        }
        /// <summary>
        /// Find the index of a gene
        /// </summary>
        /// <param name="geneName"></param>
        /// <returns>-1 if not found</returns>
        public int GetGeneIdx(string geneName)
        {
            return Array.IndexOf(geneNames, geneName.ToLower());
        }

        /// <summary>
        /// data[genes][cells]
        /// </summary>
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
        /// Get the counts for all cells of a specific gene
        /// </summary>
        /// <param name="geneIdx"></param>
        /// <returns></returns>
        public int[] GetGeneValues(int geneIdx)
        {
            return data[geneIdx];
        }

        /// <summary>
        /// Get the total count for a specific cell
        /// </summary>
        /// <param name="cellIdx"></param>
        /// <returns></returns>
        public double CellSum(int cellIdx)
        {
            double sum = 0.0;
            foreach (int value in IterCellValues(cellIdx))
                sum += value;
            return sum;
        }

        /// <summary>
        /// Iterate the counts for all genes of a specific cell
        /// </summary>
        /// <param name="cellIdx"></param>
        /// <returns></returns>
        public IEnumerable<int> IterCellValues(int cellIdx)
        {
            for (int geneIdx = 0; geneIdx < GeneCount; geneIdx++)
                yield return data[geneIdx][cellIdx];
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
                        else if (line.StartsWith("Feature\t") && cellNames != null)
                            dataReached = true;
                        else if (line.StartsWith("\t") || line.StartsWith("Feature\t"))
                        {
                            Match m = Regex.Match(line, "\t[ABCDEFGH]0[0-9]\t[ABCDEFGH]0[0-9]\t");
                            if (m.Success)
                            {
                                dataReached = true;
                                cellNames = line.Substring(m.Index + 1).Trim().Split('\t');
                                firstDataCol = line.Substring(0, m.Index + 1).Split('\t').Length;
                                Console.WriteLine("Firstdatacol:" + firstDataCol.ToString());
                            }
                        }
                    }
                    else
                    {
                        if (line.StartsWith("RNA_SPIKE") || line.StartsWith("r_") || line.StartsWith("SingleMol"))
                            continue;
                        string[] fields = line.Split('\t');
                        if (fields[0] == "")
                            continue;
                        int nDataFields = fields.Length - firstDataCol;
                        if (nDataFields != cellNames.Length)
                            throw new IOException("File format error - Wrong number of data columns:\n" + line);
                        int[] geneData = new int[nDataFields];
                        for (int col = firstDataCol; col < fields.Length; col++)
                            geneData[col - firstDataCol] = int.Parse(fields[col]);
                        data.Add(geneData);
                        genes.Add(fields[0].ToLower());
                    }
                }
            }
            if (!dataReached)
                throw new IOException("File format error - The last line before data must start with 'Feature'!");
            geneNames = genes.ToArray();            
        }

        /// <summary>
        /// Calculate the average total per cell
        /// </summary>
        /// <returns></returns>
        public double AllCellMean()
        {
            double total = 0.0;
            foreach (int value in IterValues(true)) total += value;
            return total / CellCount;
        }

        /// <summary>
        /// Remove cells that have total expression less than threshold parts of the average cell total
        /// </summary>
        /// <param name="fractionThreshold">A value of 100 will remove cells with total less than 1/100 of the average cell</param>
        public void FilterEmptyCells(double fractionThreshold)
        {
            double threshold = AllCellMean() / fractionThreshold;
            List<int> keepCells = new List<int>();
            for (int cellIdx = 0; cellIdx < CellCount; cellIdx++)
            {
                if (CellSum(cellIdx) >= threshold)
                    keepCells.Add(cellIdx);
            }
            for (int geneIdx = 0; geneIdx < data.Count; geneIdx++)
            {
                int[] newGeneData = new int[keepCells.Count];
                for (int i = 0; i < keepCells.Count; i++)
                    newGeneData[i] = data[geneIdx][keepCells[i]];
                data[geneIdx] = newGeneData;
            }
            List<string> keepCellNames = new List<string>();
            foreach (int cellIdx in keepCells)
                keepCellNames.Add(cellNames[cellIdx]);
            cellNames = keepCellNames.ToArray();
        }

        /// <summary>
        /// Remove genes that have average expression level lower than minExprLevel
        /// </summary>
        /// <param name="minExprLevel"></param>
        public void FilterLowGenes(double minExprLevel)
        {
            List<int> keepGenes = new List<int>();
            for (int geneIdx = 0; geneIdx < data.Count; geneIdx++)
            {
                if (GeneMean(geneIdx) >= minExprLevel)
                    keepGenes.Add(geneIdx);
            }
            List<string> keepGeneNames = new List<string>();
            List<int[]> newData = new List<int[]>();
            foreach (int keepGeneIdx in keepGenes)
            {
                newData.Add(data[keepGeneIdx]);
                keepGeneNames.Add(geneNames[keepGeneIdx]);
            }
            data = newData;
            geneNames = keepGeneNames.ToArray();
        }

        /// <summary>
        /// Shuffle values randomly within cells between genes of similar expression levels.
        /// This result is a data set that should be un-correlated between gene pairs but preserve global
        /// dependencies on expression level and cell totals.
        /// </summary>
        public void ShuffleBetweenSimilarLevelGenes()
        {
            Random rnd = new Random(DateTime.Now.Millisecond);
            List<double> geneMeans = new List<double>(data.Count);
            List<int> geneIndices = new List<int>(data.Count);
            for (int geneIdx = 0; geneIdx < GeneCount; geneIdx++)
            {
                geneMeans.Add(GeneMean(geneIdx));
                geneIndices.Add(geneIdx);
            }
            Sort.QuickSort(geneMeans, geneIndices);
            for (int takeIdx = 0; takeIdx < GeneCount; takeIdx++)
            {
                for (int cellIdx = 0; cellIdx < CellCount; cellIdx++)
                {
                    int exchangeIdx = takeIdx + rnd.Next(21) - 10;
                    while (exchangeIdx == takeIdx || exchangeIdx < 0 || exchangeIdx >= data.Count)
                        exchangeIdx = takeIdx + rnd.Next(21) - 10;
                    int temp = data[geneIndices[takeIdx]][cellIdx];
                    data[geneIndices[takeIdx]][cellIdx] = data[geneIndices[exchangeIdx]][cellIdx];
                    data[geneIndices[exchangeIdx]][cellIdx] = temp;
                }
            }
        }

        public void TotalShuffle()
        {
            Random rnd = new Random(DateTime.Now.Millisecond);
            for (int takeGeneIdx = 0; takeGeneIdx < GeneCount; takeGeneIdx++)
            {
                for (int takeCellIdx = 0; takeCellIdx < CellCount; takeCellIdx++)
                {
                    int putGeneIdx = rnd.Next(GeneCount);
                    while (putGeneIdx == takeGeneIdx) putGeneIdx = rnd.Next(GeneCount);
                    int putCellIdx = rnd.Next(CellCount);
                    while (putCellIdx == takeGeneIdx) putCellIdx = rnd.Next(CellCount);
                    int temp = data[takeGeneIdx][takeCellIdx];
                    data[takeGeneIdx][takeCellIdx] = data[putCellIdx][putCellIdx];
                    data[putCellIdx][putCellIdx] = temp;
                }
            }
        }

    }
}
