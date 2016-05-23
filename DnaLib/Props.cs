﻿using System;
using System.Linq;
using System.Configuration;
using Linnarsson.Utilities;
using System.IO;

namespace Linnarsson.Dna
{
    /// <summary>
    /// Determines whether (non-directional) multireads will be assigned to all or one random of alternative transcripts
    /// </summary>
    public enum MultiReadMappingType { All, Random, Most5Prime };
    /// <summary>
    /// Used in the Props.ExtractionReadLimitType to allow a limit on the number of reads to extract from the .fq file(s)
    /// </summary>
    public enum ReadLimitType { None, TotalReads, TotalReadsPerBarcode, TotalValidReads, TotalValidReadsPerBarcode };
    /// <summary>
    /// Used in the Props.GenomeBuildRepeatMaskingType to specify if and which repeats should be masked when building STRT genomes
    /// </summary>
    public enum RepeatMaskingType { None, Exon, All };
    /// <summary>
    /// Used in the Props.RndTagMutationFilter to define how molecules due to mutated random labels should be eliminated
    /// </summary>
    public enum UMIMutationFilter { FractionOfMax, FractionOfMean, Singleton, LowPassFilter, Hamming1Singleton };

    /// <summary>
    /// Defines various configurations used by many classes.
    /// Defaults values are given here, but actual data is read from an XML file
    /// in the directory where the executables are located.
    /// Note that Props.props is a singleton.
    /// </summary>
    [Serializable]
    public sealed class Props
    {
        [NonSerialized]
        public static readonly string configFilename = "SilverBulletConfig.xml"; // Filename of machine specific Props

        // Following props are imported from the "SB.exe.config" file, where they are encrypted, see below.
        public string MySqlServerConnectionString = "server=127.0.0.1;uid=user;pwd=password;database=joomla;Connect Timeout=300;Charset=utf8;";
        [NonSerialized]
        public string OutgoingMailServer = "send.my.server";
        [NonSerialized]
        public string OutgoingMailUser = "";
        [NonSerialized]
        public string OutgoingMailPassword = "";
        [NonSerialized]
        public int OutgoingMailPort = 0;
        [NonSerialized]
        public bool OutgoingMailUseSsl = true;

        // Default values for configuration follows.
        public string IlluminaRunReadyFilename = "Basecalling_Netcopy_complete.txt"; // File in Illumina runs folders that indicate run completed
        public string GenomesFolder = "\\\\127.0.0.1\\data\\genomes";
        public string RunsFolder = "\\\\127.0.0.1\\data\\runs"; // Where Illumina raw data are stored
        public string ReadsFolder = "\\\\127.0.0.1\\data\\reads"; // Where FastQ files of concatenated reads for each lane are gathered
        public string ProjectsFolder = "\\\\127.0.0.1\\data\\strt";
        public string UploadsFolder = "\\\\127.0.0.1\\uploads";
        public string BarcodesFolder = "\\\\127.0.0.1\\data\\strt\barcodes";
        public string ResultDownloadUrl = "strtserver@127.0.0.1:/html/strt/";
        public bool ResultUrlIsMounted = true;
        public string ResultDownloadFolderHttp = "http://127.0.0.1/html/strt/";
        public string ResultDownloadScpPort = "0";
        public string FailureReportAndAnonDownloadEmail = "strt.pipeline@ki.se";
        public string OutgoingMailSender = "strt.pipeline@ki.se";
        public string OutputDocFile = "\\\\127.0.0.1\\data\\strt\\STRTOutputManual.pdf";
        public int BkgBackuperStartHour = 17;
        public int BkgBackuperStopHour = 8;
        public bool DebugAnnotation = false; // Will give output files of non-annotated and non-exon reads
        public bool GenerateWiggle = true; // Generate wiggle files for upload to UCSC Genome Browser
        public bool GenerateBed = false; // Generate read BED file
        public bool GenerateBarcodedWiggle = false; // Generate wiggle files per barcode for upload to UCSC Genome Browser
        public bool GenerateReadCountsByUMI = false; // Generate BED like files that include UMI info
        public bool AnalyzeSNPs = true;
        public bool DetermineMotifs = false; // Analyse over-represented sequence motifs around read start
        public string[] SeqStatsChrIds = null; // Used to limit detailed statistics to only a subset of chromosomes
        public string[] GenesToPaint =
            new string[] { }; // Override with a 'genes_to_paint.txt' file in the project folder
        public string[] GenePaintIntervals =
            new string[] {}; // {"Morf4l2,136741000,136744000", "Hnrnpf,117905000,117920000", "Use1,71366000,71370000", "Uba1,20658000,20665000"};
        public bool MakeGeneReadsPerMoleculeHistograms = true; // Will display histograms of #reads detected per molecule at various positions
        public string[] GenesToShowRndTagProfile = new string[] {}; // { "Sox2" ,"Actb", "Nanog" };
        public int[] SelectedBcWiggleAnnotations;
        public bool SnpRndTagVerification = false;
        public string SnpRndTagVerificationChr = "19"; // Set to id(or ids separated by comma) of specific chr to analyze, otherwise ""
        public int MinPhredScoreInRandomTag = 17;
        public int MinMoleculesToTestSnp = 4; // SNP analysis minimum coverage to test a potential SNP positions (when using random labels)
        public int MinReadsToTestSnp = 10; // SNP analysis minimum coverage to test a potential SNP positions (without random labeled barcodes)
        public bool GenerateTranscriptProfiles = false;
        public bool GenerateGeneLocusProfiles = false;
        public int LocusProfileBinSize = 50;
        public bool GenerateGeneProfilesByBarcode = false; // Will show exon hits by barcode for each gene
        public bool AnalyzeAllGeneVariants = true; // Analyze all alternative splice sites in exons etc.
        public bool DirectionalReads = true; // STRT are always directional reads
        public bool UseRPKM = false; // Give RPKM instead of RPM in output files for non-STRT samples
        public int LocusFlankLength = 1000; // Maximum length of UPSTREAM and DOWNSTREAM regions to analyse
        public int StandardReadLen = 50; // Better not use actual reads that are longer - otherwise some junction hits may be missed
        public int MaxExonsSkip = 12; // Max number of exons to consider for splice out in junction chromosome
        public bool AnalyzeExtractionQualities = false; // Analyze read quality and color balance
        public int MinExtractionInsertLength = 25; // Min acceptable read length excluding barcode and GGG
		public int MinExtractionInsertNonAs = 5; // Min number of C/G/T in an acceptable read
        public int LargestPossibleReadLength = 300; // Used for dimensioning extraction quality calculators
        public int MaxAlignmentMismatches = 3;  // Should be the value used in bowtie calls
        public int MaxAlternativeMappings = 25; // Max number of alignments allowed for a multiread. Multireads with more will not be processed
        public byte QualityScoreBase = 64; // For ASCII-encoding of phred scores
        public string BowtieIndexArgs = "$FastaPaths $IndexPath";
        public string BowtieAlignArgs = "-p $NThreads --phred$QualityScoreBase-quals -M $MaxAlternativeMappings -k $MaxAlternativeMappings -v $MaxAlignmentMismatches --best --strata $IndexPath $FqPath $OutPath";
        public string StarIndexArgs = "--runMode genomeGenerate --runThreadN $NThreads --outFileNamePrefix $IndexDir/ --genomeDir $IndexDir --genomeFastaFiles $FastaPaths";
        public string StarAlignArgs = "--runMode alignReads --genomeLoad LoadAndKeep --outFilterMultimapNmax $MaxAlternativeMappings --alignIntronMax 1 --outFilterMismatchNmax $MaxAlignmentMismatches --outFileNamePrefix $OutFolder/ --runThreadN $NThreads --genomeDir $IndexPath --readFilesIn $FqPath";
        public double SyntheticReadsRandomMutationProb = 0.0; // Used only in synthetic data construction
        public double SyntheticReadsBackgroundFreq = 0.0; // Frequency of random background reads in synthetic data
        public bool SynthesizeReadsFromGeneVariants = false; // Used only in synthetic data construction
        public string TestAnalysisFileMarker = "SYNT_READS"; // Indicator in files related to synthesized test reads
        public int MaxFeatureLength = 2500000; // Longer features (loci) are excluded from analysis
        public int NumberOfAlignmentThreadsDefault = 16; // Bowtie multi-processor alignment
        public int ExtractionCounterWordLength = 12; // Used for over-representation analysis by ExtractionWordCounter
        public string LayoutFile = ""; // If "", the plate layout file is looked for in the project folder with default filename format
        public string SampleLayoutFileFormat = "{0}_SampleLayout.txt"; // Default filename format for plate layout filenames. Arg0 is project name
        public int TotalNumberOfAddedSpikeMolecules = 6975;
        public bool UseMost5PrimeExonMapping = true; // true: directional exonic multireads get one single hit at the transcript with closest 5' end
        public MultiReadMappingType DefaultExonMapping = MultiReadMappingType.Random; // Decides non-directional multiread mapping method
        public bool ShowTranscriptSharingGenes = true;
        public bool SaveNonMappedReads = false; // Non-mapped reads from Bowtie may be stored in separate files
        public bool AnalyzeSeqUpstreamTSSite = false; // if true, will check if false reads were made by barcode matching in template
        public bool AnalyzeSpliceHitsByBarcode = false; // If true, will show transcript cross-junction hits per barcode
        public int GeneFeature5PrimeExtension = 0; // Extend all transcript 5' annotations to allow for unknown more upstream start sites.
        public int CapRegionSize = 100; // Defines the size in bp to consider as hits to 5' end for the CAPRegion hit counting
        public UMIMutationFilter RndTagMutationFilter = UMIMutationFilter.FractionOfMax;
        public int RndTagMutationFilterParam = 50;
        public int MinAltNtsReadCountForSNPDetection = 10; // Positions with less reads with the non-ref bases will not be considered for SNP analysis
        public RepeatMaskingType GenomeBuildRepeatMaskingType = RepeatMaskingType.Exon;
        public ReadLimitType ExtractionReadLimitType;
        public int ExtractionReadLimit = 0;
        public string BackupDestinationFolder = "sb_backup@some.server:/mnt/data_reads/";
        public int MappingsBySpikeReadsSampleDist = 0; // Set > 0 to sample per-barcode curves of # unique mappings as fn. of # processed spike reads
        public string RemoveTrailingReadPrimerSeqs = ""; // Comma-separated list of seqs to remove if during extraction reads end with (part of) them.
        public string ForbiddenReadInternalSeqs = ""; // Comma-separated list of seqs that if found inside reads disqualify them during extraction.
        public int SampleDistPerBcForAccuStats = 20000; // Statistics as fn. of #reads processed will be collected every this # of reads
        public bool SampleAccuFilteredExonMols = false; // Sample #EXON Mols after mutation filter as fn. of processed reads (slow)
        public string[] CAPCloseSiteSearchCutters = new string[] { "PvuI" };
        public bool InsertCellDBData = true; // Insert results into DB if 1) project is "C1-" 2) genes read from DB 3) using ProjDBProcessor 4) using props.CellDBAligner
        public bool AnalyzeGCContent = false; // Analyze the GC content of transcript mapping reads
        public bool LogMode = false;
        public bool SenseStrandIsSequenced = true; // Exon/splice reads come from the sense strand (meaningful for DirectionalReads)
        public bool WriteHotspots = false; // Output a file with local hotspots
        public bool WriteSlaskFiles = true; // Output unused seqs to slask.fq.gz during extraction
        public bool DenseUMICounter = false; // Save memory using ZeroOneMoreTagItem. Only with LowPass/0, LowPass/1 and Singleton/0 RndTagMutation filters.
        public bool WriteCAPRegionHits = false; // true to write hit counts within +/- something of 5' end of transcripts
        public string ChrCTRLId = "CTRL";
        public string[] CommonChrIds = new string[] { "CTRL", "EXTRA" };
        public bool AddRefFlatToNonRefSeqBuilds = false;
        public double CriticalOccupiedUMIFraction = 0.80; // Limit for warning from overoccupied UMI counter
        public string Aligner = "bowtie"; // Default aligner
        public string CellDBAligner = "bowtie"; // The aligner used when data is inserted into CellDB
        public string DBPrefix = "jos_"; // Table prefix use in CellDB
        public int OutputLevel = 2; // Controls how much data will be output
        public bool AnalyzeLoci = false; // For nuclear RNA, will consider each gene as the whole locus, and not exons.
        public bool WritePlateReadFile = false; // If true, extractor writes fq.gz file(s) with non-filtered reads having current bc
        public string[] AllMixinBcSets = new string[] { "C1-1", "C1-2", "C1-3", "C1-4" };
        public bool ParallellFastqCopy = false; // If true, will speed up bcl->fq conversion by parallell threads
        public bool UseNewDbSetup = false; // If true, will use one single combined DB for sample/cell and expression data
        public string Tn5Seq = "CTGTCTCTTATACAC"; //..."ATCTGACGC";
        public bool RemoveIntermediateFiles = false; // Remove extracted fq and aligned map files after annotation finished
        public bool InsertBcWigToDB = false; // Insert wiggle plots per barcode into DB, but only if InsertCellDBData==true

        private Barcodes m_Barcodes;
        public Barcodes Barcodes {
            get { if (m_Barcodes == null) m_Barcodes = Barcodes.GetBarcodes(BarcodesName); return m_Barcodes; }
        }
        [NonSerialized]
        private string m_BarcodesName;
        public string BarcodesName
        {
            get { return m_BarcodesName; }
            set { m_BarcodesName = value; m_Barcodes = null; }
        }

        public bool Apply5PrimeMapping { get { return DirectionalReads && UseMost5PrimeExonMapping; } }
        public MultiReadMappingType MultireadMappingMode { get { return Apply5PrimeMapping ? MultiReadMappingType.Most5Prime : DefaultExonMapping; } }

        public bool UseMaxAltMappings { get { return props.BowtieAlignArgs.Contains("MaxAlternativeMappings"); } }

        public string AssemblyVersion { get { return System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString(); } }

        private static Props Read()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string configFilePath = Path.Combine(appDir, configFilename);
            Props props = null;
            try
            {
                props = SimpleXmlSerializer.FromXmlFile<Props>(configFilePath);
            }
            catch (FileNotFoundException)
            {
                props = new Props();
                SimpleXmlSerializer.ToXmlFile(configFilePath, props);
            }
            SetConnectionStrings(props);
            return props;
        }

        private static void SetConnectionStrings(Props props)
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string exeFilePath = Path.Combine(appDir, "SB.exe"); // The application that holds ConnectionString config
                Configuration config = ConfigurationManager.OpenExeConfiguration(exeFilePath);
                ConnectionStringsSection section = config.GetSection("connectionStrings") as ConnectionStringsSection;
                if (!section.SectionInformation.IsProtected)
                {
                    section.SectionInformation.ProtectSection("RsaProtectedConfigurationProvider");
                    config.Save(ConfigurationSaveMode.Full, true);
                }
                section.SectionInformation.UnprotectSection();
                ConnectionStringSettings settings = section.ConnectionStrings["SB.Properties.Settings.MainDBConnString"];
                if (settings != null)
                    props.MySqlServerConnectionString = settings.ConnectionString;
                settings = section.ConnectionStrings["SB.Properties.Settings.EmailConnString"];
                if (settings != null)
                {
                    foreach (string part in settings.ConnectionString.Split(';'))
                    {
                        string[] fields = part.Split('=');
                        if (fields.Length == 2)
                        {
                            if (fields[0] == "server")
                                props.OutgoingMailServer = fields[1];
                            else if (fields[0] == "uid")
                                props.OutgoingMailUser = fields[1];
                            else if (fields[0] == "pwd")
                                props.OutgoingMailPassword = fields[1];
                            else if (fields[0] == "port")
                                props.OutgoingMailPort = int.Parse(fields[1]);
                            else if (fields[0] == "usessl")
                                props.OutgoingMailUseSsl = bool.Parse(fields[1]);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Warning: Props could not load encrypted DB connection setup: {0}", e.Message);
            }
        }

        // Singleton stuff below
        Props()
        {
        }
        public static Props props
        {
            get
            {
                return PropsHolder.instance;
            }
        }
        class PropsHolder
        {
            static PropsHolder()
            {
            }
            internal static readonly Props instance = Read();
        }

    }

}
