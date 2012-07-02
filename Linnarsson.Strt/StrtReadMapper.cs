﻿using System;
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
				Console.WriteLine("Processing {0}", fileName);

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
                            string bcfileName = Path.Combine(outputFolder, string.Format("{0}_{1}.fq", fileName, bc));
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
                    sw_summary.WriteLine("{0}\t{1}\t{2}", Path.GetFileName(file), bc, counts[bc]);
                sw_summary.WriteLine("{0}\tNOBAR\t{1}", Path.GetFileName(file), nobcCount);
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
                Console.WriteLine("Processing {0}", fileName);

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
                    Console.WriteLine("{0} {1}", barcodes.Seqs[i], barcodeCounts[i]);
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
        /// <param name="laneArgs">Items of "RunNo:LaneNos[:idxSeqs]" that define the lanes of the project.
        ///                        If empty, all sequence files in projectFolder/Reads are used.</param>
		public List<LaneInfo> Extract(string project, List<string> laneArgs)
		{
            project = PathHandler.GetRootedProjectFolder(project);
            List<LaneInfo> laneInfos = new List<LaneInfo>();
            if (laneArgs.Count > 0)
                laneInfos = PathHandler.ListReadsFiles(laneArgs);
            else
                foreach (string extractedFile in PathHandler.CollectReadsFilesNames(project))
                    laneInfos.Add(new LaneInfo(extractedFile, "X", 'x'));
            string outputFolder = PathHandler.MakeExtractedFolder(project, barcodes.Name, EXTRACTION_VERSION);
            Extract(laneInfos, outputFolder);
            return laneInfos;
        }

        public void Extract(ProjectDescription pd)
        {
            pd.extractionInfos = PathHandler.ListReadsFiles(pd.runIdsLanes.ToList());
            pd.extractionVersion = EXTRACTION_VERSION;
            string outputFolder = PathHandler.MakeExtractedFolder(pd.ProjectFolder, barcodes.Name, EXTRACTION_VERSION);
            Extract(pd.extractionInfos, outputFolder);
        }

        public static readonly string EXTRACTION_VERSION = "31";
        private void Extract(List<LaneInfo> laneInfos, string outputFolder)
        {
            DateTime start = DateTime.Now;
            ReadExtractor readExtractor = new ReadExtractor(props);
            foreach (LaneInfo laneInfo in laneInfos)
			{
                laneInfo.extractionTopFolder = outputFolder;
                GetExtractedFilePaths(outputFolder, laneInfo);
                if (!AllFilePathsExist(laneInfo.extractedFilePaths) || !File.Exists(laneInfo.summaryFilePath))
                {
                    ReadCounter readCounter = new ReadCounter();
                    ExtractionWordCounter wordCounter = new ExtractionWordCounter(props.ExtractionCounterWordLength);
                    StreamWriter[] sws_barcoded = OpenStreamWriters(laneInfo.extractedFilePaths);
                    StreamWriter sw_slask = laneInfo.slaskFilePath.OpenWrite();
                    int bcIdx;
                    ExtractionQuality extrQ = (props.AnalyzeExtractionQualities) ? new ExtractionQuality(props.LargestPossibleReadLength) : null;
                    double totLen = 0.0;
                    long nRecords = 0;
                    int[] nValidSTRTReadsByBc = new int[barcodes.Count];
                    int[] nTotalBarcodedReadsByBc = new int[barcodes.Count];
                    foreach (FastQRecord fastQRecord in 
                                BarcodedReadStream.Stream(barcodes, laneInfo.readFilePath, props.QualityScoreBase, laneInfo.idxSeqFilter))
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
                            nValidSTRTReadsByBc[bcIdx]++;
                            sws_barcoded[bcIdx].WriteLine(rec.ToString(props.QualityScoreBase));
                        }
                        else sw_slask.WriteLine(rec.ToString(props.QualityScoreBase));
                        if (bcIdx >= 0) nTotalBarcodedReadsByBc[bcIdx]++;
                    }
                    CloseStreamWriters(sws_barcoded);
                    sw_slask.Close();
                    using (StreamWriter sw_summary = new StreamWriter(laneInfo.summaryFilePath))
                    {
                        int averageReadLen = (int)Math.Round(totLen / nRecords);
                        readCounter.AddReadFile(laneInfo.readFilePath, averageReadLen);
                        sw_summary.WriteLine(readCounter.TotalsToTabString(barcodes.HasRandomBarcodes));
                        sw_summary.WriteLine("#\tBarcode\tValidSTRTReads\tTotalBarcodedReads");
                        for (int bc = 0; bc < nValidSTRTReadsByBc.Length; bc++)
                            sw_summary.WriteLine("BARCODEREADS\t{0}\t{1}\t{2}", barcodes.Seqs[bc], nValidSTRTReadsByBc[bc], nTotalBarcodedReadsByBc[bc]);
                        sw_summary.WriteLine("\nBelow are the most common words among all reads.\n");
                        sw_summary.WriteLine(wordCounter.GroupsToString(200));
                    }
                    if (extrQ != null)
                        extrQ.Write(laneInfo);
                    laneInfo.nReads = readCounter.PartialTotal;
                    laneInfo.nPFReads = readCounter.PartialCount(ReadStatus.VALID);
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
            {
                sws_barcoded[i].Close();
                sws_barcoded[i].Dispose();
            }
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
            Console.WriteLine("StrtReadMapper.Process(" + projDescr.projectName + ")");
            SetBarcodeSet(projDescr.barcodeSet);
            props.TotalNumberOfAddedSpikeMolecules = projDescr.SpikeMoleculeCount;
            logWriter.WriteLine("{0} Extracting {1} lanes with barcodes {2}...", DateTime.Now, projDescr.runIdsLanes.Length, projDescr.barcodeSet);
            logWriter.Flush();
            if (barcodes.HasRandomBarcodes)
                logWriter.WriteLine("{0} MinPhredScoreInRandomTag={1}", DateTime.Now, props.MinPhredScoreInRandomTag);
            Extract(projDescr);
            string[] speciesArgs = GetSpeciesArgs(projDescr.SampleLayoutPath, projDescr.defaultSpecies);
            projDescr.annotationVersion = ANNOTATION_VERSION;
            foreach (string speciesArg in speciesArgs)
            {
                StrtGenome genome = StrtGenome.GetGenome(speciesArg, projDescr.analyzeVariants, projDescr.defaultBuild);
                genome.ReadLen = GetReadLen(projDescr);
                SetAvailableBowtieIndexVersion(projDescr, genome);
                logWriter.WriteLine("{0} Mapping to {1}...", DateTime.Now, genome.GetBowtieSplcIndexName()); logWriter.Flush();
                CreateBowtieMaps(genome, projDescr.extractionInfos);
                List<string> mapFilePaths = LaneInfo.RetrieveAllMapFilePaths(projDescr.extractionInfos);
                props.UseRPKM = projDescr.rpkm;
                props.DirectionalReads = !projDescr.rpkm;
                logWriter.WriteLine("{0} Annotating {1} map files...", DateTime.Now, mapFilePaths.Count);
                logWriter.WriteLine("{0} setting: AllTrVariants={1} DirectionalReads={2} RPKM={3}",
                                    DateTime.Now, projDescr.analyzeVariants, props.DirectionalReads, props.UseRPKM);
                logWriter.Flush();
                ResultDescription resultDescr = ProcessAnnotation(genome, projDescr.ProjectFolder, projDescr.projectName, mapFilePaths);
                projDescr.resultDescriptions.Add(resultDescr);
                System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(projDescr.GetType());
                StreamWriter writer = new StreamWriter(Path.Combine(resultDescr.resultFolder, "config.xml"));
                x.Serialize(writer, projDescr);
                writer.Close();
                logWriter.WriteLine("{0} Results stored in {1}.", DateTime.Now, resultDescr.resultFolder);
                logWriter.Flush();
            }
        }

        private static void SetAvailableBowtieIndexVersion(ProjectDescription projDescr, StrtGenome genome)
        {
            string bowtieIndexVersion = PathHandler.GetSpliceIndexVersion(genome);
            if (bowtieIndexVersion == "" && genome.Annotation != "UCSC")
            {
                Console.WriteLine("Could not find a Bowtie index for {0} - trying UCSC instead for {1}",
                                  genome.Annotation, projDescr.projectName);
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
            Console.WriteLine("Using bowtie index {0} with version {1}", splcIndexName, splcIndexVersion);
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
        private static string GetSplcIndexVersion(StrtGenome genome)
        {
            string splcIndexVersion = PathHandler.GetSpliceIndexVersion(genome); // The current version including date
            if (splcIndexVersion == "")
                throw new Exception("Please use idx function to make a bowtie splice index with ReadLen=" + genome.ReadLen
                                    + " or at least " + (genome.ReadLen - 5) + " for " + genome.GetBowtieMainIndexName());
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
            int nThreads = props.NumberOfAlignmentThreadsDefault;
            string threadArg = (nThreads == 1) ? "" : string.Format("-p {0}", nThreads);
            string unmappedArg = "";
            if (outputFqUnmappedReadPath != "")
            {
                string crapMaxPath = Path.Combine(Path.GetDirectoryName(outputFqUnmappedReadPath), "bowtie_maxM_reads_map.temp");
                unmappedArg = string.Format(" --un {0} --max {1}", outputFqUnmappedReadPath, crapMaxPath);
            }
            string arguments = String.Format("{0} {1} {2} {3} \"{4}\" \"{5}\"", props.BowtieOptions, threadArg,
                                                unmappedArg, bowtieIndex, inputFqReadPath, outputPath);
            CmdCaller cc = new CmdCaller("bowtie", arguments);
            StreamWriter logWriter = new StreamWriter(bowtieLogFile, true);
            logWriter.WriteLine("--- {0} on {1} ---", bowtieIndex, inputFqReadPath);
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
            Console.WriteLine("Processing data from {0}", extractedFolder);
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

        public static readonly string ANNOTATION_VERSION = "40";
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
            string mainPattern = string.Format("*_{0}.map", genome.GetBowtieMainIndexName());
            string splcPattern = string.Format("*_{0}chr{1}_*.map", genome.Build, genome.VarAnnot);
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
            Console.WriteLine("Annotated {0} map files from {1} to {2}", mapFiles.Count, projectName, resultDescr.bowtieIndexVersion);
            return resultDescr.resultFolder;
        }

        private List<LaneInfo> SetupLaneInfosFromExistingExtraction(string extractedFolder)
        {
            string fqFolder = Path.Combine(extractedFolder, "fq");
            List<LaneInfo> laneInfos = new List<LaneInfo>();
            foreach (string extractedByBcFolder in Directory.GetDirectories(fqFolder))
            {
                Match m = Regex.Match(extractedByBcFolder, "Run([0-9]+)_L([0-9])_[0-9]_[0-9]+");
                if (!m.Success) continue;
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
            string resultSubFolder = string.Format("{0}_{1}_{2}_{3}", projectName, barcodes.Name, genome.GetBowtieMainIndexName(), DateTime.Now.ToPathSafeString());
            string outputFolder = Path.Combine(projectFolder, resultSubFolder);
            ReadCounter readCounter = new ReadCounter();
            readCounter.AddExtractionSummaries(CollectExtractionSummaryPaths(mapFilePaths, genome));
            int averageReadLen = readCounter.AverageReadLen;
            if (averageReadLen == 0)
            {
                averageReadLen = EstimateReadLengthFromMapFiles(mapFilePaths);
                Console.WriteLine("WARNING: Could not read any extraction summary files - estimated read length from first map file = " + averageReadLen);
            }
            MappedTagItem.AverageReadLen = averageReadLen;
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
                Console.WriteLine("WARNING: contigIds of reads do not seem to match with genome Ids.\nWas the Bowtie index made on a different genome or contig set?");
            Console.WriteLine("Totally {0} annotations: {1} expressed genes and {2} expressed repeat types.",
                              ts.GetNumMappedReads(), annotations.GetNumExpressedGenes(), annotations.GetNumExpressedRepeats());
            Directory.CreateDirectory(outputFolder);
            Console.WriteLine("Saving to {0}...", outputFolder);
            ts.SaveResult(readCounter, outputPathbase);
            string bowtieIndexVersion = PathHandler.GetSpliceIndexVersion(genome);
            return new ResultDescription(mapFilePaths, bowtieIndexVersion, outputFolder);
        }

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

        private void UpdateGenesToPaint(string projectFolder, Props props)
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
            int nTot = 0, nBarcoded = 0, nTooShort = 0;
            using (StreamWriter writer = new StreamWriter(outFile))
            {
                string[] barcodesGGG = barcodes.GetBarcodesWithTSSeq();
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
                                writer.WriteLine(">{0}\n{1}", rec.HeaderLine, seq.Substring(pos, seqLen));
                                nBarcoded++;
                            }
                            else
                                nTooShort++;
                            break;
                        }
                    }
                }
            }
            Console.WriteLine("{0} sequences scanned, {1} were barcoded, {2} had no barcoded, {3} were too short",
                              nTot, nBarcoded, (nTot - nBarcoded - nTooShort), nTooShort);
            Console.WriteLine("Output file ready for extraction is in {0}", outFile);
        }

        /// <summary>
        /// If readLength == 0, dumps the whole sequence for each gene, otherwise dumps
        /// all possible subsequences of readLength from each gene.
        /// If barcodes == null, no barcode and GGG sequences will be inserted 
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="readLen">length of read seq without barcodes + GGG</param>
        public void DumpTranscripts(Barcodes barcodes, StrtGenome genome, int readLen, int step, int maxPerGene, string outFile,
                                    bool outputFastq, bool makeSplices, int minOverhang, int maxSkip)
        {
            if (readLen > 0) genome.ReadLen = readLen;
            bool variantGenes = genome.GeneVariants;
            string annotationsPath = genome.VerifyAnAnnotationPath();
            if (makeSplices)
                Console.WriteLine("Making all splices that have >= {0} bases overhang and max {1} exons excised.", minOverhang, maxSkip);
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
            using (StreamWriter outWriter = new StreamWriter(outFile))
            {
                StreamWriter spliceWriter = null;
                string spliceOutput = outputFastq? outFile.Replace(".fq", "") + "_splices_only.fq" :
                                                   outFile.Replace(".fa", "") + "_splices_only.fa";
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
                            string header = string.Format("Gene={0}:Chr={1}{2}:Pos={3}:TrLen={4}", gf.Name, gf.Chr, gf.Strand, gf.Start, gfTrFwSeq.Count);
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
