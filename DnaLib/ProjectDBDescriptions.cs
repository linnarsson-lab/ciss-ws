using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;

namespace Linnarsson.Dna
{
    [Serializable()]
    public class ResultDescription
    {
        public enum ResultStats { UnknownBc, Tn5, AverageBcMolCount };
        private Dictionary<ResultStats, double> stats = new Dictionary<ResultStats, double>();
        public double getStat(ResultStats category)
        {
            return stats[category];
        }
        public void setStat(ResultStats category, double value)
        {
            stats[category] = value;
        }

        public string build { get; set; }
        public string annotAndDate { get; set; }
        public string variants { get; set; }
        public string splcIndexVersion { get; set; }
        public string resultFolder { get; set; }
        public List<string> mapFileFolders { get; set; }

        public ResultDescription() { }
        public ResultDescription(List<string> mapFilePaths, StrtGenome genome, string resultFolder)
        {
            build = genome.Build;
            annotAndDate = genome.Annotation + genome.AnnotationDate;
            variants = genome.GeneVariants ? "all" : "single";
            splcIndexVersion = genome.GetSplcIndexAndDate();
            this.resultFolder = resultFolder;
            this.mapFileFolders = new List<string>();
            foreach (string mapFilePath in mapFilePaths)
            {
                string mapFileFolder = Path.GetDirectoryName(mapFilePath);
                if (!mapFileFolders.Contains(mapFileFolder)) mapFileFolders.Add(mapFileFolder);
            }
        }
    }

    [Serializable()]
    public class ProjectDescription
    {
        public string analysisname { get; set; }               // E.g. 'C1-1772177-293' or '1772-177-293_CellsGCB_01'
        public string plateid { get; set; }                    // Plate or chip id, e.g. 'C1-1772177-293' or '1772-177-293'
        public string sample { get; set; }                     // Sample id, e.g. 'CellsGCB_01' (newer database)
        public string emails { get; set; }
        public string[] laneArgs { get; set; }
        public string barcodeset { get; set; }
        public int spikemolecules = Props.props.TotalNumberOfAddedSpikeMolecules;
        public string extractionVersion { get; set; }
        public string annotationVersion { get; set; }
        public string layoutfile { get; set; }
        public string build { get { return genome.Build; } }
        public string annotation { get { return genome.Annotation; } }
        public string variant { get { return genome.GeneVariants ? "all" : "single"; } }
        public string rpkm = Props.props.UseRPKM ? "1" : "0";
        public int readdir = Props.props.DirectionalReads ? (Props.props.SenseStrandIsSequenced ? 1 : -1) : 0;
        public string aligner = Props.props.Aligner;
        public string comment { get; set; }
        public string user { get; set; }

        public List<LaneInfo> laneInfos { get; set; }

        private List<ResultDescription> resultDescriptions;
        public List<ResultDescription> ResultDescriptions { get { return resultDescriptions; } }

        [XmlIgnoreAttribute]
        public int laneCount { get { return laneInfos.Count; } }
        [XmlIgnoreAttribute]
        public bool UseRPKM { get { return rpkm == "1"; } }
        [XmlIgnoreAttribute]
        public bool DirectionalReads { get { return readdir != 0; } }
        [XmlIgnoreAttribute]
        public bool SenseStrandIsSequenced { get { return readdir == 1; } }
        [XmlIgnoreAttribute]
        public string ProjectFolder { get { return Path.Combine(Props.props.ProjectsFolder, analysisname); } }
        [XmlIgnoreAttribute]
        public string layoutpath { get { return Path.Combine(ProjectFolder, layoutfile); } }
        [XmlIgnoreAttribute]
        public string defaultSpecies { get; set; }
        [XmlIgnoreAttribute]
        public string defaultBuild { get; set; }
        [XmlIgnoreAttribute]
        public bool defaultVariants { get; set; }
        [XmlIgnoreAttribute]
        public string status;

        [XmlIgnoreAttribute]
        public string dbanalysisid { get; set; }               // Database id of the analysis
        [XmlIgnoreAttribute]
        public string dbchipid { get; set; }                   // Database id of the chip or plate
        [XmlIgnoreAttribute]
        public string dbsampleid { get; set; }                 // Database id of the sample (newer database)

        [XmlIgnoreAttribute]
        public StrtGenome genome { get; set; }

        public static readonly string STATUS_INQUEUE = "inqueue";
        public static readonly string STATUS_PROCESSING = "processing";
        public static readonly string STATUS_EXTRACTING = "extracting";
        public static readonly string STATUS_ALIGNING = "aligning";
        public static readonly string STATUS_ANNOTATING = "annotating";
        public static readonly string STATUS_READY = "ready";
        public static readonly string STATUS_FAILED = "failed";

        /// <summary>
        /// Default constructor needed only for serialization!!
        /// </summary>
        public ProjectDescription()
        { }

        /// <summary>
        /// Constructor when starting an analysis from database
        /// </summary>
        public ProjectDescription(string analysisname, string plateid, string sample,
                          string barcodeset, string defaultSpecies, List<string> laneArgs,
                          string layoutfile, string status, string emails,
                          string defaultBuild, string defaultVariants, string aligner,
                          string dbanalysisid, string dbchipid, string dbsampleid,
                          string rpkm, int spikemolecules, int readdir, string comment, string user)
        {
            this.dbanalysisid = dbanalysisid;
            this.dbchipid = dbchipid;
            this.dbsampleid = dbsampleid;
            this.analysisname = analysisname;
            this.plateid = plateid;
            this.sample = sample;
            this.barcodeset = barcodeset;
            this.defaultSpecies = defaultSpecies;
            this.laneArgs = laneArgs.ToArray();
            this.layoutfile = layoutfile;
            this.status = status;
            this.emails = emails;
            this.defaultBuild = defaultBuild;
            this.defaultVariants = (defaultVariants == "all");
            this.aligner = aligner;
            this.rpkm = rpkm;
            this.spikemolecules = spikemolecules;
            this.readdir = readdir;
            this.comment = comment;
            this.user = user;
            this.resultDescriptions = new List<ResultDescription>();
        }

        public void AddResultDescription(ResultDescription rd)
        {
            resultDescriptions.Add(rd);
        }

        public int LaneCount
        {
            get
            {
                int n = 0;
                foreach (string laneArg in laneArgs)
                    n += laneArg.Split(':')[1].Length;
                return n;
            }
        }

    }

    [Serializable()]
    public class MailTaskDescription
    {
        public string id { get; set; }
        public string runNo { get; set; }
        public string laneNo { get; set; }
        public string email { get; set; }
        public string status { get; set; }

        public MailTaskDescription(string id, string runNo, string laneNo, string email, string status)
        {
            this.id = id;
            this.runNo = runNo;
            this.laneNo = laneNo;
            this.email = email;
            this.status = status;
        }
    }
}
