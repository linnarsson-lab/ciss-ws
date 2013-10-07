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

        public string C1RunsFolder = "/data2/c1-runs";
        public string C1CaptureFilenamePattern = "capture_rep*.txt";
        public string C1MetadataFilenamePattern = "*metadata*.txt";
        public string GeneOntologySubPath = "gene_ontology/go-basic.obo";
        public string C1BFImageSubfoldernamePattern = "BF_*";
        public string[] C1AllImageSubfoldernamePatterns = new string[] { "BF_*" } ; //, "Filter1_*", "Filter2_*", "Filter3_*" };
        public string C1ImageFilenamePattern = "well_*_center.png";
        public int SpikeMoleculeCount = 27900;
        public string AutoAnalysisMailRecepients = "peter.lonnerberg@ki.se";
        public string AutoAnalysisBuild = "UCSC";
        public string AutoAnalysisBuildVariants = "single";

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
