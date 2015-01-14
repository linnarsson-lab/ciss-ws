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
        private Barcodes barcodes;

        public StrtReadMapper()
        {
            barcodes = Props.props.Barcodes;
        }
        private void SetBarcodeSet(string barcodesName)
        {
            Props.props.BarcodesName = barcodesName;
            barcodes = Props.props.Barcodes;
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
                              annotationFile, genome.GetMainIndexName(), Path.GetFileName(errorsPath));
            Background.Message("Updating annotations...");
            AnnotationBuilder builder = new AnnotationBuilder(AnnotationReader.GetAnnotationReader(genome, annotationFile));
            builder.UpdateSilverBulletGenes(genome, errorsPath);
            Console.WriteLine("Done.");
            Background.Progress(100);
            Background.Message("Ready");
        }

        /// <summary>
        /// Construct the repeat-masked genome, artificial splice junction chromosome and transcript annotation file.
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="annotationFile">Optional specific annotation filename</param>
        public void BuildJunctions(StrtGenome genome, string annotationFile)
        {
            AnnotationReader annotReader = AnnotationReader.GetAnnotationReader(genome, annotationFile);
            genome.AnnotationDate = annotReader.AnnotationDate; // DateTime.Now.ToString("yyMMdd");
            string strtAnnotFolder = genome.GetStrtAnnotFolder();
            if (!Directory.Exists(strtAnnotFolder))
                Directory.CreateDirectory(strtAnnotFolder);
            DateTime startTime = DateTime.Now;
            annotationFile = AnnotationReader.GetAnnotationFilename(genome, annotationFile);
            Console.WriteLine("*** Build of {0} junctions into {1} from {2} started {3} ***",
                genome.GetMainIndexName(), strtAnnotFolder, annotationFile, DateTime.Now);
            AnnotationBuilder builder = new AnnotationBuilder(annotReader);
            builder.BuildExonSplices(genome);
        }

        /// <summary>
        /// Construct the artificial splice chromosome, the transcript annotation file, and build the aligner index.
        /// </summary>
        /// <param name="genome"></param>
        public void BuildJunctionsAndIndex(StrtGenome genome)
        {
            BuildJunctionsAndIndex(genome, "");
        }
		public void BuildJunctionsAndIndex(StrtGenome genome, string annotationFile)
		{
            Aligner aligner = Aligner.GetAligner(genome);
            if (genome.GeneVariants == false)
            {
                BuildJunctions(genome, annotationFile);
                NonExonRepeatMasker nerm = new NonExonRepeatMasker();
                nerm.Mask(genome, genome.GetStrtAnnotFolder());
                aligner.BuildIndex();
            }
            genome.GeneVariants = true;
            BuildJunctions(genome, annotationFile);
            aligner.BuildIndex();
        }

        public void BuildIndex(StrtGenome genome)
        {
            Aligner aligner = Aligner.GetAligner(genome);
            aligner.BuildIndex();
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
            Props.props.TotalNumberOfAddedSpikeMolecules = projDescr.SpikeMoleculeCount;
            logWriter.WriteLine("{0} Extracting {1} lanes with barcodes {2}...", DateTime.Now, projDescr.runIdsLanes.Length, projDescr.barcodeSet);
            logWriter.Flush();
            if (barcodes.HasUMIs)
                logWriter.WriteLine("{0} MinPhredScoreInRandomTag={1}", DateTime.Now, Props.props.MinPhredScoreInRandomTag);
            Extract(projDescr);
            string[] speciesArgs = ParsePlateLayout(projDescr.plateId, projDescr.SampleLayoutPath, projDescr.defaultSpecies);
            projDescr.annotationVersion = ANNOTATION_VERSION;
            foreach (string speciesArg in speciesArgs)
            {
                StrtGenome genome = StrtGenome.GetGenome(speciesArg, projDescr.analyzeVariants, projDescr.defaultBuild, true);
                int[] genomeBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
                genome.SplcIndexReadLen = GetAverageReadLen(projDescr.laneInfos);
                logWriter.WriteLine("{0} Aligning to {1}...", DateTime.Now, genome.BuildVarAnnot); logWriter.Flush();
                CreateAlignments(genome, projDescr.laneInfos, genomeBcIndexes);
                List<string> mapFiles = LaneInfo.RetrieveAllMapFilePaths(projDescr.laneInfos);
                Props.props.UseRPKM = projDescr.rpkm;
                Props.props.DirectionalReads = projDescr.DirectionalReads;
                Props.props.SenseStrandIsSequenced = projDescr.SenseStrandIsSequenced;
                projDescr.SetGenomeData(genome);
                logWriter.WriteLine("{0} Annotating {1} alignment files...", DateTime.Now, mapFiles.Count);
                logWriter.WriteLine("{0} setting: AllTrVariants={1} Gene5'Extensions={4} #SpikeMols={5} DirectionalReads={2} RPKM={3}",
                                    DateTime.Now, projDescr.analyzeVariants, Props.props.DirectionalReads, Props.props.UseRPKM,
                                    Props.props.GeneFeature5PrimeExtension, Props.props.TotalNumberOfAddedSpikeMolecules);
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
        /// If sampleLayoutPath is parsable, the species/build name(s) to use are extracted from that file,
        /// and the layout it read into Barcodes object. Otherwise the defaultSpeciesArg is simply returned.
        /// </summary>
        /// <param name="projectName"></param>
        /// <param name="sampleLayoutPath"></param>
        /// <param name="defaultSpeciesArg"></param>
        /// <returns>Ids of all the species/builds that are on the plate</returns>
        private string[] ParsePlateLayout(string projectName, string sampleLayoutPath, string defaultSpeciesArg)
        {
            string[] speciesArgs = new string[] { defaultSpeciesArg };
            PlateLayout sampleLayout = PlateLayout.GetPlateLayout(projectName, sampleLayoutPath);
            Console.WriteLine(projectName + " " + sampleLayoutPath);
            if (sampleLayout != null)
            {
                barcodes.SetSampleLayout(sampleLayout);
                speciesArgs = sampleLayout.BuildIds;
            }
            return speciesArgs;
        }

        /// <summary>
        /// Find or make a proper aligner index and align the lanes defined by laneInfo
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="laneInfos"></param>
        /// <param name="genomeBcIndexes">optionally only process specific barcodes</param>
        private void CreateAlignments(StrtGenome genome, List<LaneInfo> laneInfos, int[] genomeBcIndexes)
        {
            Aligner aligner = AssertASplcIndex(genome);
            Console.WriteLine("{0} aligning {1} lanes against {2}...", Props.props.Aligner, laneInfos.Count, genome.GetSplcIndexName());
            foreach (LaneInfo laneInfo in laneInfos)
                aligner.CreateAlignments(laneInfo, genomeBcIndexes);
        }

        /// <summary>
        /// Returns an aligner for the genome. Builds the STRT genome if needed. Throws exception on failure.
        /// </summary>
        /// <param name="genome"></param>
        /// <returns></returns>
        private Aligner AssertASplcIndex(StrtGenome genome)
        {
            Aligner aligner = Aligner.GetAligner(genome);
            if (!aligner.FindASplcIndex())
            {
                int actualReadLen = genome.SplcIndexReadLen;
                genome.SplcIndexReadLen = genome.SplcIndexReadLen - (genome.SplcIndexReadLen % 4);
                Console.WriteLine("Cannot find {0} index - building one with ReadLen={1}", Props.props.Aligner, genome.SplcIndexReadLen);
                BuildJunctionsAndIndex(genome);
                if (!aligner.FindASplcIndex())
                    throw new Exception("Could not build aligner index with ReadLen between " + genome.SplcIndexReadLen +
                                        " and " + actualReadLen + " for " + genome.Build + " and " + genome.Annotation);
                genome.SplcIndexReadLen = actualReadLen;
            }
            return aligner;
        }

        /// <summary>
        /// Run the aligner on reads in a specific Extraction folder, or on all reads in the last Extraction folder of a project.
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
            genome.SplcIndexReadLen = GetAverageReadLen(laneInfos);
            int[] genomeBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
            CreateAlignments(genome, laneInfos, genomeBcIndexes);
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
                genome.SplcIndexReadLen = GetAverageReadLen(laneInfos);
                string spResultFolderName = resultFolderName;
                if (resultFolderName == "" || resultFolderName == null)
                    spResultFolderName = MakeDefaultResultFolderName(genome, projectFolder, projectName);
                string readDir = !Props.props.DirectionalReads ? "No" : Props.props.SenseStrandIsSequenced ? "Sense" : "Antisense";
                CreateAlignments(genome, laneInfos, genomeSelectedBcIdxs);
                List<string> mapFiles = LaneInfo.RetrieveAllMapFilePaths(laneInfos);
                Console.WriteLine("Annotating {0} map files from {1}\nDirectionalReads={2} RPKM={3} SelectedMappingType={4}...",
                                  mapFiles.Count, projectName, readDir, Props.props.UseRPKM, Props.props.SelectedMappingType);
                ResultDescription resultDescr = ProcessAnnotation(genome, projectFolder, projectName, spResultFolderName, mapFiles);
                Console.WriteLine("...output in {0}", resultDescr.resultFolder);
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
            return string.Format("{0}_{1}_{2}_{3}", projectName, barcodes.Name, genome.GetMainIndexName(), DateTime.Now.ToPathSafeString());
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
            genome.SplcIndexReadLen = averageReadLen;
            UpdateGenesToPaintProp(projectFolder);
            GenomeAnnotations annotations = new GenomeAnnotations(genome);
            annotations.Load();
            TranscriptomeStatistics ts = new TranscriptomeStatistics(annotations, Props.props, resultFolder, projectId);
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
            ResultDescription resultDescr = new ResultDescription(mapFilePaths, genome, resultFolder);
            ts.SaveResult(readCounter, resultDescr);
            System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(Props.props.GetType());
            using (StreamWriter writer = new StreamWriter(Path.Combine(resultFolder, "SilverBulletConfig.xml")))
                x.Serialize(writer, Props.props);
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
            if (projectId.StartsWith(C1Props.C1ProjectPrefix) && annotations.GenesSetupFromC1DB)
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
                    c1db.InsertAnalysisSetup(projectId, resultDescr.splcIndexVersion, resultDescr.resultFolder, parString);
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
                    Props.props.GenesToPaint = genesToPaint;
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
            if (readLen > 0) genome.SplcIndexReadLen = readLen;
            bool variantGenes = genome.GeneVariants;
            if (makeSplices)
                Console.WriteLine("Making all splices that have >= {0} bases overhang and max {1} exons excised.", minOverhang, maxSkip);
            Props.props.AnalyzeSeqUpstreamTSSite = true; // To force chr sequence reading
            GenomeAnnotations annotations = new GenomeAnnotations(genome);
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
                foreach (GeneFeature gf in annotations.IterOrderedGeneFeatures(true, true))
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
