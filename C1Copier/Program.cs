using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using System.Globalization;
using Linnarsson.Dna;
using Linnarsson.Strt;
using Linnarsson.Mathematics;

namespace Linnarsson.C1
{
    /// <summary>
    /// Parser of C1 Cell metadata, that reads info from text files in C1Props.props.C1RunsFolder, and
    /// inserts/updates Cell data in the Sanger database.
    /// At startup, all available data files are processed, but at subsequence scans, at regular intervals, only
    /// new and changed metadata files are processed.
    /// If a metadata folder contains several files with names that match the pattern (from C1Props config)
    /// for the given metadata type, the last written file is selected.
    /// If there are several capture image folders, the one with the highest name in alphabetical order is selected.
    /// </summary>
    class C1Copier
    {
        static string logFile;
        static StreamWriter logWriter;
        static int minutesWait = 10;
        static int nExceptions = 0;
        static int maxNExceptions = 10;
        static DateTime lastCopyTime = new DateTime(2012, 1, 1);
        static List<string> copiedChipDirs = new List<string>();
        static bool runOnce = false;
        static string specificChipDir = "";
        static List<string> testedChipDirs = new List<string>();
        static string layoutFilePattern = Props.props.SampleLayoutFileFormat.Replace("{0}", "*");

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            logFile = new FileInfo("C1C_" + Process.GetCurrentProcess().Id + ".log").FullName;
            try
            {
                int i = 0;
                while (i < args.Length)
                {
                    string arg = args[i];
                    if (arg == "-i")
                        C1Props.props.C1RunsFolder = args[++i];
                    else if (arg.StartsWith("-i"))
                        C1Props.props.C1RunsFolder = arg.Substring(2);
                    else if (arg == "-l")
                        logFile = args[++i];
                    else if (arg.StartsWith("-l"))
                        logFile = arg.Substring(2);
                    else if (arg == "-s")
                        runOnce = true;
                    else if (arg == "-u")
                        specificChipDir = args[++i];
                    else throw new ArgumentException("OptionError");
                    i++;
                }
            }
            catch (Exception e)
            {
                if (!e.Message.Equals("OptionError"))
                    Console.WriteLine(e);
                Console.WriteLine("This program regularly (every " + minutesWait + " minutes) scans the C1 chip data folder, " +
                                  "defined by property C1RunsFolder in C1Config file, for new or updated cell, " +
                                  "image and files, and inserts cell data and image paths into the Sanger database.\n" +
                                  "Note that chips first have to be registered manually on the Sanger database web site." +
                                  "\nOptions:\n\n" +
                                  "-i <folder>   - specify a non-standard c1 input folder (default=" + C1Props.props.C1RunsFolder + ")\n" +
                                  "-l <file>     - specify a non-standard log file\n" +
                                  "-s            - run only a single time, then quit\n" +
                                  "-u <folder>   - load or update from a specific chip folder, then quit\n" +
                                  "                Will succeed even if donor/mouse file is missing.\n" +
                                  "Start with nohup and put in crontab for starting at every reboot.\n" +
                                  "\nLogfile defaults to a name with Pid included like: " + logFile);
                return;
            }
            if (specificChipDir != "")
            {
                Console.WriteLine(Copy(specificChipDir));
                return;
            }
            if (!File.Exists(logFile))
            {
                Console.WriteLine("Can not find {0}. Creating it.", logFile);
                File.Create(logFile).Close();
            }
            using (logWriter = new StreamWriter(File.Open(logFile, FileMode.Append)))
            {
                logWriter.WriteLine(DateTime.Now.ToString() + " Starting C1Copier");
                logWriter.Flush();
                Console.WriteLine("C1Copier started at " + DateTime.Now.ToString() + " and logging to " + logFile);
                while (nExceptions < maxNExceptions)
                {
                    try
                    {
                        DateTime startTime = DateTime.Now;
                        TryCopy(logWriter);
                        lastCopyTime = startTime;
                    }
                    catch (Exception e)
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " ERROR: " + e.ToString());
                        logWriter.Flush();
                    }
                    if (runOnce)
                        break;
                    Thread.Sleep(1000 * 60 * minutesWait);
                }
                logWriter.WriteLine(DateTime.Now.ToString() + " C1Copier quit");
                logWriter.Flush();
            }
        }

        private static bool TryCopy(StreamWriter logWriter)
        {
            bool someCopyDone = false;
            string[] availableChipDirs = Directory.GetDirectories(C1Props.props.C1RunsFolder, "*-*-*");
            foreach (string chipDir in availableChipDirs)
            {
                if (HasChanged(chipDir) || !copiedChipDirs.Contains(chipDir))
                {
                    string msg = Copy(chipDir);
                    if (msg.StartsWith("OK"))
                    {
                        someCopyDone = true;
                        copiedChipDirs.Add(chipDir);
                        logWriter.WriteLine("{0} {1}: {2}", DateTime.Now.ToString(), chipDir, msg);
                        logWriter.Flush();
                    }
                    else if (!testedChipDirs.Contains(chipDir))
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " " + msg);
                        logWriter.Flush();
                        if (msg.StartsWith("ERROR"))
                            NotifyManager(chipDir, msg);
                    }
                    testedChipDirs.Add(chipDir);
                }
            }
            return someCopyDone;
        }

        private static void NotifyManager(string chipDir, string errormsg)
        {
            string subject = "C1Copier.exe error on loading " + chipDir;
            string body = "<html><p>" + errormsg + "</p><p>Please consult logfile " + logFile + ".</p></html>\n";
            EmailSender.ReportFailureToAdmin(subject, body, true);
        }

        private static string Copy(string chipDir)
        {
            try
            {
                HashSet<string> emptyWells = ReadWellFile(chipDir, C1Props.props.WellExcludeFilePattern);
                List<Cell> celldata = ReadCellDataFromC1Files(chipDir, emptyWells);
                if (celldata == null) celldata = ReadCellDataFromLayoutFile(chipDir, emptyWells);
                if (celldata == null)
                {
                    if (specificChipDir == "")
                        return "WARNING: Skipped " + chipDir + " - no celldata.";
                    Console.WriteLine(DateTime.Now.ToString() + " " + chipDir + ": No celldata - faking 96 cells");
                    celldata = FakeCellData(emptyWells);
                }
                string chipId = GetChipIdFromChipDir(chipDir);
                return InsertCells(celldata, chipId);
            }
            catch (Exception e)
            {
                return "ERROR: Loading " + chipDir + " - " + e.ToString();
            }
        }

        private static string InsertCells(List<Cell> cells, string chipId)
        {
            ProjectDB pdb = new ProjectDB();
            int jos_aaachipid = pdb.GetIdOfChip(chipId);
            if (jos_aaachipid == -1)
                return "NOTICE: Chip " + chipId + " has not yet been registered in database.";
            foreach (Cell cell in cells)
            {
                cell.jos_aaachipid = jos_aaachipid;
                pdb.InsertOrUpdateCell(cell);
            }
            return "OK: Loaded.";
        }

        /// <summary>
        /// Convert 'XXXX-XXX-XXX' to 'XXXXXXX-XXX' if C1Prop ConvertChipIds==true
        /// </summary>
        /// <param name="chipDir"></param>
        /// <returns></returns>
        private static string GetChipIdFromChipDir(string chipDir)
        {
            string chipId = Path.GetFileName(chipDir);
            if (!C1Props.props.ConvertChipIds) return chipId;
            Match m = Regex.Match(chipId, "^([0-9][0-9][0-9][0-9])-([0-9][0-9][0-9]-[0-9][0-9][0-9])[-_]?[a-zA-Z]*$");
            if (m.Success)
                return m.Groups[1].Value + m.Groups[2].Value;
            return chipId;
        }

        /// <summary>
        /// Sort matching files in folder by last write time and return the most recent
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="filePattern"></param>
        /// <returns>null if no matching file is found</returns>
        private static string GetLastMatchingFile(string folder, string filePattern)
        {
            string[] matching = Directory.GetFiles(folder, filePattern);
            DateTime[] timestamps = Array.ConvertAll(matching, f => new FileInfo(f).LastWriteTime);
            if (matching.Length == 0)
                return null;
            Sort.QuickSort(timestamps, matching);
            return matching[matching.Length - 1];
        }
        /// <summary>
        /// Return the last by string comparison matching subfolders in folder. '*' in filePattern is taken as '[\-_0-9]*'
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="filePattern"></param>
        /// <returns>null if no matching folder is found</returns>
        private static string GetLastMatchingFolder(string folder, string filePattern)
        {
            string[] matching = Directory.GetDirectories(folder, filePattern);
            string rePat = filePattern;
            if (rePat.Contains("*")) rePat = rePat.Replace("*", "[\\-_0-9]*");
            if (!rePat.EndsWith("$")) rePat += "$";
            matching = Array.FindAll(matching, m => Regex.IsMatch(m, rePat));
            if (matching.Length == 0)
                return null;
            Array.Sort(matching);
            return matching[matching.Length - 1];
        }

        /// <summary>
        /// true if the "capture*txt" in last "BF*" folder, "wells_to_exclude.txt", or
        /// any of the "wells_positive_COLOR.txt" files have been updated
        /// </summary>
        /// <param name="chipDir"></param>
        /// <returns></returns>
        private static bool HasChanged(string chipDir)
        {
            string bf = GetLastMatchingFolder(chipDir, C1Props.props.C1BFImageSubfoldernamePattern);
            if (bf == null) return false;
            string cf = GetLastMatchingFile(bf, C1Props.props.C1CaptureFilenamePattern);
            bool cfNew = cf != null && (new FileInfo(cf).LastWriteTime > lastCopyTime || new FileInfo(cf).CreationTime > lastCopyTime);
            string xf = GetLastMatchingFile(chipDir, C1Props.props.WellExcludeFilePattern);
            bool xfNew = xf != null && (new FileInfo(xf).LastWriteTime > lastCopyTime || new FileInfo(xf).CreationTime > lastCopyTime);
            string rm = GetLastMatchingFile(chipDir, C1Props.props.WellMarkerFilePattern.Replace("COLOR", "red"));
            bool rmNew = rm != null && (new FileInfo(rm).LastWriteTime > lastCopyTime || new FileInfo(rm).CreationTime > lastCopyTime);
            string gm = GetLastMatchingFile(chipDir, C1Props.props.WellMarkerFilePattern.Replace("COLOR", "green"));
            bool gmNew = gm != null && (new FileInfo(gm).LastWriteTime > lastCopyTime || new FileInfo(gm).CreationTime > lastCopyTime);
            string bm = GetLastMatchingFile(chipDir, C1Props.props.WellMarkerFilePattern.Replace("COLOR", "blue"));
            bool bmNew = bm != null && (new FileInfo(bm).LastWriteTime > lastCopyTime || new FileInfo(bm).CreationTime > lastCopyTime);
            string lf = GetLastMatchingFile(chipDir, layoutFilePattern);
            bool lfNew = lf != null && (new FileInfo(lf).LastWriteTime > lastCopyTime || new FileInfo(lf).CreationTime > lastCopyTime);
            return (cfNew || xfNew || rmNew || gmNew || bmNew || lfNew);
        }

        /// <summary>
        /// get full path to chip folder, path to last "BF* folder", and path to the corresponding "capture*txt" file
        /// </summary>
        /// <param name="chipDir"></param>
        /// <param name="chipFolder"></param>
        /// <param name="BFFolder"></param>
        /// <param name="lastCapPath"></param>
        /// <returns>false if some file is missing</returns>
        private static bool GetCellPaths(string chipDir, out string chipFolder, out string BFFolder, out string lastCapPath)
        {
            lastCapPath = null;
            chipFolder = Path.Combine(C1Props.props.C1RunsFolder, chipDir);
            BFFolder = GetLastMatchingFolder(chipFolder, C1Props.props.C1BFImageSubfoldernamePattern);
            if (BFFolder == null)
                return false;
            lastCapPath = GetLastMatchingFile(BFFolder, C1Props.props.C1CaptureFilenamePattern);
            return (lastCapPath != null);
        }

        /// <summary>
        /// Use to read exclude-file, or manual read/green/blue well file
        /// </summary>
        /// <param name="chipDir"></param>
        /// <param name="filenamePat"></param>
        /// <returns>null if no file was found, otherwise the set of wells listed in the file formatted like 'A01'</returns>
        private static HashSet<string> ReadWellFile(string chipDir, string filenamePat)
        {
            HashSet<string> wells = null;
            string chipFolder = Path.Combine(C1Props.props.C1RunsFolder, chipDir);
            string wellFile = GetLastMatchingFile(chipFolder, filenamePat);
            if (wellFile != null)
            {
                wells = new HashSet<string>();
                using (StreamReader reader = new StreamReader(wellFile))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line == "" || line.StartsWith("#") || line.Contains("ow") || line.Contains("ol"))
                            continue;
                        line = line.Trim();
                        char row = line[0];
                        int idx = line.Contains("\t") ? line.IndexOf("\t") + 1 : 1;
                        int wellNo = int.Parse(line.Substring(idx));
                        string well = string.Format("{0}{1:00}", row, wellNo);
                        wells.Add(well);
                    }
                }
            }
            return wells;
        }

        private static List<Cell> FakeCellData(HashSet<string> emptyWells)
        {
            List<Cell> cells = new List<Cell>();
            foreach (char wr in "ABCDEFGH")
            {
                for (int wc = 1; wc <= 12; wc++)
                {
                    string chipwell = string.Format("{0}{1:00}", wr, wc);
                    Cell newCell = new Cell(null, 0, chipwell, "", 0.0, 0.0, 0, 0, 0, true);
                    newCell.valid = !emptyWells.Contains(chipwell);
                    cells.Add(newCell);
                }
            }
            return cells;
        }

        private static int GetColorStatus(string chipwell, string[] fields, int fieldIdx, HashSet<string> positiveWells)
        {
            int status = Detection.Unknown;
            if (positiveWells != null)
                status = positiveWells.Contains(chipwell) ? Detection.Yes : Detection.No;
            else if (fields.Length > fieldIdx)
                status = (fields[fieldIdx] == "1") ? Detection.Yes : Detection.No;
            return status;
        }

        private static List<Cell> ReadCellDataFromC1Files(string chipDir, HashSet<string> emptyWells)
        {
            string chipFolder, BFFolder, lastCapPath;
            if (!GetCellPaths(chipDir, out chipFolder, out BFFolder, out lastCapPath))
                return null;
            List<Cell> cells = new List<Cell>();
            HashSet<string> redWells = ReadWellFile(chipDir, C1Props.props.WellMarkerFilePattern.Replace("COLOR", "red"));
            HashSet<string> greenWells = ReadWellFile(chipDir, C1Props.props.WellMarkerFilePattern.Replace("COLOR", "green"));
            HashSet<string> blueWells = ReadWellFile(chipDir, C1Props.props.WellMarkerFilePattern.Replace("COLOR", "blue"));
            string[] colors = new string[] { "yellow", "green", "blue", "orange", "red", "magenta", "cyan", "bf" };
            bool missing = false;
            using (StreamReader r = new StreamReader(lastCapPath))
            {
                string line = r.ReadLine(); // Header
                while ((line = r.ReadLine()) != null)
                {
                    if ((line = line.Trim()).Length == 0)
                        continue;
                    string[] fields = line.Split('\t');
                    string chipwell = string.Format("{0}{1:00}", fields[0], int.Parse(fields[1]));
                    string wellShort = fields[0] + fields[1];
                    double area = double.Parse(fields[3]);
                    double diameter = double.Parse(fields[4]);
                    int red = GetColorStatus(chipwell, fields, 5, redWells);
                    int green = GetColorStatus(chipwell, fields, 6, greenWells);
                    int blue = GetColorStatus(chipwell, fields, 7, blueWells);
                    bool valid = (emptyWells == null) || !emptyWells.Contains(chipwell);
                    Cell newCell = new Cell(null, 0, chipwell, "", diameter, area, red, green, blue, valid);
                    List<CellImage> cellImages = new List<CellImage>();
                    HashSet<string> reporters = new HashSet<string>();
                    foreach (string imgSubfolderPat in C1Props.props.C1AllImageSubfoldernamePatterns)
                    {
                        string imgFolder = GetLastMatchingFolder(chipFolder, imgSubfolderPat);
                        if (imgFolder == null)
                            continue;
                        string imgFolderName = Path.GetFileName(imgFolder);
                        string reporter = imgFolderName.ToLower();
                        foreach (string color in colors)
                        {
                            if (reporter.Contains(color))
                            {
                                reporter = color;
                                break;
                            }
                        }
                        if (reporters.Contains(reporter)) continue;
                        reporters.Add(reporter);
                        string imgPath = Path.Combine(imgFolder, C1Props.props.C1ImageFilenamePattern.Replace("*", wellShort));
                        if (imgFolder == BFFolder && !File.Exists(imgPath))
                        {
                            if (!missing)
                            {
                                missing = true;
                                logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Some image file(s) does not exist: " + imgPath + "...");
                                logWriter.Flush();
                            }
                            continue;
                        }
                        cellImages.Add(new CellImage(null, null, reporter, imgFolderName, Detection.Unknown, imgPath));
                    }
                    newCell.cellImages = cellImages;
                    cells.Add(newCell);
                }
            }
            return cells;
        }

        private static string GetValueFromLayoutFile(string key, Dictionary<string, int> field2idx, string[] fields)
        {
            return field2idx.ContainsKey(key) ? fields[field2idx[key]] : null;
        }

        private static int ParseColor(string value)
        {
            if (value == null) return Detection.Unknown;
            if (value == "yes" || value == "y" || value == "1" || value == "x")
                return Detection.Yes;
            return Detection.No;
        }

        private static List<Cell> ReadCellDataFromLayoutFile(string chipDir, HashSet<string> emptyWells)
        {
            string[] cellfields = new string[] { "red", "green", "blue", "area", "diameter" };
            string layoutPath = GetLastMatchingFile(chipDir, layoutFilePattern);
            if (layoutPath == null) return null;
            List<Cell> cells = new List<Cell>();
            using (StreamReader r = new StreamReader(layoutPath))
            {
                string header = r.ReadLine().ToLower();
                string[] hfields = header.Split('\t');
                if (hfields.Length < 2 || hfields[0] != "well" || hfields[1] != "species")
                {
                    logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: layout file header has wrong format: " + layoutPath + "...");
                    logWriter.Flush();
                    return null;
                }
                Dictionary<string, int> field2idx = new Dictionary<string,int>();
                for (int i = 1; i < hfields.Length; i++)
                    field2idx[hfields[i]] = i;
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if ((line = line.Trim()).Length == 0)
                        continue;
                    string[] fields = line.Split('\t');
                    string well = fields[0];
                    string species = fields[1].ToLower();
                    bool valid = (species != "empty") && ((emptyWells == null) || !emptyWells.Contains(well));
                    double area, diameter;
                    double.TryParse(GetValueFromLayoutFile("area", field2idx, fields), out area);
                    double.TryParse(GetValueFromLayoutFile("red", field2idx, fields), out diameter);
                    int red = ParseColor(GetValueFromLayoutFile("red", field2idx, fields));
                    int green = ParseColor(GetValueFromLayoutFile("green", field2idx, fields));
                    int blue = ParseColor(GetValueFromLayoutFile("blue", field2idx, fields));
                    Cell newCell = new Cell(null, 0, well, "", diameter, area, red, green, blue, valid);
                    foreach (KeyValuePair<string, int> p in field2idx)
                    {
                        if (Array.IndexOf(cellfields, p.Key) == -1)
                            newCell.cellAnnotations.Add(new CellAnnotation(null, 0, p.Key, fields[p.Value]));
                    }
                    cells.Add(newCell);
                }
            }
            return cells;
        }

    }
}
