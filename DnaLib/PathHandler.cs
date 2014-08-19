﻿using System;
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
        public static readonly string extractionSummaryFilename = "summary.txt";
        public static readonly string statsSubFolder = "statistics";
        public static readonly string nonPFSubFolder = "nonPF";
        public static readonly string readFileIdCreatePattern = "Run{0:00000}_L{1}_{2}_{3}";

        public static string GetReadFileId(string runFolderName, int runNo, int lane, int read)
        {
            return string.Format(readFileIdCreatePattern, runNo, lane, read, runFolderName);
        }
        public static string GetPFFilePath(string readsFolder, string runFolderName, int runNo, int lane, int read)
        {
            return Path.Combine(readsFolder, GetReadFileId(runFolderName, runNo, lane, read) + ".fq.gz");
        }
        public static string GetNonPFFilePath(string readsFolder, string runFolderName, int runNo, int lane, int read)
        {
            return Path.Combine(readsFolder, Path.Combine(nonPFSubFolder, GetReadFileId(runFolderName, runNo, lane, read) + "_nonPF.fq.gz"));
        }
        public static string GetReadStatsFilePath(string readsFolder, string runFolderName, int runNo, int lane, int read)
        {
            return Path.Combine(readsFolder, Path.Combine(statsSubFolder, GetReadFileId(runFolderName, runNo, lane, read) + ".txt"));
        }

        /// <summary>
        /// Locates the folder where bowtie stores its indexes
        /// </summary>
        /// <returns>Folder for bowtie indexes</returns>
        public static string GetBowtieIndicesFolder()
        {
            if (Props.props.BowtieIndexFolder != "" && Props.props.BowtieIndexFolder != null)
                return Props.props.BowtieIndexFolder;
            string pathVar = Environment.GetEnvironmentVariable("PATH");
            string[] vars = pathVar.Contains(";")? pathVar.Split(';') : pathVar.Split(':');
            foreach (string v in vars)
                if (!v.Contains("bowtie2") && v.Contains("bowtie")) return Path.Combine(v, "indexes");
            return Props.props.BowtieIndexFolder;
        }

        /// <summary>
        /// Tries to find a bowtie index version, including create date, with read length exactly matching or slightly less than genome.ReadLen
        /// </summary>
        /// <param name="genome"></param>
        /// <returns>Empty string if none found</returns>
        public static string GetSpliceIndexVersion(StrtGenome genome)
        {
            string indexName = genome.GetBowtieSplcIndexName();
            if (indexName == "")
                return "";
            string testFile = Path.Combine(GetBowtieIndicesFolder(), indexName + ".1.ebwt");
            FileInfo fInfo = new FileInfo(testFile);
            return indexName + fInfo.CreationTime.ToString("yyMMdd");
        }
        /// <summary>
        /// Convert e.g. "mm9chrsUCSC_42bp120112" to "mm9_UCSC_120112"
        /// </summary>
        /// <param name="spliceIndexVersion"></param>
        /// <returns></returns>
        public static string MakeMapFolder(string spliceIndexVersion)
        {
            Match m = Regex.Match(spliceIndexVersion, "(.+)chr[as](.+)_.+bp([0-9]+)");
            return m.Groups[1].Value + "_" + m.Groups[2].Value + "_" + m.Groups[3].Value;
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
        /// Construct the proper path to the layout file. It is located inside project folder unless Props.SampleLayoutFileFolder
        /// points to a common folder for all layout files.
        /// </summary>
        /// <param name="projectNameOrFolder"></param>
        /// <returns>Path to project layout file, even if it does not exist.</returns>
        public static string GetSampleLayoutPath(string projectNameOrFolder)
        {
            string layoutFolder = Props.props.SampleLayoutFileFolder;
            if (layoutFolder == "" || layoutFolder == null)
                layoutFolder = GetRootedProjectFolder(projectNameOrFolder);
            string projectName = Path.GetFileName(projectNameOrFolder);
            string layoutFilename = string.Format(Props.props.SampleLayoutFileFormat, projectName);
            return Path.Combine(layoutFolder, layoutFilename);
        }

        public static string GetSyntLevelFilePath(string projectFolder, bool useRndTags)
        {
            string molPart = useRndTags ? "mol" : "read";
            string syntPat = Path.Combine(projectFolder, "Run00000_L0_1_" + Props.props.TestAnalysisFileMarker + "*." + molPart + "levels");
            string[] syntPatMatches = Directory.GetFiles(projectFolder, syntPat);
            return (syntPatMatches.Length == 1) ? syntPatMatches[0] : "";
        }
        public static string MakeSyntLevelFileHead(string dataId)
        {
            return Path.Combine(Props.props.ReadsFolder, "Run00000_L0_1_" + DateTime.Now.ToString("yyMMdd") +
                                Props.props.TestAnalysisFileMarker + "_0000_" + dataId);
        }

        /// <summary>
        /// Return an array of the repeat mask files of the given genome
        /// </summary>
        /// <param name="genome"></param>
        /// <returns></returns>
        public static string[] GetRepeatMaskFiles(StrtGenome genome)
        {
            string genomeFolder = genome.GetOriginalGenomeFolder();
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

        private static string GetReadFileMatchPattern(string runNoOrFlowcellId, char readNo, string extension)
        {
            string matchPat = "Run*_L{0}_" + readNo + "_*_?" + runNoOrFlowcellId + extension; // FlowcellId pattern
            int runNo;
            if (int.TryParse(runNoOrFlowcellId, out runNo))
                matchPat = "Run" + string.Format("{0:00000}", runNo) + "_L{0}_" + readNo + "*" + extension; // RunNo pattern
            return matchPat;
        }

        /// <summary>
        /// Checks that all read files are collected by checking that the statistics files have been generated
        /// in the /data/reads/statistics directory.
        /// </summary>
        /// <param name="runId">Either a run number, a run folder, or a flowcell id</param>
        /// <param name="laneNumbers">a string of lane numbers</param>
        /// <param name="reqReadNo">Highest read number needed (1,2, or 3)</param>
        /// <returns>The run number if all are ready, else -1</returns>
        public static int CheckReadsCollected(string runId, string laneNumbers, char reqReadNo)
        {
            if (runId.Contains('_'))
                runId = Regex.Match(runId, "_([0-9]+)_").Groups[1].Value;
            string statFileMatchPat = GetReadFileMatchPattern(runId, reqReadNo, ".txt");
            string readStatFolder = Path.Combine(Props.props.ReadsFolder, PathHandler.statsSubFolder);
            int runNo = -1;
            foreach (char laneNo in laneNumbers)
            {
                string laneStatFileMatchPat = string.Format(statFileMatchPat, laneNo);
                string[] statsFiles = Directory.GetFiles(readStatFolder, laneStatFileMatchPat);
                if (statsFiles.Length == 0) return -1;
                runNo = int.Parse(Path.GetFileName(statsFiles[0]).Substring(3, 5));
            }
            return runNo; 
        }

        /// <summary>
        /// laneArgs have the form RUNNO:LANENOS[:IDXSEQS]
        /// RUNNO is a single digit, LANENOS may be serveral digits, and IDXSEQS is the index sequence
        /// to filter by (or "" for using all indexes in that lane) for each of the lanes, separated with ','
        /// </summary>
        /// <param name="laneArgs"></param>
        /// <returns></returns>
        public static List<LaneInfo> ListReadsFiles(List<string> laneArgs)
        {
            List<LaneInfo> laneInfos = new List<LaneInfo>();
            foreach (string laneArg in laneArgs)
            {
                string[] parts = laneArg.Split(':');
                string runId = parts[0];
                string matchPat = GetReadFileMatchPattern(runId, '1', ".fq.gz");
                string idxSeqFilterString = new string(',', parts[1].Length - 1);
                if (parts.Length >= 3)
                {
                    if (parts[2].Split(',').Length != parts[1].Length)
                        throw new ArgumentException("One (possibly empty) index filter seq must exist for each lane");
                    idxSeqFilterString = parts[2];
                }
                string[] idxSeqFilter = idxSeqFilterString.Split(',');
                int n = 0;
                foreach (char laneNo in parts[1])
                {
                    string readFilePat = string.Format(matchPat, laneNo);
                    string[] laneFiles = Directory.GetFiles(Props.props.ReadsFolder, readFilePat);
                    if (laneFiles.Length > 0)
                        laneInfos.Add(new LaneInfo(laneFiles[0], runId, laneNo, idxSeqFilter[n++]));
                }
            }
            return laneInfos;
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

        public static readonly string extractionFolderCenter = "_ExtractionVer";
        public static string extractionFolderMakePattern = "{0}" + extractionFolderCenter + "{1}_{2}";
        public static string extractionFolderMatchPattern = extractionFolderCenter + "([0-9]+)_(.+)$";

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
            return Path.Combine(projectFolder, string.Format(extractionFolderMakePattern, projectName, extractionVersion, barcodeSet));
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
            string bcPath = Path.Combine(Props.props.ProjectsFolder, "barcodes");
            return Path.Combine(bcPath, barcodeSetName + ".barcodes");
        }

        /// <summary>
        /// Return an array of all barcode set names defined by barcode definition files
        /// </summary>
        /// <returns></returns>
        public static string[] GetAllCustomBarcodeSetNames()
        {
            string[] bcFiles = Directory.GetFiles(Path.Combine(Props.props.ProjectsFolder, "barcodes"), "*.barcodes");
            return Array.ConvertAll(bcFiles, f => Path.GetFileNameWithoutExtension(f));
        }

        /// <summary>
        /// Extract the barcode set name from the path name of an Extracted reads folder
        /// </summary>
        /// <param name="extractedFolder"></param>
        /// <returns>The DefaultBarcodeSet if path is not parsable</returns>
        public static string ParseBarcodeSet(string extractedFolder)
        {
            Match m = Regex.Match(extractedFolder, extractionFolderMatchPattern);
            if (m != null)
                return m.Groups[2].Value;
            return Props.props.DefaultBarcodeSet;
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
            return Path.Combine(Props.props.GenomesFolder, "SilverBulletCTRLConc.txt");
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
