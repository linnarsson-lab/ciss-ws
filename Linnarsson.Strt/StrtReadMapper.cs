using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Linnarsson.C1;
using Linnarsson.Dna;
using Linnarsson.Mathematics;
using Linnarsson.Mathematics.SortSearch;
using Linnarsson.Utilities;

namespace Linnarsson.Strt
{
    /// <summary>
    /// Various methods for major steps and utility functionalities of the STRT pipeline.
    /// </summary>
    public class StrtReadMapper
	{
        public StrtReadMapper()
        {
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
            AnnotationBuilder builder = new AnnotationBuilder(genome);
            builder.UpdateSilverBulletGenes(errorsPath);
            Console.WriteLine("Done.");
            Background.Progress(100);
            Background.Message("Ready");
        }

        /// <summary>
        /// Construct the repeat-masked genome, artificial splice junction chromosome and transcript annotation file.
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="optionalAnnotationPath">Optional path of non-standard annotation file</param>
        public void BuildJunctions(StrtGenome genome, string optionalAnnotationPath)
        {
            AnnotationBuilder builder = new AnnotationBuilder(genome);
            DateTime startTime = DateTime.Now;
            Console.WriteLine("*** Build of {0} junctions starting {1} ***", genome.GetMainIndexName(), DateTime.Now);
            builder.BuildExonSplices(optionalAnnotationPath);
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
            string extractionFolder = PathHandler.MakeExtractionFolderSubPath(resultProjectFolder, Props.props.Barcodes.Name, EXTRACTION_VERSION);
            List<LaneInfo> laneInfos = LaneInfo.LaneInfosFromLaneArgs(laneArgs, extractionFolder);
            if (laneInfos.Count == 0)
                Console.WriteLine("WARNING: No read files found corresponding to {0}", string.Join("/", laneArgs.ToArray()));
            ExtractIfNeeded(laneInfos);
            return laneInfos;
        }

        public static readonly string EXTRACTION_VERSION = "34";

        /// <summary>
        /// Extract and filter reads for the barcodes where there is no existing extracted file,
        /// or the file is of an older version, or the read file is newer than the extracted file.
        /// </summary>
        /// <param name="laneInfos"></param>
        private void ExtractIfNeeded(List<LaneInfo> laneInfos)
        {
            foreach (LaneInfo laneInfo in laneInfos.Where(l => l.ExtractionNeeded()))
			{
                new SampleReadWriter(Props.props.Barcodes, laneInfo).ProcessLaneAsRecSets();
            }
		}

        /// <summary>
        /// Find or make a proper aligner index and align the lanes defined by laneInfo
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="laneInfos"></param>
        /// <param name="genomeBcIndexes">optionally only process specific barcodes</param>
        public void CreateAlignments(StrtGenome genome, List<LaneInfo> laneInfos, int[] selectedBcIdxs)
        {
            int[] genomeBcIndexes = Props.props.Barcodes.GenomeAndEmptyBarcodeIndexes(genome);
            if (selectedBcIdxs != null)
                genomeBcIndexes = genomeBcIndexes.Where(i => selectedBcIdxs.Contains(i)).ToArray();
            genome.SplcIndexReadLen = new ReadCounter(Props.props.Barcodes).GetAverageReadLen(laneInfos);
            Aligner aligner = AssertASplcIndex(genome);
            Console.WriteLine("{0}: {1} aligning {2} lanes against {3}...", DateTime.Now, Props.props.Aligner, laneInfos.Count, genome.GetSplcIndexName());
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
                Console.WriteLine("WARNING: No {0} index to use with averageReadLen={1}. Building one with ReadLen={2}.",
                    Props.props.Aligner, actualReadLen, genome.SplcIndexReadLen);
                BuildJunctionsAndIndex(genome);
                if (!aligner.FindASplcIndex())
                    throw new Exception("Could not build " + Props.props.Aligner + " index with readLen=" + genome.SplcIndexReadLen +
                        " for averageReadLen=" + actualReadLen +  " with " + genome.Build + " and " + genome.Annotation);
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
            List<LaneInfo> laneInfos = LaneInfo.SetupLaneInfosFromExistingExtraction(extractedFolder);
            StrtGenome genome = StrtGenome.GetGenome(speciesArg, defaultGeneVariants, defaultAnnotation, false);
            CreateAlignments(genome, laneInfos, null);
        }

        public static readonly string ANNOTATION_VERSION = "45";

        /// <summary>
        /// Uses the SampleLayout file to decide which species(s) to run bowtie and annotate against.
        /// If it does not exist, use the default species supplied.
        /// Note that this command line called method uses the standard (PathHandler) layout filename. When instead
        /// using the ProjectDB, the layout filename is taken from the database to allow updates with alternatively versioned names.
        /// </summary>
        /// <param name="projectFolderOrName"></param>
        /// <param name="speciesArg">If "", species to use will be taken from layout file</param>
        /// <param name="analyzeAllGeneVariants">true to analyze all transcript splice variants defined in annoatation file</param>
        /// <param name="defaultAnnotation">Annotation source to use if layout file is missing</param>
        /// <param name="resultFolderName">Will make a standard subfolder if "".</param>
        /// <summary>
        public void MapAndAnnotate(string projectOrExtractedFolderOrName, string speciesArg, 
                                     bool defaultGeneVariants, string defaultAnnotation, string resultFolder, int[] selectedBcIdxs)
        {
            string extractedFolder = SetupForLatestExtractionFolder(projectOrExtractedFolderOrName);
            List<LaneInfo> laneInfos = LaneInfo.SetupLaneInfosFromExistingExtraction(extractedFolder);
            string projectFolder = PathHandler.GetRootedProjectFolder(projectOrExtractedFolderOrName);
            MapAndAnnotate(speciesArg, defaultGeneVariants, defaultAnnotation, resultFolder,
                            null, selectedBcIdxs, laneInfos, projectFolder);
        }

        public void MapAndAnnotate(string speciesArg, bool defaultGeneVariants, string defaultAnnotation, string resultFolder,
                                   string resultFileprefix, int[] selectedBcIdxs, List<LaneInfo> laneInfos, string projectFolder)
        {
            Props.props.InsertCellDBData = false;
            string sampleLayoutPath = PathHandler.GetLayoutPath(projectFolder);
            string plateId = Path.GetFileName(projectFolder);
            string[] speciesArgs = Props.props.Barcodes.ParsePlateLayout(plateId, sampleLayoutPath); // Read in various annotations from DB or layout
            if (speciesArg != "") speciesArgs = new string[] { speciesArg }; // Only use specific species if specified on command line
            foreach (string sp in speciesArgs)
            {
                StrtGenome genome = StrtGenome.GetGenome(sp, defaultGeneVariants, defaultAnnotation, true);
                CreateAlignments(genome, laneInfos, selectedBcIdxs);
                List<string> mapFiles = LaneInfo.RetrieveAllMapFilePaths(laneInfos);
                if (mapFiles.Count == 0)
                    throw new Exception("Both alignment (map/sam) files and extracted fq files to align are missing! You have to (re-)run the extraction first.");
                string readDir = !Props.props.DirectionalReads ? "No" : Props.props.SenseStrandIsSequenced ? "Sense" : "Antisense";
                Console.WriteLine("{0}: Annotating {1} map files from {2}\nDirectionalReads={3} RPKM={4} SelectedMappingType={5}...",
                                 DateTime.Now, mapFiles.Count, plateId, readDir, Props.props.UseRPKM, Props.props.MultireadMappingMode);
                ResultDescription resultDescr = ProcessAnnotation(genome, projectFolder, plateId, resultFolder, resultFileprefix, mapFiles);
                Console.WriteLine("{0}: ...output in {1}", DateTime.Now, resultDescr.resultFolder);
            }
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
            if (barcodeSet == null)
                throw new Exception("Can not parse barcode set name from " + extractedFolder + ". Please redo extraction!");
            Props.props.BarcodesName = barcodeSet;
            return extractedFolder;
        }

        /// <summary>
        /// List all read extraction summary files that correspond to the .map files.
        /// </summary>
        /// <param name="mapFilePaths"></param>
        /// <returns></returns>
        private ReadCounter ReadExtractionSummaryFiles(List<string> mapFilePaths)
        {
            ReadCounter readCounter = new ReadCounter(Props.props.Barcodes);
            Dictionary<string, object> summaryPaths = new Dictionary<string, object>();
            foreach (string mapFilePath in mapFilePaths)
            {
                string laneMapFolder = Path.GetDirectoryName(mapFilePath);
                string laneFolderName = Path.GetFileName(laneMapFolder);
                string extractionFolder = Path.GetDirectoryName(Path.GetDirectoryName(laneMapFolder));
                string summaryPath = LaneInfo.GetSummaryPath(extractionFolder, laneFolderName);
                if (!summaryPaths.ContainsKey(summaryPath))
                {
                    summaryPaths[summaryPath] = null;
                    readCounter.AddExtractionSummary(summaryPath);
                }
            }
            return readCounter;
        }

        /// <summary>
        /// Make a result folder name located under projectFolder
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="projectFolder"></param>
        /// <param name="analysisId"></param>
        /// <returns>full path to result folder</returns>
        public string GetResultFolder(StrtGenome genome, string projectFolder, string analysisId)
        {
            string resultFolderName = string.Format("{0}_{1}_{2}_{3}_{4}", analysisId, Props.props.Barcodes.Name, genome.GetMainIndexName(),
                                                        Props.props.Aligner.ToUpper()[0], DateTime.Now.ToPathSafeString());
            return Path.Combine(projectFolder, resultFolderName);
        }

        /// <summary>
        /// Annotate features on genome from mapped data in mapFilePaths
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="projectFolder"></param>
        /// <param name="plateId"></param>
        /// <param name="resultFolder">if null, the default result folder will be constructed</param>
        /// <param name="mapFilePaths"></param>
        /// <returns></returns>
        public ResultDescription ProcessAnnotation(StrtGenome genome, string projectFolder, string plateId, 
                                                    string resultFolder, string resultFileprefix, List<string> mapFilePaths)
        {
            if (mapFilePaths.Count == 0)
                return null;
            if (resultFolder == "" || resultFolder == null)
                resultFolder = GetResultFolder(genome, projectFolder, plateId);
            ReadCounter readCounter = ReadExtractionSummaryFiles(mapFilePaths);
            int averageReadLen = readCounter.AverageReadLen;
            MappedTagItem.AverageReadLen = averageReadLen;
            genome.SplcIndexReadLen = averageReadLen;
            UpdateGenesToPaintProp(projectFolder);
            GenomeAnnotations annotations = new GenomeAnnotations(genome);
            annotations.Load();
            if (resultFileprefix == null) resultFileprefix = plateId;
            TranscriptomeStatistics ts = new TranscriptomeStatistics(annotations, resultFolder, resultFileprefix, plateId);
            string syntLevelFile = PathHandler.GetSyntLevelFilePath(projectFolder, Props.props.Barcodes.HasUMIs);
            if (syntLevelFile != "")
                ts.SetSyntReadReporter(syntLevelFile);
            ts.ProcessMapFiles(mapFilePaths, averageReadLen);
            if (ts.GetNumMappedReads() == 0)
                Console.WriteLine("WARNING: contigIds of reads do not seem to match with genome Ids.\nWas the Bowtie index made on a different genome or contig set?");
            Console.WriteLine("{0}: Totally {1} annotations: {2} expressed genes and {3} expressed repeat types.",
                             DateTime.Now, ts.GetNumMappedReads(), annotations.GetNumExpressedTranscripts(), annotations.GetNumExpressedRepeats());
            Directory.CreateDirectory(resultFolder);
            Console.WriteLine("{0}: Saving to {1}...", DateTime.Now, resultFolder);
            ResultDescription resultDescr = new ResultDescription(mapFilePaths, genome, resultFolder);
            ts.SaveResult(readCounter, resultDescr);
            System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(Props.props.GetType());
            using (StreamWriter writer = new StreamWriter(Path.Combine(resultFolder, Props.configFilename)))
                x.Serialize(writer, Props.props);
            if (Props.props.InsertCellDBData)
                InsertCells10kData(plateId, annotations, resultDescr);
            return resultDescr;
        }

        /// <summary>
        /// Insert expression value and analysis setup info into database.
        /// </summary>
        /// <param name="plateid"></param>
        /// <param name="annotations"></param>
        /// <param name="resultDescr"></param>
        private static void InsertCells10kData(string plateid, GenomeAnnotations annotations, ResultDescription resultDescr)
        {
            IDB pdb = DBFactory.GetProjectDB();
            Console.WriteLine("{0}: Saving expression BLOB:s to expression database...", DateTime.Now);
            try
            {
                Dictionary<string, int> cellIdByWell = DBFactory.GetProjectDB().GetWell2CellIdMapping(plateid);
                IExpressionDB edb = DBFactory.GetExpressionDB();
                string parString = MakeParameterString();
                edb.InsertAnalysisSetup(plateid, resultDescr.splcIndexVersion, resultDescr.resultFolder, parString);
                edb.InsertExprBlobs(annotations.IterC1DBExprBlobs(cellIdByWell, true), true, Props.props.Aligner);
                edb.InsertExprBlobs(annotations.IterC1DBExprBlobs(cellIdByWell, false), false, Props.props.Aligner);
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR when inserting expression BLOB:s to database: {0}", e);
            }
        }

        private static string MakeParameterString()
        {
            List<string> parameters = new List<string>();
            parameters.Add(string.Format("UMIFilter={0}/{1}", Props.props.RndTagMutationFilter, Props.props.RndTagMutationFilterParam));
            parameters.Add("MultireadMappingMode=" + Props.props.MultireadMappingMode);
            parameters.Add("Aligner=" + Props.props.Aligner);
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
            //MapFile mapFileReader = MapFile.GetMapFile(mapFilePaths[0], Props.props.Barcodes);
            MapFile mapFileReader = MapFile.GetMapFile(mapFilePaths[0]);
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
