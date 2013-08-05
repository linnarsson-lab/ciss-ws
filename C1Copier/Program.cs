using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Linnarsson.C1Model;
using Linnarsson.Strt;

namespace C1Copier
{
    class Program
    {
        static StreamWriter logWriter;
        static int minutesWait = 10;
        static int nExceptions = 0;
        static string c1RunsFolder = "/data2/c1-runs";
        static string captureFilename = "capture_report.txt";
        static string metadataFilename = "metadata.txt";
        static string imgSubfoldername = "";
        static string imgfilePattern = "well_*.png";

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
                        c1RunsFolder = args[++i];
                    else if (arg.StartsWith("-i"))
                        c1RunsFolder = arg.Substring(2);
                    else if (arg == "-l")
                        logFile = args[++i];
                    else if (arg.StartsWith("-l"))
                        logFile = arg.Substring(2);
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
                while (nExceptions < 10)
                {
                    try
                    {
                        bool copying = true;
                        while (copying)
                        {
                            copying = TryCopy(logWriter);
                        }
                    }
                    catch (Exception e)
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " ERROR: " + e.ToString());
                    }
                    Thread.Sleep(1000 * 60 * minutesWait);
                }
                logWriter.WriteLine(DateTime.Now.ToString() + "C1Copier quit");
            }
        }

        private static bool TryCopy(StreamWriter logWriter)
        {
            bool someCopyDone = false;
            string[] availableChips = Directory.GetDirectories(c1RunsFolder, "*-*-*");
            List<string> loadedPlateIds = new C1DB().GetAllPlateIds();
            foreach (string chipId in availableChips)
            {
                if (!loadedPlateIds.Contains(chipId))
                {
                    Dictionary<string, string> metadata = ReadMetaData(chipId);
                    InsertNewProject(metadata);
                    List<Cell> celldata = ReadCellData(chipId, metadata);
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

        private static List<Cell> ReadCellData(string chipId, Dictionary<string, string> metadata)
        {
            List<Cell> cells = new List<Cell>();
            string imgSubfolder = Path.Combine(Path.Combine(c1RunsFolder, chipId), imgSubfoldername);
            using (StreamReader r = new StreamReader(Path.Combine(imgSubfolder, captureFilename)))
            {
                string header = r.ReadLine();
                string line = r.ReadLine();
                while (line != null)
                {
                    string[] fields = line.Split('\t');
                    string well = fields[0] + fields[1];
                    double area = double.Parse(fields[3]);
                    double diameter = double.Parse(fields[4]);
                    string imgPath = Path.Combine(imgSubfolder, imgfilePattern.Replace("*", well));
                    if (!File.Exists(imgPath))
                        logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Image file does not exist: " + imgPath);
                    Cell newCell = new Cell(null, metadata["Plate"], well, diameter, area, 
                                            metadata["Principal Investigator"], metadata["Operator"]);
                    cells.Add(newCell);
                }
            }
            return cells;
        }

        private static Dictionary<string, string> ReadMetaData(string chipId)
        {
            Dictionary<string, string> data = new Dictionary<string,string>();
            using (StreamReader r = new StreamReader(Path.Combine(Path.Combine(c1RunsFolder, chipId), metadataFilename)))
            {
                string line = r.ReadLine();
                while (line != null)
                {
                    string[] fields = line.Split('\t');
                    data[fields[0].Trim()] = fields[1].Trim();
                    line = r.ReadLine();
                }
            }
            data["Plate"] = chipId;
            return data;
        }

        private static void InsertNewProject(Dictionary<string, string> metadata)
        {
            string plateId = new ProjectDB().InsertProject(metadata);
            metadata["PlateId"] = plateId;
        }

    }
}
