using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using System.Net.Mail;
using Linnarsson.Dna;
using Linnarsson.Strt;
using Linnarsson.Mathematics;

namespace C1
{
    /// <summary>
    /// Parser of C1 chip metadata, that reads info from text files in C1Props.props.C1RunsFolder, and
    /// inserts/updates data in the cells10k database.
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
        static bool runOnce = false;
        static string specificChipDir = "";
        static List<string> testedChips = new List<string>();
        static List<string> loadedChips = new List<string>();

        static void Main(string[] args)
        {
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
                                  "image and metadata files, and inserts data and image paths into the cells10k database." +
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
                loadedChips = new C1DB().GetLoadedChips().ConvertAll(d => d.Replace("-", ""));
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
                if (HasChanged(chipDir))
                {
                    string dirChipName = GetDirChipName(chipDir);
                    string msg = Copy(chipDir);
                    if (msg.StartsWith("OK"))
                    {
                        loadedChips.Add(dirChipName);
                        someCopyDone = true;
                        logWriter.WriteLine("{0} {1}: {2}", DateTime.Now.ToString(), chipDir, msg);
                        logWriter.Flush();
                    }
                    else if (!testedChips.Contains(dirChipName))
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " " + msg);
                        logWriter.Flush();
                        if (msg.StartsWith("ERROR"))
                            NotifyManager(chipDir, msg);
                    }
                    testedChips.Add(dirChipName);
                }
            }
            return someCopyDone;
        }

        private static void NotifyManager(string chipDir, string errormsg)
        {
            string from = Props.props.ProjectDBProcessorNotifierEmailSender;
            string subject = "C1Copier.exe error on loading " + chipDir;
            string body = "<html><p>" + errormsg + "</p><p>Please consult logfile " + logFile + ".</p></html>\n";
            string toEmails = Props.props.FailureReportEmail;
            MailMessage message = new MailMessage(from, toEmails, subject, body);
            message.IsBodyHtml = true;
            SmtpClient mailClient = new SmtpClient("localhost", 25);
            mailClient.Send(message);
        }

        private static string Copy(string chipDir)
        {
            try
            {
                Dictionary<string, string> metadata = ReadMetaData(chipDir);
                if (metadata == null)
                    return "WARNING: Skipped " + chipDir + " - missing metadata file.";
                List<Cell> celldata = ReadCellData(chipDir, metadata);
                if (celldata == null)
                    return "WARNING: Skipped " + chipDir + " - no celldata.";
                InsertCells(celldata);
                return loadedChips.Contains(GetDirChipName(chipDir)) ? "OK: Updated." : "OK: Loaded.";
            }
            catch (Exception e)
            {
                return "ERROR: Loading " + chipDir + " - " + e.ToString();
            }
        }

        private static void InsertCells(List<Cell> celldata)
        {
            C1DB db = new C1DB();
            foreach (Cell c in celldata)
            {
                db.InsertOrUpdateCell(c);
            }
        }

        private static string GetDirChipName(string chipDir)
        {
            return Path.GetFileName(chipDir).Replace("-", "");
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

        private static bool HasChanged(string chipDir)
        {
            string mp = GetMetaDataPath(chipDir);
            if (mp != null && (new FileInfo(mp).LastWriteTime > lastCopyTime || new FileInfo(mp).CreationTime > lastCopyTime)) return true;
            string dp = GetDonorFilePath(chipDir);
            if (dp != null && (new FileInfo(dp).LastWriteTime > lastCopyTime || new FileInfo(dp).CreationTime > lastCopyTime)) return true;
            string bf = GetLastMatchingFolder(chipDir, C1Props.props.C1BFImageSubfoldernamePattern);
            if (bf == null) return false;
            string lcp = GetLastMatchingFile(bf, C1Props.props.C1CaptureFilenamePattern);
            return (lcp != null && (new FileInfo(lcp).LastWriteTime > lastCopyTime || new FileInfo(lcp).CreationTime > lastCopyTime));
        }

        private static bool GetCellPaths(string chipId, out string chipFolder, out string BFFolder, out string lastCapPath)
        {
            lastCapPath = null;
            chipFolder = Path.Combine(C1Props.props.C1RunsFolder, chipId);
            BFFolder = GetLastMatchingFolder(chipFolder, C1Props.props.C1BFImageSubfoldernamePattern);
            if (BFFolder == null)
                return false;
            lastCapPath = GetLastMatchingFile(BFFolder, C1Props.props.C1CaptureFilenamePattern);
            return (lastCapPath != null);
        }

        private static string GetMetaDataPath(string chipDir)
        {
            return GetLastMatchingFile(chipDir, C1Props.props.C1MetadataFilenamePattern);
        }

        private static string GetDonorFilePath(string chipDir)
        {
            return GetLastMatchingFile(chipDir, C1Props.props.C1DonorDataFilenamePattern);
        }

        private static List<Cell> ReadCellData(string chipId, Dictionary<string, string> metadata)
        {
            List<Cell> cells = new List<Cell>();
            string chipFolder, BFFolder, lastCapPath;
            if (!GetCellPaths(chipId, out chipFolder, out BFFolder, out lastCapPath))
                return null;
            bool missing = false;
            using (StreamReader r = new StreamReader(lastCapPath))
            {
                string line = r.ReadLine(); // Header
                while ((line = r.ReadLine()) != null)
                {
                    if ((line = line.Trim()).Length == 0)
                        continue;
                    string[] fields = line.Split('\t');
                    string chipWell = string.Format("{0}{1:00}", fields[0], int.Parse(fields[1]));
                    string wellShort = fields[0] + fields[1];
                    double area = double.Parse(fields[3]);
                    double diameter = double.Parse(fields[4]);
                    DateTime dateOfRun = new DateTime(2010, 1, 1), dateDissected = new DateTime(2010, 1, 1);
                    DateTime.TryParse(metadata["date of run"], out dateOfRun);
                    DateTime.TryParse(metadata["datedissected"], out dateDissected);
                    int red = (fields.Length < 6) ? Detection.Unknown : (fields[5] == "1") ? Detection.Yes : Detection.No;
                    int green = (fields.Length < 7) ? Detection.Unknown : (fields[6] == "1") ? Detection.Yes : Detection.No;
                    int blue = (fields.Length < 8) ? Detection.Unknown : (fields[7] == "1") ? Detection.Yes : Detection.No;
                    Cell newCell = new Cell(chipWell, dateDissected, dateOfRun, diameter, area, red, green, blue, metadata);
                    List<CellImage> cellImages = new List<CellImage>();
                    foreach (string imgSubfolderPat in C1Props.props.C1AllImageSubfoldernamePatterns)
                    {
                        string imgFolder = GetLastMatchingFolder(chipFolder, imgSubfolderPat);
                        if (imgFolder == null)
                            continue;
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
                        string imgFolderName = Path.GetFileName(imgFolder);
                        Match m = Regex.Match(imgFolderName, "^(.+)_ [0-9]+$");
                        string reporter = m.Success ? m.Groups[1].Value : imgFolderName;
                        cellImages.Add(new CellImage(null, null, reporter, imgFolderName, Detection.Unknown, imgPath));
                    }
                    newCell.cellImages = cellImages;
                    cells.Add(newCell);
                }
            }
            return cells;
        }

        private static Dictionary<string, string> ReadMetaData(string chipDir)
        {
            string lastMetaFilePath = GetMetaDataPath(chipDir);
            if (lastMetaFilePath == null) return null;
            Dictionary<string, string> metadata = new Dictionary<string, string>();
            metadata["date of run"] = "2001-01-01";
            metadata["age"] = metadata["strain"] = metadata["treatment"] = metadata["tissue"] = "?";
            metadata["sex"] = "?";
            metadata["weight"] = metadata["donorid"] = "?";
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
                    string[] fields = GetFields(line);
                    string key = fields[0].Trim().ToLower();
                    string value = (fields.Length == 2) ? fields[1].Trim() : "";
                    if (key == "tissue/cell type/source") key = "tissue";
                    metadata[key] = value;
                }
            }
            metadata["chipfolder"] = chipDir;
            string chipId = C1DB.StandardizeChipId(Path.GetFileName(chipDir));
            metadata["chip serial number"] = chipId;
            ProjectDB pdb = new ProjectDB();
            metadata["principal investigator"] = pdb.TryGetPerson("jos_aaaclient", "principalinvestigator", metadata["principal investigator"], new Person(0, metadata["principal investigator"])).name;
            metadata["scientist"] = pdb.TryGetPerson("jos_aaacontact", "contactperson", metadata["scientist"], new Person(0, metadata["scientist"])).name;
            metadata["operator"] = pdb.TryGetPerson("jos_aaamanager", "person", metadata["operator"], new Person(0, metadata["operator"])).name;
            while (metadata["date of run"].StartsWith("0"))
                metadata["date of run"] = metadata["date of run"].Substring(1);
            metadata["datedissected"] = metadata["date of run"];
            string lastDonorFilePath = GetDonorFilePath(chipDir);
            if (lastDonorFilePath != null)
                AddDonorInfo(lastDonorFilePath, metadata);
            if (metadata["age"].Replace(" ", "").ToLower().EndsWith("weeks"))
            {
                try
                {
                    metadata["age"] = string.Format("p{0}", 7 * int.Parse(metadata["age"].Replace(" ", "").Replace("weeks", "")));
                }
                catch (Exception)
                {
                }
            }
            return metadata;
        }

        private static string[] GetFields(string line)
        {
            string[] fields = line.Split('\t');
            if (line.ToLower().StartsWith("strain"))
                fields = new string[] { "strain", line.Substring(7).Trim() };
            else if (line.ToLower().StartsWith("mouse_number"))
                fields = new string[] { "mouse_number", line.Substring(12).Trim() };
            else if (line.ToLower().StartsWith("comments"))
                fields = new string[] { "comments", line.Substring(8).Trim() };
            else if (fields.Length != 2)
            {
                fields = line.Split(':');
                if (fields.Length < 2)
                    fields = line.Split(new char[] { ' ' }, 2);
            }
            return fields;
        }

        /// <summary>
        /// Add mouse metadata from the "mice_metadata.txt" file
        /// </summary>
        /// <param name="donorFilePath"></param>
        /// <param name="metadata"></param>
        /// <returns>true if the data could be read</returns>
        private static bool AddDonorInfo(string donorFilePath, Dictionary<string, string> metadata)
        {
            using (StreamReader r = new StreamReader(donorFilePath))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (line == "" || line.StartsWith("#"))
                        continue;
                    string[] fields = GetFields(line);
                    if (fields.Length == 1)
                        continue;
                    string key = fields[0].Trim().ToLower();
                    if (key == "mouse_number" || key == "mouse number") key = "donorid";
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
            while (metadata["datedissected"].StartsWith("0"))
                metadata["datedissected"] = metadata["datedissected"].Substring(1);
            return true;
        }

    }
}
