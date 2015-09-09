﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    /// <summary>
    /// Handles files that define genome contents. The genome folder of each organism contains two subfolders, one for
    /// original chr and annotation files, one with STRT specific files generated by the AnnotationBuilder.
    /// </summary>
	public class StrtGenome
	{
        private static string AnnotationsFilePattern = "Annotations_*_#bp.txt";
        private static string AnnotationsBuildRegex = "Annotations_[as]([^_]+)_.+txt";
        private static string AnnotationsBuildPattern = "Annotations_*.txt";

        public static string buildMatchPat = "^([A-Za-z]+)[0-9\\.]+$";
        public static string[] AnnotationSources = new string[] { "UCSC", "VEGA", "ENSE", "RFSQ", "UALL", "GENC" };
        public readonly static string DefaultAnnotationSource = "UCSC";
        /// <summary>
        /// Ids of chromosomes shared by all genomes. Includes spike CTRL:s and possibly EXTRA genes like GFP and other markers.
        /// </summary>

        public override string ToString()
        {
            return "Abrev=" + Abbrev + " Build=" + Build + " VarAnnot=" + VarAnnot;
        }
        public int SplcIndexReadLen { get; set; }
		public string Name { get; set; }
        public string Abbrev { get; set; }
		public string LatinName { get; set; }
		public string Description { get; set; }
        /// <summary>
        /// Version of the genome build, e.g. "mm9.2" or "hg19"
        /// </summary>
        public string Build { get; set; }

        private string m_Annotation;
        /// <summary>
        /// Source of annotations, e.g. "UCSC" or "VEGA"
        /// </summary>
        public string Annotation
        { 
            get { return m_Annotation; }
            set 
            {
                if (value.Length >= 5 && "as".Contains(value[0]))
                {
                    m_Annotation = value.Substring(1);
                    GeneVariants = (value[0] == 'a');
                }
                else
                    m_Annotation = value;
            }
        }

        public string AnnotationDate { get; set; }
        public DateTime AnnotationDateTime {
            get
            {
                return new DateTime(int.Parse(AnnotationDate.Substring(0, 2)),
                                    int.Parse(AnnotationDate.Substring(2, 4)),
                                    int.Parse(AnnotationDate.Substring(4, 6)));
            }
        }
        public bool GeneVariants { get; set; }
        public string GeneVariantsChar { get { return GeneVariants ? "a" : "s"; } }
        public string VarAnnot { get { return GeneVariantsChar + Annotation; } }
        public string BuildVarAnnot { get { return Build + "_" + VarAnnot; } }

        public string GetOriginalGenomeFolder()
        {
            return Path.Combine(Path.Combine(Props.props.GenomesFolder, Build), "genome");
        }

        public Dictionary<string, string> GetOriginalGenomeFilesMap()
        {
            string[] chrFiles = Directory.GetFiles(GetOriginalGenomeFolder(), "chr*");
            Dictionary<string, string> chrIdToFileMap = new Dictionary<string, string>();
            foreach (string filePath in chrFiles)
            {
                string filename = Path.GetFileName(filePath);
                if (IsChrInBuild(Path.GetFileName(filename)) && !filename.Contains("rmsk"))
                {
                    string chrId = ExtractChrId(filename);
                    chrIdToFileMap[chrId] = filePath;
                }
            }
            return chrIdToFileMap;
        }
        private string ExtractChrId(string filename)
        {
            Match m = Regex.Match(filename, "chromosome\\.([^\\.]+)\\.");
            if (!m.Success)
                m = Regex.Match(filename, "chr_?([^\\.]+)\\.");
            string chrId = m.Groups[1].Value;
            if (chrId.IndexOf(Annotation) >= 0) chrId = Annotation;
            return chrId;
        }

        /// <summary>
        /// Return path to strt subfolder, e.g. GenomesFolder/mm10/strt
        /// </summary>
        /// <returns></returns>
        public string GetGenomeStrtFolder()
        {
            return GetGenomeStrtFolder(Build);
        }
        public static string GetGenomeStrtFolder(string build)
        {
            return Path.Combine(Path.Combine(Props.props.GenomesFolder, build), "strt");
        }

        /// <summary>
        /// Get/Construct the path of a STRT annotations/chr folder, e.g. "GenomesFolder/mm10/strt/UCSC141211".
        /// If AnnotationDate was null, return path of the last existing matching folder and set AnnotationDate.
        /// Return null if AnnotationDate was null and no folder exists.
        /// </summary>
        /// <returns></returns>
        public string GetStrtAnnotFolder()
        {
            string strtFolder = GetGenomeStrtFolder();
            if (AnnotationDate == null)
            {
                string[] annotFolders = Directory.GetDirectories(strtFolder, Annotation + "??????");
                if (annotFolders.Length == 0)
                    return null;
                Array.Sort(annotFolders);
                string lastAnnotFolderName = Path.GetFileName(annotFolders[annotFolders.Length - 1]);
                AnnotationDate = lastAnnotFolderName.Substring(Annotation.Length);
            }
            return Path.Combine(strtFolder, Annotation + AnnotationDate);
        }

        /// <summary>
        /// Find path of the, last or specified AnnotationDate, aligner index folder that match with the genome settings.
        /// Return "" on failure.
        /// </summary>
        /// <returns></returns>
        public string FindStrtIndexFolder()
        {
            string strtFolder = GetGenomeStrtFolder();
            string annotPattern = (AnnotationDate == null)? Annotation + "??????" : Annotation + AnnotationDate;
            string[] annotFolders = Directory.GetDirectories(strtFolder, annotPattern);
            Array.Sort(annotFolders);
            Array.Reverse(annotFolders);
            foreach (string annotFolder in annotFolders)
            {
                string indexFolder = Path.Combine(annotFolder, Props.props.Aligner);
                if (Directory.Exists(indexFolder))
                {
                    AnnotationDate = Path.GetFileName(annotFolder).Substring(Annotation.Length);
                    return indexFolder;
                }
            }
            return "";
        }


        
        public string MakeMaskedChrFileName(string chrId)
        {
            return "chr" + chrId + "_" + Annotation + "Masked.fa";
        }

        public string[] GetMaskedChrPaths()
        {
            return Directory.GetFiles(GetStrtAnnotFolder(), "chr*_" + Annotation + "Masked.fa");
        }

        /// <summary>
        /// Return a mapping from chromosome ids ("1", "2", "M" etc.) to each repeat masked fasta file in STRT genome directory
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetStrtChrFilesMap()
        {
            Dictionary<string, string> chrIdToFileMap = new Dictionary<string, string>();
            foreach (string filePath in GetMaskedChrPaths())
            {
                Match m = Regex.Match(filePath, "chr(.+)_[^_]+");
                string chrId = m.Groups[1].Value;
                chrIdToFileMap[chrId] = filePath;
            }
            chrIdToFileMap[Annotation] = GetJunctionChrPath();
            return chrIdToFileMap;
        }

        public string GetJunctionChrPath()
        {
            return Path.Combine(GetStrtAnnotFolder(), GetJunctionChrFileName());
        }
        public string GetJunctionChrFileName()
        {
            return string.Format("chr{0}_{1}bp.splices", VarAnnot, SplcIndexReadLen);
        }
        public string GetJunctionChrId()
        {
            return "chr" + Annotation;
        }

        /// <summary>
        /// Use the predefined ReadLen for the path
        /// </summary>
        /// <returns></returns>
        public string MakeStrtAnnotPath()
        {
            string file = AnnotationsFilePattern.Replace("*", VarAnnot).Replace("#", SplcIndexReadLen.ToString());
            return Path.Combine(GetStrtAnnotFolder(), file);
        }

        /// <summary>
        /// Search the genomes directory for the existing anntation file that best fits with current ReadLen.
        /// throw exception if none appropriate is found.
        /// </summary>
        /// <returns></returns>
        public string AssertAStrtAnnotPath()
        {
            string pathPattern = Path.Combine(GetStrtAnnotFolder(), AnnotationsFilePattern.Replace("*", VarAnnot));
            string tryAnnotationsPath = FindABpVersion(pathPattern);
            string annotationsPath = PathHandler.ExistsOrGz(tryAnnotationsPath);
            if (annotationsPath == null)
                throw new Exception("Could not find annotation file " + MakeStrtAnnotPath() + " or one with similar read length.");
            return annotationsPath;
        }

        /// <summary>
        /// Returns 'Build_Annotation', e.g. 'mm9_UCSC'
        /// </summary>
        /// <returns></returns>
        public string GetMainIndexName()
        {
            return Build + "_" + Annotation;
        }

        /// <summary>
        /// Returns e.g. 'mm10chrsUCSC_38bp'
        /// </summary>
        /// <returns></returns>
        public string GetSplcIndexName()
        {
            return GetSplcIndexName(SplcIndexReadLen.ToString());
        }
        public string GetSplcIndexName(string readLenItem)
        {
            return string.Format("{0}chr{1}_{2}bp", Build, VarAnnot, readLenItem);
        }
        public string GetSplcIndexNamePattern()
        {
            return GetSplcIndexName("*") + "*";
        }
        public string GetSplcIndexAndDate()
        {
            return GetSplcIndexName() + AnnotationDate;
        }

        /// <summary>
        /// Search for an existing path matching '#' in pattern replaced by current readLen or less. Return "" on failure
        /// </summary>
        /// <param name="pathPattern">should contain a '#' at the place of the readLen</param>
        /// <returns></returns>
        public string FindABpVersion(string pathPattern)
        {
            int stopLen = SplcIndexReadLen - 7;
            for (int testReadLen = SplcIndexReadLen; testReadLen >= stopLen; testReadLen--)
            {
                string path = pathPattern.Replace("#", testReadLen.ToString());
                if (File.Exists(path) || Directory.Exists(path))
                {
                    SplcIndexReadLen = testReadLen;
                    return path;
                }
            }
            Console.WriteLine("Error: No match for " + pathPattern + " where " + stopLen + " <= '#' <= " + SplcIndexReadLen);
            return "";
        }

        public bool IsChrInBuild(string filename)
        {
            return filename.Equals(GetJunctionChrFileName()) || !IsASpliceAnnotation(filename);
        }

        public static bool IsASpliceAnnotation(string chrIdOrFilename)
        {
            foreach (string a in AnnotationSources)
                if (chrIdOrFilename.IndexOf(a) >= 0) return true;
            return false;
        }
        /// <summary>
        /// Check if the chr is a splice or a common or control gene chromosome
        /// </summary>
        /// <param name="chrId"></param>
        /// <returns></returns>
        public static bool IsSyntheticChr(string chrId)
        {
            return Props.props.CommonChrIds.Any(id => chrId.EndsWith(id)) || IsASpliceAnnotation(chrId);
        }

        public static bool IsACommonChrId(string chrId)
        {
            return Props.props.CommonChrIds.Any(id => chrId.EndsWith(id));
        }

        private StrtGenome() 
        {
            SplcIndexReadLen = Props.props.StandardReadLen;
        }
        private StrtGenome(string build, string abbrev, string annotation)
        {
            SplcIndexReadLen = Props.props.StandardReadLen;
            Abbrev = abbrev;
            Build = build;
            Description = "genome of " + build;
            Name = "unknown";
            Annotation = annotation;
            LatinName = "unknown";
            GeneVariants = Props.props.AnalyzeAllGeneVariants;
        }

        private static StrtGenome m_Human = 
                      new StrtGenome { Description = "reference genome", Abbrev = "hs",
                                       Name = "Human", LatinName = "Homo Sapiens", Build = "hg19",
                                       Annotation = DefaultAnnotationSource };
		private static StrtGenome m_Mouse =
                      new StrtGenome { Description = "C57BL/6J", Abbrev = "mm",
                                       Name = "Mouse", LatinName = "Mus Musculus", Build = "mm10",
                                       Annotation = DefaultAnnotationSource };
        private static StrtGenome m_Chicken =
                      new StrtGenome { Description = "galGal4", Abbrev = "gg",
                                       Name = "Chicken", LatinName = "Gallus gallus", Build = "gg4",
                                       Annotation = DefaultAnnotationSource };

        public static StrtGenome Human { get { return m_Human; } }
		public static StrtGenome Mouse	{ get { return m_Mouse; } }
        public static StrtGenome Chicken { get { return m_Chicken; } }
        public static StrtGenome[] GetGenomes()
        {
            return GetGenomes(false);
        }
        /// <summary>
        /// Return data on all genomes that can be use in an analysis.
        /// </summary>
        /// <param name="requireStrtFolder">if true, only consider genomes where the STRT-specific subfolder has been created</param>
        /// <returns></returns>
        public static StrtGenome[] GetGenomes(bool requireStrtFolder)
        {
            List<StrtGenome> existingGenomes =  new List<StrtGenome> { Human, Mouse, Chicken };
            Dictionary<string, object> abbrevs = new Dictionary<string, object>() { {"hs", null} ,  {"mm", null}, {"gg", null } };
            string[] buildFolders = Directory.GetDirectories(Props.props.GenomesFolder);
            Array.Sort(buildFolders);
            buildFolders.Reverse();
            foreach (string buildFolder in buildFolders)
            {
                string build = Path.GetFileName(buildFolder);
                Match m = Regex.Match(build, buildMatchPat);
                string abbrev = build.ToLower();
                if (m.Success && !abbrevs.ContainsKey(m.Groups[1].Value))
                    abbrev = m.Groups[1].Value.ToLower();
                abbrevs[abbrev] = null; // Used to only get latest version of each genome
                string strtFolder = GetGenomeStrtFolder(build);
                if (!requireStrtFolder)
                    existingGenomes.Add(new StrtGenome(build, abbrev, DefaultAnnotationSource));
                if (Directory.Exists(strtFolder))
                {
                    foreach (string annotFolder in Directory.GetDirectories(strtFolder))
                    {
                        Match m1 = Regex.Match(Path.GetFileName(annotFolder), "^(....)[0-9][0-9][0-9][0-9][0-9][0-9]$");
                        if (!m1.Success || !AnnotationSources.Contains(m1.Groups[1].Value)) continue;
                        string[] annFiles = Directory.GetFiles(annotFolder, AnnotationsBuildPattern);
                        foreach (string file in annFiles)
                        {
                            m = Regex.Match(file, AnnotationsBuildRegex);
                            if (m.Success)
                            {
                                string annotation = m.Groups[1].Value;
                                if (existingGenomes.Any(g => (g.Build == build && g.Annotation == annotation)))
                                    continue;
                                existingGenomes.Add(new StrtGenome(build, abbrev, annotation));
                            }
                        }
                    }
                }
            }
            return existingGenomes.ToArray();
        }

        /// <summary>
        /// Create all the strings that would define a valid genome to use for STRT analyses, e.g. "hs", "mm10", "mm9_aUCSC", "mouse"
        /// </summary>
        /// <returns></returns>
        public static List<string> GetValidGenomeStrings()
        {
            HashSet<string> s = new HashSet<string>();
            foreach (StrtGenome g in GetGenomes())
            {
                s.Add(g.Name);
                s.Add(g.Build);
                if (g.Abbrev != g.Build.ToLower()) s.Add(g.Abbrev);
                s.Add(g.Build + "_" + g.Annotation);
                s.Add(g.Build + "_a" + g.Annotation);
                s.Add(g.Build + "_s" + g.Annotation);
            }
            return s.ToList();
        }

        /// <summary>
        /// Special usage when building STRT genomes
        /// </summary>
        /// <param name="speciesArg"></param>
        /// <returns></returns>
        public static StrtGenome GetBaseGenome(string speciesArg)
        {
            return GetGenome(speciesArg, Props.props.AnalyzeAllGeneVariants, "", false);
        }

        /// <summary>
        /// Returns the StrtGenome corresponding to argument.
        /// Arg examples: "Mm", "Mm_a", "Hs_s", "hs_VEGA", "mm9_sVEGA141204", "mouse", "mm10"
        /// if arg contains "_", what follows specifies whether all/single gene variants should be analysed
        /// if the first letter is "a"/"s", and/or defines an annotation source (by the letters after "a"/"s").
        /// The other versions allow to specify defaults for a/s and the annotation source.
        /// </summary>
        /// <param name="speciesArg">Index name, build, species name or abbreviation</param>
        /// <returns>StrtGenome data for specified organism</returns>
        public static StrtGenome GetGenome(string speciesArg)
        {
            return GetGenome(speciesArg, Props.props.AnalyzeAllGeneVariants);
        }
        public static StrtGenome GetGenome(string speciesArg, bool defaultGeneVariants)
        {
            return GetGenome(speciesArg, defaultGeneVariants, "", true);
        }
        public static StrtGenome GetGenome(string speciesArg, bool defaultGeneVariants, string defaultAnnotation, bool requireStrtFolder)
        {
            string spOrBuild = speciesArg;
            string annotation = defaultAnnotation;
            if (spOrBuild.Contains('_'))
            {
                annotation = spOrBuild.Split('_')[1];
                spOrBuild = spOrBuild.Split('_')[0];
            }
            foreach (StrtGenome g in GetGenomes(requireStrtFolder))
            {
                if (spOrBuild.ToLower() == g.Abbrev || spOrBuild.ToLower() == g.Name.ToLower() ||
                    spOrBuild == g.LatinName || spOrBuild == g.Build)
                {
                    g.GeneVariants = defaultGeneVariants;
                    if (annotation.StartsWith("a") || annotation.StartsWith("s"))
                    {
                        g.GeneVariants = (annotation[0] == 's') ? false : true;
                        annotation = annotation.Substring(1);
                    }
                    if (annotation.Length > 0)
                    {
                        if (Regex.IsMatch(annotation, ".+[0-9][0-9][0-9][0-9][0-9][0-9]$"))
                        {
                            g.AnnotationDate = annotation.Substring(annotation.Length - 6);
                            annotation = annotation.Substring(0, annotation.Length - 6);
                        }
                        g.Annotation = annotation;
                    }
                    return g;
                }
            }
            throw new ArgumentException("Genome data is not defined for " + speciesArg);
        }
	}
}
