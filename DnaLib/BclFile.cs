﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Windows.Forms;

namespace Linnarsson.Dna
{
    public class BclFile
    {
        /// <summary>
        /// Reads BCL tile files and returns a stream of FastQRecords. The method will locate the relevant 
        /// Illumina config files to figure out which BCL file belongs to which read. It's ok (not much overhead) 
        /// to call this method repeatedly for all values of lane and read.
        /// </summary>
        /// <param name="runFolder">The full path to the run folder</param>
        /// <param name="lane">The lane (1-8)</param>
        /// <param name="read">The read (1-3)</param>
        /// <returns></returns>
        public static IEnumerable<FastQRecord> Stream(string runFolder, int lane, int read)
        {
            if (lane < 1 || lane > 8) throw new InvalidProgramException("Lane must be a number from 1 to 8");
            // Get the run id
            string[] items = Path.GetFileNameWithoutExtension(runFolder).Split('_');
            if (items.Length < 3) throw new InvalidDataException("Run folder does not seem to have a valid run ID");
            int r = 0;
            if (!int.TryParse(items[2], out r)) throw new InvalidDataException("Run folder does not seem to have a valid run ID");
            string runId = items[2];
            string flowcellId = items[3];

            string bcFolder = Path.Combine(runFolder, "Data");
            bcFolder = Path.Combine(bcFolder, "Intensities");
            bcFolder = Path.Combine(bcFolder, "BaseCalls");

            string laneFolder = Path.Combine(bcFolder, "L00" + lane.ToString());

            var config = XDocument.Load(Path.Combine(bcFolder, "config.xml"));

            // Make a list of the reads in this configuration (and their spans)
            var readConfig = from c in config.Descendants("Reads")
                             select new
                             {
                                 Index = int.Parse(c.Attribute("Index").Value),
                                 FirstCycle = int.Parse(c.Descendants("FirstCycle").First().Value),
                                 LastCycle = int.Parse(c.Descendants("LastCycle").First().Value)
                             };
            // fish out the one that corresponds to our read
            var readDescriptor = readConfig.FirstOrDefault((v) => v.Index == read);
            if (readDescriptor == null)
                yield break;
            // Make a list of tiles
            IEnumerable<int> tileConfig = from c in config.Descendants("Lane")
                             where c.Attribute("Index").Value == lane.ToString()
                             from v in c.Descendants("Tile")
                             select int.Parse(v.Value);
            List<int> vaildTiles = tileConfig.ToList();
            // Load all the filter files (file_without_extension->byte_array[numberOfClusters])
            Dictionary<string, byte[]> filters = new Dictionary<string, byte[]>();
            var filterFiles = Directory.GetFiles(laneFolder, "s_" + lane + "_*.filter");
            foreach (var f in filterFiles)
            {
                Match m = Regex.Match(f, "_([0-9]+)\\.filter$");
                if (m != null && vaildTiles.Contains(int.Parse(m.Groups[1].Value)))
                    filters[Path.GetFileNameWithoutExtension(f)] = File.ReadAllBytes(f);
            }

            // Parse out all the BCL files

            foreach (var tile in filters.Keys)
            {
                // Load all the cycles for this read & tile into memory
                List<byte[]> bclData = new List<byte[]>();
                for (int cycle = readDescriptor.FirstCycle; cycle <= readDescriptor.LastCycle; cycle++)
                {
                    // Find the cycle folder. Find the "C<cycle>.<y>" folder with the highest <y> (due to rerun cycles)
                    int rerunIndex = 1;
                    string cycleFolder = Path.Combine(laneFolder, "C" + cycle + "." + rerunIndex);
                    while (true)
                    {
                        rerunIndex++;
                        string temp = Path.Combine(laneFolder, "C" + cycle + "." + rerunIndex);
                        if (Directory.Exists(temp)) cycleFolder = temp;
                        else break;
                    }
                    if (!Directory.Exists(cycleFolder))
                    {
                        Console.Error.WriteLine("Could not find cycle folder: " + cycleFolder);
                        continue;
                    }
                    // Get the tile bcl data
                    string bclFile = Path.Combine(cycleFolder, tile + ".bcl");
                    if (!File.Exists(bclFile))
                    {
                        Console.Error.WriteLine("BCL file not found: " + bclFile);
                        continue;
                    }
                    bclData.Add(File.ReadAllBytes(bclFile));
                }

                char[] readSeq = new char[bclData.Count];
                byte[] quals = new byte[bclData.Count];
                StringBuilder sb = new StringBuilder();
                // Write out the tile data to the output file
                for (int ix = 0; ix < bclData[0].Length; ix++) // ix is an index into the clusters (i.e. it is a read)
                {
                    // make a header for the read
                    // Run0002_D0CYAAABXX_L1_R1_T1102_C34124 (run, lane, read, tile, cluster)
                    sb.Append("Run");
                    sb.Append(runId);
                    sb.Append("_");
                    sb.Append(flowcellId);
                    sb.Append("_L");
                    sb.Append(lane);
                    sb.Append("_R");
                    sb.Append(readDescriptor.Index);
                    sb.Append("_T");
                    sb.Append(tile.Split('_')[2]);
                    sb.Append("_C");
                    sb.Append(ix);
                    string hdr = sb.ToString();
                    sb.Length = 0;	// Clear it for the next read;

                    // make the read sequence
                    for (int c = 0; c < bclData.Count; c++)
                    {
                        int nt = (bclData[c][ix] & 3);
                        readSeq[c] = "ACGT"[nt];
                        quals[c] = (byte)((bclData[c][ix] & 252) >> 2);
                    }
                    bool pf = filters[tile][ix + 8] == 1;
                    yield return new FastQRecord(hdr, new string(readSeq), quals, pf);
                }
            }
        }

        public static bool ConvertToFastq(string runFolder, int lane, int read, StreamWriter PFWriter, StreamWriter nonPFWriter, byte qualityScoreBase,
                                          out int readLen, out int nPFReads, out int nNonPFReads)
        {
            readLen = nPFReads = nNonPFReads = 0;
            if (lane < 1 || lane > 8) throw new InvalidProgramException("Lane must be a number from 1 to 8");
            string[] items = Path.GetFileNameWithoutExtension(runFolder).Split('_');
            if (items.Length < 3) throw new InvalidDataException("Run folder does not seem to have a valid run ID");
            int r = 0;
            if (!int.TryParse(items[2], out r)) throw new InvalidDataException("Run folder does not seem to have a valid run ID");
            string runId = items[2];
            string flowcellId = items[3];
            string bcFolder = Path.Combine(runFolder, "Data");
            bcFolder = Path.Combine(bcFolder, "Intensities");
            bcFolder = Path.Combine(bcFolder, "BaseCalls");
            string laneFolder = Path.Combine(bcFolder, "L00" + lane.ToString());
            var config = XDocument.Load(Path.Combine(bcFolder, "config.xml"));
            // Make a list of the reads in this configuration (and their spans)
            var readConfig = from c in config.Descendants("Reads")
                             select new
                             {
                                 Index = int.Parse(c.Attribute("Index").Value),
                                 FirstCycle = int.Parse(c.Descendants("FirstCycle").First().Value),
                                 LastCycle = int.Parse(c.Descendants("LastCycle").First().Value)
                             };
            var readDescriptor = readConfig.FirstOrDefault((v) => v.Index == read);
            if (readDescriptor == null) return false;
            Dictionary<string, byte[]> filters = new Dictionary<string, byte[]>();
            var filterFiles = Directory.GetFiles(laneFolder, "s_" + lane + "_*.filter");
            foreach (var f in filterFiles)
            {
                filters[Path.GetFileNameWithoutExtension(f)] = File.ReadAllBytes(f);
            }
            foreach (var tile in filters.Keys)
            { // Load all the cycles for this read & tile into memory                
                List<byte[]> bclData = new List<byte[]>();
                for (int cycle = readDescriptor.FirstCycle; cycle <= readDescriptor.LastCycle; cycle++)
                { // Find the cycle folder. Find the "C<cycle>.<y>" folder with the highest <y> (due to rerun cycles)                   
                    int rerunIndex = 1;
                    string cycleFolder = Path.Combine(laneFolder, "C" + cycle + "." + rerunIndex);
                    while (true)
                    {
                        rerunIndex++;
                        string temp = Path.Combine(laneFolder, "C" + cycle + "." + rerunIndex);
                        if (Directory.Exists(temp)) cycleFolder = temp;
                        else break;
                    }
                    if (!Directory.Exists(cycleFolder))
                    {
                        Console.Error.WriteLine("Could not find cycle folder: " + cycleFolder);
                        continue;
                    }
                    string bclFile = Path.Combine(cycleFolder, tile + ".bcl");
                    if (!File.Exists(bclFile))
                    {
                        Console.Error.WriteLine("BCL file not found: " + bclFile);
                        continue;
                    }
                    bclData.Add(File.ReadAllBytes(bclFile));
                }
                readLen = bclData.Count;
                char[] readSeq = new char[readLen];
                char[] quals = new char[readLen];
                StreamWriter writer;
                string hdrStart = string.Format("Run{0}_{1}_L{2}_R{3}_T{4}_C", runId, flowcellId, lane, readDescriptor.Index, tile.Split('_')[2]);
                for (int ix = 0; ix < bclData[0].Length; ix++) // ix is an index into the clusters (i.e. it is a read)
                {
                    for (int c = 0; c < readLen; c++)
                    {
                        int nt = (bclData[c][ix] & 3);
                        readSeq[c] = "ACGT"[nt];
                        quals[c] = (char)(((bclData[c][ix] & 252) >> 2) + qualityScoreBase);
                    }
                    bool pf = filters[tile][ix + 8] == 1;
                    if (pf)
                    {
                        writer = PFWriter;
                        nPFReads++;
                    }
                    else
                    {
                        writer = nonPFWriter;
                        nNonPFReads++;
                    }
                    writer.WriteLine("@" + hdrStart + ix + Environment.NewLine +
                                     readSeq + Environment.NewLine +
                                     "+" + Environment.NewLine +
                                     quals);
                }
            }
            return (nPFReads + nNonPFReads) > 0;
        }

    }
}
