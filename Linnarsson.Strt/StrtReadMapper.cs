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

namespace Linnarsson.Strt
{
    /// <summary>
    /// Various methods for major steps and utility functionalities of the STRT pipeline.
    /// </summary>
    public class StrtReadMapper
	{
        private Props props;
        private Barcodes barcodes;
        private PathHandler pathHandler;

        public StrtReadMapper(Props props)
        {
            this.props = props;
            barcodes = props.Barcodes;
            this.pathHandler = new PathHandler(props);
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
        public void UpdateSilverBulletGenes(StrtGenome genome, string errorsPath)
        {
            Console.WriteLine("*** Updating annotation file for {0} using {1} ***",
                              genome.GetBowtieIndexName(), Path.GetFileName(errorsPath));
            Background.Message("Updating annotations...");
            AnnotationBuilder builder = AnnotationBuilder.GetAnnotationBuilder(props, genome);
            builder.UpdateSilverBulletGenes(genome, errorsPath);
            Console.WriteLine("Done.");
            Background.Progress(100);
            Background.Message("Ready");
        }

        /// <summary>
        /// Construct the artificial splice junction chromosome and transcript annotation file.
        /// </summary>
        /// <param name="genome"></param>
        public void BuildJunctions(StrtGenome genome)
        {
            BuildJunctions(genome, "");
        }
        public void BuildJunctions(StrtGenome genome, string newIndexName)
        {
            DateTime startTime = DateTime.Now;
            Background.Message("Building junctions");
            Background.Progress(0);
            Console.WriteLine("*** Build of spliced exon junctions for {0} started at {1} ***", genome.GetBowtieIndexName(), DateTime.Now);
            AnnotationBuilder builder = AnnotationBuilder.GetAnnotationBuilder(props, genome);
            builder.BuildExonSplices(genome, newIndexName);
            Console.WriteLine("*** Splice build completed at {0} ***", DateTime.Now);
            Background.Progress(100);
        }

        /// <summary>
        /// Construct the artificial splice chromosome, the transcript annotation file, and build the Bowtie index.
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="newIndexName">If null or "", the index will get its default name</param>
		public void BuildJunctionsAndIndex(StrtGenome genome, string newIndexName)
		{
            string btIdxFolder = PathHandler.GetBowtieIndicesFolder();
            if (!Directory.Exists(btIdxFolder))
                throw new IOException("The Bowtie index folder cannot be found. Please set the BowtieIndexFolder property.");
            BuildJunctions(genome, newIndexName);
            if (Background.CancellationPending) return;
            DateTime startTime = DateTime.Now;
            if (newIndexName == null || newIndexName == "")
                newIndexName = genome.GetBowtieIndexName();
            Console.WriteLine("*** Build of Bowtie index {0} started at {1} ***", newIndexName, DateTime.Now);
            Background.Message("Running bowtie-build");
            Background.Progress(20);
            string genomeFolder = PathHandler.GetGenomeSequenceFolder(genome);
            List<string> chrFiles = new List<string>();
            foreach (string f in Directory.GetFiles(genomeFolder, "*chr*.fa"))
                if (genome.IsChrInBuild(PathHandler.ExtractChrId(f)))
                    chrFiles.Add(f);
            string chrFilesArg = string.Join(",", chrFiles.ToArray());
            string outfileHead = Path.Combine(props.BowtieIndexFolder, newIndexName);
            string arguments = String.Format("{0} {1}", chrFilesArg, outfileHead);
            string cmd = "bowtie-build";
            Console.WriteLine(cmd + " " + arguments);
            int exitCode = CmdCaller.Run(cmd, arguments);
            if (exitCode != 0)
                Console.Error.WriteLine("Failed to run bowtie-build. ExitCode={0}", exitCode);
            Background.Progress(100);
            Background.Message("Ready");
            Console.WriteLine("*** Splice junction and Bowtie index build completed at {0} ***", DateTime.Now);
		}

        /// <summary>
        /// Split reads into individual files according to the barcodes.
        /// No filtering or removal of barcodes is performed.
        /// </summary>
        /// <param name="projectFolder"></param>
		public void Split(string projectFolder)
		{
            Barcodes barcodes = props.Barcodes;
			string date = DateTime.Now.ToPathSafeString();

			// Put everything in the right place
			string readsFolder = PathHandler.GetReadsFolder(projectFolder);
			string outputFolder = Path.Combine(readsFolder, "ByBarcode " + date);

			Directory.CreateDirectory(outputFolder);
			var outputFiles = new Dictionary<string, StreamWriter>();
			DateTime start = DateTime.Now;
            List<string> files = PathHandler.CollectReadsFilesNames(projectFolder);
			Console.WriteLine("Splitting {0} files from {1}...", files.Count, readsFolder);
			foreach(string file in files)
			{
				int count = 0;
                int nobcCount = 0;
				string fileName = Path.GetFileNameWithoutExtension(file);
				Console.WriteLine("Processing " + fileName);

				Dictionary<string, StreamWriter> bcodeFiles = new Dictionary<string, StreamWriter>();
                Dictionary<string, int> counts = new Dictionary<string,int>();
                StreamWriter sw_slask = new StreamWriter(Path.Combine(outputFolder, fileName + "_NOBAR.fq"));
				foreach (FastQRecord rec in FastQFile.Stream(file, props.QualityScoreBase))
				{
					count++;
					int insertLength = rec.Sequence.Length;
					StreamWriter f = sw_slask;
                    int i = Array.IndexOf(barcodes.Seqs, rec.Sequence.Substring(0, barcodes.SeqLength));
                    if (i >= 0 && i < barcodes.FirstNegBarcodeIndex)
                    {
                        string bc = barcodes.Seqs[i];
                        if (!bcodeFiles.ContainsKey(bc))
                        {
                            string bcfileName = Path.Combine(outputFolder, fileName + "_" + bc + ".fq");
                            bcodeFiles[bc] = new StreamWriter(bcfileName);
                            counts[bc] = 0;
                        }
                        f = bcodeFiles[bc];
                        counts[bc]++;
                    }
                    else nobcCount++;
					f.WriteLine(rec.ToString(props.QualityScoreBase));

					if((DateTime.Now - start).TotalSeconds > 2)
					{
						start = DateTime.Now;
						Background.Message(Math.Round(count / 1e6d, 1).ToString() + "M reads extracted.");
					}
					if(Background.CancellationPending) break;
				}
				sw_slask.Close();
                StreamWriter sw_summary = new StreamWriter(Path.Combine(outputFolder, fileName + "_SUMMARY.fq"));
                foreach (string bc in bcodeFiles.Keys)
                    sw_summary.WriteLine(Path.GetFileName(file) + "\t" + bc + "\t" + counts[bc]);
                sw_summary.WriteLine(Path.GetFileName(file) + "\tNOBAR\t" + nobcCount);
                sw_summary.Close();
                foreach (StreamWriter sw in bcodeFiles.Values)
                    if (sw != null) sw.Close();
				if(Background.CancellationPending) break;

			}
			Background.Progress(100);
			Background.Message("Ready");
		}

        public void BarcodeStats(string projectFolder)
        {
            Barcodes barcodes = props.Barcodes;
            const int MAX_READS = 200000;
            string date = DateTime.Now.ToPathSafeString();
            Console.WriteLine("Calculating...");
            DateTime start = DateTime.Now;

            List<string> files = PathHandler.CollectReadsFilesNames(projectFolder);
            foreach (string file in files)
            {
                int count = 0;
                string fileName = Path.GetFileNameWithoutExtension(file);
                Console.WriteLine("Processing " + fileName);

                int bcWithTSSeqLen = barcodes.GetLengthOfBarcodesWithTSSeq();
                int[] barcodeCounts = new int[barcodes.Count];
                string[] barcodesWTSSeq = barcodes.GetBarcodesWithTSSeq();
                foreach (FastQRecord rec in FastQFile.Stream(file, props.QualityScoreBase))
                {
                    count++;
                    if (count > MAX_READS) break;
                    for (int i = 0; i < barcodesWTSSeq.Length; i++)
                    {
                        if (barcodesWTSSeq[i] == rec.Sequence.Substring(0, bcWithTSSeqLen))
                        {
                            barcodeCounts[i]++;
                            break;
                        }
                    }
                    if ((DateTime.Now - start).TotalSeconds > 2)
                    {
                        start = DateTime.Now;
                        Background.Message(Math.Round(count / 1e6d, 1).ToString() + "M reads extracted.");
                    }
                    if (Background.CancellationPending) break;
                }
                if (Background.CancellationPending) break;

                for (int i = 0; i < barcodes.Seqs.Length; i++)
                {
                    Console.WriteLine(barcodes.Seqs[i] + " " + barcodeCounts[i].ToString());
                }
            }
            Background.Progress(100);
            Background.Message("Ready");
        }

        /// <summary>
        /// Extract and filter reads from the raw reads files in the project Reads/ directory.
        /// Will, depending on barcodeSet specification, extract barcodes and trim template switch G:s.
        /// Removes low complexity, low quality and short reads.
        /// Accepted reads are written in FastQ format to Extracted.../...barcoded.fq, and
        /// rejected reads to Extracted.../...slask.fq files.
        /// </summary>
        /// <param name="project">project folder or project name</param>
        /// <param name="laneArgs">Items of "RunNo:LaneNos" that define the lanes of the project.
        ///                        If empty, all sequence files in projectFolder/Reads are used.</param>
		public List<LaneInfo> Extract(string project, List<string> laneArgs)
		{
            project = PathHandler.GetRootedProjectFolder(project);
            List<LaneInfo> extrInfos = new List<LaneInfo>();
            if (laneArgs.Count > 0)
                extrInfos = PathHandler.ListReadsFiles(laneArgs);
            else
                foreach (string extractedFile in PathHandler.CollectReadsFilesNames(project))
                    extrInfos.Add(new LaneInfo(extractedFile, "X", 'x'));
            string outputFolder = pathHandler.MakeExtractedFolder(project, barcodes.Name, EXTRACTION_VERSION);
            Extract(extrInfos, outputFolder);
            return extrInfos;
        }

        public void Extract(ProjectDescription pd)
        {
            pd.extractionInfos = PathHandler.ListReadsFiles(pd.runIdsLanes.ToList());
            pd.extractionVersion = EXTRACTION_VERSION;
            string outputFolder = pathHandler.MakeExtractedFolder(pd.ProjectFolder, barcodes.Name, EXTRACTION_VERSION);
            Extract(pd.extractionInfos, outputFolder);
        }

        public static readonly string EXTRACTION_VERSION = "27";
        private void Extract(List<LaneInfo> extrInfos, string outputFolder)
        {
            DateTime start = DateTime.Now;
            ReadExtractor readExtractor = new ReadExtractor(props);
            foreach (LaneInfo extrInfo in extrInfos)
			{
                extrInfo.extractionTopFolder = outputFolder;
                ReadCounter readCounter = new ReadCounter();
                readCounter.AddReadFilename(extrInfo.readFilePath);
                ExtractionWordCounter wordCounter = new ExtractionWordCounter(props.ExtractionCounterWordLength);
                GetExtractedFilePaths(outputFolder, extrInfo);
                if (!AllFilePathsExist(extrInfo.extractedFilePaths) || !File.Exists(extrInfo.summaryFilePath))
                {
                    StreamWriter[] sws_barcoded = OpenStreamWriters(extrInfo.extractedFilePaths);
                    StreamWriter sw_slask = extrInfo.slaskFilePath.OpenWrite();
                    int bcIdx;
                    ExtractionQuality extrQ = (props.AnalyzeExtractionQualities) ? new ExtractionQuality(props.LargestPossibleReadLength) : null;
                    foreach (FastQRecord fastQRecord in FastQFile.Stream(extrInfo.readFilePath, props.QualityScoreBase))
                    {
                        FastQRecord rec = fastQRecord;
                        if (extrQ != null) extrQ.Add(rec);
                        wordCounter.AddRead(rec.Sequence);
                        int readStatus = readExtractor.Extract(ref rec, out bcIdx);
                        readCounter.Add(readStatus);
                        if (readStatus == ReadStatus.VALID)
                        {
                            sws_barcoded[bcIdx].WriteLine(rec.ToString(props.QualityScoreBase));
                        }
                        else sw_slask.WriteLine(rec.ToString(props.QualityScoreBase));
                    }
                    CloseStreamWriters(sws_barcoded);
                    sw_slask.Close();
                    StreamWriter sw_summary = extrInfo.summaryFilePath.OpenWrite();
                    sw_summary.WriteLine(readCounter.TotalsToTabString());
                    sw_summary.WriteLine("\nBelow are the most common words among all reads.\n");
                    sw_summary.WriteLine(wordCounter.GroupsToString(200));
                    sw_summary.Close();
                    if (extrQ != null)
                        extrQ.Write(extrInfo);
                    extrInfo.nReads = readCounter.PartialTotal;
                    extrInfo.nPFReads = readCounter.PartialCount(ReadStatus.VALID);
                }
                if (Background.CancellationPending) break;
            }
		}

        private void GetExtractedFilePaths(string extractedFolder, LaneInfo laneInfo)
        {
            Match m = Regex.Match(laneInfo.readFilePath, "(Run[0-9]+_L[0-9]_[0-9]_[0-9]+)_");
            string extractedByBcFolder = Path.Combine(Path.Combine(extractedFolder, "fq"), m.Groups[1].Value);
            if (!Directory.Exists(extractedByBcFolder))
                Directory.CreateDirectory(extractedByBcFolder);
            SetExtractedFilesInfo(laneInfo, extractedByBcFolder);
        }

        private void SetExtractedFilesInfo(LaneInfo laneInfo, string extractedByBcFolder)
        {
            laneInfo.extractedFileFolder = extractedByBcFolder;
            string[] extractedFilePaths = new string[Math.Max(1, barcodes.AllCount)];
            for (int i = 0; i < extractedFilePaths.Length; i++)
                extractedFilePaths[i] = Path.Combine(extractedByBcFolder, i.ToString() + ".fq");
            laneInfo.extractedFilePaths = extractedFilePaths;
            laneInfo.slaskFilePath = Path.Combine(extractedByBcFolder, "slask.fq.gz");
            laneInfo.summaryFilePath = Path.Combine(extractedByBcFolder, "summary.txt");
        }

        private StreamWriter[] OpenStreamWriters(string[] extractedFilePaths)
        {
            StreamWriter[] sws_barcoded = new StreamWriter[extractedFilePaths.Length];
            for (int i = 0; i < extractedFilePaths.Length; i++)
                sws_barcoded[i] = new StreamWriter(extractedFilePaths[i]);
            return sws_barcoded;
        }

        private static void CloseStreamWriters(StreamWriter[] sws_barcoded)
        {
            for (int i = 0; i < sws_barcoded.Length; i++)
                sws_barcoded[i].Close();
        }

        private bool AllFilePathsExist(string[] filePaths)
        {
            foreach (string path in filePaths)
                if (!File.Exists(path))
                    return false;
            return true;
        }

        /// <summary>
        /// Performs extraction, mapping, and annotation on the lanes, bc, and layout/species defined by projDescr.
        /// Extraction and mapping are done if no data are available with the current software/index versions.
        /// Annotation is always performed and data put in a dated result folder.
        /// </summary>
        /// <param name="projDescr"></param>
        public void Process(ProjectDescription projDescr)
        {
            Extract(projDescr);
            string[] speciesArgs = GetSpeciesArgs(projDescr.SampleLayoutPath, projDescr.defaultSpecies);
            projDescr.annotationVersion = ANNOTATION_VERSION;
            foreach (string speciesArg in speciesArgs)
            {
                StrtGenome genome = StrtGenome.GetGenome(speciesArg, projDescr.analyzeVariants, projDescr.defaultBuild);
                SetAvailableBowtieIndexVersion(projDescr, genome);
                CreateBowtieMaps(genome, projDescr.extractionInfos);
                List<string> mapFilePaths = GetAllMapFilePaths(projDescr.extractionInfos);
                ResultDescription resultDescr = ProcessAnnotation(projDescr.barcodeSet, genome, projDescr.ProjectFolder, 
                                                                  projDescr.projectName, mapFilePaths);
                projDescr.resultDescriptions.Add(resultDescr);
                System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(projDescr.GetType());
                StreamWriter writer = new StreamWriter(Path.Combine(resultDescr.resultFolder, "config.xml"));
                x.Serialize(writer, projDescr);
                writer.Close();
            }
        }

        private static void SetAvailableBowtieIndexVersion(ProjectDescription projDescr, StrtGenome genome)
        {
            string bowtieIndexVersion = PathHandler.GetIndexVersion(genome.GetBowtieSplcIndexName());
            if (bowtieIndexVersion == "" && genome.Annotation != "UCSC")
            {
                Console.WriteLine("Could not find a Bowtie index for " + genome.Annotation +
                                    " - trying UCSC instead for " + projDescr.projectName);
                genome.Annotation = "UCSC";
            }
        }

        /// <summary>
        /// Uses the SampleLayout file to decide which species(s) to run bowtie and annotate against.
        /// If it does not exist, use the default species supplied.
        /// Note that this command line called method uses the standard (PathHandler) layout filename. When instead
        /// using the ProjectDB, the layout filename is taken from the database to allow updates with alternatively versioned names.
        /// </summary>
        /// <param name="projectFolderOrName"></param>
        /// <param name="defaultSpeciesArg">Species to use if no layout file exists in projectFolder</param>
        /// <param name="analyzeAllGeneVariants">true to analyze all transcript splice variants defined in annoatation file</param>
        /// <returns>The subpaths to result folder (one per species) under project folder</returns>
        public List<string> MapAndAnnotateWithLayout(string projectFolderOrName, string defaultSpeciesArg, bool analyzeAllGeneVariants)
        {
            string projectFolder = PathHandler.GetRootedProjectFolder(projectFolderOrName);
            string[] speciesArgs = GetSpeciesArgs(projectFolder, defaultSpeciesArg);
            List<string> resultSubFolders = new List<string>();
            foreach (string speciesArg in speciesArgs)
            {
                string resultSubFolder = MapAndAnnotate(projectFolder, speciesArg, analyzeAllGeneVariants);
                if (resultSubFolder != null) resultSubFolders.Add(resultSubFolder);
            }
            return resultSubFolders;
        }

        private string[] GetSpeciesArgs(string projectFolder, string defaultSpeciesArg)
        {
            string sampleLayoutPath = PathHandler.GetSampleLayoutPath(projectFolder);
            string[] speciesArgs = new string[] { defaultSpeciesArg };
            if (File.Exists(sampleLayoutPath))
            {
                PlateLayout sampleLayout = new PlateLayout(sampleLayoutPath);
                barcodes.SetSampleLayout(sampleLayout);
                speciesArgs = sampleLayout.GetSpeciesAbbrevs();
            }
            return speciesArgs;
        }

        private static List<string> GetAllMapFilePaths(List<LaneInfo> laneInfos)
        {
            List<string> mapFiles = new List<string>();
            foreach (LaneInfo info in laneInfos)
                mapFiles.AddRange(info.mappedFilePaths);
            return mapFiles;
        }

        private void CreateBowtieMaps(StrtGenome genome, List<LaneInfo> extrInfos)
        {
            foreach (LaneInfo extrInfo in extrInfos)
                CreateBowtieMaps(genome, extrInfo);
        }
        private void CreateBowtieMaps(StrtGenome genome, LaneInfo extrInfo)
        {
            int n = 0;
            string splcIndexVersion = SetMappedFileFolder(genome, extrInfo);
            string mapFolder = extrInfo.mappedFileFolder;
            if (!Directory.Exists(mapFolder))
                Directory.CreateDirectory(mapFolder);
            extrInfo.bowtieLogFilePath = Path.Combine(mapFolder, "bowtie_output.txt");
            List<string> mapFiles = new List<string>();
            int[] genomeBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
            foreach (string fqPath in extrInfo.extractedFilePaths)
            {
                int bcIdx = int.Parse(Path.GetFileNameWithoutExtension(fqPath));
                if (Array.IndexOf(genomeBcIndexes, bcIdx) == -1)
                    continue;
                string fqUnmappedReadsPath = Path.Combine(mapFolder, bcIdx + ".fq-" + genome.Build);
                string outputMainPath = Path.Combine(mapFolder, bcIdx + "_" + genome.Build + ".map");
                AssertBowtieOutputFile(genome.Build, fqPath, outputMainPath, fqUnmappedReadsPath, extrInfo.bowtieLogFilePath);
                mapFiles.Add(outputMainPath);
                string outputSplcPath = Path.Combine(mapFolder, bcIdx + "_" +  splcIndexVersion + ".map");
                AssertBowtieOutputFile(genome.GetBowtieSplcIndexName(), fqUnmappedReadsPath, outputSplcPath, "", extrInfo.bowtieLogFilePath);
                mapFiles.Add(outputSplcPath);
                Background.Progress((int)(++n / extrInfo.extractedFilePaths.Length));
                if (Background.CancellationPending) break;
            }
            extrInfo.mappedFilePaths = mapFiles.ToArray();
        }

        private static string SetMappedFileFolder(StrtGenome genome, LaneInfo extrInfo)
        {
            string splcIndexVersion = PathHandler.GetIndexVersion(genome.GetBowtieSplcIndexName()); // The current version including date
            extrInfo.mappedFileFolder = Path.Combine(Path.Combine(extrInfo.extractionTopFolder, splcIndexVersion), extrInfo.ExtractedFileFolderName);
            return splcIndexVersion;
        }

        private bool AssertBowtieOutputFile(string bowtieIndex, string inputFqReadPath, string outputPath,
                                   string outputFqUnmappedReadPath, string bowtieLogFile)
        {
            if (!File.Exists(outputPath))
            {
                int nThreads = props.NumberOfAlignmentThreadsDefault;
                string threadArg = (nThreads == 1) ? "" : ("-p " + nThreads.ToString());
                string unmappedArg = (outputFqUnmappedReadPath != "") ? (" --un " + outputFqUnmappedReadPath) : "";
                string bowtieOptions = props.BowtieMultiOptions.Replace("<BowtieMaxNumAltMappings>", props.BowtieMaxNumAltMappings.ToString());
                string arguments = String.Format("{0} {1} {2} {3} \"{4}\" \"{5}\"", bowtieOptions, threadArg,
                                                  unmappedArg, bowtieIndex, inputFqReadPath, outputPath);
                CmdCaller cc = new CmdCaller("bowtie", arguments);
                StreamWriter logWriter = new StreamWriter(bowtieLogFile, true);
                logWriter.WriteLine("--- " + bowtieIndex + " on " + inputFqReadPath + " ---");
                logWriter.WriteLine(cc.StdError);
                logWriter.Close();
                if (cc.ExitCode != 0)
                {
                    Console.Error.WriteLine("Failed to run Bowtie on {0}. ExitCode={1}", inputFqReadPath, cc.ExitCode);
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                    return false;
                }
            }
            return true;
        }

        public static readonly string ANNOTATION_VERSION = "33";
        /// <summary>
        /// Annotate output from Bowtie alignment
        /// </summary>
        /// <param name="projectFolderOrName">Either the path to a specific Extracted folder,
        ///                             or the path of the projectFolder, in which case the latest
        ///                             Extracted folder will be processed</param>
        /// <param name="genome">Genome to annotate against</param>
        /// <returns>subpath under ProjectMap to results, or null if no processing was needed</returns>
        public string Annotate(string projectFolderOrName, StrtGenome genome)
		{
            string projectFolder = PathHandler.GetRootedProjectFolder(projectFolderOrName);
            string extractedFolder = PathHandler.GetLatestExtractedFolder(projectFolder);
            List<LaneInfo> laneInfos = SetupLaneInfosFromExistingExtraction(extractedFolder);
            List<string> mapFiles = SetExistingMapFilePaths(genome, laneInfos);
            return AnnotateMapFiles(genome, projectFolder, extractedFolder, mapFiles);
		}

        private static List<string> SetExistingMapFilePaths(StrtGenome genome, List<LaneInfo> laneInfos)
        {
            List<string> mapFiles = new List<string>();
            foreach (LaneInfo info in laneInfos)
            {
                SetMappedFileFolder(genome, info);
                info.mappedFilePaths = Directory.GetFiles(info.mappedFileFolder, "*.map");
                mapFiles.AddRange(info.mappedFilePaths);
            }
            return mapFiles;
        }

        public void Map(string projectFolderOrName, StrtGenome genome)
        {
            string projectFolder = PathHandler.GetRootedProjectFolder(projectFolderOrName);
            string extractedFolder = PathHandler.GetLatestExtractedFolder(projectFolder);
            List<LaneInfo> laneInfos = SetupLaneInfosFromExistingExtraction(extractedFolder);
            CreateBowtieMaps(genome, laneInfos);
        }

        public string MapAndAnnotate(string projectFolderOrName, string speciesArg, bool defaultGeneVariants)
        {
            StrtGenome genome = StrtGenome.GetGenome(speciesArg, defaultGeneVariants);
            string projectFolder = PathHandler.GetRootedProjectFolder(projectFolderOrName);
            string extractedFolder = PathHandler.GetLatestExtractedFolder(projectFolder);
            List<LaneInfo> laneInfos = SetupLaneInfosFromExistingExtraction(extractedFolder);
            CreateBowtieMaps(genome, laneInfos);
            List<string> mapFiles = GetAllMapFilePaths(laneInfos);
            return AnnotateMapFiles(genome, projectFolder, extractedFolder, mapFiles);
        }

        private string AnnotateMapFiles(StrtGenome genome, string projectFolder, string extractedFolder, List<string> mapFiles)
        {
            string barcodeSet = PathHandler.ParseBarcodeSet(extractedFolder);
            SetBarcodeSet(barcodeSet);
            string projectName = Path.GetFileName(projectFolder);
            ResultDescription resultDescr = ProcessAnnotation(barcodeSet, genome, projectFolder, projectName, mapFiles);
            Console.WriteLine("Annotated " + mapFiles.Count + " map files from " + projectName + " to " + resultDescr.bowtieIndexVersion);
            return resultDescr.resultFolder;
        }

        private List<LaneInfo> SetupLaneInfosFromExistingExtraction(string extractedFolder)
        {
            string fqFolder = Path.Combine(extractedFolder, "fq");
            List<LaneInfo> laneInfos = new List<LaneInfo>();
            foreach (string extractedByBcFolder in Directory.GetDirectories(fqFolder))
            {
                Match m = Regex.Match(extractedByBcFolder, "Run([0-9]+)_L([0-9])_[0-9]_[0-9]+");
                LaneInfo laneInfo = new LaneInfo(m.Groups[0].Value, m.Groups[1].Value, m.Groups[2].Value[0]);
                SetExtractedFilesInfo(laneInfo, extractedByBcFolder);
                laneInfo.extractionTopFolder = extractedFolder;
                laneInfos.Add(laneInfo);
            }
            return laneInfos;
        }

        private ResultDescription ProcessAnnotation(string barcodeSet, StrtGenome genome, string projectFolder,
                                                    string projectName, List<string> mapFilePaths)
        {
            if (mapFilePaths.Count == 0)
                return null;
            SetBarcodeSet(barcodeSet);
            string resultSubFolder = projectName + "_" + genome.GetBowtieIndexName() + "_" + DateTime.Now.ToPathSafeString();
            string outputFolder = Path.Combine(projectFolder, resultSubFolder);
            ReadCounter readCounter = new ReadCounter();
            UpdateGenesToPaint(projectFolder, props);
            AbstractGenomeAnnotations annotations = new UCSCGenomeAnnotations(props, genome);
            annotations.Load();
            string outputNamebase = projectName + (barcodes.HasRandomBarcodes ? "MC_" : "");
            string outputPathbase = Path.Combine(outputFolder, outputNamebase);
            TranscriptomeStatistics ts = new TranscriptomeStatistics(annotations, props);
            string syntLevelFile = PathHandler.GetSyntLevelFile(projectFolder);
            if (File.Exists(syntLevelFile))
                ts.TestReporter = new SyntReadReporter(syntLevelFile, genome.GeneVariants, outputPathbase, annotations.geneFeatures);
            int nFile = 1;
            Console.WriteLine("Processing " + mapFilePaths.Count + " map files...");
            mapFilePaths.Sort(); // Important to have them sorted by barcode
            foreach (string mapFile in mapFilePaths)
            {
                Background.Message("File " + (nFile++) + "/" + mapFilePaths.Count);
                int n = ts.AnnotateMapFile(mapFile);
                if (ts.GetNumMappedReads() == 0)
                    Console.WriteLine("WARNING: contigIds of reads do not seem to match with genome Ids.\n" +
                                      "Was the Bowtie index made on a different genome or contig set?");
                Console.WriteLine("Totally {0} reads were annotated: {1} expressed genes and {2} expressed repeat types.",
                                  ts.GetNumMappedReads(), annotations.GetNumExpressedGenes(), annotations.GetNumExpressedRepeats());
                readCounter.AddSummaryTabfile(PathHandler.MakeExtractionSummaryPath(mapFile));
            }
            Directory.CreateDirectory(outputFolder);
            ts.SampleStatistics();
            Console.WriteLine("Saving to {0}...", outputFolder);
            ts.Save(readCounter, outputPathbase);
            string bowtieIndexVersion = PathHandler.GetIndexVersion(genome.GetBowtieSplcIndexName());
            return new ResultDescription(mapFilePaths, bowtieIndexVersion, outputFolder);
        }

        private void UpdateGenesToPaint(string projectFolder, Props props)
        {
            string paintPath = Path.Combine(projectFolder, "genes_to_paint.txt");
            if (File.Exists(paintPath))
            {
                StreamReader reader = paintPath.OpenRead();
                string line = reader.ReadLine().Trim();
                reader.Close();
                string[] genesToPaint = line.Split(',');
                Console.WriteLine(genesToPaint.Length + " genes to paint defined by file " + paintPath);
                for (int i = 0; i < genesToPaint.Length; i++)
                    genesToPaint[i] = genesToPaint[i].Trim();
                props.GenesToPaint = genesToPaint;
            }
        }

        /// <summary>
        /// Load the .wig files, paint the hits onto the chromosomes, assign counts to features & splices,
        /// then save the results in appropriate formats 
        /// </summary>
        /// <param name="wiggleFolder">Folder of .wg.gz files to process</param>
        /// <param name="genome">Genome to annotate against</param>
		public void AnnotateFromWiggles(string wiggleFolder, StrtGenome genome)
		{
            AbstractGenomeAnnotations annotations = new UCSCGenomeAnnotations(props, genome);
            annotations.Load();
            TranscriptomeStatistics ts = new TranscriptomeStatistics(annotations, props);
            ReadCounter readCounter = new ReadCounter();
			Console.WriteLine("Processing wiggle files...");
			int countMappedReads = 0;
            int n = 1;
            MultiReadMappings mappings = new MultiReadMappings(1, barcodes);
			foreach(string file in Directory.GetFiles(wiggleFolder, "*.wig.gz"))
			{
                readCounter.AddReadFilename(file);
				Console.WriteLine("Processing " + file);
				var wiggle = file.OpenRead();
				wiggle.ReadLine(); // Skip header
				while(true)
				{
					string line = wiggle.ReadLine();
					if(line == null) break;
					string[] fields = line.Split('\t');
					string chr = fields[0].Substring(3);
					int start = int.Parse(fields[1]);
                    if (start < 1) continue;
					int end = int.Parse(fields[2]);
					int height = int.Parse(fields[3]);
					char strand = file.Contains("fw") ? '+' : '-';
                    mappings.InitSingleMapping("Line" + n, chr, strand, start, end - start, 0, "");
                    for (int i = 0; i < height; i++)
                        ts.Add(mappings);
                    n++;
					countMappedReads += (end - start) * height / 25; // assuming 25 bp read length on average
				}
				wiggle.Close();
				Console.WriteLine("Found approximately {0} mapped reads", countMappedReads);
                readCounter.Add(ReadStatus.VALID, countMappedReads);
				Console.WriteLine("Found {0} distinct expressed features", annotations.GetNumExpressedGenes());
			}
			Console.WriteLine();
			ts.Save(readCounter, Path.Combine(wiggleFolder, DateTime.Now.ToPathSafeString()));
		}

        /// <summary>
        /// Extracts reads from a FASTA-formatted file to a Fasta file in a STRT project folder.
        /// Removes too short reads and barcodes+GGG if a barcode set is defined.
        /// </summary>
        /// <param name="fastaFile">Path to input file</param>
        /// <param name="projectFolder"></param>
        /// <param name="minReadLength">Minimum read length excluding barcode+GGG</param>
        /// <param name="maxReadLength">Longer sequences are trunctated at this length</param>
        public void ConvertToReads(string fastaFile, string projectFolder,
                                   int minReadLength, int maxReadLength)
        {
            string readsFolder = PathHandler.GetReadsFolder(projectFolder);
            Directory.CreateDirectory(readsFolder);
            string readsFile = Path.GetFileNameWithoutExtension(fastaFile) + "_trimmed.fasta";
            string outFile = Path.Combine(readsFolder, readsFile);
            StreamWriter writer = outFile.OpenWrite();
            string[] barcodesGGG = barcodes.GetBarcodesWithTSSeq();
            int nTot = 0, nBarcoded = 0, nTooShort = 0;
            foreach (FastaRecord rec in FastaFile.Stream(fastaFile))
            {
                nTot++;
                foreach (string bcGGG in barcodesGGG)
                {
                    string seq = rec.Sequence.ToString();
                    int pos = seq.IndexOf(bcGGG);
                    if (pos >= 0)
                    {
                        if (seq.Length - pos >= minReadLength)
                        {
                            int seqLen = Math.Min(maxReadLength, seq.Length - pos);
                            writer.WriteLine(">" + rec.HeaderLine + "\n" + seq.Substring(pos, seqLen));
                            nBarcoded++;
                        }
                        else
                            nTooShort++;
                        break;
                    }
                }
            }
            writer.Close();
            Console.WriteLine("{0} sequences scanned, {1} were barcoded, {2} had no barcoded, {3} were too short",
                              nTot, nBarcoded, (nTot - nBarcoded - nTooShort), nTooShort);
            Console.WriteLine("Output file ready for extraction is in " + outFile);
        }

        /// <summary>
        /// Generates synthetic transcript read data as a FASTA file for testing of the analysis pipeline.
        /// Will avoid to pick polyA sequences that will not be accepted by the extraction step.
        /// Generates random mutations depending of the Props settings.
        /// </summary>
        /// <param name="genome">Genome to pick reads from</param>
        /// <param name="outputFolder">Name of file to write Fasta-formatted data to</param>
        /// <param name="readLengthSamples">Array of read lengths to randomly sample from (lengths excluding barcode+GGG)</param>
        /// <param name="readSpacing">Distance between sampled positions in transcripts</param>
        public void SynthetizeReads(StrtGenome genome, string projectFolder)
        {
            int[] readLengthSamples = new int[10] {50, 50, 50, 50, 50, 50, 49, 48, 47, 46 };
            int maxExprLevel = 10000;
            double exprLevelTiltPower = 7.0;
            double hotspotProb = 0.1;
            double trPosTiltPower = 8.0;
            double backgroundFreq = props.SyntheticReadsBackgroundFreq;
            double randomMutationProb = props.SyntheticReadsRandomMutationProb;
            bool variantGenes = genome.GeneVariants;
            Background.Progress(0);
            int maxReadLength = DescriptiveStatistics.Max(readLengthSamples);
            string[] barcodesGGG = barcodes.GetBarcodesWithTSSeq();
            PathHandler ph = new PathHandler(props);
            string annotationsPath = ph.GetAnnotationsPath(genome);
            annotationsPath = PathHandler.ExistsOrGz(annotationsPath);
            if (annotationsPath == null)
                throw new NoAnnotationsFileFoundException("Could not find annotation file: " + annotationsPath);
            Console.WriteLine("Annotations are taken from " + annotationsPath);
            Dictionary<string, string> chrIdToFileMap = ph.GetGenomeFilesMap(genome);
            Dictionary<string, List<LocusFeature>> chrIdToFeature = new Dictionary<string, List<LocusFeature>>();
            foreach (string chrId in chrIdToFileMap.Keys)
            {
                if (StrtGenome.IsSpliceAnnotationChr(chrId)) continue;
                chrIdToFeature[chrId] = new List<LocusFeature>();
            }
            Background.Message("Reading annotations...");
            foreach (LocusFeature gf in UCSCAnnotationReader.IterAnnotationFile(annotationsPath))
                if (chrIdToFeature.ContainsKey(gf.Chr))
                    chrIdToFeature[gf.Chr].Add(gf);
            Random rnd = new Random();
            int readNumber = 1;
            projectFolder = PathHandler.GetRootedProjectFolder(projectFolder);
            string readsFolder = Path.Combine(projectFolder, "Reads");
            if (!Directory.Exists(readsFolder))
                Directory.CreateDirectory(readsFolder);
            string fastaOutput = Path.Combine(readsFolder, "Run00000_L0_1_" + props.TestAnalysisFileMarker + ".fasta");
            string reportOutput = PathHandler.GetSyntLevelFile(projectFolder);
            StreamWriter fastaWriter = fastaOutput.OpenWrite();
            StreamWriter reportWriter = reportOutput.OpenWrite();
            reportWriter.WriteLine("Synthetic data - parameters:\nBarcodeSet\t{0}\nGenome\t{1}\nMutationProb\t{2}",
                                   barcodes.Name, genome.GetBowtieIndexName(), randomMutationProb);
            reportWriter.WriteLine("MaxExprLevel\t{0}\nHotspotProb\t{1}\nBackgroundFreq\t{2}\n\nGeneFeature\tExprLevel",
                                   maxExprLevel, hotspotProb, backgroundFreq);
            int nTrSeqs = 0; int nBkgSeqs = 0; int chrIdx = 1;
            foreach (string chrId in chrIdToFeature.Keys)
            {
                Background.Message("Processing chromosome " + chrId + "...");
                Background.Progress((int)(chrIdx / chrIdToFeature.Keys.Count));
                Console.Write(chrId + "."); Console.Out.Flush();
                DnaSequence chrSeq = AbstractGenomeAnnotations.readChromosomeFile(chrIdToFileMap[chrId]);
                int nChrBkgSeqs = rnd.Next((int)(chrSeq.Count * backgroundFreq));
                nBkgSeqs += nChrBkgSeqs;
                for (; nChrBkgSeqs >= 0; nChrBkgSeqs--)
                {
                    string extraGs = new String('G', Math.Max(0, rnd.Next(11) - 7));
                    int bkgPos = rnd.Next((int)chrSeq.Count - maxReadLength);
                    int seqPartLen = 1 + readLengthSamples[rnd.Next(readLengthSamples.Length)] - extraGs.Length;
                    while (chrSeq.CountCases('N', bkgPos, bkgPos + seqPartLen) > seqPartLen / 2)
                        bkgPos = rnd.Next((int)chrSeq.Count - maxReadLength);
                    DnaSequence bkgSeq = chrSeq.SubSequence(bkgPos, seqPartLen);
                    char bkgStrand = (rnd.NextDouble() < 0.5)? '+' : '-';
                    if (bkgStrand == '-') bkgSeq.RevComp();
                    string bkgReadSeq = bkgSeq.ToString().Replace("-", "N");
                    string bkgMutations = Mutate(randomMutationProb, rnd, ref bkgReadSeq);
                    string hdr = string.Format("Synt:BKG:{0}{1}:0:{2}{3}.{4}", chrId, bkgStrand, bkgPos, bkgMutations, readNumber++);
                    string bcSeq = barcodesGGG[rnd.Next(barcodesGGG.Length)] + extraGs;
                    fastaWriter.WriteLine(">" + hdr + "\n" + bcSeq + bkgReadSeq);
                }
                foreach (LocusFeature f in chrIdToFeature[chrId])
                {
                    GeneFeature gf = (GeneFeature)f;
                    if (!variantGenes && gf.IsVariant())
                        continue;
                    if (gf.Length > props.MaxFeatureLength) continue;
                    DnaSequence gfTrFwSeq = new LongDnaSequence(gf.Length);
                    for (int exonIdx = 0; exonIdx < gf.ExonCount; exonIdx++)
                    {
                        int exonLen = 1 + gf.ExonEnds[exonIdx] - gf.ExonStarts[exonIdx];
                        gfTrFwSeq.Append(chrSeq.SubSequence(gf.ExonStarts[exonIdx], exonLen));
                    }
                    if (gf.Strand == '-')
                        gfTrFwSeq.RevComp();
                    int maxPos = (int)gfTrFwSeq.Count - maxReadLength;
                    if (maxPos < 1) continue;
                    int targetExprLevel = (int)(Math.Pow(rnd.NextDouble(), exprLevelTiltPower) * maxExprLevel);
                    int actualExprLevel = 0;
                    foreach (int trPos in SyntReadPositions(targetExprLevel, trPosTiltPower, hotspotProb, maxPos))
                    {
                        string extraGs = new String('G', Math.Max(0, rnd.Next(11) - 7));
                        int readLen = 1 + readLengthSamples[rnd.Next(readLengthSamples.Length)] - extraGs.Length;
                        string exonReadSeq = gfTrFwSeq.SubSequence(trPos, readLen).ToString().Replace("-", "N");
                        int nNonAs = 0;
                        foreach (char rc in exonReadSeq)
                            if (!"AaNn".Contains(rc)) nNonAs++;
                        if (nNonAs <= props.MinExtractionInsertNonAs)
                            continue;
                        string mutations = Mutate(randomMutationProb, rnd, ref exonReadSeq);
                        string hdr = string.Format("Synt:{0}:{1}{2}:{3}:{4}{5}.{6}", gf.Name, 
                                                    gf.Chr, gf.Strand, gf.Start, trPos, mutations, readNumber++);
                        string bcSeq = barcodesGGG[rnd.Next(barcodesGGG.Length)] + extraGs;
                        fastaWriter.WriteLine(">" + hdr + "\n" + bcSeq + exonReadSeq);
                        actualExprLevel++;
                        nTrSeqs++;
                    }
                    reportWriter.WriteLine(gf.Name + "\t" + actualExprLevel);
                }
            }
            Console.WriteLine("\nWrote " + nTrSeqs + " expressed gene and " + nBkgSeqs + " background reads to fasta file " + fastaOutput);
            Console.WriteLine("Wrote parameters and levels to " + reportOutput);
            fastaWriter.Close();
            reportWriter.Close();
            Background.Progress(100);
        }

        private static string Mutate(double randomMutationProb, Random rnd, ref string readSeq)
        {
            string mutations = "";
            while (rnd.NextDouble() < randomMutationProb)
            {
                int mPos = rnd.Next(readSeq.Length);
                int i = "ACGT".IndexOf(readSeq[mPos]);
                char newNt = "ACGT"[(i + 1 + rnd.Next(3)) % 4];
                mutations += ":" + mPos + readSeq[mPos] + ">" + newNt;
                readSeq = readSeq.Substring(0, mPos) + newNt + readSeq.Substring(mPos + 1);
            }
            return mutations;
        }

        private IEnumerable<int> SyntReadPositions(int exprLevel, double tiltPower, double hotspotProb, int maxPos)
        {
            Random rnd = new Random();
            int n = exprLevel;
            while (n > 1 && rnd.NextDouble() < hotspotProb)
            {
                int hotspotPos = rnd.Next(maxPos);
                int hotspotCount = rnd.Next(n / 5);
                for (int i = 0; i < hotspotCount; i++)
                    yield return hotspotPos;
                n -= hotspotCount;
            }
            for (; n >= 0; n--)
            {
                double relPos = Math.Pow(rnd.NextDouble(), tiltPower);
                int pos = (int)(maxPos * relPos);
                yield return pos;
            }
        }

        private class ReadFrag
        {
            public int Pos { get; set; }
            public DnaSequence Seq { get; set; }
            public int Length { get { return (int)Seq.Count; } }
            public List<int> ExonIds { get; set; }
            public ReadFrag(int pos, DnaSequence seq, List<int> exonIds)
            {
                Pos = pos;
                Seq = seq;
                ExonIds = exonIds;
            }
        }
        private void MakeReadFragContinuations(int nLeft, DnaSequence accseq, List<int> exonIds, int exonIdx, 
                               List<ReadFrag> results, int currentPos, bool splices, List<DnaSequence> exons, int maxSkip, int minOverhang)
        {
            int imax = splices? Math.Min(exonIdx + 1 + maxSkip, exons.Count) : exonIdx + 1;
            for (int i = exonIdx; i < imax; i++)
            {
                if (i > exonIdx && (nLeft < minOverhang || accseq.Count < minOverhang)) continue; // Avoid splices where the remaining bases are very few
                int take = Math.Min(nLeft, (int)exons[i].Count);
                ShortDnaSequence seq = new ShortDnaSequence(accseq);
                seq.Append(exons[i].SubSequence(0, take));
                List<int> nextExons = new List<int>(exonIds);
                nextExons.Add(i);
                if (nLeft - take == 0)
                    results.Add(new ReadFrag(currentPos, seq, nextExons));
                else
                    MakeReadFragContinuations(nLeft - take, seq, nextExons, i + 1, results, currentPos, splices, exons, maxSkip, minOverhang);
            }
        }
        private void MakeAllReadFrags(int readLen, bool makeSplices, int maxSkip, int minOverhang, List<DnaSequence> exons,
                                      out List<ReadFrag> results)
        {
            results = new List<ReadFrag>();
            int totLen = 0;
            foreach (ShortDnaSequence s in exons)
                totLen += (int)s.Count;
            int sIdx = 0;
            int exPos = 0;
            int exLeft = (int)exons[sIdx].Count;
            for (int trPosInChrDir = 0; trPosInChrDir < totLen - readLen; trPosInChrDir++)
            {
                int take = Math.Min(readLen, exLeft);
                DnaSequence seq = exons[sIdx].SubSequence(exPos, take);
                int nLeft = readLen - take;
                List<int> exonIds = new List<int>();
                exonIds.Add(sIdx);
                if (nLeft == 0)
                    results.Add(new ReadFrag(trPosInChrDir, seq, exonIds));
                else
                    MakeReadFragContinuations(nLeft, seq, exonIds, sIdx + 1, results, trPosInChrDir, makeSplices, exons, maxSkip, minOverhang);
                exLeft--;
                exPos++;
                if (exLeft == 0)
                {
                    sIdx++;
                    exPos = 0;
                    exLeft = (int)exons[sIdx].Count;
                }
            }
        }

        /// <summary>
        /// If readLength== 0, dumps the whole sequence for each gene, otherwise dumps
        /// all possible subsequences of readLength from each gene
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="readLength"></param>
        public void DumpTranscripts(StrtGenome genome, int readLength, int step, int maxPerGene, string fqOutput,
                                    bool makeSplices, int minOverhang, int maxSkip)
        {
            bool variantGenes = genome.GeneVariants;
            PathHandler ph = new PathHandler(props);
            string annotationsPath = ph.GetAnnotationsPath(genome);
            annotationsPath = PathHandler.ExistsOrGz(annotationsPath);
            if (annotationsPath == null)
                throw new NoAnnotationsFileFoundException("Could not find annotation file: " + annotationsPath);
            Console.WriteLine("Annotations are taken from " + annotationsPath);
            if (makeSplices)
                Console.WriteLine("Making all splices that have >= " + minOverhang + " bases overhang and max " + maxSkip + " exons excised.");
            Dictionary<string, string> chrIdToFileMap = ph.GetGenomeFilesMap(genome);
            Dictionary<string, List<LocusFeature>> chrIdToFeature = new Dictionary<string, List<LocusFeature>>();
            foreach (string chrId in chrIdToFileMap.Keys)
            {
                if (StrtGenome.IsSpliceAnnotationChr(chrId)) continue;
                chrIdToFeature[chrId] = new List<LocusFeature>();
            }
            GeneFeature.SpliceFlankLen = props.SpliceFlankLength;
            foreach (LocusFeature gf in UCSCAnnotationReader.IterAnnotationFile(annotationsPath))
                if (chrIdToFeature.ContainsKey(gf.Chr))
                    chrIdToFeature[gf.Chr].Add(gf);
            StreamWriter fastaWriter = fqOutput.OpenWrite();
            StreamWriter spliceWriter = null;
            string spliceOutput = fqOutput.Replace(".fq", "") + "_splices_only.fq";
            if (makeSplices)
                spliceWriter = spliceOutput.OpenWrite();
            int nTrSeqs = 0, nSplSeq = 0;
            foreach (string chrId in chrIdToFeature.Keys)
            {
                Console.Write(chrId + "."); Console.Out.Flush();
                DnaSequence chrSeq = AbstractGenomeAnnotations.readChromosomeFile(chrIdToFileMap[chrId]);
                foreach (LocusFeature f in chrIdToFeature[chrId])
                {
                    GeneFeature gf = (GeneFeature)f;
                    if (!variantGenes && gf.IsVariant())
                        continue;
                    List<DnaSequence> exonSeqsInChrDir = new List<DnaSequence>(gf.ExonCount);
                    int trLen = 0;
                    for (int exonIdx = 0; exonIdx < gf.ExonCount; exonIdx++)
                    {
                        int exonLen = 1 + gf.ExonEnds[exonIdx] - gf.ExonStarts[exonIdx];
                        trLen += exonLen;
                        exonSeqsInChrDir.Add(chrSeq.SubSequence(gf.ExonStarts[exonIdx], exonLen));
                    }
                    if (readLength == 0)
                    {
                        DnaSequence gfTrFwSeq = new ShortDnaSequence(gf.Length);
                        foreach (DnaSequence s in exonSeqsInChrDir)
                            gfTrFwSeq.Append(s);
                        if (gf.Strand == '-')
                            gfTrFwSeq.RevComp();
                        fastaWriter.WriteLine("@Gene=" + gf.Name + ":Chr=" + gf.Chr + gf.Strand + ":Pos=" + gf.Start);
                        fastaWriter.WriteLine(gfTrFwSeq);
                        fastaWriter.WriteLine("+\n" + new String('b', (int)gfTrFwSeq.Count));
                    }
                    else
                    {
                        int n = 0;
                        List<ReadFrag> readFrags = new List<ReadFrag>();
                        MakeAllReadFrags(readLength, makeSplices, maxSkip, minOverhang, exonSeqsInChrDir, out readFrags);
                        foreach (ReadFrag frag in readFrags)
                        {
                            string exonNos = string.Join("-", frag.ExonIds.ConvertAll(v => v.ToString()).ToArray());
                            int posInTrFw = (gf.Strand == '+') ? frag.Pos : (trLen - frag.Pos - (int)frag.Length);
                            int posInChr = (gf.Strand == '+') ? gf.GetChrPos(frag.Pos) : gf.GetChrPos(trLen - 1 - frag.Pos);
                            if (gf.Strand == '-')
                                frag.Seq.RevComp();
                            string outBlock = "@Gene=" + gf.Name + ":Chr=" + gf.Chr + gf.Strand + ":Pos=" + posInChr +
                                                  ":TrPos=" + posInTrFw + ":Exon=" + exonNos + "\n" +
                                               frag.Seq.ToString() + "\n" +
                                               "+\n" + new String('b', readLength);
                            fastaWriter.WriteLine(outBlock);
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
            }
            Console.WriteLine("\nWrote " + nTrSeqs + " sequences to " + fqOutput);
            if (spliceWriter != null)
            {
                spliceWriter.Close();
                Console.WriteLine("\nAlso wrote the " + nSplSeq + " splice spanning sequences to " + spliceOutput);
            }
            fastaWriter.Close();
        }

        private class HitMapping
        {
            private static Dictionary<string, int> geneIds = new Dictionary<string, int>();
            private static List<string> geneNames = new List<string>();
            private int geneId;
            private int typeAndPos;
            private int exonIdCode;
            public HitMapping(string geneName, int annotType, int trPos, List<int> exonIds)
            {
                if (!geneIds.TryGetValue(geneName, out geneId))
                {
                    geneId = geneNames.Count;
                    geneIds[geneName] = geneId;
                    geneNames.Add(geneName);
                }
                typeAndPos = (annotType << 26) + trPos;
                foreach (int exonId in exonIds)
                    exonIdCode = (exonIdCode << 8) | exonId;
            }
            public override string ToString()
            {
                int code = exonIdCode;
                string exonIdString = (code & 255).ToString();
                while (code > 0)
                {
                    exonIdString += "-" + (code & 255).ToString();
                    code >>= 8;
                }
                return geneNames[geneId] + "," + (typeAndPos & 0xffffff) + "," + AnnotType.GetName(typeAndPos >> 26) + "," + exonIdString;
            }
        }
        public void ParseReadMapFile(string mapFile, string outputFile)
        {
            Dictionary<string, long> chrToCode = new Dictionary<string, long>();
            Dictionary<long, string> codeToChr = new Dictionary<long, string>();
            SortedDictionary<long, List<HitMapping>> hitMappings = new SortedDictionary<long, List<HitMapping>>();
            string line;
            int n = 0;
            using (StreamReader reader = new StreamReader(mapFile))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    n++;
                    if (n % 1000000 == 0)
                        Console.WriteLine(n);
                    string[] fields = line.Split('\t');
                    string hitChr = fields[2];
                    char strand = fields[1][0];
                    long hitPos = int.Parse(fields[3]);
                    Match m = Regex.Match(fields[0], "Gene=(.+):Chr=(.+)([-+]):Pos=([0-9]+):TrPos=([0-9]+):Exon=(.+)");
                    string realGeneName = m.Groups[1].Value;
                    string realChr = m.Groups[2].Value;
                    char realStrand = m.Groups[3].Value[0];
                    int realChrPos = int.Parse(m.Groups[4].Value);
                    int realTrPos = int.Parse(m.Groups[5].Value);
                    string exonIdString = m.Groups[6].Value;
                    List<int> exonIds = new List<int>();
                    foreach (string x in exonIdString.Split('-'))
                        exonIds.Add(int.Parse(x) + 1); // Change exon index to 1-based.
                    if (!chrToCode.ContainsKey(hitChr))
                    {
                        codeToChr[chrToCode.Count] = hitChr;
                        chrToCode[hitChr] = chrToCode.Count << 54;
                    }
                    long codedHitPos = chrToCode[hitChr] | hitPos;
                    int annotType = (exonIds.Count > 1) ? AnnotType.SPLC : AnnotType.EXON;
                    if (strand == '-') annotType = AnnotType.MakeAntisense(annotType);
                    HitMapping hm = new HitMapping(realGeneName, annotType, realTrPos, exonIds);
                    if (!hitMappings.ContainsKey(codedHitPos))
                        hitMappings[codedHitPos] = new List<HitMapping>();
                    hitMappings[codedHitPos].Add(hm);
                }
            }
            Console.WriteLine("Read " + n + " lines from " + mapFile);
            int l = 0;
            using (StreamWriter writer = new StreamWriter(outputFile))
            {
                foreach (KeyValuePair<long, List<HitMapping>> item in hitMappings)
                {
                    l++;
                    string chr = codeToChr[item.Key >> 54];
                    long hitPos = item.Key & 0xfffffffffffff;
                    writer.Write(chr + "\t" + hitPos);
                    foreach (HitMapping m in item.Value)
                        writer.Write("\t" + m.ToString());
                    writer.WriteLine();
                }
            }
            Console.WriteLine("Wrote " + l + "mapping lines to " + outputFile);
        }
    }
}
