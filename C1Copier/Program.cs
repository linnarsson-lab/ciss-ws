using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Linnarsson.Strt;
using Linnarsson.Mathematics;

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
            List<string> loadedChipDirs = new ProjectDB().GetProjectColumn("plateid", C1Props.C1ProjectPrefix+"%", "platereference");
            foreach (string chipDir in availableChipDirs)
            {
                if (!loadedChipDirs.Contains(chipDir))
                {
                    Dictionary<string, string> metadata = ReadMetaData(chipDir);
                    if (metadata == null)
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Skipping " + chipDir + " - no metadata.");
                        logWriter.Flush();
                        continue;
                    }
                    metadata["Chipfolder"] = chipDir;
                    metadata["Chip serial number"] = C1DB.StandardizeChipId(metadata["Chip serial number"]);
                    List<Cell> celldata = ReadCellData(chipDir, metadata);
                    if (celldata == null)
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Skipping " + chipDir + " - no celldata.");
                        logWriter.Flush();
                        continue;
                    }
                    logWriter.WriteLine(DateTime.Now.ToString() + " Copying data from " + chipDir + "...");
                    logWriter.Flush();
                    InsertNewProject(metadata);
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

        /// <summary>
        /// Sort matching files in folder by last write time and return the most recent
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="filePattern"></param>
        /// <returns></returns>
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
        /// Sort matching files in folder and return the last by string comparison
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="filePattern"></param>
        /// <returns></returns>
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
                    Detection red = (fields.Length < 6)? Detection.Unknown : (fields[5] == "1") ? Detection.Yes : Detection.No;
                    Detection green = (fields.Length < 7) ? Detection.Unknown : (fields[6] == "1") ? Detection.Yes : Detection.No;
                    Detection blue = (fields.Length < 8) ? Detection.Unknown : (fields[7] == "1") ? Detection.Yes : Detection.No;
                    Cell newCell = new Cell(null, metadata["Plate"], well, metadata["Protocol"],
                                    DateTime.Parse(metadata["Date of Run"]), metadata["Species"],
                                    metadata["Strain"], metadata["Age"], metadata["Sex"][0], metadata["Tissue/cell type/source"],
                                    metadata["Treatment"], diameter, area, metadata["Principal Investigator"], metadata["Operator"],
                                    metadata["Comments"], red, green, blue);
                    List<CellImage> cellImages = new List<CellImage>();
                    foreach (string imgSubfolderPat in C1Props.props.C1AllImageSubfoldernamePatterns)
                    {
                        string imgFolder = GetLastMatchingFolder(chipFolder, imgSubfolderPat);
                        if (imgFolder == null)
                            continue;
                        string imgPath = Path.Combine(imgFolder, C1Props.props.C1ImageFilenamePattern.Replace("*", well));
                        if (imgFolder == BFFolder && !File.Exists(imgPath))
                        {
                            logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Image file does not exist: " + imgPath);
                            logWriter.Flush();
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
            data["Age"] = data["Strain"] = data["Treatment"] = data["Tissue"] = data["Sex"] = "?";
            data["Spikes"] = C1Props.props.SpikeMoleculeCount.ToString();
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
            chipId = VerifyChipfolder(m["Chipfolder"], chipId);
            string species = m["Species"].ToLower();
            if (species == "mouse" || species.StartsWith("mus")) species = "Mm";
            if (species == "human" || species.StartsWith("homo")) species = "Hs";
            ProjectDescription pd = new ProjectDescription(m["Scientist"], m["Operator"], m["Principal Investigator"],
                chipId, DateTime.Parse(m["Date of Run"]), (C1Props.C1ProjectPrefix+chipId), m["Chipfolder"], species, m["Tissue/cell type/source"],
                "single cell", "C1", "", m["Protocol"], "Tn5", "", layoutFile, m["Comments"], int.Parse(m["Spikes"]));
            new ProjectDB().InsertNewProject(pd);
        }

        private static string VerifyChipfolder(string chipFolder, string chipId)
        {
            string folderId = Path.GetFileName(chipFolder);
            folderId = C1DB.StandardizeChipId(folderId);
            if (chipId != folderId && new ProjectDB().GetProjectColumn("plateid", C1Props.C1ProjectPrefix + chipId, "platereference").Count > 0)
            {
                chipId = folderId;
                logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Mismatching folder and chipid in " + chipFolder + ". Changing id to " + chipId);
                logWriter.Flush();
            }
            return chipId;
        }

    }
}
