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
        public string analysisid { get; set; }
        public string analysisname { get; set; }
        public string ProjectFolder { get { return Path.Combine(Props.props.ProjectsFolder, analysisname); } }
        public string plateid { get; set; }
        public string sample { get; set; }
        public string emails { get; set; }
        public string[] runIdsLanes { get; set; }
        public string barcodeset { get; set; }
        public int spikemolecules { get; set; }
        public bool analyzeVariants { get; set; }
        public string extractionVersion { get; set; }
        public string annotationVersion { get; set; }
        public string rpkm { get; set; }
        public bool isRpkm { get { return rpkm == "1"; } }
        public int readdir { get; set; }
        public string layoutfile { get; set; }
        public string build { get; set; }
        public string annotation { get; set; }
        public string variant { get; set; }
        public string aligner { get; set; }
        public string comment { get; set; }
        public string user { get; set; }

        public List<LaneInfo> laneInfos { get; set; }
        public int laneCount { get { return laneInfos.Count; } }
        public List<ResultDescription> resultDescriptions { get; set; }

        [XmlIgnoreAttribute]
        public bool DirectionalReads { get { return readdir != 0; } }
        [XmlIgnoreAttribute]
        public bool SenseStrandIsSequenced { get { return readdir == 1; } }

        [XmlIgnoreAttribute]
        public string layoutpath { get { return Path.Combine(ProjectFolder, layoutfile); } }

        [XmlIgnoreAttribute]
        public string defaultBuild { get; set; }
        [XmlIgnoreAttribute]
        public string defaultSpecies;
        [XmlIgnoreAttribute]
        public string status;
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
        /// Constructor when starting analysis of projects in database
        /// </summary>
        public ProjectDescription(string analysisname, string plateid, string sample,
                          string barcodeset, string defaultSpecies, List<string> laneInfos,
                          string layoutfile, string status, string emails,
                          string defaultBuild, string variants, string aligner, string analysisid,
                          string rpkm, int spikemolecules, int readdir, string comment, string user)
        {
            this.analysisid = analysisid;
            this.analysisname = analysisname;
            this.plateid = plateid;
            this.sample = sample;
            this.barcodeset = barcodeset;
            this.defaultSpecies = defaultSpecies;
            this.runIdsLanes = laneInfos.ToArray();
            this.layoutfile = layoutfile;
            this.status = status;
            this.emails = emails;
            this.defaultBuild = defaultBuild;
            this.analyzeVariants = (variants == "all");
            this.aligner = aligner;
            this.rpkm = rpkm;
            this.spikemolecules = spikemolecules;
            this.readdir = readdir;
            this.comment = comment;
            this.user = user;
            this.resultDescriptions = new List<ResultDescription>();
        }

        public void SetGenomeData(StrtGenome genome)
        {
            build = genome.Build;
            variant = genome.GeneVariants ? "all" : "single";
            annotation = genome.Annotation;
        }

        public int LaneCount
        {
            get
            {
                int n = 0;
                foreach (string laneArg in runIdsLanes)
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
