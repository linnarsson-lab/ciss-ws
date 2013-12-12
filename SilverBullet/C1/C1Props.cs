using System;
using System.Linq;
using System.IO;
using Linnarsson.Utilities;

namespace C1
{
    /// <summary>
    /// Defines various configurations used by many classes.
    /// Defaults values are given here, but actual data is read from an XML file
    /// in the directory where the executables are located.
    /// Note that C1Props.props is a singleton.
    /// </summary>
    [Serializable]
    public sealed class C1Props
    {
        [NonSerialized]
        public static readonly string configFilename = "C1Config.xml"; // Filename of machine specific Props
        [NonSerialized]
        public static readonly string C1ProjectPrefix = "C1-"; // Prefix used for cells10k plates in ProjectDB.

        public string C1StandardBarcodeSet = "Tn5UMI6";
        public string MySqlServerIP = "130.237.117.141";
        public string C1SeqPlatesFolder = "/data2/c1-seqplates";
        public string C1SeqPlateFilenamePattern = "*.txt";
        public string C1DonorDataFilenamePattern = "mice_metadata_*.txt";
        public string C1MetadataFilenamePattern = "C1_metadata*.txt";
        public string C1RunsFolder = "/data2/c1-runs";
        public string C1CaptureFilenamePattern = "capture_rep*.txt";
        public string GeneOntologySubPath = "gene_ontology/go-basic.obo";
        public string C1BFImageSubfoldernamePattern = "BF_*";
        public string[] C1AllImageSubfoldernamePatterns = new string[] { "BF_*" } ; //, "Filter1_*", "Filter2_*", "Filter3_*" };
        public string C1ImageFilenamePattern = "well_*_center.png";
        public int SpikeMoleculeCount = 27900;
        public string AutoAnalysisMailRecepients = "peter.lonnerberg@ki.se";
        public string AutoAnalysisBuild = "UCSC";
        public string AutoAnalysisBuildVariants = "single";
        public string WellExcludeFilePattern = "wells_to_exclude*.txt";
        public string C1BarcodeSet1 = "Tn5UMI6";
        public string C1BarcodeSet2 = "Tn5UMI6Plate2";
        public int C1RequiredSeqCycles = 50;
        public int C1RequiredIdxCycles = 8;
        public string C1SeqPrimer = "C1-P1-SEQ";
        public string C1IdxPrimer = "Illumina index primer";

        private static C1Props Read()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string configFilePath = Path.Combine(appDir, configFilename);
            C1Props props = null;
            try
            {
                props = SimpleXmlSerializer.FromXmlFile<C1Props>(configFilePath);
            }
            catch (FileNotFoundException)
            {
                props = new C1Props();
                SimpleXmlSerializer.ToXmlFile(configFilePath, props);
            }
            return props;
        }

        // Singleton stuff below
        C1Props()
        {
        }
        public static C1Props props
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
            internal static readonly C1Props instance = Read();
        }

    }

}
