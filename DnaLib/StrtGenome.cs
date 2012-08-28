using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
	public class StrtGenome
	{
        public static string[] AnnotationSources = new string[] { "UCSC", "VEGA", "ENSE", "ENSEMBL" };
        public static string chrCTRLId = "CTRL";

        public override string ToString()
        {
            return "Abrev=" + Abbrev + " Build=" + Build + " VarAnnot=" + VarAnnot;
        }
        public int ReadLen { get; set; }
		public string Name { get; set; }
        public string Abbrev { get; set; }
		public string LatinName { get; set; }
		public string Description { get; set; }
        /// <summary>
        /// Version of the genome build, e.g. "mm9" or "hg19"
        /// </summary>
        public string Build { get; set; }
        public string m_Annotation;
        /// <summary>
        /// Source of annotations, e.g. "UCSC" or "VEGA"
        /// </summary>
        public string Annotation
        { 
            get { return m_Annotation; }
            set 
            {
                if (value.Length == 5 && "as".Contains(value[0]))
                {
                    m_Annotation = value.Substring(1);
                    GeneVariants = (value[0] == 'a');
                }
                else
                    m_Annotation = value;
            }
        }
        public bool GeneVariants { get; set; }
        public string GeneVariantsChar { get { return GeneVariants ? "a" : "s"; } }
        public string VarAnnot { get { return GeneVariantsChar + Annotation; } }

        public static string AddVariantChar(string speciesArg, bool defaultGeneVariants)
        {
            char variantChar = defaultGeneVariants ? 'a' : 's';
            int variantPos = speciesArg.IndexOf('_') + 1;
            if (variantPos == 0)
                return speciesArg + "_" + variantChar;
            if ("as".Contains(speciesArg[variantPos]))
                return speciesArg.Substring(0, variantPos) + variantChar + speciesArg.Substring(variantPos + 1);
            return speciesArg.Replace("_", "_" + variantChar);
        }

        public string GetOriginalGenomeFolder()
        {
            return Path.Combine(Path.Combine(Props.props.GenomesFolder, Build), "genome");
        }
        public string GetStrtGenomesFolder()
        {
            return GetStrtGenomesFolder(Build);
        }
        public static string GetStrtGenomesFolder(string build)
        {
            return Path.Combine(Path.Combine(Props.props.GenomesFolder, build), "strt");
        }

        public string MakeMaskedChrFileName(string chrId)
        {
            return "chr" + chrId + "_" + Annotation + "Masked.fa";
        }
        public string[] GetMaskedChrPaths()
        {
            return Directory.GetFiles(GetStrtGenomesFolder(), "chr*_" + Annotation + "Masked.fa");
        }

        public Dictionary<string, string> GetStrtChrFilesMap()
        {
            Dictionary<string, string> chrIdToFileMap = new Dictionary<string, string>();
            foreach (string filePath in GetMaskedChrPaths())
            {
                Match m = Regex.Match(filePath, "chr(.+)_[^_]+");
                string chrId = m.Groups[1].Value;
                chrIdToFileMap[chrId] = filePath;
            }
            chrIdToFileMap[Annotation] = MakeJunctionChrPath();
            return chrIdToFileMap;
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

        public string MakeJunctionChrPath()
        {
            return Path.Combine(GetStrtGenomesFolder(), GetJunctionChrFileName());
        }
        public string GetJunctionChrFileName()
        {
            return ReplaceReadLen(ReadLen, "chr" + VarAnnot + "{0}.splices");
        }

        private static string AnnotationsFilePattern = "Annotations_#{0}.txt";
        private static string AnnotationsBuildRegex = "Annotations_[as]([^_]+)_.+txt";
        private static string AnnotationsBuildPattern = "Annotations_*.txt";

        public string MakeAnnotationsPath()
        {
            string pathPattern = Path.Combine(GetStrtGenomesFolder(), AnnotationsFilePattern.Replace("#", VarAnnot));
            return ReplaceReadLen(ReadLen, pathPattern);
        }
        public string GetAnAnnotationsPath()
        {
            string pathPattern = Path.Combine(GetStrtGenomesFolder(), AnnotationsFilePattern.Replace("#", VarAnnot));
            return FindABpVersion(ReadLen, pathPattern);
        }
        public string VerifyAnAnnotationPath()
        {
            string tryAnnotationsPath = GetAnAnnotationsPath();
            string annotationsPath = PathHandler.ExistsOrGz(tryAnnotationsPath);
            if (annotationsPath == null)
                throw new Exception("Could not find annotation file " + MakeAnnotationsPath() + " or one with similar read length.");
            Console.WriteLine("Annotations are taken from " + annotationsPath);
            return annotationsPath;
        }

        public string GetBowtieMainIndexName()
        {
            return Build + "_" + Annotation;
        }

        /// <summary>
        /// Tries to find an existing splice chr bowtie index that has read length as close as possible below ReadLen.
        /// </summary>
        /// <returns>Empty string if none found</returns>
        public string GetBowtieSplcIndexName()
        {
            string pathPattern = Path.Combine(PathHandler.GetBowtieIndicesFolder(), Build + "chr" + VarAnnot + "{0}.1.ebwt");
            return Path.GetFileName(FindABpVersion(ReadLen, pathPattern)).Replace(".1.ebwt", "");
        }
        public string MakeBowtieSplcIndexName()
        {
            return ReplaceReadLen(ReadLen, Build + "chr" + VarAnnot + "{0}");
        }

        private string ReplaceReadLen(int readLen, string pathPattern)
        {
            string readLenPart = string.Format("_{0}bp", readLen);
            return string.Format(pathPattern, readLenPart);
        }
        private string FindABpVersion(int readLen, string pathPattern)
        {
            for (int mapLen = readLen; mapLen > readLen - 10; mapLen--)
            {
                string path = ReplaceReadLen(mapLen, pathPattern);
                if (File.Exists(path))
                    return path;
            }
            return "";
        }

        public bool IsChrInBuild(string filename)
        {
            return filename.Equals(GetJunctionChrFileName()) || !IsASpliceAnnotationChr(filename);
        }

        public static bool IsASpliceAnnotationChr(string chrIdOrFilename)
        {
            foreach (string a in AnnotationSources)
                if (chrIdOrFilename.IndexOf(a) >= 0) return true;
            return false;
        }
        /// <summary>
        /// Check if the chr is a splice or a control gene chromosome
        /// </summary>
        /// <param name="chrId"></param>
        /// <returns></returns>
        public static bool IsSyntheticChr(string chrId)
        {
            return chrId.EndsWith(chrCTRLId) || IsASpliceAnnotationChr(chrId);
        }

        private StrtGenome() 
        {
            ReadLen = Props.props.StandardReadLen;
        }
        private StrtGenome(string build, string abbrev, string annotation)
        {
            ReadLen = Props.props.StandardReadLen;
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
                                       Annotation = "UCSC" };
		private static StrtGenome m_Mouse =
                      new StrtGenome { Description = "C57BL/6J", Abbrev = "mm",
                                       Name = "Mouse", LatinName = "Mus Musculus", Build = "mm9",
                                       Annotation = "UCSC" };
        private static StrtGenome m_Chicken =
                      new StrtGenome { Description = "galGal3", Abbrev = "gg",
                                       Name = "Chicken", LatinName = "Gallus gallus", Build = "gg3",
                                       Annotation = "UCSC" };

        public static StrtGenome Human { get { return m_Human; } }
		public static StrtGenome Mouse	{ get { return m_Mouse; } }
        public static StrtGenome Chicken { get { return m_Chicken; } }
        public static StrtGenome[] GetGenomes()
        {
            return GetGenomes(false);
        }
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
                Match m = Regex.Match(build, "^([A-Za-z][A-Za-z])[0-9]+$");
                string abbrev = build.ToLower();
                if (m.Success && !abbrevs.ContainsKey(m.Groups[1].Value))
                    abbrev = m.Groups[1].Value.ToLower();
                abbrevs[abbrev] = null; // Used to only get latest version of each genome
                string strtFolder = GetStrtGenomesFolder(build);
                if (!requireStrtFolder)
                    existingGenomes.Add(new StrtGenome(build, abbrev, "UCSC"));
                if (Directory.Exists(strtFolder))
                {
                    string[] annFiles = Directory.GetFiles(strtFolder, AnnotationsBuildPattern);
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
            return existingGenomes.ToArray();
        }

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

        public static StrtGenome GetBaseGenome(string speciesArg)
        {
            return GetGenome(speciesArg, Props.props.AnalyzeAllGeneVariants, "", false);
        }

        /// <summary>
        /// Returns the StrtGenome corresponding to argument.
        /// Arg examples: "Mm", "Mm_a", "Hs_s", "hs_VEGA", "mm9_sVEGA", "mouse", "mm9"
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
        public static StrtGenome GetGenome(string speciesArg, bool defaultGeneVariants, string defaultAnnotation,
                                           bool requireStrtFolder)
        {
            string spOrBuild = speciesArg;
            string annotation = defaultAnnotation;
            if (spOrBuild.Contains('_'))
            {
                annotation = spOrBuild.Split('_')[1];
                spOrBuild = spOrBuild.Split('_')[0];
            }
            foreach (StrtGenome g in GetGenomes(requireStrtFolder))
                if (spOrBuild.ToLower() == g.Abbrev || spOrBuild.ToLower() == g.Name.ToLower() ||
                    spOrBuild == g.LatinName || spOrBuild == g.Build)
                {
                    g.GeneVariants = defaultGeneVariants;
                    if (annotation.StartsWith("a") || annotation.StartsWith("s"))
                    {
                        g.GeneVariants = (annotation[0] == 's') ? false : true;
                        annotation = annotation.Substring(1);
                    }
                    if (annotation.Length > 0) g.Annotation = annotation;
                    return g;
                }
            throw new ArgumentException("Genome data is not defined for " + speciesArg);
        }
	}
}
