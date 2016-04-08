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
        // Read folder stuff
        public static readonly string readFileAndLaneFolderCreatePattern = "Run{0:00000}_L{1}_{2}_{3}"; // 0=RunNo - 1=LaneNo - 2=ReadNo - 3=Date or RunFolderName
        public static readonly string readFileAndLaneFolderMatchPat = "Run([0-9]+)_L([0-9])_([0-9])_([0-9]+)"; // RunNo - LaneNo - ReadNo - Date or RunFolderName
        public static readonly string readFileGlobPatWFlowCellId = "Run*_L{1}_{2}_*_?{0}"; // 0=FlowCellId - 1=LaneNo - 2=ReadNo
        public static readonly string readStatsSubFolder = "statistics";
        public static readonly string nonPFReadsSubFolder = "nonPF";

        // Extraction folder stuff
        private static readonly string extractionFolderCenter = "_ExtractionVer";
        public static readonly string extractionFolderCreatePattern = "{0}" + extractionFolderCenter + "{1}_{2}";
        public static readonly string extractionFolderMatchPattern = extractionFolderCenter + "([0-9]+)_(.+)$";
        public static readonly string extractionSummaryFilename = "summary.txt";

        public static readonly string ctrlConcFilename = "SilverBulletCTRLConc.txt";

        private static string CreateReadFilename(string runFolderName, int runNo, int lane, int read)
        {
            return string.Format(readFileAndLaneFolderCreatePattern, runNo, lane, read, runFolderName);
        }
        public static string GetPFFilePath(string readsFolder, string runFolderName, int runNo, int lane, int read)
        {
            return Path.Combine(readsFolder, CreateReadFilename(runFolderName, runNo, lane, read) + ".fq.gz");
        }
        public static string ConvertToNonPFFilePath(string PFFilePath)
        {
            string nonPFFilename = Path.GetFileName(PFFilePath).Replace(".fq", "_nonPF.fq");
            string nonPFDir = Path.Combine(Path.GetDirectoryName(PFFilePath), nonPFReadsSubFolder);
            return Path.Combine(nonPFDir, nonPFFilename);
        }
        public static string GetReadStatsFilePath(string readsFolder, string runFolderName, int runNo, int lane, int read)
        {
            return Path.Combine(readsFolder, Path.Combine(readStatsSubFolder, CreateReadFilename(runFolderName, runNo, lane, read) + ".txt"));
        }

        /// <summary>
        /// Replace the "NNbp" part of a splc map file with "*" to enable searching of arbitrary read length splice files
        /// </summary>
        /// <param name="mapFile"></param>
        /// <returns></returns>
        public static string StarOutReadLenInSplcMapFile(string mapFile)
        {
            Match m = Regex.Match(mapFile, "(.+_)[0-9]+(bp.+)");
            return m.Groups[1].Value + "*" + m.Groups[2].Value;
        }

        /// <summary>
        /// Construct the proper path to the layout file. It is located inside project folder,
        /// unless a specific file has been specified with Props.LayoutFile.
        /// </summary>
        /// <param name="projectNameOrFolder"></param>
        /// <returns>Path to project layout file, even if it does not exist.</returns>
        public static string GetLayoutPath(string projectNameOrFolder)
        {
            if (Props.props.LayoutFile != "")
                return Props.props.LayoutFile;
            string layoutFolder = GetRootedProjectFolder(projectNameOrFolder);
            string projectName = Path.GetFileName(projectNameOrFolder);
            string layoutFilename = string.Format(Props.props.SampleLayoutFileFormat, projectName);
            return Path.Combine(layoutFolder, layoutFilename);
        }

        public static string GetSyntLevelFilePath(string projectFolder, bool useRndTags)
        {
            string extension = "." + (useRndTags ? "mol" : "read") + "levels";
            string syntPat = string.Format(readFileAndLaneFolderCreatePattern, 0, 0, 1, "*") + "_" + Props.props.TestAnalysisFileMarker + "*" + extension;
            string[] syntPatMatches = Directory.GetFiles(projectFolder, syntPat);
            return (syntPatMatches.Length == 1) ? syntPatMatches[0] : "";
        }
        public static string MakeSyntLevelFileHead(string dataId)
        {
            return Path.Combine(Props.props.ReadsFolder, string.Format(readFileAndLaneFolderCreatePattern, 0, 0, 1, DateTime.Now.ToString("yyMMdd")) +
                                                         "_" + Props.props.TestAnalysisFileMarker + "_0000_" + dataId);
        }

        /// <summary>
        /// Return an array of the repeat mask files of the given genome
        /// </summary>
        /// <param name="genome"></param>
        /// <returns></returns>
        public static string[] GetRepeatMaskFiles(StrtGenome genome)
        {
            return GetRepeatMaskFiles(genome.GetOriginalGenomeFolder());
        }
        public static string[] GetRepeatMaskFiles(string genomeFolder)
        {
            string[] rmskFiles = Directory.GetFiles(genomeFolder, "*rmsk.txt.gz");
            if (rmskFiles.Length == 0)
                rmskFiles = Directory.GetFiles(genomeFolder, "*rmsk.txt");
            return rmskFiles;
        }

        public static string GetGVFFile(StrtGenome genome)
        {
            string genomeFolder = genome.GetOriginalGenomeFolder();
            string[] gvfFiles = Directory.GetFiles(genomeFolder, "*_incl_consequences.gvf");
            if (gvfFiles.Length == 0)
                return "";
            return gvfFiles[0];
        }

        public static string GetReadFileMatchPattern(string runNoOrFlowcellId, char laneNo, char readNo, string extension)
        {
            int runNo;
            if (int.TryParse(runNoOrFlowcellId, out runNo))
                return string.Format(readFileAndLaneFolderCreatePattern, runNo, laneNo, readNo, "*") + extension; // arg was RunNo
            else
                return string.Format(readFileGlobPatWFlowCellId, runNoOrFlowcellId, laneNo, readNo) + extension; // arg was FlowcellId 
        }

        /// <summary>
        /// Checks that all read files are collected by checking that the statistics files have been generated
        /// in the /data/reads/statistics directory.
        /// </summary>
        /// <param name="runId">Either a run number, a run folder, or a flowcell id</param>
        /// <param name="laneNumbers">a string of lane numbers</param>
        /// <param name="highestRequiredReadNo">Highest read number needed (1,2, or 3)</param>
        /// <returns>The run number if all are ready, else -1</returns>
        public static int CheckReadsCollected(string runId, string laneNumbers, char highestRequiredReadNo)
        {
            if (runId.Contains('_'))
                runId = Regex.Match(runId, "_([0-9]+)_").Groups[1].Value;
            string readStatFolder = Path.Combine(Props.props.ReadsFolder, readStatsSubFolder);
            int runNo = -1;
            foreach (char laneNo in laneNumbers)
            {
                string laneStatFileMatchPat = GetReadFileMatchPattern(runId, laneNo, highestRequiredReadNo, ".txt");
                string[] statsFiles = Directory.GetFiles(readStatFolder, laneStatFileMatchPat);
                if (statsFiles.Length == 0) return -1;
                runNo = int.Parse(Path.GetFileName(statsFiles[0]).Substring(3, 5));
            }
            return runNo; 
        }

        /// <summary>
        /// List all sequence files contained in folder.
        /// </summary>
        /// <param name="readsFolder">Directory containing .fq, .qseq, or .fasta (.gz) files</param>
        /// <returns></returns>
        public static List<string> ListAllSeqFiles(string readsFolder)
        {
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
            return files;
        }

        /// <summary>
        /// If input is the name of a project folder, it is rooted in the project directory.
        /// If input is an Extraction folder, return the rooted project folder
        /// </summary>
        /// <param name="projectFolderOrName"></param>
        /// <returns></returns>
        public static string GetRootedProjectFolder(string projectFolderOrName)
        {
            projectFolderOrName = GetRooted(projectFolderOrName);
            if (Path.GetFileName(projectFolderOrName).IndexOf(extractionFolderCenter) >= 0)
                projectFolderOrName = Path.GetDirectoryName(projectFolderOrName);
            return projectFolderOrName;
        }

        /// <summary>
        /// Expand the argument to a full project folder path, if needed
        /// </summary>
        /// <param name="projectFolderOrName"></param>
        /// <returns></returns>
        public static string GetRooted(string projectFolderOrName)
        {
            if (!Path.IsPathRooted(projectFolderOrName))
                projectFolderOrName = Path.Combine(Props.props.ProjectsFolder, projectFolderOrName);
            return projectFolderOrName;
        }

        /// <summary>
        /// Construct an Extraction folder name and return it combined as a subfolder to the projectFolder path
        /// </summary>
        /// <param name="projectFolder"></param>
        /// <param name="barcodeSet"></param>
        /// <param name="extractionVersion"></param>
        /// <returns></returns>
        public static string MakeExtractionFolderSubPath(string projectFolder, string barcodeSet, string extractionVersion)
        {
            string projectName = Path.GetFileName(projectFolder);
            return Path.Combine(projectFolder, string.Format(extractionFolderCreatePattern, projectName, extractionVersion, barcodeSet));
        }

        /// <summary>
        /// Finds the latest Extraction folder inside input Project Folder,
        /// or just adds any missing root to an input Extracted folder.
        /// </summary>
        /// <param name="inputFolder"></param>
        /// <returns>Path to the latest Extraction folder in inputFolder, 
        ///          or the rooted inputFolder if it already was the path of 
        ///          an Extraction folder</returns>
        public static string GetLatestExtractionFolder(string inputFolder)
        {
            inputFolder = GetRooted(inputFolder);
            string latestExtracted = inputFolder;
            if (!inputFolder.Contains(extractionFolderCenter))
            { // inputFolder was a project folder
                string projectName = Path.GetFileName(inputFolder);
                string pat = projectName + extractionFolderMatchPattern;
                List<string> extractedDirs = new List<string>();
                string[] allDirs = Directory.GetDirectories(inputFolder);
                foreach (string dir in allDirs)
                {
                    Match m = Regex.Match(dir, pat);
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
        /// Get the version of extraction from an Extraction folder path
        /// </summary>
        /// <param name="extractedFolder"></param>
        /// <returns>"0" if path is not parsable</returns>
        public static string GetExtractionVersion(string extractedFolder)
        {
            Match m = Regex.Match(extractedFolder, extractionFolderMatchPattern);
            if (m.Success) return m.Groups[1].Value;
            return "0";
        }

        /// <summary>
        /// Checks if either 'filePath', or 'filePath.gz' exists. Returns the existing path, or null if not.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>The path that existed, or null if neither did.</returns>
        public static string ExistsOrGz(string path)
        {
            if (File.Exists(path)) return path;
            if (File.Exists(path + ".gz")) return path + ".gz";
            return null;
        }

        /// <summary>
        /// Return "Data/Intensities/BaseCalls" with proper path delimiter
        /// </summary>
        /// <returns></returns>
        public static string MakeRunDataSubPath()
        {
            return Path.Combine("Data", Path.Combine("Intensities", "BaseCalls"));
        }

        /// <summary>
        /// Return the full path to a barcode set definition file
        /// </summary>
        /// <param name="barcodeSetName"></param>
        /// <returns></returns>
        public static string MakeBarcodeFilePath(string barcodeSetName)
        {
            return Path.Combine(Props.props.BarcodesFolder, barcodeSetName + ".barcodes");
        }

        /// <summary>
        /// Return an array of all (lower case-converted) barcode set names defined by barcode definition files
        /// </summary>
        /// <returns></returns>
        public static string[] GetAllCustomBarcodeSetNames()
        {
            string[] bcFiles = Directory.GetFiles(Props.props.BarcodesFolder, "*.barcodes");
            return Array.ConvertAll(bcFiles, f => Path.GetFileNameWithoutExtension(f).ToLower());
        }

        /// <summary>
        /// Extract the barcode set name from the path name of an Extracted reads folder
        /// </summary>
        /// <param name="extractedFolder"></param>
        /// <returns>null if path is not parsable</returns>
        public static string ParseBarcodeSet(string extractedFolder)
        {
            Match m = Regex.Match(extractedFolder, extractionFolderMatchPattern);
            return (m != null) ? m.Groups[2].Value : null;
        }

        /// <summary>
        /// Return the full path to a CTRL or EXTRA chromosome sequence
        /// </summary>
        /// <param name="commonChrId"></param>
        /// <returns></returns>
        public static string GetCommonChrPath(string commonChrId)
        {
            return Path.Combine(Props.props.GenomesFolder, "chr" + commonChrId + ".fa");
        }

        /// <summary>
        /// Return the full path to a CTRL or EXTRA chromosome (RefFlat style) annotation file
        /// </summary>
        /// <param name="commonChrId"></param>
        /// <returns></returns>
        public static string GetCommonGenesPath(string commonChrId)
        {
            return Path.Combine(Props.props.GenomesFolder, "SilverBullet" + commonChrId + ".txt");
        }

        /// <summary>
        /// Return the full path to the TAB-file giving CTRL gene concentrations
        /// </summary>
        /// <returns></returns>
        public static string GetCTRLConcPath()
        {
            return Path.Combine(Props.props.GenomesFolder, ctrlConcFilename);
        }

        /// <summary>
        /// Replace any characters in name that are not valid in filenames with '_'
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string MakeSafeFilename(string name)
        {
            string safeName = name;
            foreach (char c in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(c, '_');
            return safeName;
        }
    }
}
