using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;

namespace Linnarsson.Dna
{
    public class ExtractionTask
    {
        public string analysisId;
        public string projectName;
        public string barcodeSet;

        public ExtractionTask(string analysisId, string projectName, string barcodeSet)
        {
            this.analysisId = analysisId;
            this.projectName = projectName;
            this.barcodeSet = barcodeSet;
        }
    }



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
        public string plateId { get; set; }
        public string ProjectFolder { get { return Path.Combine(Props.props.ProjectsFolder, plateId); } }
        public string managerEmails { get; set; }
        public string[] runIdsLanes { get; set; }
        public int[] runNumbers { get; set; }
        public string barcodeSet { get; set; }
        public int SpikeMoleculeCount { get; set; }
        public bool analyzeVariants { get; set; }
        public string extractionVersion { get; set; }
        public string annotationVersion { get; set; }
        public bool rpkm { get; set; }
        public int readDirection { get; set; }
        public string layoutFile { get; set; }
        public string build { get; set; }
        public string annotation { get; set; }
        public string variant { get; set; }
        public string aligner { get; set; }
        public int gene5PrimeExtension { get; set; }
        public List<LaneInfo> laneInfos { get; set; }
        public string analysisId { get; set; }
        public List<ResultDescription> resultDescriptions { get; set; }

        [XmlIgnoreAttribute]
        public bool DirectionalReads { get { return readDirection != 0; } }
        [XmlIgnoreAttribute]
        public bool SenseStrandIsSequenced { get { return readDirection == 1; } }

        [XmlIgnoreAttribute]
        public string SampleLayoutPath { get { return Path.Combine(ProjectFolder, layoutFile); } }

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

        public string title { get; set; }
        public string tissue { get; set; }
        public string description { get; set; }
        public string protocol { get; set; }
        public string comment { get; set; }
        public string plateReference { get; set; }
        public string sampleType { get; set; }
        public string collectionMethod { get; set; }
        public string labBookPage { get; set; }
        [XmlIgnoreAttribute]
        public DateTime productionDate { get; set; }
        [XmlIgnoreAttribute]
        public int jos_aaacontactid { get; set; }
        [XmlIgnoreAttribute]
        public int jos_aaamanagerid { get; set; }
        [XmlIgnoreAttribute]
        public int jos_aaaclientid { get; set; }

        [XmlIgnoreAttribute]
        public int nSeqCycles { get; set; }
        [XmlIgnoreAttribute]
        public int nIdxCycles { get; set; }
        [XmlIgnoreAttribute]
        public int nPairedCycles { get; set; }
        [XmlIgnoreAttribute]
        public string seqPrimer { get; set; }
        [XmlIgnoreAttribute]
        public string idxPrimer { get; set; }
        [XmlIgnoreAttribute]
        public string pairedPrimer { get; set; }

        /// <summary>
        /// Default constructor needed only for serialization!!
        /// </summary>
        public ProjectDescription()
        { }

        /// <summary>
        /// Constructor when starting analysis of projects in database
        /// </summary>
        public ProjectDescription(string plateId, string barcodesName, string defaultSpecies, List<string> laneInfos,
                          string layoutFile, string status, string emails,
                          string defaultBuild, string variants, string aligner, string analysisId,
                          bool rpkm, int spikeMoleculeCount, int readdir)
        {
            this.plateId = plateId;
            this.barcodeSet = barcodesName;
            this.defaultSpecies = defaultSpecies;
            this.runIdsLanes = laneInfos.ToArray();
            this.runNumbers = new int[runIdsLanes.Length];
            this.layoutFile = layoutFile;
            this.status = status;
            this.managerEmails = emails;
            this.defaultBuild = defaultBuild;
            this.analyzeVariants = (variants == "all");
            this.aligner = aligner;
            this.rpkm = rpkm;
            this.readDirection = readdir;
            this.SpikeMoleculeCount = spikeMoleculeCount;
            this.analysisId = analysisId;
            this.resultDescriptions = new List<ResultDescription>();
        }

        /// <summary>
        /// Constructor for inserting new projects
        /// </summary>
        public ProjectDescription(int jos_aaacontactid, int jos_aaamanagerid, int jos_aaaclientid,
                    string title, DateTime productiondate, string plateid, string platereference, string species,
                    string tissue, string sampletype, string collectionmethod, string description, string protocol,
                    string barcodeSet, string labbookpage, string layoutFile, string comment, int spikeMoleculeCount)
        {
            this.jos_aaacontactid = jos_aaacontactid;
            this.jos_aaamanagerid = jos_aaamanagerid;
            this.jos_aaaclientid = jos_aaaclientid;
            this.title = title;
            this.productionDate = productiondate;
            this.plateId = plateid;
            this.plateReference = platereference;
            this.defaultSpecies = species;
            this.tissue = tissue;
            this.sampleType = sampletype;
            this.collectionMethod = collectionmethod;
            this.description = description;
            this.protocol = protocol;
            this.barcodeSet = barcodeSet;
            this.labBookPage = labbookpage;
            this.layoutFile = layoutFile;
            this.comment = comment;
            this.SpikeMoleculeCount = spikeMoleculeCount;
            this.status = STATUS_INQUEUE;
        }

        public void SetGenomeData(StrtGenome genome)
        {
            build = genome.Build;
            variant = genome.GeneVariants ? "all" : "single";
            annotation = genome.Annotation;
            gene5PrimeExtension = Props.props.GeneFeature5PrimeExtension;
        }

        public override string ToString()
        {
            return string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\n", plateId, string.Join("|", runIdsLanes.ToArray()),
                                 barcodeSet, defaultSpecies, ProjectFolder, layoutFile, status, managerEmails);
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
