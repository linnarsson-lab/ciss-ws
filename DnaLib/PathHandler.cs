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

        public static string GetGenomeBuildFolder(StrtGenome genome)
        {
            return Path.Combine(Props.props.GenomesFolder, genome.Build);
        }

        public static string GetBowtieIndicesFolder()
        {
            string pathVar = Environment.GetEnvironmentVariable("PATH");
            string[] vars = pathVar.Contains(";")? pathVar.Split(';') : pathVar.Split(':');
            foreach (string v in vars)
                if (v.Contains("bowtie")) return Path.Combine(v, "indexes");
            return Props.props.BowtieIndexFolder;
        }

        public static string GetIndexVersion(string buildName)
        {
            string indexFolder = GetBowtieIndicesFolder();
            string testFile = Path.Combine(indexFolder, buildName + ".1.ebwt");
            if (!File.Exists(testFile))
                return "";
            FileInfo fInfo = new FileInfo(testFile);
            return buildName + fInfo.CreationTime.ToString("yyMMdd");
        }

        public static string GetGenomeSequenceFolder(StrtGenome genome)
        {
            return Path.Combine(GetGenomeBuildFolder(genome), "genome");
        }

        public static string ExtractChrId(string filenameOrPath)
        {
            string filename = Path.GetFileName(filenameOrPath);
            Match m = Regex.Match(filename, "chromosome\\.([^\\.]+)\\.");
            if (!m.Success)
                m = Regex.Match(filename, "chr([^\\.]+)\\.");
            string chrId = m.Groups[1].Value;
            return chrId;
        }

        /// <summary>
        /// Generates a dictonary from chromosome Ids (without any "chr") to sequences.
        /// The sequences may be .gz compressed.
        /// </summary>
        /// <param name="genome">Genome to pick sequences from</param>
        /// <returns></returns>
        public Dictionary<string, string> GetGenomeFilesMap(StrtGenome genome)
        {
            string genomeFolder = GetGenomeSequenceFolder(genome);
            string[] chrFiles = GetFilesOrGz(genomeFolder, "*chr*.fa");
            string[] gbkFiles = GetFilesOrGz(genomeFolder, "*chr*.gbk");
            if (gbkFiles.Length > chrFiles.Length)
                chrFiles = gbkFiles;
            Dictionary<string, string> chrIdToFileMap = new Dictionary<string, string>();
            foreach (string file in chrFiles)
            {
                string chrId = ExtractChrId(file);
                if (genome.IsChrInBuild(chrId))
                {
                    chrId = StrtGenome.ConvertIfAnnotation(chrId);
                    chrIdToFileMap[chrId] = file;
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

        public string GetAnnotationsPath(StrtGenome genome)
        {
            string annotationsFilename = genome.GetAnnotationsFileName();
            string annotationsPath = Path.Combine(GetGenomeSequenceFolder(genome), annotationsFilename);
            return annotationsPath;
        }

        public static string GetRedundancyPath(StrtGenome genome, int averageReadLen)
        {
            string redundancyFilename = genome.GetRedundancyFileName(averageReadLen);
            return Path.Combine(GetGenomeSequenceFolder(genome), redundancyFilename);
        }

        public string GetJunctionChrPath(StrtGenome genome)
        {
            return Path.Combine(GetGenomeSequenceFolder(genome), genome.GetJunctionChrFileName() + ".fa");
        }

        public string[] GetRepeatMaskFiles(StrtGenome genome)
        {
            string genomeFolder = GetGenomeSequenceFolder(genome);
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
        /// </summary>
        /// <param name="projectFolderOrName"></param>
        /// <returns></returns>
        public static string GetRootedProjectFolder(string projectFolderOrName)
        {
            if (!Path.IsPathRooted(projectFolderOrName))
                return Path.Combine(Props.props.ProjectsFolder, projectFolderOrName);
            return projectFolderOrName;
        }

        /// <summary>
        /// If input is a partial run folder name, root it in the standard runs folder.
        /// If it is a number, convert it to a full run folder path.
        /// If it is a rooted path, return as-is.
        /// </summary>
        /// <param name="runNameOrNumber"></param>
        /// <returns>Rooted path to a run folder</returns>
        public string GetRootedRunFolder(string runNameOrNumber)
        {
            string runFolder = runNameOrNumber;
            string runsFolder = props.RunsFolder;
            int runNo;
            if (int.TryParse(runNameOrNumber, out runNo))
            {
                string searchPattern = string.Format("*_{0:0000}", runNo);
                string[] files = Directory.GetDirectories(runsFolder, searchPattern);
                if (files.Length == 0)
                {
                    searchPattern = string.Format("*_{0:00000}_FC", runNo);
                    files = Directory.GetDirectories(runsFolder, searchPattern);
                }
                if (files.Length == 0)
                    throw new IOException("No run with number " + runNameOrNumber + " found in " + runsFolder);
                if (files.Length > 1)
                {
                    Array.Sort(files);
                    Array.Reverse(files);
                    Console.Error.WriteLine("Several runs with number " + runNameOrNumber + " found in " + runsFolder);
                    Console.Error.WriteLine("Using latest: " + files[0]);
                }
                runFolder = files[0];
            }
            if (!Path.IsPathRooted(runFolder))
                runFolder = Path.Combine(runsFolder, runFolder);
            return runFolder;
        }

        /// <summary>
        /// Convert a run number or partial run folder path to the valid
        /// directory name of the raw Illumina qseq.txt files.
        /// If input is a full path to or name of a run folder that does
        /// not contain the default Illumina sub-structure, the
        /// return value is the input path itself, rooted in the top runs folder, if needed.
        /// </summary>
        /// <param name="runNameOrNumber"></param>
        /// <returns></returns>
        public string GetRootedRunDataFolder(string runNameOrNumber)
        {
            string runFolder = GetRootedRunFolder(runNameOrNumber);
            if (!runFolder.Contains(MakeRunDataSubPath()))
            {
                string stdSubFolder = Path.Combine(runFolder, MakeRunDataSubPath());
                if (Directory.Exists(stdSubFolder))
                    runFolder = stdSubFolder;
            }
            return runFolder;
        }

        public string MakeRandomTagFilteredFilename(string projectFolder)
        {
            return Path.Combine(projectFolder, "unique_tags.fq");
        }

        public static string CreateExtractedFileHead(string outputFolder, string readsFile, string extractionVersion)
        {
            string fileName = Path.GetFileNameWithoutExtension(readsFile);
            Match m = Regex.Match(fileName, "^Run[0-9]+_L[0-9]+_[0-9]");
            fileName = fileName.Substring(0, m.Length) + "_" + extractionVersion;
            return Path.Combine(outputFolder, fileName);
        }
        public static string MakeExtractionSummaryPath(string mapFilePath)
        {
            Match m = Regex.Match(mapFilePath, "^.*Run[0-9]+_L[0-9]+_[0-9]_[0-9]+");
            return mapFilePath.Substring(0, m.Length) + "_summary.txt";
        }
        public static string GetExtractionSummaryPath(string outputFolder, string readsFile, string extractionVersion)
        {
            return CreateExtractedFileHead(outputFolder, readsFile, extractionVersion) + "_summary.txt";
        }
        /// <summary>
        /// To be used only with the piped Extract.exe > Bowtie > Annotate.exe process line.
        /// </summary>
        /// <param name="projectFolder"></param>
        /// <returns></returns>
        public static string GetGlobalExtractionSummaryPath(string projectFolder)
        {
            string projectName = Path.GetFileName(projectFolder);
            return Path.Combine(projectFolder, projectName + "_extraction_summary.txt");
        }
        public static string GetSlaskReadsPath(string outputFolder, string readsFile, string extractionVersion)
        {
            return CreateExtractedFileHead(outputFolder, readsFile, extractionVersion) + "_slask.fq.gz";
        }
        public static string GetBarcodedReadsPath(string outputFolder, string readsFile, string extractionVersion)
        {
            return CreateExtractedFileHead(outputFolder, readsFile, extractionVersion) + "_barcoded.fq";
        }

        public static string extractedFolderMakePattern = "{0}_ExtractionVer{1}_{2}";
        public static string extractedFolderMatchPattern = "^[^_]+_ExtractionVer[0-9]+_[^_]+$";
        public string MakeExtractedFolder(string projectFolder, string barcodeSet, string extractionVersion)
        {
            string projectName = Path.GetFileName(projectFolder);
            return Path.Combine(projectFolder, string.Format(extractedFolderMakePattern, projectName, extractionVersion, barcodeSet));
        }
        /// <summary>
        /// Finds the latest Extracted folder inside (project) inputFolder
        /// </summary>
        /// <param name="inputFolder"></param>
        /// <returns>Path to the latest Extracted folder in inputFolder, 
        ///          or the rooted inputFolder if it already was the path of 
        ///          an Extracted folder</returns>
        public static string GetLatestExtractedFolder(string inputFolder)
        {
            inputFolder = GetRootedProjectFolder(inputFolder);
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

        /// <summary>
        /// Return paths to the bowtie output files in folder that correspond to the genome,
        /// and are either main maps, or splc maps of the latest common bowtie index date
        /// </summary>
        /// <param name="projectFolder">Project folder</param>
        /// <param name="genome">Selected genome</param>
        /// <param name="randomTagged">true to look for .bam instead of .map files</param>
        /// <param name="indexVersion">will return the selected latest bowtie index version (e.g. hg19_sUCSC110606) </param>
        /// <returns>The genome-corresponding alignment files of the latest bowtie index date from folder</returns>
        public static List<string> FindBowtieOutputFiles(string projectFolder, StrtGenome genome)
        {
            string extractedFolder = GetLatestExtractedFolder(projectFolder);
            string mainPattern = string.Format(".+_{0}.map$", genome.Build);
            List<string> files = new List<string>();
            foreach (string file in Directory.GetFiles(projectFolder))
                if (Regex.Match(file, mainPattern).Success)
                    files.Add(file);
            string splcPattern = string.Format(".+_{0}([0-9]+).map$", genome.GetBowtieSplcIndexName());
            Dictionary<string, List<string>> splcFilesByDate = new Dictionary<string, List<string>>();
            foreach (string file in Directory.GetFiles(projectFolder))
            {
                Match m = Regex.Match(file, splcPattern);
                if (m.Success)
                {
                    string date = m.Groups[1].Value;
                    if (!splcFilesByDate.ContainsKey(date))
                        splcFilesByDate[date] = new List<string>();
                    splcFilesByDate[date].Add(file);
                }
            }
            List<string> dates = splcFilesByDate.Keys.ToList();
            string latestDate = dates.Max();
            files.AddRange(splcFilesByDate[latestDate]);
            return files;
        }

        public static string ExtractIndexVersion(string mapFile)
        {
            return Regex.Match(mapFile, string.Format("_([^_]+_[^_]+).[bm]a[mp]$")).Groups[1].Value;
        }

        public static readonly string BarcodedFileEnding = "barcoded.fq";
        public static readonly string ExtractionSummaryFileEnding = "summary.txt";
        public static string CombineToMapFilename(string bcFilename, string indexVersion, string extension)
        {
            return bcFilename.Replace(BarcodedFileEnding, indexVersion + extension);
        }

        public static string MakeAnnotFolderName(string projectName, string bowtieIndexVersion, string annotationVersion)
        {
            return string.Format("{0}_AnnotationVer{1}_{2}", projectName, annotationVersion, bowtieIndexVersion);
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

        public static string GetIndexNameFromResultSubPath(string resultSubFolder)
        {
            Match m = Regex.Match(resultSubFolder, "AnnotationVer[0-9]+_(.+)$");
            return m.Groups[1].Value;
        }

    }
}
