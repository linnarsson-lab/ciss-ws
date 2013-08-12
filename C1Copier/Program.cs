using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Linnarsson.Strt;

namespace C1
{
    class C1Copier
    {
        static StreamWriter logWriter;
        static int minutesWait = 10;
        static int nExceptions = 0;
        static int maxNExceptions = 10;
        static bool runOnce = false;

        static void Main(string[] args)
        {
            string logFile = new FileInfo("C1C_" + Process.GetCurrentProcess().Id + ".log").FullName;
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
                    else throw new ArgumentException("OptionError");
                    i++;
                }
            }
            catch (Exception e)
            {
                if (!e.Message.Equals("OptionError"))
                    Console.WriteLine(e);
                Console.WriteLine("\nOptions:\n\n" +
                                  "-i<file>     - specify a non-standard c1 input folder\n" +
                                  "-l<file>     - specify a non-standard log file\n" +
                                  "-s           - run only a single time, then quit\n" +
                                  "Put in crontab for starting at every reboot.\n" +
                                  "\nLogfile defaults to a name with Pid included like: " + logFile);
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
                        TryCopy(logWriter);
                    }
                    catch (Exception e)
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " ERROR: " + e.ToString());
                    }
                    if (runOnce)
                        break;
                    Thread.Sleep(1000 * 60 * minutesWait);
                }
                logWriter.WriteLine(DateTime.Now.ToString() + " C1Copier quit");
            }
        }

        private static bool TryCopy(StreamWriter logWriter)
        {
            bool someCopyDone = false;
            string[] availableChipDirs = Directory.GetDirectories(C1Props.props.C1RunsFolder, "*-*-*");
            List<string> loadedChipDirs = new ProjectDB().GetProjectColumn("plateid", "C1-%", "platereference");
            foreach (string chipDir in availableChipDirs)
            {
                if (!loadedChipDirs.Contains(chipDir))
                {
                    Dictionary<string, string> metadata = ReadMetaData(chipDir);
                    if (metadata == null)
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Skipping " + chipDir + " - no metadata file found.");
                        continue;
                    }
                    metadata["Chipfolder"] = chipDir;
                    InsertNewProject(metadata);
                    List<Cell> celldata = ReadCellData(chipDir, metadata);
                    if (celldata == null)
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Skipping " + chipDir + " - no celldata found.");
                        continue;
                    }
                    InsertCells(celldata);
                    someCopyDone = true;
                }
            }
            return someCopyDone;
        }

        private static void InsertCells(List<Cell> celldata)
        {
            C1DB db = new C1DB();
            foreach (Cell c in celldata)
            {
                db.InsertCell(c);
            }
        }

        private static string GetLastMatchingFile(string folder, string filePattern)
        {
            string[] matching = Directory.GetFiles(folder, filePattern);
            if (matching.Length == 0)
                return null;
            Array.Sort(matching);
            return matching[matching.Length - 1];
        }
        private static string GetLastMatchingFolder(string folder, string filePattern)
        {
            string[] matching = Directory.GetDirectories(folder, filePattern);
            if (matching.Length == 0)
                return null;
            Array.Sort(matching);
            return matching[matching.Length - 1];
        }

        private static List<Cell> ReadCellData(string chipId, Dictionary<string, string> metadata)
        {
            List<Cell> cells = new List<Cell>();
            string chipFolder = Path.Combine(C1Props.props.C1RunsFolder, chipId);
            string BFFolder = GetLastMatchingFolder(chipFolder, C1Props.props.C1BFImageSubfoldernamePattern);
            if (BFFolder == null)
                return null;
            string lastCapPath = GetLastMatchingFile(BFFolder, C1Props.props.C1CaptureFilenamePattern);
            if (lastCapPath == null) return null;
            using (StreamReader r = new StreamReader(lastCapPath))
            {
                string line = r.ReadLine(); // Header
                while ((line = r.ReadLine()) != null)
                {
                    if ((line = line.Trim()).Length == 0)
                        continue;
                    string[] fields = line.Split('\t');
                    string well = string.Format("{0}{1:00}", fields[0], fields[1]);
                    double area = double.Parse(fields[3]);
                    double diameter = double.Parse(fields[4]);
                    Cell newCell = new Cell(null, metadata["Plate"], well, metadata["Protocol"],
                                    DateTime.Parse(metadata["Date of Run"]), metadata["Species"],
                                    "", "", '-', metadata["Tissue/cell type/source"], "", diameter, area, 
                                    metadata["Principal Investigator"], metadata["Operator"], metadata["Comments"]);
                    List<CellImage> cellImages = new List<CellImage>();
                    foreach (string imgSubfolderPat in C1Props.props.C1AllImageSubfoldernamePatterns)
                    {
                        string imgFolder = GetLastMatchingFolder(chipFolder, imgSubfolderPat);
                        Console.WriteLine(imgFolder);
                        if (imgFolder == null)
                            continue;
                        string imgPath = Path.Combine(imgFolder, C1Props.props.C1ImageFilenamePattern.Replace("*", well));
                        Console.WriteLine(imgPath);
                        if (imgFolder == BFFolder && !File.Exists(imgPath))
                        {
                            logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Image file does not exist: " + imgPath);
                            continue;
                        }
                        string imgFolderName = Path.GetFileName(imgFolder);
                        cellImages.Add(new CellImage(null, null, imgFolderName, imgFolderName, Detection.Unknown, imgPath));
                    }
                    newCell.cellImages = cellImages;
                    cells.Add(newCell);
                }
            }
            return cells;
        }

        private static Dictionary<string, string> ReadMetaData(string chipDir)
        {
            string lastMetaFilePath = GetLastMatchingFile(chipDir, C1Props.props.C1MetadataFilenamePattern);
            if (lastMetaFilePath == null) return null;
            Dictionary<string, string> data = new Dictionary<string, string>();
            using (StreamReader r = new StreamReader(lastMetaFilePath))
            {
                string line = r.ReadLine();
                while (line != null)
                {
                    string[] fields = line.Split('\t');
                    data[fields[0].Trim()] = fields[1].Trim();
                    line = r.ReadLine();
                }
            }
            string chipId = Path.GetFileName(chipDir);
            data["Plate"] = chipId;
            return data;
        }

        /// <summary>
        /// Insert a new project into STRT pipeline
        /// </summary>
        /// <param name="m">metadata from the C1 folder</param>
        private static void InsertNewProject(Dictionary<string, string> m)
        {
            string layoutFile = ""; // TODO: May be wanted to bring more specific metadata on each cell
            string chipId = m["Chip serial number"];
            string sp = m["Species"].ToLower();
            if (sp == "mouse" || sp.StartsWith("mus")) sp = "Mm";
            if (sp == "human" || sp.StartsWith("homo")) sp = "Hs";
            ProjectDescription pd = new ProjectDescription(m["Scientist"], m["Operator"], m["Principal Investigator"],
                chipId, DateTime.Parse(m["Date of Run"]), ("C1-"+chipId), m["Chipfolder"], sp, m["Tissue/cell type/source"],
                "single cell", "C1", "", m["Protocol"], "Tn5", "", layoutFile, m["Comments"], 0);
            new ProjectDB().InsertNewProject(pd);
        }

    }
}
