using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    public class PathHandler
    {
        private Props props;

        public PathHandler(Props props)
        {
            this.props = props;
        }

        /// <summary>
        /// Locates the folder where bowtie stores its indexes
        /// </summary>
        /// <returns>Folder for bowtie indexes</returns>
        public static string GetBowtieIndicesFolder()
        {
            string pathVar = Environment.GetEnvironmentVariable("PATH");
            string[] vars = pathVar.Contains(";")? pathVar.Split(';') : pathVar.Split(':');
            foreach (string v in vars)
                if (v.Contains("bowtie")) return Path.Combine(v, "indexes");
            return Props.props.BowtieIndexFolder;
        }

        /// <summary>
        /// Tries to find a bowtie index version, including create date, with read length matchin or close below genome.ReadLen
        /// </summary>
        /// <param name="genome"></param>
        /// <returns>Empty string if none found</returns>
        public static string GetIndexVersion(StrtGenome genome)
        {
            string indexName = genome.GetBowtieSplcIndexName();
            if (indexName == "")
                return "";
            string testFile = Path.Combine(GetBowtieIndicesFolder(), indexName + ".1.ebwt");
            FileInfo fInfo = new FileInfo(testFile);
            return indexName + fInfo.CreationTime.ToString("yyMMdd");
        }

        /// <summary>
        /// Generates a dictionary from chromosome Ids (without any "chr") to sequences.
        /// The sequences may be .gz compressed.
        /// </summary>
        /// <param name="genome">Genome to pick sequences from</param>
        /// <param name="strtFiles">True to map chr:s from strt genome mao, false for original chr:s</param>
        /// <returns></returns>
        public static Dictionary<string, string> GetGenomeFilesMap(StrtGenome genome, bool strtFiles)
        {
            string genomeFolder = (strtFiles)? genome.GetStrtGenomesFolder() : genome.GetOriginalGenomeFolder();
            string[] chrFiles = GetFilesOrGz(genomeFolder, "chr*");
            Dictionary<string, string> chrIdToFileMap = new Dictionary<string, string>();
            foreach (string filePath in chrFiles)
            {
                string filename = Path.GetFileName(filePath);
                if (genome.IsChrInBuild(Path.GetFileName(filename)))
                {
                    string chrId = ExtractChrId(filename);
                    chrId = StrtGenome.ConvertIfAnnotation(chrId);
                    chrIdToFileMap[chrId] = filePath;
                }
            }
            return chrIdToFileMap;
        }
        private static string[] GetFilesOrGz(string folder, string pattern)
        {
            string[] chrFiles = Directory.GetFiles(folder, pattern);
            string[] chrGzFiles = Directory.GetFiles(folder, pattern + ".gz");
            if (chrGzFiles.Length > chrFiles.Length)
                return chrGzFiles;
            return chrFiles;
        }
        private static string ExtractChrId(string filename)
        {
            Match m = Regex.Match(filename, "chromosome\\.([^\\.]+)\\.");
            if (!m.Success)
                m = Regex.Match(filename, "chr_?([^\\.]+)\\.");
            string chrId = m.Groups[1].Value;
            return chrId;
        }

        public static string GetSampleLayoutPath(string projectNameOrFolder)
        {
            string layoutFolder = Props.props.SampleLayoutFileFolder;
            if (layoutFolder == "" || layoutFolder == null)
                layoutFolder = GetRootedProjectFolder(projectNameOrFolder);
            string projectName = Path.GetFileName(projectNameOrFolder);
            string layoutFilename = string.Format(Props.props.SampleLayoutFileFormat, projectName);
            return Path.Combine(layoutFolder, layoutFilename);
        }

        public static string GetSyntLevelFile(string projectFolder)
        {
            return Path.Combine(projectFolder, "Run00000_L0_1_" + Props.props.TestAnalysisFileMarker + ".levels");
        }

        public string[] GetRepeatMaskFiles(StrtGenome genome)
        {
            string genomeFolder = genome.GetOriginalGenomeFolder();
            string[] rmskFiles = Directory.GetFiles(genomeFolder, "*rmsk.txt.gz");
            if (rmskFiles.Length == 0)
                rmskFiles = Directory.GetFiles(genomeFolder, "*rmsk.txt");
            return rmskFiles;
        }

        private static string GetReadFileMatchPattern(string runNoOrFlowcellId)
        {
            string matchPat = "Run*_L{0}_1_*_?" + runNoOrFlowcellId + ".*"; // FlowcellId pattern
            int runNo;
            if (int.TryParse(runNoOrFlowcellId, out runNo))
                matchPat = "Run" + string.Format("{0:00000}", runNo) + "_L{0}_*.*"; // RunNo pattern
            return matchPat;
        }

        /// <summary>
        /// Checks that all read files are collected by checking that the statistics files have been generated
        /// in the /data/reads/statistics directory.
        /// </summary>
        /// <param name="runId">Either a run number, a run folder, or a flowcell id</param>
        /// <param name="laneNumbers">a string of lane numbers</param>
        /// <returns>The run number if all are ready, else -1</returns>
        public static int CheckReadsCollected(string runId, string laneNumbers)
        {
            if (runId.Contains('_'))
                runId = Regex.Match(runId, "_([0-9]+)_").Groups[1].Value;
            string matchPat = GetReadFileMatchPattern(runId);
            string readStatFolder = Path.Combine(Props.props.ReadsFolder, "statistics");
            int runNo = -1;
            foreach (char laneNo in laneNumbers)
            {
                string readStatFilePat = string.Format(matchPat, laneNo);
                string[] statsFiles = Directory.GetFiles(readStatFolder, readStatFilePat);
                if (statsFiles.Length == 0) return -1;
                runNo = int.Parse(Path.GetFileName(statsFiles[0]).Substring(3, 5));
            }
            return runNo; 
        }

        public static List<LaneInfo> ListReadsFiles(List<string> laneArgs)
        {
            List<LaneInfo> extrInfos = new List<LaneInfo>();
            foreach (string laneArg in laneArgs)
            {
                string[] parts = laneArg.Split(':');
                string runId = parts[0];
                string matchPat = GetReadFileMatchPattern(runId);
                foreach (char laneNo in parts[1])
                {
                    string readFilePat = string.Format(matchPat, laneNo);
                    string[] laneFiles = Directory.GetFiles(Props.props.ReadsFolder, readFilePat);
                    if (laneFiles.Length > 0)
                        extrInfos.Add(new LaneInfo(laneFiles[0], runId, laneNo));
                }
            }
            return extrInfos;
        }

        /// <summary>
        /// Returns rooted path to the Reads folder in projectFolder.
        /// Changes nothing if projectFolder is rooted and ends with Reads/ or Reads\.
        /// </summary>
        /// <param name="projectFolder"></param>
        /// <returns></returns>
        public static string GetReadsFolder(string projectFolder)
        {
            projectFolder = GetRootedProjectFolder(projectFolder);
            string readsFolder = projectFolder;
            if (!readsFolder.TrimEnd(new char[] { '/', '\\' }).EndsWith("Reads"))
                readsFolder = Path.Combine(projectFolder, "Reads");
            return readsFolder;
        }

        /// <summary>
        /// List all sequence files contained in folder.
        /// Exclude files that were produced by random tag filtering.
        /// </summary>
        /// <param name="folder">Either a ...Lxxx/Reads/ reads folder, or a project folder or project name</param>
        /// <returns></returns>
        public static List<string> CollectReadsFilesNames(string folder)
        {
            string readsFolder = GetReadsFolder(folder);
            List<string> files = new List<string>();
            files.AddRange(Directory.GetFiles(readsFolder, "*.fq"));
            files.AddRange(Directory.GetFiles(readsFolder, "*.fq.gz"));
            files.AddRange(Directory.GetFiles(readsFolder, "*.fastq"));
            files.AddRange(Directory.GetFiles(readsFolder, "*.fastq.gz"));
            files.AddRange(Directory.GetFiles(readsFolder, "*sequence.txt"));
            files.AddRange(Directory.GetFiles(readsFolder, "*sequence.txt.gz"));
            files.AddRange(Directory.GetFiles(readsFolder, "*qseq.txt"));
            files.AddRange(Directory.GetFiles(readsFolder, "*qseq.txt.gz"));
            files.AddRange(Directory.GetFiles(readsFolder, "*.fasta"));
            files.RemoveAll((s) => (s.Contains("duplicated_tags") || s.Contains("unique_tags")));
            return files;
        }

        /// <summary>
        /// If input is the name of a project folder, it is rooted in the project directory.
        /// If input is an Extracted folder, return the rooted project folder
        /// </summary>
        /// <param name="projectFolderOrName"></param>
        /// <returns></returns>
        public static string GetRootedProjectFolder(string projectFolderOrName)
        {
            projectFolderOrName = GetRooted(projectFolderOrName);
            if (Path.GetFileName(projectFolderOrName).IndexOf("_ExtractionVer") >= 0)
                projectFolderOrName = Path.GetDirectoryName(projectFolderOrName);
            return projectFolderOrName;
        }

        public static string GetRooted(string projectFolderOrName)
        {
            if (!Path.IsPathRooted(projectFolderOrName))
                projectFolderOrName = Path.Combine(Props.props.ProjectsFolder, projectFolderOrName);
            return projectFolderOrName;
        }

        public static string MakeExtractionSummaryPath(string mapFilePath)
        {
            Match m = Regex.Match(mapFilePath, "^.*Run[0-9]+_L[0-9]+_[0-9]_[0-9]+");
            return mapFilePath.Substring(0, m.Length) + "_summary.txt";
        }

        public static string extractedFolderMakePattern = "{0}_ExtractionVer{1}_{2}";
        public static string extractedFolderMatchPattern = "^[^_]+_ExtractionVer([0-9]+)_[^_]+$";
        public string MakeExtractedFolder(string projectFolder, string barcodeSet, string extractionVersion)
        {
            string projectName = Path.GetFileName(projectFolder);
            return Path.Combine(projectFolder, string.Format(extractedFolderMakePattern, projectName, extractionVersion, barcodeSet));
        }

        /// <summary>
        /// Finds the latest Extracted folder inside input Project Folder,
        /// or just adds any missing root to an input Extracted folder.
        /// </summary>
        /// <param name="inputFolder"></param>
        /// <returns>Path to the latest Extracted folder in inputFolder, 
        ///          or the rooted inputFolder if it already was the path of 
        ///          an Extracted folder</returns>
        public static string GetLatestExtractedFolder(string inputFolder)
        {
            inputFolder = GetRooted(inputFolder);
            string latestExtracted = inputFolder;
            if (!inputFolder.Contains("_ExtractionVer"))
            {
                List<string> extractedDirs = new List<string>();
                string[] allDirs = Directory.GetDirectories(inputFolder);
                foreach (string dir in allDirs)
                {
                    Match m = Regex.Match(dir, extractedFolderMatchPattern);
                    if (m.Success) extractedDirs.Add(dir);
                }
                if (extractedDirs.Count == 0)
                    throw new FileNotFoundException("No extracted data folders found in " + inputFolder);
                extractedDirs.Sort();
                latestExtracted = extractedDirs[extractedDirs.Count - 1];
            }
            return latestExtracted;
        }

        /// <summary>
        /// Get the version of extraction from an extracted folder path
        /// </summary>
        /// <param name="extractedFolder"></param>
        /// <returns>"0" if path is not parsable</returns>
        public static string GetExtractionVersion(string extractedFolder)
        {
            Match m = Regex.Match(extractedFolder, extractedFolderMatchPattern);
            if (m.Success) return m.Groups[1].Value;
            return "0";
        }

        /// <summary>
        /// Checks if either 'filePath', or 'filePath.gz' exists.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>The path that existed, or null if neither did.</returns>
        public static string ExistsOrGz(string path)
        {
            if (File.Exists(path)) return path;
            if (File.Exists(path + ".gz")) return path + ".gz";
            return null;
        }

        public static string MakeRunDataSubPath()
        {
            return Path.Combine("Data", Path.Combine("Intensities", "BaseCalls"));
        }

        public static string MakeBarcodeFilePath(string bcSetName)
        {
            string bcPath = Path.Combine(Props.props.ProjectsFolder, "barcodes");
            return Path.Combine(bcPath, bcSetName + ".barcodes");
        }

        public static string ParseBarcodeSet(string extractedFolder)
        {
            Match m = Regex.Match(extractedFolder, ".*Extract[^_]+_([^_]+)");
            if (m != null)
                return m.Groups[1].Value;
            return Props.props.DefaultBarcodeSet;
        }

        public string GetChrCTRLPath()
        {
            return Path.Combine(props.GenomesFolder, "chr" + StrtGenome.chrCTRLId + ".fa");
        }

        public string GetCTRLGenesPath()
        {
            return Path.Combine(props.GenomesFolder, "SilverBulletCTRL.txt");
        }

        public static string MakeSafeFilename(string name)
        {
            string safeName = name;
            foreach (char c in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(c, '_');
            return safeName;
        }
    }
}
