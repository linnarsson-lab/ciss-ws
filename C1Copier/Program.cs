using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
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
            List<string> loadedChips = new C1DB().GetLoadedChips().ConvertAll(d => d.Replace("-", ""));
            foreach (string chipDir in availableChipDirs)
            {
                string dirChipName = Path.GetFileName(chipDir).Replace("-", "");
                if (!loadedChips.Contains(dirChipName))
                {
                    logWriter.WriteLine(DateTime.Now.ToString() + " Loading " + chipDir + "...");
                    Dictionary<string, string> metadata = ReadMetaData(chipDir);
                    if (metadata == null)
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Skipping " + chipDir + " - no metadata.");
                        logWriter.Flush();
                        continue;
                    }
                    List<Cell> celldata = ReadCellData(chipDir, metadata);
                    if (celldata == null)
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Skipping " + chipDir + " - no celldata.");
                        logWriter.Flush();
                        continue;
                    }
                    logWriter.WriteLine(DateTime.Now.ToString() + " Copying data from " + chipDir + "...");
                    logWriter.Flush();
                    InsertCells(celldata);
                    someCopyDone = true;
                    loadedChips.Add(chipDir);
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
            bool missing = false;
            using (StreamReader r = new StreamReader(lastCapPath))
            {
                string line = r.ReadLine(); // Header
                while ((line = r.ReadLine()) != null)
                {
                    if ((line = line.Trim()).Length == 0)
                        continue;
                    string[] fields = line.Split('\t');
                    string well = string.Format("{0}{1:00}", fields[0], int.Parse(fields[1]));
                    double area = double.Parse(fields[3]);
                    double diameter = double.Parse(fields[4]);
                    int red = (fields.Length < 6) ? Detection.Unknown : (fields[5] == "1") ? Detection.Yes : Detection.No;
                    int green = (fields.Length < 7) ? Detection.Unknown : (fields[6] == "1") ? Detection.Yes : Detection.No;
                    int blue = (fields.Length < 8) ? Detection.Unknown : (fields[7] == "1") ? Detection.Yes : Detection.No;
                    Cell newCell = new Cell(null, metadata["chip serial number"], well, "", "", metadata["protocol"],
                                    DateTime.Parse(metadata["datedissected"]), DateTime.Parse(metadata["date of run"]),
                                    metadata["species"], metadata["strain"], metadata["donorid"],
                                    metadata["age"], metadata["sex"][0], metadata["tissue"],
                                    metadata["treatment"], diameter, area, metadata["principal investigator"], metadata["operator"],
                                    metadata["scientist"], metadata["comments"], red, green, blue);
                    List<CellImage> cellImages = new List<CellImage>();
                    foreach (string imgSubfolderPat in C1Props.props.C1AllImageSubfoldernamePatterns)
                    {
                        string imgFolder = GetLastMatchingFolder(chipFolder, imgSubfolderPat);
                        if (imgFolder == null)
                            continue;
                        string imgPath = Path.Combine(imgFolder, C1Props.props.C1ImageFilenamePattern.Replace("*", well));
                        if (imgFolder == BFFolder && !File.Exists(imgPath))
                        {
                            if (!missing)
                            {
                                missing = true;
                                logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Image file(s) does not exist: " + imgPath + "...");
                                logWriter.Flush();
                            }
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
            Dictionary<string, string> metadata = new Dictionary<string, string>();
            metadata["date of run"] = "2001-01-01";
            metadata["age"] = metadata["strain"] = metadata["treatment"] = metadata["tissue"] = metadata["sex"] = "?";
            metadata["operator"] = metadata["scientist"] = metadata["principal investigator"] = "?";
            metadata["comments"] = "";
            metadata["spikes"] = C1Props.props.SpikeMoleculeCount.ToString();
            using (StreamReader r = new StreamReader(lastMetaFilePath))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (line == "" || line.StartsWith("#"))
                        continue;
                    string[] fields = line.Split('\t');
                    string key = fields[0].Trim().ToLower();
                    if (key == "tissue/cell type/source") key = "tissue";
                    metadata[key] = fields[1].Trim();
                }
            }
            metadata["chipfolder"] = chipDir;
            string chipId = C1DB.StandardizeChipId(Path.GetFileName(chipDir));
            metadata["chip serial number"] = chipId;
            ProjectDB pdb = new ProjectDB();
            metadata["principal investigator"] = pdb.TryGetPerson("jos_aaaclient", "principalinvestigator", metadata["principal investigator"], new Person(0, metadata["principal investigator"])).name;
            metadata["scientist"] = pdb.TryGetPerson("jos_aaacontact", "contactperson", metadata["scientist"], new Person(0, metadata["scientist"])).name;
            metadata["operator"] = pdb.TryGetPerson("jos_aaamanager", "person", metadata["operator"], new Person(0, metadata["operator"])).name;
            AddDonorInfo(chipDir, metadata);
            return metadata;
        }

        private static void AddDonorInfo(string chipDir, Dictionary<string, string> metadata)
        {
            metadata["datedissected"] = metadata["date of run"];
            metadata["donorid"] = "";
            string lastDonorFilePath = GetLastMatchingFile(chipDir, C1Props.props.C1DonorDataFilenamePattern);
            if (lastDonorFilePath == null) return;
            using (StreamReader r = new StreamReader(lastDonorFilePath))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (line == "" || line.StartsWith("#"))
                        continue;
                    string[] fields = line.Split('\t');
                    if (fields.Length == 1 && line.Split(' ').Length == 2)
                        fields = line.Split(' ');
                    string key = fields[0].Trim().ToLower();
                    if (key == "mouse_number") key = "donorid";
                    if (key == "date") key = "datedissected";
                    if (key == "gender") key = "sex";
                    if (key == "comments" && fields[1].Trim().Length > 0)
                    {
                        metadata["comments"] += ((metadata["comments"].Length>0)? " / " : "") + fields[1].Trim();
                        continue;
                    }
                    metadata[key] = fields[1].Trim();
                }
            }
        }

    }
}
