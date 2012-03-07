using System;
using System.Linq;
using Linnarsson.Utilities;
using System.IO;

namespace Linnarsson.Dna
{
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
        public string GenomesFolder = "C:\\data\\genomes";
        public string RunsFolder = "C:\\data\\runs"; // Where Illumina raw data are stored
        public string ReadsFolder = "\\\\192.168.1.12\\data\\reads"; // Where FastQ files of concatenated reads for each lane are gathered
        public string ProjectsFolder = "C:\\data\\strt";
        public string UploadsFolder = "C:\\data\\uploads";
        public string ResultDownloadUrl = "strtserver@192.168.1.3:/srv/www/htdocs/strt/";
        public string ResultDownloadFolderHttp = "http://linnarsson.mbb.ki.se/strt/";
        public string FailureReportEmail = "peter.lonnerberg@ki.se";
        public string ProjectDBProcessorNotifierEmailSender = "peter.lonnerberg@ki.se";
        public string BowtieIndexFolder = "\\\\192.168.1.12\\sequtils\\bowtie-0.12.7\\indexes";
        public string OutputDocFile = "\\\\192.168.1.12\\data\\strt\\STRTOutputManual.pdf";
        public int BkgBackuperStartHour = 17;
        public int BkgBackuperStopHour = 8;

        public bool DebugAnnotation = false; // Will give output files of non-annotated and non-exon reads
        public bool GenerateWiggle = true; // Generate wiggle files for upload to UCSC Genome Browser
        public bool GenerateBarcodedWiggle = false; // Generate wiggle files per barcode for upload to UCSC Genome Browser
        public bool AnalyzeSNPs = true;
        public bool DetermineMotifs = false; // Analyse over-represented sequence motifs around read start
        public string[] SeqStatsChrIds = null; // Used to limit detailed statistics to only a subset of chromosomes
        public string[] GenesToPaint =
            new string[] { "Sox2" ,"Actb", "Nanog", "Klf4", "Calb1", "Rnr2", "Tmpo", "Trpm6", "Pou5f1",
                           "Rnr1", "Nd1", "Cox2", "Vcam1", "Zfp42", "Fgf2r", "Nt5e", "Runx2", "Taz",
                           "Osx", "Twist", "Ap1", "Sox9", "Sox6", "Sox5", "Bmp2", "Smad1", "Smad4" };
        public string[] GenesToShowRndTagProfile =
            new string[] { "Sox2" ,"Actb", "Nanog" };
        public bool SnpRndTagVerification = false;
        public int MinPhredScoreInRandomTag = 17;
        public int MinMoleculesToTestSnp = 4; // SNP analysis minimum coverage to test a potential SNP positions (when using random labels)
        public int MinReadsToTestSnp = 10; // SNP analysis minimum coverage to test a potential SNP positions (without random labeled barcodes)
        public bool GenerateTranscriptProfiles = false;
        public bool GenerateGeneLocusProfiles = false;
        public bool GenerateGeneProfilesByBarcode = false;
        public bool AnalyzeAllGeneVariants = true; // Analyze all alternative splice sites in exons etc.
        public bool DirectionalReads = true; // STRT are always directional reads
        public bool UseRPKM = false; // Give RPKM instead of RPM in output files for non-STRT samples
        public string DefaultBarcodeSet = "v4"; // This is the default barcode set
        public int LocusFlankLength = 1000; // Maximum length of UPSTREAM and DOWNSTREAM regions to analyse
        public int StandardReadLen = 50; // Better not use actual reads that are longer - otherwise some junction hits may be missed
        public int MaxAlignmentMismatches = 3;  // Should be the value used in bowtie calls
        public int MaxExonsSkip = 12; // Max number of exons to consider for splice out in junction chromosome
        public bool AnalyzeExtractionQualities = false; // Analyze read quality and color balance
        public int MinExtractionInsertLength = 25; // Min acceptable read length excluding barcode and GGG
		public int MinExtractionInsertNonAs = 5; // Min number of C/G/T in an acceptable read
        public int LargestPossibleReadLength = 300; // Used for dimensioning extraction quality calculators
        public int CapRegionSize = 200; // Used for elongation efficiency (full length cDNA) estimation.
		public byte QualityScoreBase = 64; // For ASCII-encoding of phred scores (if you change this, then also change Bowtie options below)
        public string BowtieOptions = "--phred64-quals -a -v MaxAlignmentMismatches --best --strata";
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

        [NonSerialized]
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
