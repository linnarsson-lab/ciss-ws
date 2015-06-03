using System;
using System.Linq;
using System.IO;
using System.Configuration;
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
    public class C1Props
    {
        [NonSerialized]
        public static readonly string configFilename = "C1Config.xml"; // Filename of machine specific Props
        [NonSerialized]
        public static readonly string C1ProjectPrefix = "C1-"; // Prefix used for cells10k plates in ProjectDB.
        // ConnectionString is now imported from the "SB.exe.config" file, where it is encrypted, see below.
        public string MySQlConnectionString = "server=127.0.0.1;uid=user;pwd=password;database=c1db;Connect Timeout=300;Charset=utf8;";
        public string C1RunsFolder = "/data2/c1-runs";
        public string C1CaptureFilenamePattern = "capture_rep*.txt";
        public string GeneOntologySubPath = "gene_ontology/go-basic.obo";
        // The star last in these patterns is understood as [0-9]+$ on a RegEx basis
        public string C1BFImageSubfoldernamePattern = "BF*";
        public string[] C1AllImageSubfoldernamePatterns = new string[] { "BF*", "filter3_red*", "green_filter1*", "filter1_green*",
                                                                         "pcr_green*", "green*" };
        public string C1ImageFilenamePattern = "well_*_center.png";
        public string WellExcludeFilePattern = "wells_to_exclude*.txt";
        public string WellMarkerFilePattern = "wells_pos*tive_COLOR*.txt"; // COLOR is green, red, blue
        public bool ConvertChipIds = true;

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
            SetConnectionStrings(props);
            return props;
        }

        private static void SetConnectionStrings(C1Props props)
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
                ConnectionStringSettings settings = section.ConnectionStrings["SB.Properties.Settings.C1DBConnString"];
                if (settings != null)
                    props.MySQlConnectionString = settings.ConnectionString;
            }
            catch (Exception)
            {
                Console.WriteLine("Warning: C1Props could not load encrypted DB connection setup");
            }
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
