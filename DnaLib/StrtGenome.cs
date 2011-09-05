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
        public static string AnnotationsFilenamePattern = "SilverBulletGenes_{0}{1}.txt";
        public static string RedundancyFilenamePattern = "SilverBulletRedundacies_{2}bp_{0}{1}.txt";
        public static string[] AnnotationSources = new string[] { "UCSC", "VEGA", "ENSE", "ENSEMBL" };
        public static string chrCTRLId = "CTRL";

		public string Name { get; set; }
        public string Abbrev { get; set; }
		public string LatinName { get; set; }
		public string Description { get; set; }
        public string Build { get; set; }
        public string m_Annotation;
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
        public string GetJunctionChrFileName()
        {
            return "chr" + GeneVariantsChar + Annotation;
        }
        public string GetAnnotationsFileName()
        {
            return string.Format(AnnotationsFilenamePattern, GeneVariantsChar, Annotation);
        }
        public string GetRedundancyFileName(int averageReadLen)
        {  // Use redundant alignment files at steps of 10bp
            int roundedReadLen = 10 * (int)((averageReadLen + 9) / 10);
            return string.Format(RedundancyFilenamePattern, GeneVariantsChar, Annotation, roundedReadLen);
        }
        public string GetBowtieIndexName()
        {
            return Build + "_" + GeneVariantsChar + Annotation;
        }
        public bool IsChrInBuild(string chr)
        {
            return !IsSpliceAnnotationChr(chr) || chr.EndsWith(GeneVariantsChar + Annotation);
        }

        private StrtGenome() { }
        private StrtGenome(string build, string abbrev)
        {
            Abbrev = abbrev;
            Build = build;
            Description = "genome of " + build;
            Name = "unknown";
            Annotation = "UCSC";
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
            List<StrtGenome> existingGenomes =  new List<StrtGenome> { Human, Mouse, Chicken };
            Dictionary<string, object> abbrevs = new Dictionary<string,object>() { {"hs", null} ,  {"mm", null}, {"gg", null } };
            string[] buildFolders = Directory.GetDirectories(Props.props.GenomesFolder);
            Array.Sort(buildFolders);
            buildFolders.Reverse();
            foreach (string buildFolder in buildFolders)
            {
                string buildFolderName = Path.GetFileName(buildFolder);
                Match m = Regex.Match(buildFolderName, "^([A-Za-z][A-Za-z])[0-9]+$");
                string abbrev = buildFolderName.ToLower();
                if (m.Success && !abbrevs.ContainsKey(m.Groups[1].Value))
                    abbrev = m.Groups[1].Value.ToLower();
                foreach (StrtGenome g in existingGenomes)
                    if (g.Build == buildFolderName)
                        continue;
                abbrevs[abbrev] = null;
                StrtGenome existingGenome = new StrtGenome(buildFolderName, abbrev);
                existingGenomes.Add(existingGenome);
            }
            return existingGenomes.ToArray();
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
            return GetGenome(speciesArg, defaultGeneVariants, "");
        }
        public static StrtGenome GetGenome(string speciesArg, bool defaultGeneVariants, string defaultAnnotation)
        {
            string spOrBuild = speciesArg;
            string annotation = defaultAnnotation;
            if (spOrBuild.Contains('_'))
            {
                annotation = spOrBuild.Split('_')[1];
                spOrBuild = spOrBuild.Split('_')[0];
            }
            foreach (StrtGenome g in GetGenomes())
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

        public static string ConvertIfAnnotation(string chrId)
        {
            foreach (string a in AnnotationSources)
                if (chrId.EndsWith(a)) return a;
            return chrId;
        }

        public static bool IsSpliceAnnotationChr(string chr)
        {
            foreach (string a in AnnotationSources)
                if (chr.EndsWith(a)) return true;
            return false;
        }

        public static bool IsSyntheticChr(string chr)
        {
            return chr.EndsWith(chrCTRLId) || IsSpliceAnnotationChr(chr);
        }
	}
}
