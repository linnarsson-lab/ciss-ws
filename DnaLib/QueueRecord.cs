using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class QueueRecord : ICloneable
    {
        public static string WAITING = "Waiting";
        public static string PROCESSING = "Processing";
        public static string READY = "Ready";
        public static string FAILED = "FAILED";

        private string m_RunFolder;
        private string m_LaneNumbers;
        private string m_ProjectFolder;
        private string m_Species;
        private string m_Build;
        private string m_BarcodeSet;
        public string m_Status = WAITING;
        public int Id = 0;
        public string ResultSubPath;

        public string Status
        {
            get { return m_Status; }
            set { m_Status = value; }
        }
        /// <summary>
        /// Path to, or name of, the Run folder where Illumina qseq output files is located
        /// </summary>
        public string RunFolder
        {
            get { return m_RunFolder; }
            set { m_RunFolder = (value == null) ? "" : value; }
        }
        /// <summary>
        /// Name of the barcode used used for extraction of sequences
        /// </summary>
        public string BarcodeSet
        {
            get { return m_BarcodeSet; }
            set { m_BarcodeSet = (value == null) ? "" : value; }
        }
        /// <summary>
        /// A string containing, as characters, the numbers of the lanes to be extracted
        /// </summary>
        public string LaneNumbers
        {
            get { return m_LaneNumbers; }
            set { m_LaneNumbers = (value == null) ? "" : value.Replace(" ", ""); }
        }
        /// <summary>
        /// Path to, or name of, the Project folder where analysed data will be stored
        /// </summary>
        public string ProjectFolder
        {
            get { return m_ProjectFolder; }
            set { m_ProjectFolder = (value == null) ? "" : value; }
        }
        /// <summary>
        /// Abbreviation for the species of the samples, e.g. "Mm"
        /// </summary>
        public string Species
        {
            get { return m_Species; }
            set { m_Species = (value == null) ? "" : value; }
        }
        /// <summary>
        /// The name of the Bowtie index to be used for alignment
        /// </summary>
        public string Build
        {
            get { return m_Build; }
            set { m_Build = (value == null) ? "" : value; }
        }

        public Object Clone()
        {
            return this.MemberwiseClone() as QueueRecord;
        }

        public override string ToString()
        {
            string res = RunFolder + "\t" + LaneNumbers + "\t" + ProjectFolder + "\t" +
                         Species + "\t" + Build + "\t" + BarcodeSet + "\t" +
                         Status + "\t" + Id.ToString() + "\t" + ResultSubPath;
            return res;
        }

        /// <summary>
        /// Check if the other QueueRecords covered up by the current, i.e. has at
        /// least all the lane numbers in other and all other fields equal
        /// </summary>
        /// <param name="other"></param>
        /// <returns>true if other is redundant</returns>
        public bool Contains(QueueRecord other)
        {
            return (RunFolder == other.RunFolder && ProjectFolder == other.ProjectFolder &&
                    Species == other.Species && Build == other.Build &&
                    BarcodeSet == other.BarcodeSet &&
                    LaneNumbers.Contains(other.LaneNumbers));
        }

        public bool IsProcessing()
        {
            return Status == "Processing";
        }
        public void SetProcessing()
        {
            SetStatus("Processing");
        }
        public void SetStatus(string status)
        {
            Status = status;
        }
        public bool IsReady()
        {
            return Status == "Ready";
        }
        public bool IsWaiting()
        {
            return Status == "Waiting";
        }

    }
}
