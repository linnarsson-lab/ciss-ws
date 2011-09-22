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
            DateTime startTime = DateTime.Now;
            Console.WriteLine("*** Build of spliced exon junctions for {0} started at {1} ***", genome.GetBowtieIndexName(), DateTime.Now);
            AnnotationBuilder builder = AnnotationBuilder.GetAnnotationBuilder(props, genome);
            builder.BuildExonSplices(genome);
            Console.WriteLine("*** Splice build completed at {0} ***", DateTime.Now);
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
            Background.Message("Building junctions");
            Background.Progress(0);
            BuildJunctions(genome);
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
		public void Extract(string project, List<string> laneArgs)
		{
            project = PathHandler.GetRootedProjectFolder(project);
            List<ExtractionInfo> extrInfos = new List<ExtractionInfo>();
            if (laneArgs.Count > 0)
                extrInfos = PathHandler.ListReadsFiles(laneArgs);
            else
                foreach (string extractedFile in PathHandler.CollectReadsFilesNames(project))
                    extrInfos.Add(new ExtractionInfo(extractedFile, "X", 'x'));
            string outputFolder = pathHandler.MakeExtractedFolder(project, barcodes.Name, EXTRACTION_VERSION);
            Extract(extrInfos, outputFolder);
        }

        public void Extract(ProjectDescription pd)
        {
            List<ExtractionInfo> extrInfos = PathHandler.ListReadsFiles(pd.runIdsLanes.ToList());
            pd.extractionVersion = EXTRACTION_VERSION;
            string outputFolder = pathHandler.MakeExtractedFolder(pd.ProjectFolder, barcodes.Name, EXTRACTION_VERSION);
            Extract(extrInfos, outputFolder);
        }

        public static readonly string EXTRACTION_VERSION = "25";
        private void Extract(List<ExtractionInfo> extrInfos, string outputFolder)
        {
            DateTime start = DateTime.Now;
            ReadExtractor readExtractor = new ReadExtractor(props);
            Background.Progress(0);
            Background.Message("Extracting...");
            Directory.CreateDirectory(outputFolder);
            List<string> outputFiles = new List<string>();
            foreach (ExtractionInfo extrInfo in extrInfos)
			{
                string readFile = extrInfo.readFilePath;
                ReadCounter readCounter = new ReadCounter();
                string bcOutputPath = PathHandler.GetBarcodedReadsPath(outputFolder, readFile, EXTRACTION_VERSION);
                string summaryOutputPath = PathHandler.GetExtractionSummaryPath(outputFolder, readFile, EXTRACTION_VERSION);
                extrInfo.extractedFilePath = bcOutputPath;
                if (File.Exists(bcOutputPath))
                    continue;
                readCounter.AddReadFilename(readFile);
                ExtractionWordCounter wordCounter = new ExtractionWordCounter(props.ExtractionCounterWordLength);
				StreamWriter sw_barcoded = new StreamWriter(bcOutputPath);
                string slaskFile = PathHandler.GetSlaskReadsPath(outputFolder, readFile, EXTRACTION_VERSION);
				StreamWriter sw_slask = slaskFile.OpenWrite();
                ExtractionQuality extrQ = (props.AnalyzeExtractionQualities)? new ExtractionQuality(props.LargestPossibleReadLength) : null;
				foreach (FastQRecord fastQRecord in FastQFile.Stream(readFile, props.QualityScoreBase))
				{
                    FastQRecord rec = fastQRecord;
                    if (extrQ != null) extrQ.Add(rec);
                    wordCounter.AddRead(rec.Sequence);
                    int readStatus = readExtractor.Extract(ref rec);
                    readCounter.Add(readStatus);
                    if (readStatus == ReadStatus.VALID) sw_barcoded.WriteLine(rec.ToString(props.QualityScoreBase));
                    else sw_slask.WriteLine(rec.ToString(props.QualityScoreBase));
				}
				sw_barcoded.Close();
				sw_slask.Close();
                StreamWriter sw_summary = summaryOutputPath.OpenWrite();
                sw_summary.WriteLine(readCounter.TotalsToTabString());
                sw_summary.WriteLine("\nBelow are the most common words among all reads.\n");
                sw_summary.WriteLine(wordCounter.GroupsToString(200));
                sw_summary.Close();
                if (extrQ != null)
                {
                    string fHead = PathHandler.CreateExtractedFileHead(outputFolder, readFile, EXTRACTION_VERSION);
                    extrQ.Write(readFile, fHead);
                }
                extrInfo.nReads = readCounter.PartialTotal;
                extrInfo.nPFReads = readCounter.PartialCount(ReadStatus.VALID);
                if (Background.CancellationPending) break;
            }
            Background.Progress(100);
			Background.Message("Ready");
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
            string sampleLayoutPath = PathHandler.GetSampleLayoutPath(projectFolder);
            string[] speciesArgs = GetSpeciesArgs(sampleLayoutPath, defaultSpeciesArg);
            List<string> resultSubFolders = new List<string>();
            foreach (string speciesArg in speciesArgs)
            {
                string resultSubFolder = MapAndAnnotate(projectFolder, speciesArg, analyzeAllGeneVariants);
                if (resultSubFolder != null) resultSubFolders.Add(resultSubFolder);
            }
            return resultSubFolders;
        }

        private string[] GetSpeciesArgs(string sampleLayoutPath, string defaultSpeciesArg)
        {
            string[] speciesArgs = new string[] { defaultSpeciesArg };
            if (File.Exists(sampleLayoutPath))
            {
                PlateLayout sampleLayout = new PlateLayout(sampleLayoutPath);
                barcodes.SetSampleLayout(sampleLayout);
                speciesArgs = sampleLayout.GetSpeciesAbbrevs();
            }
            return speciesArgs;
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
            string sampleLayoutPath = Path.Combine(projDescr.ProjectFolder, projDescr.layoutFile);
            string[] speciesArgs = GetSpeciesArgs(sampleLayoutPath, projDescr.defaultSpecies);
            projDescr.annotationVersion = ANNOTATION_VERSION;
            foreach (string speciesArg in speciesArgs)
            {
                StrtGenome genome = StrtGenome.GetGenome(speciesArg, projDescr.analyzeVariants, projDescr.defaultBuild);
                string bowtieIndexName = genome.GetBowtieIndexName();
                if (bowtieIndexName == "" && genome.Annotation != "UCSC")
                {
                    genome.Annotation = "UCSC";
                    Console.WriteLine("Could not find a Bowtie index " + bowtieIndexName + 
                                        " - trying UCSC instead for " + projDescr.projectName);
                    bowtieIndexName = genome.GetBowtieIndexName();
                }
                string bowtieIndexVersion = PathHandler.GetIndexVersion(bowtieIndexName);
                List<string> mapFiles = CreateBowtieMaps(bowtieIndexName, projDescr.GetExtractedFiles());
                string resultSubFolder = projDescr.projectName + "_" + genome.GetBowtieIndexName() + "_" + DateTime.Now.ToPathSafeString();
                string resultPath = Path.Combine(projDescr.ProjectFolder, resultSubFolder);
                ProcessAnnotation(projDescr.barcodeSet, genome, projDescr.ProjectFolder, projDescr.projectName, mapFiles, resultPath);
                ResultDescription resultDescr = new ResultDescription(mapFiles, bowtieIndexVersion, resultPath);
                projDescr.resultDescriptions.Add(resultDescr);
                System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(projDescr.GetType());
                StreamWriter writer = new StreamWriter(Path.Combine(resultPath, "config.xml"));
                x.Serialize(writer, projDescr);
                writer.Close();
            }
        }

        public string MapAndAnnotate(string projectFolder, string speciesArg, bool defaultGeneVariants)
        {
            StrtGenome genome = StrtGenome.GetGenome(speciesArg, defaultGeneVariants);
            string bowtieIndexName = genome.GetBowtieIndexName();
            RunBowtie(bowtieIndexName, projectFolder);
            return AnnotateFromBowtie(projectFolder, genome);
        }

        /// <summary>
        /// Default method to process a set of Extracted barcoded.fq files with Bowtie. 
        /// </summary>
        /// <param name="bowtieIndexName">The BowtieIndex to use, typically "mm9_aVEGA" or "hg37_sUCSC"</param>
        /// <param name="projectFolderOrName">Either a path to/name of a project folder in which
        ///                             the latest Extracted folder will be processed,
        ///                             or the path to a specific Extracted folder to process, 
        ///                             or the path to a specific barcoded.fq file to process.</param>
        public void RunBowtie(string bowtieIndexName, string projectFolderOrName)
        {
            string extractedFolder;
            string[] extractedFiles;
            string projectFolder = PathHandler.GetRootedProjectFolder(projectFolderOrName);
            if (projectFolder.EndsWith("_" + PathHandler.BarcodedFileEnding))
            {
                extractedFolder = Path.GetDirectoryName(projectFolder);
                extractedFiles = new string[] { projectFolder };
            }
            else
            {
                extractedFolder = PathHandler.GetLatestExtractedFolder(projectFolder);
                extractedFiles = Directory.GetFiles(extractedFolder, "*" + PathHandler.BarcodedFileEnding);
            }
            string existingIndexVersion = GetExistingAlignmentFilesVersion(extractedFolder, extractedFiles, bowtieIndexName, barcodes.HasRandomBarcodes);
            if (existingIndexVersion == "")
            {
                ClearExistingBowtieMaps(extractedFolder, bowtieIndexName);
                CreateBowtieMaps(bowtieIndexName, extractedFiles);
            }
        }

        private void ClearExistingBowtieMaps(string extractedFolder, string bowtieIndexNameOrVersion)
        {
            foreach (string mapFile in PathHandler.FindBowtieMapFiles(extractedFolder, bowtieIndexNameOrVersion))
                File.Delete(mapFile);
            //foreach (string bamFile in PathHandler.FindBowtieOutputFiles(extractedFolder, bowtieIndexNameOrVersion, true))
            //    File.Delete(bamFile);
        }

        /// <summary>
        /// Generates Bowtie .map files from input file using the latest dated version of the
        /// given index name. If some of the .map files exist, these are not re-generated.
        /// </summary>
        /// <param name="bowtieIndexName">Bowtie index (e.g. "mm9_sUCSC")</param>
        /// <param name="extractedFiles">Paths to extraction output files to map to genome</param>
        /// <returns>Paths to output files, some of which may have existed already and hence not re-generated</returns>
        private List<string> CreateBowtieMaps(string bowtieIndexName, string[] extractedFiles)
        {
            int nThreads = props.NumberOfAlignmentThreadsDefault;
            string threadArg = (nThreads == 1) ? "" : ("-p " + nThreads.ToString());
            string cmd = "bowtie";
            Background.Progress(0);
            Background.Message("Running Bowtie...");
            int n = 0;
            List<string> mapFiles = new List<string>();
            string indexVersion = PathHandler.GetIndexVersion(bowtieIndexName); // The current version including date
            foreach (string bcFile in extractedFiles)
            {
                string outputPath = PathHandler.CombineToMapFilename(bcFile, indexVersion, ".map");
                mapFiles.Add(outputPath);
                if (File.Exists(outputPath))
                    continue;
                string bowtieOptions = props.BowtieMultiOptions.Replace("<BowtieMaxNumAltMappings>", props.BowtieMaxNumAltMappings.ToString());
                string arguments = String.Format("{0} {1} {2} \"{3}\" \"{4}\"", bowtieOptions, threadArg, bowtieIndexName, bcFile, outputPath);
                Console.WriteLine(cmd + " " + arguments);
                int exitCode = CmdCaller.Run(cmd, arguments);
                if (exitCode != 0)
                {
                    Console.Error.WriteLine("Failed to run Bowtie on {0}. ExitCode={1}", bcFile, exitCode);
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                }
                Background.Progress((int)(++n / extractedFiles.Length));
                if (Background.CancellationPending) break;
            }
            Background.Progress(100);
            Background.Message("Ready");
            return mapFiles;
        }

        private static string GetExistingAlignmentFilesVersion(string extractedFolder, string[] barcodedFiles, string buildName,
                                                         bool useBamFiles)
        {
            List<string> existingMapFiles = PathHandler.FindBowtieOutputFiles(extractedFolder, buildName, useBamFiles);
            string indexVersion = PathHandler.GetIndexVersion(buildName);
            foreach (string bcFile in barcodedFiles)
            {
                bool mapExists = false;
                foreach (string mapFile in existingMapFiles)
                {
                    if (mapFile.StartsWith(PathHandler.CombineToMapFilename(bcFile, indexVersion, "")))
                    {
                        mapExists = true;
                        indexVersion = PathHandler.ExtractIndexVersion(mapFile);
                        break;
                    }
                }
                if (!mapExists) return "";
            }
            return indexVersion;
        }

        public static readonly string ANNOTATION_VERSION = "31";
        /// <summary>
        /// Annotate output from Bowtie alignment
        /// </summary>
        /// <param name="projectFolderOrName">Either the path to a specific Extracted folder,
        ///                             or the path of the projectFolder, in which case the latest
        ///                             Extracted folder will be processed</param>
        /// <param name="genome">Genome to annotate against</param>
        /// <returns>subpath under ProjectMap to results, or null if no processing was needed</returns>
        public string AnnotateFromBowtie(string projectFolderOrName, StrtGenome genome)
		{
            string projectFolder = PathHandler.GetRootedProjectFolder(projectFolderOrName);
            string extractedFolder = PathHandler.GetLatestExtractedFolder(projectFolder);
            string projectName = Directory.GetParent(extractedFolder).Name;
            string barcodeSet = PathHandler.ParseBarcodeSet(extractedFolder);
            SetBarcodeSet(barcodeSet);
            string buildName = genome.GetBowtieIndexName();
            List<string> mapFiles = PathHandler.FindBowtieOutputFiles(extractedFolder, buildName, barcodes.HasRandomBarcodes);
            if (mapFiles.Count == 0)
                throw new NoMapFilesFoundException("There are no .map/.bam files for index " + 
                                           buildName + " in Extracted folder " + Path.GetFileName(extractedFolder));
            string indexVersion = PathHandler.ExtractIndexVersion(mapFiles[0]);
            string annotFolderName = PathHandler.MakeAnnotFolderName(projectName, indexVersion, ANNOTATION_VERSION);
            string outputFolder = Path.Combine(extractedFolder, annotFolderName);
            string outputSubFolder = null;
            if (!File.Exists(Path.Combine(outputFolder, "config.xml")))
            {
                ProcessAnnotation(barcodeSet, genome, projectFolder, projectName, mapFiles, outputFolder);
                string projectFolderName = Path.GetFileName(projectFolder);
                outputSubFolder = outputFolder.Substring(outputFolder.IndexOf(projectFolderName));
            }
            Background.Progress(100);
            Background.Message("Ready");
            Console.WriteLine("Ready annotating " + mapFiles.Count + " map files from " + 
                               projectFolderOrName + " to " + genome.GetBowtieIndexName());
            return outputSubFolder;
		}

        private void ProcessAnnotation(string barcodeSet, StrtGenome genome, string projectFolder,
                                       string projectName, List<string> mapFiles, string outputFolder)
        {
            if (mapFiles.Count == 0)
                return;
            ReadCounter readCounter = new ReadCounter();
            SetBarcodeSet(barcodeSet);
            Background.Message("Loading annotations.");
            UpdateGenesToPaint(projectFolder, props);
            AbstractGenomeAnnotations annotations = new UCSCGenomeAnnotations(props, genome);
            annotations.Load();
            bool useBamFiles = mapFiles[0].EndsWith("bam");
            bool useRandomTagFilter = useBamFiles && barcodes.HasRandomBarcodes;
            string outputNamebase = projectName + (useRandomTagFilter ? "RF_" : "");
            string outputPathbase = Path.Combine(outputFolder, outputNamebase);
            TranscriptomeStatistics ts = new TranscriptomeStatistics(annotations, props);
            string syntLevelFile = PathHandler.GetSyntLevelFile(projectFolder);
            if (File.Exists(syntLevelFile))
                ts.TestReporter = new SyntReadReporter(syntLevelFile, genome.GeneVariants, outputPathbase, annotations.geneFeatures);
            int nFile = 1;
            foreach (string mapFile in mapFiles)
            {
                Console.Write("Processing " + Path.GetFileName(mapFile) + "...");
                Background.Message("File " + (nFile++) + "/" + mapFiles.Count);
                int[] genomeBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
                int n = (useBamFiles)? ts.AnnotateBamFile(mapFile, genomeBcIndexes, useRandomTagFilter) :
                                       ts.AnnotateMapFile(mapFile, genomeBcIndexes, useRandomTagFilter);
                Console.WriteLine(n + " reads matching " + genomeBcIndexes.Length + " " + genome.Abbrev + " or empty samples were found.");
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
            Background.Message("Saving results");
            ts.Save(readCounter, outputPathbase);
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
                    ts.Add(mappings, height);
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

        /// <summary>
        /// If readLength== 0, dumps the whole sequence for each gene, otherwise dumps
        /// all possible subsequences of readLength from each gene
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="readLength"></param>
        public void DumpTranscripts(StrtGenome genome, int readLength, int step, int maxPerGene, string fastaOutput)
        {
            bool variantGenes = genome.GeneVariants;
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
            foreach (LocusFeature gf in UCSCAnnotationReader.IterAnnotationFile(annotationsPath))
                if (chrIdToFeature.ContainsKey(gf.Chr))
                    chrIdToFeature[gf.Chr].Add(gf);
            string lenStr = (readLength == 0) ? "" : "_" + readLength.ToString() + "bp";
            //string fastaOutput = "transcripts_" + genome.GetBowtieIndexName() + lenStr + ".fq";
            StreamWriter fastaWriter = fastaOutput.OpenWrite();
            int nTrSeqs = 0;
            foreach (string chrId in chrIdToFeature.Keys)
            {
                Console.Write(chrId + "."); Console.Out.Flush();
                DnaSequence chrSeq = AbstractGenomeAnnotations.readChromosomeFile(chrIdToFileMap[chrId]);
                foreach (LocusFeature f in chrIdToFeature[chrId])
                {
                    GeneFeature gf = (GeneFeature)f;
                    if (!variantGenes && gf.IsVariant())
                        continue;
                    DnaSequence gfTrFwSeq = new ShortDnaSequence(gf.Length);
                    for (int exonIdx = 0; exonIdx < gf.ExonCount; exonIdx++)
                    {
                        int exonLen = 1 + gf.ExonEnds[exonIdx] - gf.ExonStarts[exonIdx];
                        gfTrFwSeq.Append(chrSeq.SubSequence(gf.ExonStarts[exonIdx], exonLen));
                    }
                    if (gf.Strand == '-')
                        gfTrFwSeq.RevComp();
                    if (readLength == 0)
                    {
                        fastaWriter.WriteLine("@" + gf.Name + ":" + gf.Chr + gf.Strand + ":" + gf.Start);
                        fastaWriter.WriteLine(gfTrFwSeq);
                        fastaWriter.WriteLine("+\n" + new String('b', (int)gfTrFwSeq.Count));
                    }
                    else
                    {
                        int n = 0;
                        for (int p = 0; p < gfTrFwSeq.Count - readLength; p += step)
                        {
                            fastaWriter.WriteLine("@" + gf.Name + ":" + gf.Chr + gf.Strand + ":" + gf.Start + ":" + p);
                            fastaWriter.WriteLine(gfTrFwSeq.SubSequence(p, readLength));
                            fastaWriter.WriteLine("+\n" + new String('b', readLength));
                            if (n++ >= maxPerGene)
                                break;
                        }
                    }
                    nTrSeqs++;
                }
            }
            Console.WriteLine("\nWrote " + nTrSeqs + " transcripts to fasta file " + fastaOutput);
            fastaWriter.Close();
        }

    }
}
