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
        public void UpdateSilverBulletGenes(StrtGenome genome, string errorsPath)
        {
            Console.WriteLine("*** Updating annotation file for {0} using {1} ***",
                              genome.GetBowtieMainIndexName(), Path.GetFileName(errorsPath));
            Background.Message("Updating annotations...");
            AnnotationBuilder builder = AnnotationBuilder.GetAnnotationBuilder(props, genome);
            builder.UpdateSilverBulletGenes(genome, errorsPath);
            Console.WriteLine("Done.");
            Background.Progress(100);
            Background.Message("Ready");
        }

        /// <summary>
        /// Construct the repeat-masked genome, artificial splice junction chromosome and transcript annotation file.
        /// </summary>
        /// <param name="genome"></param>
        public void BuildJunctions(StrtGenome genome)
        {
            AssertStrtGenomeFolder(genome);
            DateTime startTime = DateTime.Now;
            Console.WriteLine("*** Build of spliced exon junctions for {0} started at {1} ***", genome.GetBowtieMainIndexName(), DateTime.Now);
            AnnotationBuilder builder = AnnotationBuilder.GetAnnotationBuilder(props, genome);
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
            if (genome.GeneVariants == false) return;
            string strtDir = genome.GetStrtGenomesFolder();
            NonExonRepeatMasker nerm = new NonExonRepeatMasker();
            Console.WriteLine("*** Making STRT genome by masking non-exonic repeat sequences ***");
            nerm.Mask(genome, strtDir);
        }

        /// <summary>
        /// Construct the artificial splice chromosome, the transcript annotation file, and build the Bowtie index.
        /// </summary>
        /// <param name="genome"></param>
		public void BuildJunctionsAndIndex(StrtGenome genome)
		{
            string btIdxFolder = PathHandler.GetBowtieIndicesFolder();
            if (!Directory.Exists(btIdxFolder))
                throw new IOException("The Bowtie index folder cannot be found. Please set the BowtieIndexFolder property.");
            genome.GeneVariants = true;
            BuildJunctions(genome);
            MakeMaskedStrtChromosomes(genome);
            BuildIndex(genome);
            genome.GeneVariants = false;
            BuildJunctions(genome);
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
                Console.WriteLine("NOTE: Main index " + newIndexName + " already exists. Delete index files and rerun to force rebuild.");
            else
            {
                Console.WriteLine("*** Build of main Bowtie index {0} started at {1} ***", newIndexName, DateTime.Now);
                Console.WriteLine(cmd + " " + arguments);
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
                Console.WriteLine(cmd + " " + arguments);
                int exitCode = CmdCaller.Run(cmd, arguments);
                if (exitCode != 0)
                    Console.Error.WriteLine("Failed to run bowtie-build. ExitCode={0}", exitCode);
            }
            else
                Console.Error.WriteLine("WARNING: No splice chromosome found for " + genome.GetBowtieMainIndexName() + " indexing skipped.");
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
        /// Accepted reads are written in FastQ format and separated from rejected reads written to slask.fq files.
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
            string outputFolder = PathHandler.MakeExtractedFolder(project, barcodes.Name, EXTRACTION_VERSION);
            Extract(extrInfos, outputFolder);
            return extrInfos;
        }

        public void Extract(ProjectDescription pd)
        {
            pd.extractionInfos = PathHandler.ListReadsFiles(pd.runIdsLanes.ToList());
            pd.extractionVersion = EXTRACTION_VERSION;
            string outputFolder = PathHandler.MakeExtractedFolder(pd.ProjectFolder, barcodes.Name, EXTRACTION_VERSION);
            Extract(pd.extractionInfos, outputFolder);
        }

        public static readonly string EXTRACTION_VERSION = "30";
        private void Extract(List<LaneInfo> extrInfos, string outputFolder)
        {
            DateTime start = DateTime.Now;
            ReadExtractor readExtractor = new ReadExtractor(props);
            foreach (LaneInfo extrInfo in extrInfos)
			{
                extrInfo.extractionTopFolder = outputFolder;
                ReadCounter readCounter = new ReadCounter();
                ExtractionWordCounter wordCounter = new ExtractionWordCounter(props.ExtractionCounterWordLength);
                GetExtractedFilePaths(outputFolder, extrInfo);
                if (!AllFilePathsExist(extrInfo.extractedFilePaths) || !File.Exists(extrInfo.summaryFilePath))
                {
                    StreamWriter[] sws_barcoded = OpenStreamWriters(extrInfo.extractedFilePaths);
                    StreamWriter sw_slask = extrInfo.slaskFilePath.OpenWrite();
                    int bcIdx;
                    ExtractionQuality extrQ = (props.AnalyzeExtractionQualities) ? new ExtractionQuality(props.LargestPossibleReadLength) : null;
                    double totLen = 0.0;
                    long nRecords = 0;
                    int[] nValidReadsByBc = new int[barcodes.Count];
                    foreach (FastQRecord fastQRecord in BarcodedReadStream.Stream(barcodes, extrInfo.readFilePath, props.QualityScoreBase))
                    {
                        FastQRecord rec = fastQRecord;
                        if (extrQ != null) extrQ.Add(rec);
                        wordCounter.AddRead(rec.Sequence);
                        int readStatus = readExtractor.Extract(ref rec, out bcIdx);
                        readCounter.Add(readStatus);
                        if (readStatus == ReadStatus.VALID)
                        {
                            totLen += rec.Sequence.Length;
                            nRecords++;
                            nValidReadsByBc[bcIdx]++;
                            sws_barcoded[bcIdx].WriteLine(rec.ToString(props.QualityScoreBase));
                        }
                        else sw_slask.WriteLine(rec.ToString(props.QualityScoreBase));
                    }
                    CloseStreamWriters(sws_barcoded);
                    sw_slask.Close();
                    StreamWriter sw_summary = extrInfo.summaryFilePath.OpenWrite();
                    int averageReadLen = (int)Math.Round(totLen / nRecords);
                    readCounter.AddReadFile(extrInfo.readFilePath, averageReadLen);
                    sw_summary.WriteLine(readCounter.TotalsToTabString());
                    for (int bc = 0; bc < nValidReadsByBc.Length; bc++)
                        sw_summary.WriteLine("BARCODEREADS\t" + barcodes.Seqs[bc] + "\t" + nValidReadsByBc[bc].ToString());
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
        /// <param name="logWriter">File for log information</param>
        public void Process(ProjectDescription projDescr, StreamWriter logWriter)
        {
            SetBarcodeSet(projDescr.barcodeSet);
            logWriter.WriteLine(DateTime.Now.ToString() + " Extracting " + projDescr.runIdsLanes.Length + " lanes with barcodes " + projDescr.barcodeSet + "..."); logWriter.Flush();
            if (barcodes.HasRandomBarcodes)
                logWriter.WriteLine(DateTime.Now.ToString() + " MinPhredScoreInRandomTag=" + props.MinPhredScoreInRandomTag);
            Extract(projDescr);
            string[] speciesArgs = GetSpeciesArgs(projDescr.SampleLayoutPath, projDescr.defaultSpecies);
            projDescr.annotationVersion = ANNOTATION_VERSION;
            foreach (string speciesArg in speciesArgs)
            {
                StrtGenome genome = StrtGenome.GetGenome(speciesArg, projDescr.analyzeVariants, projDescr.defaultBuild);
                genome.ReadLen = GetReadLen(projDescr);
                SetAvailableBowtieIndexVersion(projDescr, genome);
                logWriter.WriteLine(DateTime.Now.ToString() + " Mapping to " + genome.GetBowtieSplcIndexName() + "..."); logWriter.Flush();
                CreateBowtieMaps(genome, projDescr.extractionInfos);
                List<string> mapFilePaths = LaneInfo.RetrieveAllMapFilePaths(projDescr.extractionInfos);
                props.UseRPKM = projDescr.rpkm;
                props.DirectionalReads = !projDescr.rpkm;
                logWriter.WriteLine(DateTime.Now.ToString() + " Annotating " + mapFilePaths.Count + " map files...");
                logWriter.WriteLine(DateTime.Now.ToString() + " setting: AllTrVariants=" + projDescr.analyzeVariants + " DirectionalReads=" + props.DirectionalReads + " RPKM=" + props.UseRPKM);
                logWriter.Flush();
                ResultDescription resultDescr = ProcessAnnotation(genome, projDescr.ProjectFolder, projDescr.projectName, mapFilePaths);
                projDescr.resultDescriptions.Add(resultDescr);
                System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(projDescr.GetType());
                StreamWriter writer = new StreamWriter(Path.Combine(resultDescr.resultFolder, "config.xml"));
                x.Serialize(writer, projDescr);
                writer.Close();
                logWriter.WriteLine(DateTime.Now.ToString() + " Results stored in " + resultDescr.resultFolder + "."); logWriter.Flush();
            }
        }

        private static void SetAvailableBowtieIndexVersion(ProjectDescription projDescr, StrtGenome genome)
        {
            string bowtieIndexVersion = PathHandler.GetSpliceIndexVersion(genome);
            if (bowtieIndexVersion == "" && genome.Annotation != "UCSC")
            {
                Console.WriteLine("Could not find a Bowtie index for " + genome.Annotation +
                                    " - trying UCSC instead for " + projDescr.projectName);
                genome.Annotation = "UCSC";
            }
        }

        private int GetReadLen(ProjectDescription projDescr)
        {
            List<string> extractedByBcFolders = projDescr.extractionInfos.ConvertAll(l => l.extractedFileFolder);
            return GetReadLen(extractedByBcFolders.ToArray());
        }
        private int GetReadLen(string extractedFolder)
        {
            string searchIn = Path.Combine(extractedFolder, "fq");
            string[] extractedByBcFolders = Directory.GetDirectories(searchIn);
            return GetReadLen(extractedByBcFolders);
        }
        private int GetReadLen(string[] extractedByBcFolders)
        {
            ReadCounter rc = new ReadCounter();
            foreach (string extractedByBcFolder in extractedByBcFolders)
            {
                string summaryPath = Path.Combine(extractedByBcFolder, "summary.txt");
                rc.AddExtractionSummary(summaryPath);
            }
            return rc.AverageReadLen;
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
            string[] speciesArgs = GetSpeciesArgs(projectFolder, defaultSpeciesArg);
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

        private void CreateBowtieMaps(StrtGenome genome, List<LaneInfo> extrInfos)
        {
            string splcIndexVersion = GetSplcIndexVersion(genome);
            string splcIndexName = genome.GetBowtieSplcIndexName();
            if (splcIndexName == "")
                throw new Exception("Can not find a Bowtie index corresponding to " + genome.Build + "/" + genome.Annotation);
            Console.WriteLine("Using bowtie index " + splcIndexName + " with version " + splcIndexVersion);
            foreach (LaneInfo extrInfo in extrInfos)
                CreateBowtieMaps(genome, extrInfo, splcIndexVersion, splcIndexName);
        }

        /// <summary>
        /// Create any missing .map files needed for given genome and wells defined by barcodes/species.
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="laneInfo">Paths to all needed map files will be stored in laneInfo.mappedFilePaths</param>
        /// <param name="splcIndexVersion"></param>
        /// <param name="splcIndexName"></param>
        private void CreateBowtieMaps(StrtGenome genome, LaneInfo laneInfo, string splcIndexVersion, string splcIndexName)
        {
            laneInfo.SetMappedFileFolder(splcIndexVersion);
            string mapFolder = laneInfo.mappedFileFolder;
            if (!Directory.Exists(mapFolder))
                Directory.CreateDirectory(mapFolder);
            laneInfo.bowtieLogFilePath = Path.Combine(mapFolder, "bowtie_output.txt");
            List<string> mapFiles = new List<string>();
            int[] genomeBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
            foreach (string fqPath in laneInfo.extractedFilePaths)
            {
                int bcIdx = int.Parse(Path.GetFileNameWithoutExtension(fqPath));
                if (Array.IndexOf(genomeBcIndexes, bcIdx) == -1)
                    continue;
                string mainIndex = genome.GetBowtieMainIndexName();
                string fqUnmappedReadsPath = Path.Combine(mapFolder, bcIdx + ".fq-" + mainIndex);
                string outputMainPath = Path.Combine(mapFolder, bcIdx + "_" + mainIndex + ".map");
                AssertBowtieOutputFile(mainIndex, fqPath, outputMainPath, fqUnmappedReadsPath, laneInfo.bowtieLogFilePath);
                mapFiles.Add(outputMainPath);
                string outputSplcPath = Path.Combine(mapFolder, bcIdx + "_" +  splcIndexVersion + ".map");
                AssertBowtieOutputFile(splcIndexName, fqUnmappedReadsPath, outputSplcPath, "", laneInfo.bowtieLogFilePath);
                mapFiles.Add(outputSplcPath);
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
        private static string GetSplcIndexVersion(StrtGenome genome)
        {
            string splcIndexVersion = PathHandler.GetSpliceIndexVersion(genome); // The current version including date
            if (splcIndexVersion == "")
                throw new Exception("Please use idx function to make a bowtie splice index with ReadLen=" + genome.ReadLen
                                    + " or at least " + (genome.ReadLen - 5) + " for " + genome.GetBowtieMainIndexName());
            return splcIndexVersion;
        }

        private bool AssertBowtieOutputFile(string bowtieIndex, string inputFqReadPath, string outputPath,
                                   string outputFqUnmappedReadPath, string bowtieLogFile)
        {
            if (!File.Exists(outputPath))
            {
                int nThreads = props.NumberOfAlignmentThreadsDefault;
                string threadArg = (nThreads == 1) ? "" : ("-p " + nThreads.ToString());
                string unmappedArg = "";
                if (outputFqUnmappedReadPath != "")
                {
                    string crapMaxPath = Path.Combine(Path.GetDirectoryName(outputFqUnmappedReadPath), "bowtie_maxM_reads_map.temp");
                    unmappedArg = " --un " + outputFqUnmappedReadPath + " --max " + crapMaxPath;
                }
                string bowtieOptions = props.BowtieOptions.Replace("MaxAlignmentMismatches", props.MaxAlignmentMismatches.ToString());
                string arguments = String.Format("{0} {1} {2} {3} \"{4}\" \"{5}\"", bowtieOptions, threadArg,
                                                  unmappedArg, bowtieIndex, inputFqReadPath, outputPath);
                CmdCaller cc = new CmdCaller("bowtie", arguments);
                StreamWriter logWriter = new StreamWriter(bowtieLogFile, true);
                logWriter.WriteLine("--- " + bowtieIndex + " on " + inputFqReadPath + " ---");
                logWriter.WriteLine(cc.StdError);
                logWriter.Close();
                if (cc.ExitCode != 0)
                {
                    Console.Error.WriteLine("bowtie " + arguments + "\nFailed to run Bowtie on {0}. ExitCode={1}. Check logFile.", inputFqReadPath, cc.ExitCode);
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                    return false;
                }
            }
            return true;
        }

        public void Map(string projectOrExtractedFolderOrName, string speciesArg, bool defaultGeneVariants)
        {
            StrtGenome genome = StrtGenome.GetGenome(speciesArg, defaultGeneVariants);
            Map(projectOrExtractedFolderOrName, genome);
        }
        public void Map(string projectOrExtractedFolderOrName, StrtGenome genome)
        {
            string projectFolder = PathHandler.GetRootedProjectFolder(projectOrExtractedFolderOrName);
            string projectOrExtractedFolder = PathHandler.GetRooted(projectOrExtractedFolderOrName);
            string extractedFolder = SetupForLatestExtractedFolder(projectOrExtractedFolder);
            Console.WriteLine("Processing data from " + extractedFolder);
            List<LaneInfo> laneInfos = SetupLaneInfosFromExistingExtraction(extractedFolder);
            genome.ReadLen = GetReadLen(extractedFolder);
            CreateBowtieMaps(genome, laneInfos);
        }

        public string MapAndAnnotate(string projectOrExtractedFolderOrName, string speciesArg, bool defaultGeneVariants)
        {
            StrtGenome genome = StrtGenome.GetGenome(speciesArg, defaultGeneVariants);
            string projectFolder = PathHandler.GetRootedProjectFolder(projectOrExtractedFolderOrName);
            string projectOrExtractedFolder = PathHandler.GetRooted(projectOrExtractedFolderOrName);
            string extractedFolder = SetupForLatestExtractedFolder(projectOrExtractedFolder);
            List<LaneInfo> laneInfos = SetupLaneInfosFromExistingExtraction(extractedFolder);
            genome.ReadLen = GetReadLen(extractedFolder);
            CreateBowtieMaps(genome, laneInfos);
            List<string> mapFiles = LaneInfo.RetrieveAllMapFilePaths(laneInfos);
            return AnnotateMapFiles(genome, projectFolder, extractedFolder, mapFiles);
        }

        public static readonly string ANNOTATION_VERSION = "37";
        /// <summary>
        /// Annotate output from Bowtie alignment
        /// </summary>
        /// <param name="projectOrExtractedFolderOrName">Either the path to a specific Extracted folder,
        ///                             or the path of the projectFolder, in which case the latest
        ///                             Extracted folder will be processed</param>
        /// <param name="genome">Genome to annotate against</param>
        /// <returns>subpath under ProjectMap to results, or null if no processing was needed</returns>
        public string Annotate(string projectOrExtractedFolderOrName, StrtGenome genome)
		{
            string projectFolder = PathHandler.GetRootedProjectFolder(projectOrExtractedFolderOrName);
            string projectOrExtractedFolder = PathHandler.GetRooted(projectOrExtractedFolderOrName);
            string extractedFolder = SetupForLatestExtractedFolder(projectOrExtractedFolder);
            List<LaneInfo> laneInfos = SetupLaneInfosFromExistingExtraction(extractedFolder);
            List<string> mapFiles = SetExistingMapFilePaths(genome, laneInfos);
            return AnnotateMapFiles(genome, projectFolder, extractedFolder, mapFiles);
		}

        private string SetupForLatestExtractedFolder(string projectOrExtractedFolder)
        {
            string extractedFolder = PathHandler.GetLatestExtractedFolder(projectOrExtractedFolder);
            string extractionVersion = PathHandler.GetExtractionVersion(extractedFolder);
            if (int.Parse(extractionVersion) < 28)
                throw new Exception("Extractions of versions < 28 can not be processed anymore. Please redo extraction!");
            string barcodeSet = PathHandler.ParseBarcodeSet(extractedFolder);
            SetBarcodeSet(barcodeSet);
            return extractedFolder;
        }

        private static List<string> SetExistingMapFilePaths(StrtGenome genome, List<LaneInfo> laneInfos)
        {
            string splcIndexVersion = GetSplcIndexVersion(genome);
            string mainPattern = "*_" + genome.GetBowtieMainIndexName() + ".map";
            string splcPattern = "*_" + splcIndexVersion + ".map";
            List<string> allLanesMapFiles = new List<string>();
            foreach (LaneInfo info in laneInfos)
            {
                info.SetMappedFileFolder(splcIndexVersion);
                List<string> laneMapFiles = new List<string>();
                laneMapFiles.AddRange(Directory.GetFiles(info.mappedFileFolder, mainPattern));
                laneMapFiles.AddRange(Directory.GetFiles(info.mappedFileFolder, splcPattern));
                info.mappedFilePaths = laneMapFiles.ToArray();
                allLanesMapFiles.AddRange(laneMapFiles);
            }
            return allLanesMapFiles;
        }

        private string AnnotateMapFiles(StrtGenome genome, string projectFolder, string extractedFolder, List<string> mapFiles)
        {
            string barcodeSet = PathHandler.ParseBarcodeSet(extractedFolder);
            SetBarcodeSet(barcodeSet);
            string projectName = Path.GetFileName(projectFolder);
            ResultDescription resultDescr = ProcessAnnotation(genome, projectFolder, projectName, mapFiles);
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

        private int CompareMapFiles(string path1, string path2)
        {
            string name1 = Path.GetFileName(path1);
            string name2 = Path.GetFileName(path2);
            int bc1 = int.Parse(name1.Substring(0, name1.IndexOf('_')));
            int bc2 = int.Parse(name2.Substring(0, name2.IndexOf('_')));
            return bc1.CompareTo(bc2);
        }

        private List<string> CollectExtractionSummaryPaths(List<string> mapFilePaths, StrtGenome genome)
        {
            Dictionary<string, object> summaryPaths = new Dictionary<string, object>();
            foreach (string mapFilePath in mapFilePaths)
            {
                string laneMapFolder = Path.GetDirectoryName(mapFilePath);
                string laneName = Path.GetFileName(laneMapFolder);
                string extrFolder = Path.GetDirectoryName(Path.GetDirectoryName(laneMapFolder));
                string summaryFolder = Path.Combine(Path.Combine(extrFolder, "fq"), laneName);
                string summaryPath = Path.Combine(summaryFolder, "summary.txt");
                if (!summaryPaths.ContainsKey(summaryPath))
                    summaryPaths[summaryPath] = null;
            }
            return summaryPaths.Keys.ToList();
        }

        private ResultDescription ProcessAnnotation(StrtGenome genome, string projectFolder, string projectName, List<string> mapFilePaths)
        {
            if (mapFilePaths.Count == 0)
                return null;
            string resultSubFolder = projectName + "_" + barcodes.Name + "_" + genome.GetBowtieMainIndexName() + "_" + DateTime.Now.ToPathSafeString();
            string outputFolder = Path.Combine(projectFolder, resultSubFolder);
            ReadCounter readCounter = new ReadCounter();
            readCounter.AddExtractionSummaries(CollectExtractionSummaryPaths(mapFilePaths, genome));
            int averageReadLen = readCounter.AverageReadLen;
            if (averageReadLen == 0)
            {
                averageReadLen = Props.props.StandardReadLen - barcodes.GetInsertStartPos();
                Console.WriteLine("WARNING: Could not read any extraction summary files - using default readLen " + averageReadLen);
            }
            genome.ReadLen = averageReadLen;
            UpdateGenesToPaint(projectFolder, props);
            AbstractGenomeAnnotations annotations = new UCSCGenomeAnnotations(props, genome);
            annotations.Load();
            string outputPathbase = Path.Combine(outputFolder, projectName);
            TranscriptomeStatistics ts = new TranscriptomeStatistics(annotations, props);
            ts.OutputPathbase = outputPathbase;
            string syntLevelFile = PathHandler.GetSyntLevelFile(projectFolder);
            if (File.Exists(syntLevelFile))
                ts.TestReporter = new SyntReadReporter(syntLevelFile, genome.GeneVariants, outputPathbase, annotations.geneFeatures);
            ts.ProcessMapFiles(mapFilePaths, averageReadLen);
            if (ts.GetNumMappedReads() == 0)
                Console.WriteLine("WARNING: contigIds of reads do not seem to match with genome Ids.\n" +
                                  "Was the Bowtie index made on a different genome or contig set?");
            Console.WriteLine("Totally {0} reads were annotated: {1} expressed genes and {2} expressed repeat types.",
                              ts.GetNumMappedReads(), annotations.GetNumExpressedGenes(), annotations.GetNumExpressedRepeats());
            Directory.CreateDirectory(outputFolder);
            Console.WriteLine("Saving to {0}...", outputFolder);
            ts.SaveResult(readCounter, outputPathbase);
            string bowtieIndexVersion = PathHandler.GetSpliceIndexVersion(genome);
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
        /// If readLength == 0, dumps the whole sequence for each gene, otherwise dumps
        /// all possible subsequences of readLength from each gene.
        /// If barcodes == null, no barcode and GGG sequences will be inserted 
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="readLen">length of read seq without barcodes + GGG</param>
        public void DumpTranscripts(Barcodes barcodes, StrtGenome genome, int readLen, int step, int maxPerGene, string fqOutput,
                                    bool makeSplices, int minOverhang, int maxSkip)
        {
            if (readLen > 0) genome.ReadLen = readLen;
            bool variantGenes = genome.GeneVariants;
            string annotationsPath = genome.VerifyAnAnnotationPath();
            if (makeSplices)
                Console.WriteLine("Making all splices that have >= " + minOverhang + " bases overhang and max " + maxSkip + " exons excised.");
            Dictionary<string, string> chrIdToFileMap = genome.GetOriginalGenomeFilesMap();
            Dictionary<string, List<LocusFeature>> chrIdToFeature = new Dictionary<string, List<LocusFeature>>();
            foreach (string chrId in chrIdToFileMap.Keys)
            {
                if (StrtGenome.IsASpliceAnnotationChr(chrId)) continue;
                chrIdToFeature[chrId] = new List<LocusFeature>();
            }
            foreach (LocusFeature gf in new UCSCAnnotationReader(genome).IterAnnotationFile(annotationsPath))
                if (chrIdToFeature.ContainsKey(gf.Chr))
                    chrIdToFeature[gf.Chr].Add(gf);
            StreamWriter fqWriter = fqOutput.OpenWrite();
            StreamWriter spliceWriter = null;
            string spliceOutput = fqOutput.Replace(".fq", "") + "_splices_only.fq";
            if (makeSplices)
                spliceWriter = spliceOutput.OpenWrite();
            int nSeqs = 0, nTrSeqs = 0, nSplSeq = 0, bcIdx = 0;
            foreach (string chrId in chrIdToFeature.Keys)
            {
                Console.Write(chrId + "."); Console.Out.Flush();
                DnaSequence chrSeq = AbstractGenomeAnnotations.readChromosomeFile(chrIdToFileMap[chrId]);
                foreach (LocusFeature f in chrIdToFeature[chrId])
                {
                    string readStart = "";
                    if (barcodes != null)
                        readStart = new string('A', barcodes.BarcodePos) + barcodes.Seqs[bcIdx++ % barcodes.Count] + "GGG";
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
                    if (readLen == 0)
                    {
                        DnaSequence gfTrFwSeq = new ShortDnaSequence(gf.Length);
                        foreach (DnaSequence s in exonSeqsInChrDir)
                            gfTrFwSeq.Append(s);
                        if (gf.Strand == '-')
                            gfTrFwSeq.RevComp();
                        fqWriter.WriteLine("@Gene=" + gf.Name + ":Chr=" + gf.Chr + gf.Strand + ":Pos=" + gf.Start);
                        fqWriter.WriteLine(gfTrFwSeq);
                        fqWriter.WriteLine("+\n" + new String('b', (int)gfTrFwSeq.Count));
                    }
                    else
                    {
                        int n = 0;
                        List<ReadFrag> readFrags = ReadFragGenerator.MakeAllReadFrags(readLen, step, makeSplices, maxSkip, minOverhang,
                                                                                      exonSeqsInChrDir);
                        foreach (ReadFrag frag in readFrags)
                        {
                            string exonNos = string.Join("-", frag.ExonIds.ConvertAll(i => (gf.Strand == '+')? i.ToString(): (gf.ExonCount + 1 - i).ToString()).ToArray());
                            int posInTrFw = (gf.Strand == '+') ? 1 + frag.TrPosInChrDir : (1 + trLen - frag.TrPosInChrDir - (int)frag.Length);
                            int posInChr = gf.GetChrPosFromTrPosInChrDir(frag.TrPosInChrDir);
                            if (gf.Strand == '-')
                                frag.Seq.RevComp();
                            string seqString = readStart + frag.Seq.ToString();
                            string outBlock = "@Gene=" + gf.Name + ":Chr=" + gf.Chr + gf.Strand + ":Pos=" + posInChr +
                                                  ":TrPos=" + posInTrFw + ":Exon=" + exonNos + "\n" +
                                               seqString + "\n" +
                                               "+\n" + new String('b', seqString.Length);
                            nSeqs++;
                            fqWriter.WriteLine(outBlock);
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
            Console.WriteLine("\nWrote " + nSeqs + " reads from " + nTrSeqs + " transcripts to " + fqOutput);
            if (spliceWriter != null)
            {
                spliceWriter.Close();
                Console.WriteLine("\nAlso wrote the " + nSplSeq + " splice spanning reads to " + spliceOutput);
            }
            fqWriter.Close();
        }

    }
}
