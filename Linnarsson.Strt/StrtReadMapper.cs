using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using Linnarsson.Mathematics.SortSearch;
using System.Text.RegularExpressions;
using C1;

namespace Linnarsson.Strt
{
    /// <summary>
    /// Various methods for major steps and utility functionalities of the STRT pipeline.
    /// </summary>
    public class StrtReadMapper
	{
        private Props props;
        private Barcodes barcodes;

        private string tempBowtieStartMsg;

        public StrtReadMapper(Props props)
        {
            this.props = props;
            barcodes = props.Barcodes;
        }
        private void SetBarcodeSet(string barcodesName)
        {
            props.BarcodesName = barcodesName;
            barcodes = props.Barcodes;
        }

        /// <summary>
        /// Correct the starts and ends of transcripts according to analysis of previous
        /// mappings of experimental data. These update files are the "...annot_errors.tab"
        /// files in the STRT result folders.
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="errorsPath">Path to an annotation error correction file</param>
        /// <param name="annotationFile">name of annotation refFlat/mart file or leave as "" to use the default annotation file</param>
        public void UpdateSilverBulletGenes(StrtGenome genome, string errorsPath, string annotationFile)
        {
            Console.WriteLine("*** Updating annotation file {0} for {1} using {2} ***",
                              annotationFile, genome.GetBowtieMainIndexName(), Path.GetFileName(errorsPath));
            Background.Message("Updating annotations...");
            AnnotationBuilder builder = new AnnotationBuilder(props, AnnotationReader.GetAnnotationReader(genome, annotationFile));
            builder.UpdateSilverBulletGenes(genome, errorsPath);
            Console.WriteLine("Done.");
            Background.Progress(100);
            Background.Message("Ready");
        }

        /// <summary>
        /// Construct the repeat-masked genome, artificial splice junction chromosome and transcript annotation file.
        /// </summary>
        /// <param name="genome"></param>
        public void BuildJunctions(StrtGenome genome, string annotationFile)
        {
            AssertStrtGenomeFolder(genome);
            DateTime startTime = DateTime.Now;
            annotationFile = AnnotationReader.GetAnnotationFile(genome, annotationFile);
            Console.WriteLine("*** Build of spliced exon junctions for {0} from {1} started at {2} ***",
                genome.GetBowtieMainIndexName(), annotationFile, DateTime.Now);
            AnnotationBuilder builder = new AnnotationBuilder(props, AnnotationReader.GetAnnotationReader(genome, annotationFile));
            builder.BuildExonSplices(genome);
        }

        public void AssertStrtGenomeFolder(StrtGenome genome)
        {
            string strtDir = genome.GetStrtGenomesFolder();
            if (Directory.Exists(strtDir))
                return;
            Directory.CreateDirectory(strtDir);
        }

        public void MakeMaskedStrtChromosomes(StrtGenome genome)
        {
            string strtDir = genome.GetStrtGenomesFolder();
            NonExonRepeatMasker nerm = new NonExonRepeatMasker();
            nerm.Mask(genome, strtDir);
        }

        /// <summary>
        /// Construct the artificial splice chromosome, the transcript annotation file, and build the Bowtie index.
        /// </summary>
        /// <param name="genome"></param>
        public void BuildJunctionsAndIndex(StrtGenome genome)
        {
            BuildJunctionsAndIndex(genome, "");
        }
		public void BuildJunctionsAndIndex(StrtGenome genome, string annotationFile)
		{
            string btIdxFolder = PathHandler.GetBowtieIndicesFolder();
            if (!Directory.Exists(btIdxFolder))
                throw new IOException("The Bowtie index folder cannot be found. Please set the BowtieIndexFolder property.");
            if (genome.GeneVariants == false)
            {
                genome.GeneVariants = false;
                BuildJunctions(genome, annotationFile);
                MakeMaskedStrtChromosomes(genome);
                BuildIndex(genome);
            }
            genome.GeneVariants = true;
            BuildJunctions(genome, annotationFile);
            BuildIndex(genome);
        }

        private void BuildIndex(StrtGenome genome)
        {
            DateTime startTime = DateTime.Now;
            string newIndexName = genome.GetBowtieMainIndexName();
            string chrFilesArg = string.Join(",", genome.GetMaskedChrPaths());
            string outfileHead = Path.Combine(PathHandler.GetBowtieIndicesFolder(), newIndexName);
            string arguments = String.Format("{0} {1}", chrFilesArg, outfileHead);
            string cmd = "bowtie-build";
            if (Directory.GetFiles(PathHandler.GetBowtieIndicesFolder(), newIndexName + ".*.ebwt").Length >= 4)
                Console.WriteLine("NOTE: Main index {0} already exists. Delete index files and rerun to force rebuild.", newIndexName);
            else
            {
                Console.WriteLine("*** Build of main Bowtie index {0} started at {1} ***", newIndexName, DateTime.Now);
                Console.WriteLine("{0} {1}", cmd, arguments);
                int exitCode = CmdCaller.Run(cmd, arguments);
                if (exitCode != 0)
                    Console.Error.WriteLine("Failed to run bowtie-build. ExitCode={0}", exitCode);
            }
            string spliceChrFile = genome.MakeJunctionChrPath();
            if (spliceChrFile != null)
            {
                string spliceIndexName = genome.MakeBowtieSplcIndexName();
                Console.WriteLine("*** Build of Bowtie splice index {0} started at {1} ***", spliceIndexName, DateTime.Now);
                outfileHead = Path.Combine(PathHandler.GetBowtieIndicesFolder(), spliceIndexName);
                arguments = String.Format("{0} {1}", spliceChrFile, outfileHead);
                Console.WriteLine("{0} {1}", cmd, arguments);
                int exitCode = CmdCaller.Run(cmd, arguments);
                if (exitCode != 0)
                    Console.Error.WriteLine("Failed to run bowtie-build. ExitCode={0}", exitCode);
            }
            else
                Console.Error.WriteLine("WARNING: No splice chromosome found for {0}. Indexing skipped.", genome.GetBowtieMainIndexName());
        }

        /// <summary>
        /// Extract and filter reads from the raw reads files in the project Reads/ directory.
        /// Will, depending on barcodeSet specification, extract barcodes and trim template switch G:s.
        /// Removes low complexity, low quality and short reads.
        /// Accepted reads are written in FastQ format and separated from rejected reads written to slask.fq files.
        /// </summary>
        /// <param name="projectOrReadFileFolder">project folder, project name, or a folder with sequence files</param>
        /// <param name="laneArgs">Items of "RunNo:LaneNos[:idxSeqs]" that define the lanes of the project.</param>
        /// <param name="resultProject">If not null, is abolute path to, or name of, project to save results in</param>
		public List<LaneInfo> Extract(string projectOrReadFileFolder, List<string> laneArgs, string resultProject)
		{
            projectOrReadFileFolder = PathHandler.GetRootedProjectFolder(projectOrReadFileFolder);
            string resultProjectFolder = (resultProject != null) ? PathHandler.GetRooted(resultProject) : projectOrReadFileFolder;
            string extractionFolder = PathHandler.MakeExtractionFolderSubPath(resultProjectFolder, barcodes.Name, EXTRACTION_VERSION);
            List<LaneInfo> laneInfos = LaneInfo.LaneInfosFromLaneArgs(laneArgs, extractionFolder, barcodes.Count);
            if (laneInfos.Count == 0)
                Console.WriteLine("Warning: No read files found corresponding to {0}", string.Join("/", laneArgs.ToArray()));
            ExtractMissingAndOld(laneInfos, extractionFolder);
            return laneInfos;
        }

        private void Extract(ProjectDescription pd)
        {
            pd.extractionVersion = EXTRACTION_VERSION;
            string extractionFolder = PathHandler.MakeExtractionFolderSubPath(pd.ProjectFolder, barcodes.Name, EXTRACTION_VERSION);
            pd.laneInfos = LaneInfo.LaneInfosFromLaneArgs(pd.runIdsLanes.ToList(), extractionFolder, barcodes.Count);
            ExtractMissingAndOld(pd.laneInfos, extractionFolder);
        }

        public static readonly string EXTRACTION_VERSION = "34";

        /// <summary>
        /// Extract and filter reads for the barcodes where there is no existing extracted file,
        /// or the file is of an older version, or the read file is newer than the extracted file.
        /// </summary>
        /// <param name="laneInfos"></param>
        /// <param name="extractionFolder"></param>
        private void ExtractMissingAndOld(List<LaneInfo> laneInfos, string extractionFolder)
        {
            foreach (LaneInfo laneInfo in laneInfos)
			{
                bool someExtractionMissing = !laneInfo.AllExtractedFilesExist() || !File.Exists(laneInfo.summaryFilePath);
                bool readFileIsNewer = (File.Exists(laneInfo.summaryFilePath) && File.Exists(laneInfo.PFReadFilePath)) &&
                                       DateTime.Compare(new FileInfo(laneInfo.PFReadFilePath).LastWriteTime, new FileInfo(laneInfo.summaryFilePath).LastWriteTime) > 0;
                if (someExtractionMissing || readFileIsNewer)
                {
                    SampleReadWriter srw = new SampleReadWriter(barcodes, laneInfo);
                    srw.ProcessLane();
                }
                if (Background.CancellationPending) break;
            }
		}

        /// <summary>
        /// Performs extraction, mapping, and annotation on the lanes, bc, and layout/species defined by projDescr.
        /// Extraction and mapping are done if no data are available with the current software/index versions.
        /// Annotation is always performed and data put in a dated result folder.
        /// </summary>
        /// <param name="projDescr"></param>
        /// <param name="logWriter">File for log information</param>
        public void Process(ProjectDescription projDescr, StreamWriter logWriter)
        {
            Console.WriteLine("StrtReadMapper.Process(" + projDescr.plateId + ")");
            SetBarcodeSet(projDescr.barcodeSet);
            props.TotalNumberOfAddedSpikeMolecules = projDescr.SpikeMoleculeCount;
            logWriter.WriteLine("{0} Extracting {1} lanes with barcodes {2}...", DateTime.Now, projDescr.runIdsLanes.Length, projDescr.barcodeSet);
            logWriter.Flush();
            if (barcodes.HasUMIs)
                logWriter.WriteLine("{0} MinPhredScoreInRandomTag={1}", DateTime.Now, props.MinPhredScoreInRandomTag);
            Extract(projDescr);
            string[] speciesArgs = ParsePlateLayout(projDescr.plateId, projDescr.SampleLayoutPath, projDescr.defaultSpecies);
            projDescr.annotationVersion = ANNOTATION_VERSION;
            foreach (string speciesArg in speciesArgs)
            {
                StrtGenome genome = StrtGenome.GetGenome(speciesArg, projDescr.analyzeVariants, projDescr.defaultBuild, true);
                int[] genomeBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
                genome.ReadLen = GetAverageReadLen(projDescr.laneInfos);
                SetAvailableBowtieIndexVersion(projDescr, genome);
                logWriter.WriteLine("{0} Mapping to {1}...", DateTime.Now, genome.GetBowtieSplcIndexName()); logWriter.Flush();
                CreateBowtieMaps(genome, projDescr.laneInfos, genomeBcIndexes);
                List<string> mapFiles = LaneInfo.RetrieveAllMapFilePaths(projDescr.laneInfos);
                props.UseRPKM = projDescr.rpkm;
                props.DirectionalReads = projDescr.DirectionalReads;
                props.SenseStrandIsSequenced = projDescr.SenseStrandIsSequenced;
                projDescr.SetGenomeData(genome);
                logWriter.WriteLine("{0} Annotating {1} map files...", DateTime.Now, mapFiles.Count);
                logWriter.WriteLine("{0} setting: AllTrVariants={1} Gene5'Extensions={4} #SpikeMols={5} DirectionalReads={2} RPKM={3}",
                                    DateTime.Now, projDescr.analyzeVariants, props.DirectionalReads, props.UseRPKM,
                                    props.GeneFeature5PrimeExtension, props.TotalNumberOfAddedSpikeMolecules);
                logWriter.Flush();
                string resultFolderName = MakeDefaultResultFolderName(genome, projDescr.ProjectFolder, projDescr.plateId);
                ResultDescription resultDescr = ProcessAnnotation(genome, projDescr.ProjectFolder, projDescr.plateId, resultFolderName,
                                                                  mapFiles);
                projDescr.resultDescriptions.Add(resultDescr);
                System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(projDescr.GetType());
                using (StreamWriter writer = new StreamWriter(Path.Combine(resultDescr.resultFolder, "ProjectConfig.xml")))
                    x.Serialize(writer, projDescr);
                logWriter.WriteLine("{0} Results stored in {1}.", DateTime.Now, resultDescr.resultFolder);
                logWriter.Flush();
            }
        }

        /// <summary>
        /// Tries to locate a bowtie index useful for the genome and current read length. If none exists,
        /// instead tries to change to the DefaultAnnotationSource (usually UCSC)
        /// </summary>
        /// <param name="projDescr"></param>
        /// <param name="genome"></param>
        private static void SetAvailableBowtieIndexVersion(ProjectDescription projDescr, StrtGenome genome)
        {
            string bowtieIndexVersion = PathHandler.GetSpliceIndexVersion(genome);
            if (bowtieIndexVersion == "" && genome.Annotation != StrtGenome.DefaultAnnotationSource)
            {
                Console.WriteLine("Could not find a Bowtie index for {0} - trying {2} instead for {1}",
                                  genome.Annotation, projDescr.plateId, StrtGenome.DefaultAnnotationSource);
                genome.Annotation = StrtGenome.DefaultAnnotationSource;
            }
        }

        /// <summary>
        /// Calculates the average read length of all valid extracted reads
        /// </summary>
        /// <param name="laneInfos"></param>
        /// <returns></returns>
        private int GetAverageReadLen(List<LaneInfo> laneInfos)
        {
            ReadCounter rc = new ReadCounter(barcodes);
            foreach (LaneInfo laneInfo in laneInfos)
            {
                rc.AddExtractionSummary(laneInfo.summaryFilePath);
            }
            return rc.AverageReadLen;
        }

        /// <summary>
        /// If sampleLayoutPath is parsable, the species name(s) to use are extracted from that file,
        /// and reads the layout into Barcodes object. Otherwise the defaultSpeciesArg is simply returned. 
        /// </summary>
        /// <param name="projectName"></param>
        /// <param name="sampleLayoutPath"></param>
        /// <param name="defaultSpeciesArg"></param>
        /// <returns>Ids of all the species that are on the plate</returns>
        private string[] ParsePlateLayout(string projectName, string sampleLayoutPath, string defaultSpeciesArg)
        {
            string[] speciesArgs = new string[] { defaultSpeciesArg };
            PlateLayout sampleLayout = PlateLayout.GetPlateLayout(projectName, sampleLayoutPath);
            Console.WriteLine(projectName + " " + sampleLayoutPath);
            if (sampleLayout != null)
            {
                barcodes.SetSampleLayout(sampleLayout);
                speciesArgs = sampleLayout.SpeciesIds;
            }
            return speciesArgs;
        }

        /// <summary>
        /// Try to find a proper Bowtie index and align the lanes defined by laneInfo
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="laneInfos"></param>
        /// <param name="genomeBcIndexes">optionally only process specific barcodes</param>
        private void CreateBowtieMaps(StrtGenome genome, List<LaneInfo> laneInfos, int[] genomeBcIndexes)
        {
            string splcIndexVersion = GetSplcIndexVersion(genome, true);
            string splcIndexName = genome.GetBowtieSplcIndexName();
            if (splcIndexName == "")
                throw new Exception("Can not find a Bowtie index corresponding to " + genome.Build + "/" + genome.Annotation);
            string maxAlt = (props.UseMaxAltMappings)?
                               string.Format(" and limiting alternative mappings to max {0}.", props.MaxAlternativeMappings) : "";
            tempBowtieStartMsg = string.Format("Using bowtie index {0}{1}", splcIndexVersion, maxAlt);
            foreach (LaneInfo laneInfo in laneInfos)
                CreateBowtieMaps(genome, laneInfo, splcIndexVersion, splcIndexName, genomeBcIndexes);
        }

        /// <summary>
        /// Create any missing .map files needed for given genome and wells defined by barcodes/species.
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="laneInfo">Paths to all needed map files will be stored in laneInfo.mappedFilePaths</param>
        /// <param name="splcIndexVersion"></param>
        /// <param name="splcIndexName"></param>
        /// <param name="genomeBcIndexes">only these barcodes will be processed</param>
        private void CreateBowtieMaps(StrtGenome genome, LaneInfo laneInfo, string splcIndexVersion, string splcIndexName, int[] genomeBcIndexes)
        {
            laneInfo.SetMappedFileFolder(splcIndexVersion);
            string mapFolder = laneInfo.mappedFileFolder;
            if (!Directory.Exists(mapFolder))
                Directory.CreateDirectory(mapFolder);
            laneInfo.bowtieLogFilePath = Path.Combine(mapFolder, "bowtie_output.txt");
            List<string> mapFiles = new List<string>();
            foreach (string fqPath in laneInfo.extractedFilePaths)
            {
                int bcIdx = int.Parse(Path.GetFileNameWithoutExtension(fqPath));
                if (Array.IndexOf(genomeBcIndexes, bcIdx) == -1)
                    continue;
                string mainIndex = genome.GetBowtieMainIndexName();
                string fqUnmappedReadsPath = Path.Combine(mapFolder, string.Format("{0}.fq-{1}", bcIdx, mainIndex));
                string outputMainPath = Path.Combine(mapFolder, string.Format("{0}_{1}.map", bcIdx, mainIndex));
                if (!File.Exists(outputMainPath))
                    CreateBowtieOutputFile(mainIndex, fqPath, outputMainPath, fqUnmappedReadsPath, laneInfo.bowtieLogFilePath);
                mapFiles.Add(outputMainPath);
                string outputSplcFilename = string.Format("{0}_{1}.map", bcIdx, splcIndexVersion);
                string outputSplcPath = Path.Combine(mapFolder, outputSplcFilename);
                string splcFilePat = PathHandler.StarOutReadLenInSplcMapFile(outputSplcFilename);
                string[] existingSplcMapFiles = Directory.GetFiles(mapFolder, splcFilePat);
                if (existingSplcMapFiles.Length >= 1)
                    outputSplcPath = Path.Combine(mapFolder, existingSplcMapFiles[0]);
                else
                {
                    if (!File.Exists(fqUnmappedReadsPath))
                        CreateBowtieOutputFile(mainIndex, fqPath, outputMainPath, fqUnmappedReadsPath, laneInfo.bowtieLogFilePath);
                    string remainUnmappedPath = props.SaveNonMappedReads ? Path.Combine(mapFolder, bcIdx + ".fq-nonmapped") : "";
                    if (File.Exists(fqUnmappedReadsPath)) // Have to check - all reads may have mapped directly
                        CreateBowtieOutputFile(splcIndexName, fqUnmappedReadsPath, outputSplcPath, remainUnmappedPath, laneInfo.bowtieLogFilePath);
                }
                mapFiles.Add(outputSplcPath);
                // Don't delete the fqUnmappedReadsPath - it is needed if rerun with changing all/single annotation versions
                if (Background.CancellationPending) break;
            }
            laneInfo.mappedFilePaths = mapFiles.ToArray();
        }

        /// <summary>
        /// Gets the current bowtie index version (including date) for genome.
        /// throws exception if it does not exist
        /// </summary>
        /// <param name="genome"></param>
        /// <returns></returns>
        private string GetSplcIndexVersion(StrtGenome genome, bool tryBuildIfAbsent)
        {
            string splcIndexVersion = PathHandler.GetSpliceIndexVersion(genome); // The current version including date
            if (splcIndexVersion == "" && tryBuildIfAbsent)
            {
                int actualReadLen = genome.ReadLen;
                genome.ReadLen = genome.ReadLen - (genome.ReadLen % 4);
                Console.WriteLine("Can not find a proper splice index - trying to build one with ReadLen=" + genome.ReadLen);
                BuildJunctionsAndIndex(genome);
                splcIndexVersion = PathHandler.GetSpliceIndexVersion(genome);
                if (splcIndexVersion == "")
                    throw new Exception("Could not build the needed splice index with ReadLen between " + genome.ReadLen +
                                        " and " + actualReadLen + " for " + genome.Build + " and " + genome.Annotation);
                genome.ReadLen = actualReadLen;
            }
            return splcIndexVersion;
        }

        /// <summary>
        /// Run bowtie to produce a .map file.
        /// </summary>
        /// <param name="bowtieIndex"></param>
        /// <param name="inputFqReadPath"></param>
        /// <param name="outputPath"></param>
        /// <param name="outputFqUnmappedReadPath"></param>
        /// <param name="bowtieLogFile"></param>
        /// <returns>true if Bowtie returned ExitCode 0</returns>
        private bool CreateBowtieOutputFile(string bowtieIndex, string inputFqReadPath, string outputPath,
                                   string outputFqUnmappedReadPath, string bowtieLogFile)
        {
            if (tempBowtieStartMsg != null)
                Console.WriteLine(tempBowtieStartMsg);
            tempBowtieStartMsg = null;
            string indexWPath = Path.Combine(PathHandler.GetBowtieIndicesFolder(), bowtieIndex);
            int nThreads = props.NumberOfAlignmentThreadsDefault;
            string threadArg = (nThreads == 1) ? "" : string.Format("-p {0}", nThreads);
            string unmappedArg = "";
            if (outputFqUnmappedReadPath != "")
            {
                string crapMaxPath = Path.Combine(Path.GetDirectoryName(outputFqUnmappedReadPath), "bowtie_maxM_reads_map.temp");
                unmappedArg = string.Format(" --un {0} --max {1}", outputFqUnmappedReadPath, crapMaxPath);
            }
            if (!File.Exists(inputFqReadPath) && File.Exists(inputFqReadPath + ".gz"))
                CmdCaller.Run("gunzip", inputFqReadPath + ".gz");
            string opts = props.BowtieOptionPattern.Replace("MaxAlignmentMismatches", 
                        props.MaxAlignmentMismatches.ToString());
            opts = opts.Replace("QualityScoreBase", props.QualityScoreBase.ToString());
            opts = opts.Replace("MaxAlternativeMappings", props.MaxAlternativeMappings.ToString());
            string arguments = String.Format("{0} {1} {2} {3} \"{4}\" \"{5}\"", opts, threadArg,
                                                unmappedArg, indexWPath, inputFqReadPath, outputPath);
            StreamWriter logWriter = new StreamWriter(bowtieLogFile, true);
            logWriter.WriteLine("--- bowtie {0} ---", arguments); logWriter.Flush();
            CmdCaller cc = new CmdCaller("bowtie", arguments);
            logWriter.WriteLine(cc.StdError);
            logWriter.Close();
            if (cc.ExitCode != 0)
            {
                Console.Error.WriteLine("bowtie {0}\nFailed to run Bowtie on {1}. ExitCode={2}. Check logFile.", arguments, inputFqReadPath, cc.ExitCode);
                if (File.Exists(outputPath)) File.Delete(outputPath);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Run Bowtie on reads in a specific Extraction folder, or on all reads in the last Extraction folder of a project.
        /// </summary>
        /// <param name="projectOrExtractedFolderOrName"></param>
        /// <param name="speciesArg"></param>
        /// <param name="defaultGeneVariants"></param>
        /// <param name="defaultAnnotation"></param>
        public void Map(string projectOrExtractedFolderOrName, string speciesArg, bool defaultGeneVariants, string defaultAnnotation)
        {
            string extractedFolder = SetupForLatestExtractionFolder(projectOrExtractedFolderOrName);
            List<LaneInfo> laneInfos = LaneInfo.SetupLaneInfosFromExistingExtraction(extractedFolder, barcodes.Count);
            StrtGenome genome = StrtGenome.GetGenome(speciesArg, defaultGeneVariants, defaultAnnotation, false);
            genome.ReadLen = GetAverageReadLen(laneInfos);
            int[] genomeBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
            Console.WriteLine("Mapping reads in {0} against {1}", extractedFolder, genome.GetBowtieSplcIndexName());
            CreateBowtieMaps(genome, laneInfos, genomeBcIndexes);
        }

        public static readonly string ANNOTATION_VERSION = "45";

        /// <summary>
        /// Uses the SampleLayout file to decide which species(s) to run bowtie and annotate against.
        /// If it does not exist, use the default species supplied.
        /// Note that this command line called method uses the standard (PathHandler) layout filename. When instead
        /// using the ProjectDB, the layout filename is taken from the database to allow updates with alternatively versioned names.
        /// </summary>
        /// <param name="projectFolderOrName"></param>
        /// <param name="defaultSpeciesArg">Species to use if no layout file exists in projectFolder</param>
        /// <param name="analyzeAllGeneVariants">true to analyze all transcript splice variants defined in annoatation file</param>
        /// <param name="defaultAnnotation">Annotation source to use if layout file is missing</param>
        /// <param name="resultFolderName">Will make a standard subfolder if "".</param>
        /// <returns>The subpaths to result folder (one per species) under project folder</returns>
        /// <summary>
        public List<string> MapAndAnnotate(string projectOrExtractedFolderOrName, string defaultSpeciesArg, 
                                     bool defaultGeneVariants, string defaultAnnotation, string resultFolderName, int[] selectedBcIdxs)
        {
            string extractedFolder = SetupForLatestExtractionFolder(projectOrExtractedFolderOrName);
            List<LaneInfo> laneInfos = LaneInfo.SetupLaneInfosFromExistingExtraction(extractedFolder, barcodes.Count);
            string projectFolder = PathHandler.GetRootedProjectFolder(projectOrExtractedFolderOrName);
            string sampleLayoutPath = PathHandler.GetSampleLayoutPath(projectFolder);
            string[] speciesArgs = new string[] { defaultSpeciesArg };
            string projectName = Path.GetFileName(projectFolder);
            if (defaultSpeciesArg == "" && File.Exists(sampleLayoutPath))
                speciesArgs = ParsePlateLayout(projectName, sampleLayoutPath, defaultSpeciesArg);
            List<string> resultSubFolders = new List<string>();
            foreach (string speciesArg in speciesArgs)
            {
                StrtGenome genome = StrtGenome.GetGenome(speciesArg, defaultGeneVariants, defaultAnnotation, true);
                int[] genomeSelectedBcIdxs = barcodes.GenomeAndEmptyBarcodeIndexes(genome).Where(i =>
                                                        (selectedBcIdxs == null || selectedBcIdxs.Contains(i))).ToArray();
                genome.ReadLen = GetAverageReadLen(laneInfos);
                string spResultFolderName = resultFolderName;
                if (resultFolderName == "" || resultFolderName == null)
                    spResultFolderName = MakeDefaultResultFolderName(genome, projectFolder, projectName);
                string readDir = !props.DirectionalReads ? "No" : props.SenseStrandIsSequenced ? "Sense" : "Antisense";
                Console.WriteLine("Mapping and annotating {0} lanes of {1} against {2}.\nDirectionalReads={3} RPKM={4} SelectedMappingType={5}...", 
                              laneInfos.Count, projectName, genome.GetBowtieSplcIndexName(),
                              readDir, props.UseRPKM, props.SelectedMappingType);
                CreateBowtieMaps(genome, laneInfos, genomeSelectedBcIdxs);
                List<string> mapFiles = LaneInfo.RetrieveAllMapFilePaths(laneInfos);
                ResultDescription resultDescr = ProcessAnnotation(genome, projectFolder, projectName, spResultFolderName, mapFiles);
                Console.WriteLine("...annotated {0} map files from {1} to {2} with output in {3}", mapFiles.Count, projectName,
                                  resultDescr.bowtieIndexVersion, resultDescr.resultFolder);
                if (resultDescr.resultFolder != null) resultSubFolders.Add(resultDescr.resultFolder);
            }
            return resultSubFolders;
        }

        /// <summary>
        /// Finds and verifies the version of extracted reads in project folder,
        /// and sets the proper barcode set
        /// </summary>
        /// <param name="projectOrExtractedFolder"></param>
        /// <returns></returns>
        private string SetupForLatestExtractionFolder(string projectOrExtractedFolderOrName)
        {
            string projectOrExtractedFolder = PathHandler.GetRooted(projectOrExtractedFolderOrName);
            string extractedFolder = PathHandler.GetLatestExtractionFolder(projectOrExtractedFolder);
            string extractionVersion = PathHandler.GetExtractionVersion(extractedFolder);
            if (int.Parse(extractionVersion) < 28)
                throw new Exception("Extractions of versions < 28 can not be processed anymore. Please redo extraction!");
            string barcodeSet = PathHandler.ParseBarcodeSet(extractedFolder);
            SetBarcodeSet(barcodeSet);
            return extractedFolder;
        }

        /// <summary>
        /// List all read extraction summary files that correspond to the .map files.
        /// </summary>
        /// <param name="mapFilePaths"></param>
        /// <returns></returns>
        private ReadCounter ReadExtractionSummaryFiles(List<string> mapFilePaths)
        {
            ReadCounter readCounter = new ReadCounter(barcodes);
            Dictionary<string, object> summaryPaths = new Dictionary<string, object>();
            foreach (string mapFilePath in mapFilePaths)
            {
                string laneMapFolder = Path.GetDirectoryName(mapFilePath);
                string laneFolderName = Path.GetFileName(laneMapFolder);
                string extractionFolder = Path.GetDirectoryName(Path.GetDirectoryName(laneMapFolder));
                string laneExtractionFolder = Path.Combine(LaneInfo.GetFqSubFolder(extractionFolder), laneFolderName);
                string summaryPath = LaneInfo.GetSummaryPath(laneExtractionFolder);
                if (!summaryPaths.ContainsKey(summaryPath))
                {
                    summaryPaths[summaryPath] = null;
                    readCounter.AddExtractionSummary(summaryPath);
                }
            }
            return readCounter;
        }

        private string MakeDefaultResultFolderName(StrtGenome genome, string projectFolder, string projectName)
        {
            return string.Format("{0}_{1}_{2}_{3}", projectName, barcodes.Name, genome.GetBowtieMainIndexName(), DateTime.Now.ToPathSafeString());
        }

        /// <summary>
        /// Annotate features on genome from mapped data in mapFilePaths
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="projectFolder"></param>
        /// <param name="projectId"></param>
        /// <param name="resultFolderName"></param>
        /// <param name="mapFilePaths"></param>
        /// <returns></returns>
        private ResultDescription ProcessAnnotation(StrtGenome genome, string projectFolder, string projectId, 
                                                    string resultFolderName, List<string> mapFilePaths)
        {
            if (mapFilePaths.Count == 0)
                return null;
            string resultFolder = Path.Combine(projectFolder, resultFolderName);
            if (Directory.Exists(resultFolder))
                resultFolder += "_" + DateTime.Now.ToPathSafeString();
            ReadCounter readCounter = ReadExtractionSummaryFiles(mapFilePaths);
            int averageReadLen = readCounter.AverageReadLen;
            MappedTagItem.AverageReadLen = averageReadLen;
            genome.ReadLen = averageReadLen;
            UpdateGenesToPaintProp(projectFolder);
            GenomeAnnotations annotations = new GenomeAnnotations(props, genome);
            annotations.Load();
            TranscriptomeStatistics ts = new TranscriptomeStatistics(annotations, props, resultFolder, projectId);
            string syntLevelFile = PathHandler.GetSyntLevelFilePath(projectFolder, barcodes.HasUMIs);
            if (syntLevelFile != "")
                ts.SetSyntReadReporter(syntLevelFile);
            ts.ProcessMapFiles(mapFilePaths, averageReadLen);
            if (ts.GetNumMappedReads() == 0)
                Console.WriteLine("WARNING: contigIds of reads do not seem to match with genome Ids.\nWas the Bowtie index made on a different genome or contig set?");
            Console.WriteLine("Totally {0} annotations: {1} expressed genes and {2} expressed repeat types.",
                              ts.GetNumMappedReads(), annotations.GetNumExpressedTranscripts(), annotations.GetNumExpressedRepeats());
            Directory.CreateDirectory(resultFolder);
            Console.WriteLine("Saving to {0}...", resultFolder);
            string bowtieIndexVersion = PathHandler.GetSpliceIndexVersion(genome);
            ResultDescription resultDescr = new ResultDescription(mapFilePaths, bowtieIndexVersion, resultFolder);
            ts.SaveResult(readCounter, resultDescr);
            System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(props.GetType());
            using (StreamWriter writer = new StreamWriter(Path.Combine(resultFolder, "SilverBulletConfig.xml")))
                x.Serialize(writer, props);
            if (Props.props.InsertCells10Data)
                InsertCells10kData(projectId, annotations, resultDescr);
            return resultDescr;
        }

        /// <summary>
        /// Insert expression value and analysis setup info into cells10k DB.
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="annotations"></param>
        /// <param name="resultDescr"></param>
        private static void InsertCells10kData(string projectId, GenomeAnnotations annotations, ResultDescription resultDescr)
        {
            if (projectId.StartsWith(C1Props.C1ProjectPrefix))
            {
                Dictionary<string, int> cellIdByPlateWell = new ProjectDB().GetCellIdByPlateWell(projectId);
                if (cellIdByPlateWell.Count == 0)
                {
                    Console.WriteLine("Warning: No mapping from C1-chip to Seq-plate wells exists in DB. No expression data is inserted.");
                    return;
                }
                Console.WriteLine("Saving results to cells10k database...");
                try
                {
                    C1DB c1db = new C1DB();
                    c1db.InsertExpressions(annotations.IterC1DBExpressions(cellIdByPlateWell));
                    string parString = MakeParameterString();
                    c1db.InsertAnalysisSetup(projectId, resultDescr.bowtieIndexVersion, resultDescr.resultFolder, parString);
                    c1db.InsertExprBlobs(annotations.IterC1DBExprBlobs(cellIdByPlateWell));
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error inserting data to cells10k: {0}", e);
                }
            }
        }

        private static string MakeParameterString()
        {
            List<string> parameters = new List<string>();
            parameters.Add(string.Format("UMIFilter={0}/{1}", Props.props.RndTagMutationFilter, Props.props.RndTagMutationFilterParam));
            parameters.Add("UseMost5PrimeExonMapping=" + Props.props.UseMost5PrimeExonMapping);
            parameters.Add("MaxAlignmentMismatches=" + Props.props.MaxAlignmentMismatches);
            parameters.Add("MaxAlternativeMappings=" + Props.props.MaxAlternativeMappings);
            string parString = string.Join(",", parameters.ToArray());
            return parString;
        }

        /// <summary>
        /// Return average length of the first 1000 reads in mapFilePaths[0]
        /// </summary>
        /// <param name="mapFilePaths"></param>
        /// <returns></returns>
        private int EstimateReadLengthFromMapFiles(List<string> mapFilePaths)
        {
            MapFile mapFileReader = MapFile.GetMapFile(mapFilePaths[0], barcodes);
            int n = 0;
            long totalReadLength = 0;
            foreach (MultiReadMappings mrm in mapFileReader.MultiMappings(mapFilePaths[0]))
            {
                if (n++ == 1000)
                    break;
                totalReadLength += mrm.SeqLen;
            }
            return (int)Math.Ceiling((double)totalReadLength / n);
        }

        /// <summary>
        /// If there is a "gene_to_paint.txt" file in the projectFolder, Props.GenesToPaint is read
        /// as one line of comma-separated gene names from that file
        /// </summary>
        /// <param name="projectFolder"></param>
        private void UpdateGenesToPaintProp(string projectFolder)
        {
            string paintPath = Path.Combine(projectFolder, "genes_to_paint.txt");
            if (File.Exists(paintPath))
            {
                using (StreamReader reader = new StreamReader(paintPath))
                {
                    string line = reader.ReadLine().Trim();
                    string[] genesToPaint = line.Split(',');
                    Console.WriteLine("{0} genes to paint defined by file {1}", genesToPaint.Length, paintPath);
                    for (int i = 0; i < genesToPaint.Length; i++)
                        genesToPaint[i] = genesToPaint[i].Trim();
                    props.GenesToPaint = genesToPaint;
                }
            }
        }

        /// <summary>
        /// If readLength == 0, dumps the whole sequence for each gene, otherwise dumps
        /// all possible subsequences of readLength from each gene.
        /// If barcodes == null, no barcode and GGG sequences will be inserted 
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="readLen">length of read seq without barcodes + GGG</param>
        /// <param name="flankLength">sequences will be extended with this # of bases in both 5' and 3'</param>
        public void DumpTranscripts(Barcodes barcodes, StrtGenome genome, int readLen, int step, int maxPerGene, string outFile,
                                    bool outputFastq, bool makeSplices, int minOverhang, int maxSkip, int flankLength)
        {
            if (readLen > 0) genome.ReadLen = readLen;
            bool variantGenes = genome.GeneVariants;
            if (makeSplices)
                Console.WriteLine("Making all splices that have >= {0} bases overhang and max {1} exons excised.", minOverhang, maxSkip);
            props.AnalyzeSeqUpstreamTSSite = true; // To force chr sequence reading
            GenomeAnnotations annotations = new GenomeAnnotations(props, genome);
            annotations.SetupChromsomes();
            annotations.SetupGenes();
            using (StreamWriter outWriter = new StreamWriter(outFile))
            {
                StreamWriter spliceWriter = null;
                string spliceOutput = outputFastq ? outFile.Replace(".fq", "") + "_splices_only.fq" :
                                                   outFile.Replace(".fa", "") + "_splices_only.fa";
                if (makeSplices)
                    spliceWriter = spliceOutput.OpenWrite();
                int nSeqs = 0, nTrSeqs = 0, nSplSeq = 0, bcIdx = 0;
                foreach (GeneFeature gf in annotations.geneFeatures.Values)
                {
                    if (StrtGenome.IsASpliceAnnotation(gf.Chr)) continue;
                    if (!variantGenes && gf.IsVariant())
                        continue;
                    int startBeforeFlank = gf.Start;
                    int flank = 0;
                    if (!gf.IsSpike())
                    {
                        gf.Start -= flankLength;
                        gf.End += flankLength;
                        flank = flankLength;
                    }
                    string readStart = "";
                    if (barcodes != null)
                        readStart = new string('A', barcodes.BarcodePos) + barcodes.Seqs[bcIdx++ % barcodes.Count] + "GGG";
                    List<DnaSequence> exonSeqsInChrDir = annotations.GetExonSeqsInChrDir(gf);
                    if (readLen == 0)
                    {
                        DnaSequence gfTrFwSeq = new ShortDnaSequence(gf.Length);
                        foreach (DnaSequence s in exonSeqsInChrDir)
                            gfTrFwSeq.Append(s);
                        if (gf.Strand == '-')
                            gfTrFwSeq.RevComp();
                        string header = string.Format("Gene={0}:Chr={1}{2}:CAPPos={3}:TrLen={4}:Flanks={5}",
                                                      gf.Name, gf.Chr, gf.Strand, startBeforeFlank, gfTrFwSeq.Count, flank);
                        if (outputFastq)
                        {
                            outWriter.WriteLine("@{0}", header);
                            outWriter.WriteLine(gfTrFwSeq);
                            outWriter.WriteLine("+\n{0}", new String('b', (int)gfTrFwSeq.Count));
                        }
                        else
                        {
                            outWriter.WriteLine(">{0}", header);
                            outWriter.WriteLine(gfTrFwSeq);
                        }
                    }
                    else
                    {
                        int n = 0;
                        int trLen = gf.GetTranscriptLength();
                        List<ReadFrag> readFrags = ReadFragGenerator.MakeAllReadFrags(readLen, step, makeSplices, maxSkip, minOverhang,
                                                                                        exonSeqsInChrDir);
                        foreach (ReadFrag frag in readFrags)
                        {
                            string exonNos = string.Join("-", frag.ExonIds.ConvertAll(i => (gf.Strand == '+') ? i.ToString() : (gf.ExonCount + 1 - i).ToString()).ToArray());
                            int posInTrFw = (gf.Strand == '+') ? 1 + frag.TrPosInChrDir : (1 + trLen - frag.TrPosInChrDir - (int)frag.Length);
                            int posInChr = gf.GetChrPosFromTrPosInChrDir(frag.TrPosInChrDir);
                            if (gf.Strand == '-')
                                frag.Seq.RevComp();
                            string seqString = readStart + frag.Seq.ToString();
                            string header = string.Format("Gene={0}:Chr={1}{2}:Pos={3}:TrPos={4}:Exon={5}",
                                                                    gf.Name, gf.Chr, gf.Strand, posInChr, posInTrFw, exonNos);
                            string outBlock = outputFastq ?
                                                    string.Format("@{0}\n{1}\n+\n{2}", header, seqString, new String('b', seqString.Length))
                                                : string.Format(">{0}\n{1}", header, seqString);
                            nSeqs++;
                            outWriter.WriteLine(outBlock);
                            if (spliceWriter != null && frag.ExonIds.Count > 1)
                            {
                                nSplSeq++;
                                spliceWriter.WriteLine(outBlock);
                            }
                            if (maxPerGene > 0 && n++ >= maxPerGene)
                                break;
                        }
                    }
                    nTrSeqs++;
                }
                if (readLen == 0)
                    Console.WriteLine("\nWrote {0} transcripts to {1}", nTrSeqs, outFile);
                else
                    Console.WriteLine("\nWrote {0} reads from {1} transcripts to {2}", nSeqs, nTrSeqs, outFile);
                if (spliceWriter != null)
                {
                    spliceWriter.Close();
                    Console.WriteLine("\nAlso wrote the {0} splice spanning reads to {1}", nSplSeq, spliceOutput);
                }
            }
        }
    }
}
