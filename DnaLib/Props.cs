using System;
using System.Linq;
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
    public enum RndTagMutationFilterMethod { FractionOfMax, FractionOfMean, Singleton, LowPassFilter };

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

        // Default values for configuration follows.
        public string IlluminaRunReadyFilename = "Basecalling_Netcopy_complete.txt"; // File in Illumina runs folders that indicate run completed
        public string GenomesFolder = "\\\\130.237.117.141\\data\\genomes";
        public string RunsFolder = "\\\\130.237.117.141\\data2\\runs"; // Where Illumina raw data are stored
        public string MySqlServerIP = "130.237.117.141";
        public string ReadsFolder = "\\\\130.237.117.141\\data\\reads"; // Where FastQ files of concatenated reads for each lane are gathered
        public string ProjectsFolder = "\\\\130.237.117.141\\data\\strt";
        public string UploadsFolder = "\\\\130.237.117.141\\uploads";
        public string ResultDownloadUrl = "strtserver@130.237.117.141:/var/www/html/strt/";
        public string ResultDownloadFolderHttp = "http://linnarsson.mbb.ki.se/strt/";
        public string ResultDownloadScpPort = "9922";
        public string FailureReportEmail = "peter.lonnerberg@ki.se";
        public string ProjectDBProcessorNotifierEmailSender = "peter.lonnerberg@ki.se";
        public string OutgoingMailSender = "linnarsson-server@mbb.ki.se";
        public string OutgoingMailServer = "send.ki.se";
        public string OutgoingMailUser = "";
        public string OutgoingMailPassword = "";
        public int OutgoingMailPort = 587;
        public string BowtieIndexFolder = "\\\\130.237.117.141\\sequtils\\bowtie-0.12.7\\indexes";
        public string OutputDocFile = "\\\\130.237.117.141\\data\\strt\\STRTOutputManual.pdf";
        public int BkgBackuperStartHour = 17;
        public int BkgBackuperStopHour = 8;

        public bool DebugAnnotation = false; // Will give output files of non-annotated and non-exon reads
        public bool GenerateWiggle = true; // Generate wiggle files for upload to UCSC Genome Browser
        public bool GenerateBed = false; // Generate read BED file
        public bool GenerateBarcodedWiggle = false; // Generate wiggle files per barcode for upload to UCSC Genome Browser
        public bool AnalyzeSNPs = true;
        public bool DetermineMotifs = false; // Analyse over-represented sequence motifs around read start
        public string[] SeqStatsChrIds = null; // Used to limit detailed statistics to only a subset of chromosomes
        public string[] GenesToPaint =
            new string[] {}; //{ "Sox2" ,"Actb", "Nanog", "Klf4", "Calb1", "Rnr2", "Tmpo", "Trpm6", "Pou5f1",
                              // "Rnr1", "Nd1", "Cox2", "Vcam1", "Zfp42", "Fgf2r", "Nt5e", "Runx2", "Taz",
                              // "Osx", "Twist", "Ap1", "Sox9", "Sox6", "Sox5", "Bmp2", "Smad1", "Smad4" };
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
        public string DefaultBarcodeSet = "v4"; // This is the default barcode set
        public int LocusFlankLength = 1000; // Maximum length of UPSTREAM and DOWNSTREAM regions to analyse
        public int StandardReadLen = 50; // Better not use actual reads that are longer - otherwise some junction hits may be missed
        public int MaxExonsSkip = 12; // Max number of exons to consider for splice out in junction chromosome
        public bool AnalyzeExtractionQualities = false; // Analyze read quality and color balance
        public int MinExtractionInsertLength = 25; // Min acceptable read length excluding barcode and GGG
		public int MinExtractionInsertNonAs = 5; // Min number of C/G/T in an acceptable read
        public int LargestPossibleReadLength = 300; // Used for dimensioning extraction quality calculators
        public int MaxAlignmentMismatches = 3;  // Should be the value used in bowtie calls
        public int MaxAlternativeMappings = 25; // Experimental for new version handling of unique repeat positions
        public byte QualityScoreBase = 64; // For ASCII-encoding of phred scores
        public string BowtieOptionPattern = "--phredQualityScoreBase-quals -k MaxAlternativeMappings -v MaxAlignmentMismatches --best --strata";
        public double SyntheticReadsRandomMutationProb = 0.0; // Used only in synthetic data construction
        public double SyntheticReadsBackgroundFreq = 0.0; // Frequency of random background reads in synthetic data
        public bool SynthesizeReadsFromGeneVariants = false; // Used only in synthetic data construction
        public string TestAnalysisFileMarker = "SYNT_READS"; // Indicator in files related to synthesized test reads
        public int MaxFeatureLength = 2500000; // Longer features (loci) are excluded from analysis
        public int NumberOfAlignmentThreadsDefault = 12; // Bowtie multi-processor alignment
        public int ExtractionCounterWordLength = 12; // Used for over-representation analysis by ExtractionWordCounter
        public string SampleLayoutFileFolder = ""; // If empty, the PlateLayout file is looked for in the project folder
        public string SampleLayoutFileFormat = "{0}_SampleLayout.txt"; // Formatter for sample layout filenames. Arg0 is project name
        public int TotalNumberOfAddedSpikeMolecules = 2500;
        public bool UseMost5PrimeExonMapping = true; // if true, exonic multireads get only one single hit at the transcript with closest 5' end
        public MultiReadMappingType DefaultExonMapping = MultiReadMappingType.Random; // Decides non-directional multiread mapping method
        public bool ShowTranscriptSharingGenes = true;
        public bool SaveNonMappedReads = false; // Non-mapped reads from Bowtie may be stored in separate files
        public bool AnalyzeSeqUpstreamTSSite = false; // if true, will check if false reads were made by barcode matching in template
        public bool AnalyzeSpliceHitsByBarcode = false; // If true, will show transcript cross-junction hits per barcode
        public int GeneFeature5PrimeExtension = 0; // Extend all transcript 5' annotations to allow for unknown more upstream start sites.
        public int CapRegionSize = 100; // Defines the size in bp to consider as hits to 5' end for the CAPRegion hit counting
        public RndTagMutationFilterMethod RndTagMutationFilter = RndTagMutationFilterMethod.FractionOfMax;
        public int RndTagMutationFilterParam = 50;
        public int MinAltNtsReadCountForSNPDetection = 10; // Positions with less reads with the non-ref bases will not be considered for SNP analysis
        public RepeatMaskingType GenomeBuildRepeatMaskingType = RepeatMaskingType.Exon;
        public ReadLimitType ExtractionReadLimitType;
        public int ExtractionReadLimit = 0;
        public string BackupDestinationFolder = "hiseq@130.237.142.75:/mnt/davidson/hiseq/data_reads/";
        public int MappingsBySpikeReadsSampleDist = 0; // Set > 0 to sample per-barcode curves of # unique mappings as fn. of # processed spike reads
        public string RemoveTrailingReadPrimerSeqs = ""; // Comma-separated list of seqs to remove if during extraction reads end with (part of) them.
        public string ForbiddenReadInternalSeqs = ""; // Comma-separated list of seqs that if found inside reads disqualify them during extraction.
        public int sampleDistPerBcForAccuStats = 100000; // Statistics as fn. of #reads processed will be collected every this # of reads
        public bool sampleAccuFilteredExonMols = false; // Sample #EXON Mols after mutation filter as fn. of processed reads (slow)
        public bool WriteReadsAsGVFFiles = false;
        public string[] CAPCloseSiteSearchCutters = new string[] { "PvuI" };
        public bool InsertCells10Data = false; // Set to true to insert results into cells10k database for "C1-" prefixed projects
        public bool AnalyzeGCContent = false; // Analyze the GC content of transcript mapping reads
        public bool LogMode = false;

        private Barcodes m_Barcodes;
        public Barcodes Barcodes {
            get { if (m_Barcodes == null) m_Barcodes = Barcodes.GetBarcodes(BarcodesName); return m_Barcodes; }
        }
        [NonSerialized]
        private string m_BarcodesName;
        public string BarcodesName
        {
            get { if (m_BarcodesName == null) m_BarcodesName = DefaultBarcodeSet; return m_BarcodesName; }
            set { m_BarcodesName = value; m_Barcodes = null; }
        }

        public MultiReadMappingType SelectedMappingType { get { return (DirectionalReads && UseMost5PrimeExonMapping) ? MultiReadMappingType.Most5Prime : DefaultExonMapping; } }

        public bool UseMaxAltMappings { get { return props.BowtieOptionPattern.Contains("MaxAlternativeMappings"); } }

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
            
            return props;
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
