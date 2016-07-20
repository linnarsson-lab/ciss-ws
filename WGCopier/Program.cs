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

namespace Linnarsson.C1
{
    class WGCopierProgram
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
        static Dictionary<string, int> sourceWell2SubBarcodeIdx = new Dictionary<string, int>();

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            logFile = new FileInfo("WG_" + Process.GetCurrentProcess().Id + ".log").FullName;
            try
            {
                int i = 0;
                while (i < args.Length)
                {
                    string arg = args[i];
                    if (arg == "-i")
                        C1Props.props.WGRunsFolder = args[++i];
                    else if (arg.StartsWith("-i"))
                        C1Props.props.WGRunsFolder = arg.Substring(2);
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
                Console.WriteLine("This program regularly (every " + minutesWait + " minutes) scans the WG chip data folder, " +
                                  "defined by property WGRunsFolder in C1Config file, for new or updated " +
                                  "cell data and inserts into the Sanger database.\n" +
                                  "Note that chips first have to be registered manually on the Sanger database web site." +
                                  "\nOptions:\n\n" +
                                  "-i <folder>   - specify a non-standard WG run folder (default=" + C1Props.props.WGRunsFolder + ")\n" +
                                  "-l <file>     - specify a non-standard log file\n" +
                                  "-s            - run only a single time, then quit\n" +
                                  "-u <folder>   - load or update from a specific chip folder, then quit\n" +
                                  "                Will succeed even if donor/mouse file is missing.\n" +
                                  "Start with nohup and put in crontab for starting at every reboot.\n" +
                                  "\nLogfile defaults to a name with Pid included like: " + logFile);
                return;
            }
            ReadSource2BcIdx(C1Props.props.WGIdxFilePath);
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
                logWriter.WriteLine(DateTime.Now.ToString() + " Starting WGCopier");
                logWriter.Flush();
                Console.WriteLine("WGCopier started at " + DateTime.Now.ToString() + " and logging to " + logFile);
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
                logWriter.WriteLine(DateTime.Now.ToString() + " WGCopier quit");
                logWriter.Flush();
            }
        }

        private static void ReadSource2BcIdx(string path)
        {
            using (StreamReader r = new StreamReader(path))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if ((line = line.Trim()).Length == 0)
                        continue;
                    string[] fields = line.Split('\t');
                    string sourceWell = fields[0];
                    int bcIdx = int.Parse(fields[1]);
                    sourceWell2SubBarcodeIdx[sourceWell] = bcIdx;
                }
            }
        }

        private static bool TryCopy(StreamWriter logWriter)
        {
            bool someCopyDone = false;
            string[] availableChipDirs = Directory.GetDirectories(C1Props.props.WGRunsFolder, "WG*");
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
            string subject = "WGCopier.exe error on loading " + chipDir;
            string body = "<html><p>" + errormsg + "</p><p>Please consult logfile " + logFile + ".</p></html>\n";
            EmailSender.ReportFailureToAdmin(subject, body, true);
        }

        private static string Copy(string chipDir)
        {
            try
            {
                List<Cell> celldata = ReadCellDataFromWGDir(chipDir);
                if (celldata == null)
                    return "WARNING: Skipped " + chipDir + " - no celldata.";
                string chipId = Path.GetFileName(chipDir);
                return InsertCells(celldata, chipId);
            }
            catch (Exception e)
            {
                return "ERROR: Loading " + chipDir + " - " + e.ToString();
            }
        }

        private static List<Cell> ReadCellDataFromWGDir(string chipDir)
        {
            string primerFile = GetPrimerFilePath(chipDir);
            if (!File.Exists(primerFile))
                return null;
            List<Cell> cells = new List<Cell>();
            using (StreamReader r = new StreamReader(primerFile))
            {
                string line = r.ReadLine(); // Header
                while ((line = r.ReadLine()) != null)
                {
                    if ((line = line.Trim()).Length == 0)
                        continue;
                    string[] fields = line.Split('\t');
                    string chipId = fields[0];
                    string patch = fields[1];
                    string posInPatch = fields[2];
                    string subwell = "0" + posInPatch[1] + "0" + posInPatch[3];
                    string primerSourceWell = fields[3];
                    int subBarcodeNo = sourceWell2SubBarcodeIdx[primerSourceWell];
                    string mainWell = string.Format("{0}{1:00}", patch[0], int.Parse(patch.Substring(1)));
                    string chipWell = string.Format("{0}-W{1:00}", mainWell, subBarcodeNo);
                    double area = 0.0, diameter = 0.0;
                    int red = 0, green = 0, blue = 0;
                    bool valid = true;
                    int subBarcodeIdx = subBarcodeNo - 1;
                    Cell newCell = new Cell(null, 0, 0, chipWell, chipWell, diameter, area, red, green, blue, valid, subwell, subBarcodeIdx);
                    // Here should read image filenames (and possibly more data) from the "...WellList.TXT" file
                    List<CellImage> cellImages = new List<CellImage>();
                    foreach (string imgSubfolderPat in new string[] {})
                    {
                        string imgFolder = "";
                        string imgFolderName = Path.GetFileName(imgFolder);
                        string reporter = "";
                        string imgPath = "";
                        cellImages.Add(new CellImage(null, null, reporter, imgFolderName, Detection.Unknown, imgPath));
                    }
                    newCell.cellImages = cellImages;
                    cells.Add(newCell);
                }
            }
            return cells;
        }

        private static string InsertCells(List<Cell> cells, string chipId)
        {
            IDB pdb = DBFactory.GetProjectDB();
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
        /// true if the "capture*txt" in last "BF*" folder, "wells_to_exclude.txt", or
        /// any of the "wells_positive_COLOR.txt" files have been updated
        /// </summary>
        /// <param name="chipDir"></param>
        /// <returns></returns>
        private static string GetPrimerFilePath(string chipDir)
        {
            string chipId = Path.GetFileName(chipDir).Replace("WG", "");
            return Path.Combine(chipDir, C1Props.props.WGPrimerFilePathPattern.Replace("*", chipId));
        }

        public static bool HasChanged(string chipDir)
        {
            string primerFilePath = GetPrimerFilePath(chipDir);
            return File.Exists(primerFilePath) && (new FileInfo(primerFilePath).LastWriteTime > lastCopyTime || new FileInfo(primerFilePath).CreationTime > lastCopyTime);
        }

    }
}
