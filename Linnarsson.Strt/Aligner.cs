using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Utilities;

namespace Linnarsson.Strt
{
    public abstract class Aligner
    {
        protected StrtGenome genome;
        protected readonly string alignerLogFilename = "aligner_output.txt";
        protected string outFileExtension; // Including the '.', e.g. '.sam'
        protected string indexTestPattern; // Regex pattern. '(.+)' indicates location of splcIndexName (incl readLen)
        protected string indexCmd;
        protected string indexArgs;
        protected string indexChrsDelimiter; // For use by indexCmd: ',' or ' '
        protected string alignCmd;

        public static Aligner GetAligner(StrtGenome genome)
        {
            if (Props.props.Aligner.ToUpper() == "STAR")
                return new STARAligner(genome);
            return new BowtieAligner(genome);
        }

        abstract protected string MakeMapFolderName();

        /// <summary>
        /// Called after all alignment files have been generated
        /// </summary>
        protected virtual void Cleanup()
        {
        }

        /// <summary>
        /// Find a splice index with read length matching or slightly less than genome.ReadLen. Return "" on failure
        /// </summary>
        /// <returns>Empty string if none found</returns>
        public string FindASplcIndexName()
        {
            string pathPattern = Path.Combine(GetIndexFolder(), indexTestPattern.Replace("(.+)", genome.GetSplcIndexName("#")));
            string indexName = Path.GetFileName(genome.FindABpVersion(pathPattern));
            if (indexName != "")
                indexName = Regex.Match(indexName, indexTestPattern).Groups[1].Value;
            return indexName;
        }

        public void BuildIndex()
        {
            string indexFolder = GetIndexFolder();
            if (!Directory.Exists(indexFolder))
                Directory.CreateDirectory(indexFolder);
            DateTime startTime = DateTime.Now;
            string chrFileList = string.Join(indexChrsDelimiter, genome.GetMaskedChrPaths());
            string mainIndexName = genome.GetMainIndexName();
            string mainIndexPath = Path.Combine(indexFolder, mainIndexName);
            if (File.Exists(Path.Combine(indexFolder, string.Format(indexTestPattern, mainIndexName))))
                Console.WriteLine("NOTE: Main index {0} already exists. Delete index and rerun if you want to rebuild.", mainIndexPath);
            else
            {
                MakeIndex(chrFileList, mainIndexPath);
            }
            string junctionChrPath = genome.GetJunctionChrPath();
            if (junctionChrPath != null)
            {
                string splcIndexName = genome.GetSplcIndexName();
                string splcIndexPath = Path.Combine(indexFolder, splcIndexName);
                MakeIndex(junctionChrPath, splcIndexPath);
            }
            else
                Console.Error.WriteLine("NOTE: No splice chromosome found for {0}. Indexing skipped.", mainIndexName);
        }

        private void MakeIndex(string chrFileList, string indexPath)
        {
            string args = indexArgs.Replace("$NThreads", Props.props.NumberOfAlignmentThreadsDefault.ToString());
            args = args.Replace("$FastaPaths", chrFileList);
            args = args.Replace("$IndexPath", indexPath);
            if (args.Contains("$IndexDir"))
            {
                Directory.CreateDirectory(indexPath);
                args = args.Replace("$IndexDir", indexPath);
            }
            Console.WriteLine("*** Build of {0} index {1} started {2} ***", Props.props.Aligner, indexPath, DateTime.Now);
            Console.WriteLine("{0} {1}", indexCmd, args);
            int exitCode = CmdCaller.Run(indexCmd, args);
            if (exitCode != 0)
                Console.Error.WriteLine("Failed to build aligner index. ExitCode={0}", exitCode);
        }

        /// <summary>
        /// Create any missing alignment files needed for given genome and wells defined by barcodes/species.
        /// </summary>
        /// <param name="laneInfo">Paths to all needed map files will be stored in laneInfo.mappedFilePaths</param>
        /// <param name="splcIndexVersion"></param>
        /// <param name="genomeBcIndexes">only these barcodes will be processed</param>
        public void CreateAlignments(LaneInfo laneInfo, int[] genomeBcIndexes)
        {
            laneInfo.SetMapFolderName(MakeMapFolderName());
            string mapFolder = laneInfo.mappedFileFolder;
            if (!Directory.Exists(mapFolder))
                Directory.CreateDirectory(mapFolder);
            laneInfo.alignerLogFilePath = Path.Combine(mapFolder, alignerLogFilename);
            List<string> outPaths = new List<string>();
            string mainIndexName = genome.GetMainIndexName();
            string splcIndexName = genome.GetSplcIndexName();
            foreach (string fqPath in laneInfo.extractedFilePaths)
            {
                int bcIdx = int.Parse(Path.GetFileNameWithoutExtension(fqPath));
                if (Array.IndexOf(genomeBcIndexes, bcIdx) == -1)
                    continue;
                string outUnmappedPath = Path.Combine(mapFolder, string.Format("{0}.fq-{1}", bcIdx, mainIndexName));
                string outMainPath = Path.Combine(mapFolder, string.Format("{0}_{1}{2}", bcIdx, mainIndexName, outFileExtension));
                if (!File.Exists(outMainPath))
                    CreateAlignmentOutputFile(mainIndexName, fqPath, outMainPath, outUnmappedPath, laneInfo.alignerLogFilePath);
                outPaths.Add(outMainPath);
                string outSplcFilename = string.Format("{0}_{1}{2}", bcIdx, splcIndexName, outFileExtension);
                string splcFilePat = string.Format("{0}_{1}{2}", bcIdx, genome.GetSplcIndexNamePattern(), outFileExtension);
                string[] existingSplcFiles = Directory.GetFiles(mapFolder, splcFilePat);
                string outSplcPath = Path.Combine(mapFolder, outSplcFilename);
                if (existingSplcFiles.Length >= 1)
                    outSplcPath = Path.Combine(mapFolder, existingSplcFiles[0]);
                else
                {
                    if (!File.Exists(outUnmappedPath))
                        CreateAlignmentOutputFile(mainIndexName, fqPath, outMainPath, outUnmappedPath, laneInfo.alignerLogFilePath);
                    string remainUnmappedPath = Props.props.SaveNonMappedReads ? Path.Combine(mapFolder, bcIdx + ".fq-nonmapped") : "";
                    if (File.Exists(outUnmappedPath)) // Have to check - all reads may have mapped directly
                        CreateAlignmentOutputFile(splcIndexName, outUnmappedPath, outSplcPath, remainUnmappedPath, laneInfo.alignerLogFilePath);
                }
                outPaths.Add(outSplcPath);
                // Don't delete the fqUnmappedReadsPath - it is needed if rerun with changing all/single annotation versions
                if (Background.CancellationPending) break;
            }
            laneInfo.mappedFilePaths = outPaths.ToArray();
            Cleanup();
        }

        /// <summary>
        /// Run aligner to produce a .sam/.map file.
        /// </summary>
        /// <param name="indexName"></param>
        /// <param name="fqPath"></param>
        /// <param name="outPath"></param>
        /// <param name="outUnmappedPath"></param>
        /// <param name="alignerLogFile"></param>
        abstract protected void CreateAlignmentOutputFile(string alignerIndex, string inputFqReadPath, string outputPath,
                                                          string outputFqUnmappedReadPath, string alignerLogFile);

        protected bool Align(string args, string indexName, string fqPath, string outPath, string alignerLogFile)
        {
            string indexPath = Path.Combine(GetIndexFolder(), indexName);
            args = args.Replace("$MaxAlignmentMismatches", Props.props.MaxAlignmentMismatches.ToString());
            args = args.Replace("$QualityScoreBase", Props.props.QualityScoreBase.ToString());
            args = args.Replace("$MaxAlternativeMappings", Props.props.MaxAlternativeMappings.ToString());
            args = args.Replace("$NThreads", Props.props.NumberOfAlignmentThreadsDefault.ToString());
            args = args.Replace("$QualityScoreBase", Props.props.QualityScoreBase.ToString());
            args = args.Replace("$IndexPath", indexPath);
            args = args.Replace("$FqPath", '"' + fqPath + '"');
            args = args.Replace("$OutPath", '"' + outPath + '"');
            args = args.Replace("$OutFolder", '"' + Path.GetDirectoryName(outPath) + '"');
            StreamWriter logWriter = new StreamWriter(alignerLogFile, true);
            logWriter.WriteLine("--- {0} {1} ---", alignCmd, args);
            logWriter.Flush();
            CmdCaller cc = new CmdCaller(alignCmd, args);
            logWriter.WriteLine(cc.StdError);
            logWriter.Close();
            if (cc.ExitCode != 0)
            {
                Console.Error.WriteLine("{0} {1}\nFailed to run on {2}. ExitCode={3}. Check logFile.",
                                        Props.props.Aligner, args, fqPath, cc.ExitCode);
                if (File.Exists(outPath))
                    File.Delete(outPath);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Get/Make path to aligner indexes, e.g. GenomeFolder/strt/UCSC141125/bowtie".
        /// If genome had no AnnotationDate and no strt annotation folder exists, return null.
        /// </summary>
        /// <returns></returns>
        public string GetIndexFolder()
        {
            string strtAnnotFolder = genome.GetStrtAnnotFolder();
            return (strtAnnotFolder != null) ? Path.Combine(strtAnnotFolder, Props.props.Aligner) : null;
        }

    }

    public class BowtieAligner : Aligner
    {
        public BowtieAligner(StrtGenome genome)
        {
            this.genome = genome;
            outFileExtension = ".map";
            indexTestPattern = "(.+).1.ebwt";
            indexCmd = "bowtie-build";
            indexArgs = Props.props.BowtieIndexArgs;
            indexChrsDelimiter = ",";
            alignCmd = "bowtie";
        }

        protected override string MakeMapFolderName()
        {
            return genome.Build + "_" + genome.Annotation + "_" + genome.AnnotationDate;
        }

        protected override void CreateAlignmentOutputFile(string indexName, string fqPath, string outPath,
                                                          string outUnmappedPath, string alignerLogFile)
        {
            if (!File.Exists(fqPath) && File.Exists(fqPath + ".gz"))
                CmdCaller.Run("gunzip", fqPath + ".gz");
            string args = Props.props.BowtieAlignArgs;
            if (outUnmappedPath != "")
            {
                string crapMaxPath = Path.Combine(Path.GetDirectoryName(outUnmappedPath), "aligner_maxM_reads_map.temp");
                string unmappedArg = string.Format(" --un {0} --max {1} ", outUnmappedPath, crapMaxPath);
                args = unmappedArg + args;
            }
            Align(args, indexName, fqPath, outPath, alignerLogFile);
        }

    }

    public class STARAligner : Aligner
    {
        public STARAligner(StrtGenome genome)
        {
            this.genome = genome;
            outFileExtension = ".sam";
            indexTestPattern = "(.+)";
            indexCmd = "STAR";
            indexArgs = Props.props.StarIndexArgs;
            indexChrsDelimiter = " ";
            alignCmd = "STAR";
        }

        protected override string MakeMapFolderName()
        {
            return "STAR_" + genome.Build + "_" + genome.Annotation + "_" + genome.AnnotationDate;
        }

        protected override void Cleanup()
        {
            if (Props.props.StarAlignArgs.Contains("--genomeLoad LoadAndKeep"))
                new CmdCaller(alignCmd, "--genomeLoad Remove");
        }

        protected override void CreateAlignmentOutputFile(string indexName, string fqPath, string outPath,
                                                          string outUnmappedPath, string alignerLogFile)
        {
            string args = Props.props.StarAlignArgs;
            if (!File.Exists(fqPath) && File.Exists(fqPath + ".gz"))
            {
                args = " --readFilesCommand zcat " + args;
                fqPath = fqPath + ".gz";
            }
            if (outUnmappedPath != "")
                args = " --outReadsUnmapped Fastx " + args;
            bool success = Align(args, indexName, fqPath, outPath, alignerLogFile);
            if (!success)
                return;
            string outFolder = Path.GetDirectoryName(outPath);
            File.Delete(outPath);
            File.Move(Path.Combine(outFolder, "Aligned.out.sam"), outPath);
            if (outUnmappedPath != "")
            {
                File.Delete(outUnmappedPath);
                File.Move(Path.Combine(outFolder, "Unmapped.out.mate1"), outUnmappedPath);
            }
            using (StreamWriter logWriter = new StreamWriter(alignerLogFile, true))
            {
                logWriter.Write(Path.Combine(outFolder, "Log.final.out").OpenRead().ReadToEnd());
                logWriter.WriteLine();
            }
        }
    }
}

