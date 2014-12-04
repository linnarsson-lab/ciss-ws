using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Linnarsson.Dna;
using Linnarsson.Dna.GeneOntology;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using Linnarsson.Strt;
using C1;

namespace C1
{
    public delegate void AnnotationChain(ref Transcript t);

    /// <summary>
    /// Add additional annotations to new transcript models before inserting them into database.
    /// Several annotations are taken from files downloaded from UCSC/GoldenPath, and
    /// include descriptions, synonyms, EntrezIDs, UniProtIDs, GO terms, and pathways.
    /// </summary>
    public class TranscriptAnnotator
    {
        public AnnotationChain Annotate;

        public TranscriptAnnotator(StrtGenome genome)
        {
            Annotate += new kgXrefAnnotator(genome).Annotate;
            Annotate += new RefLinkAnnotator(genome).Annotate;
            //Annotate += new AliasAnnotator(genome).Annotate;  // Has to be preceeded by kgXrefAnnotator
            Annotate += new GeneAssociationAnnotator(genome).AnnotateFromGeneAssociation;
            Annotate += new BiosystemsAnnotator(genome).AnnotateFromBiosystems;
            Annotate += new CleavageSiteAnnotator(genome).Annotate;
        }
    }

    abstract class SimpleAnnotator
    {
        protected Dictionary<string, string[]> keyToValueMap = new Dictionary<string, string[]>();

        public abstract void Annotate(ref Transcript t);

        /// <summary>
        /// Generalized annotator to annotate from a TAB-delim file, using some lookup and some annotation columns
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="annotationFile"></param>
        /// <param name="keyCols">column(s) to match during lookup</param>
        /// <param name="valueCols">column(s) of anntotation values</param>
        /// <param name="annotType">type of anntotation</param>
        public SimpleAnnotator(StrtGenome genome, string annotationFile, int[] keyCols, int[] valueCols, string annotType)
        {
            string aPath = PathHandler.ExistsOrGz(Path.Combine(genome.GetOriginalGenomeFolder(), annotationFile));
            if (aPath == null)
            {
                Console.WriteLine("Please download {0} to get {1} annotations.", annotationFile, annotType);
                return;
            }
            using (StreamReader r = aPath.OpenRead())
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (line == "" || line.StartsWith("#") || line.StartsWith("!"))
                        continue;
                    string[] fields = line.Split('\t');
                    string[] values = Array.ConvertAll(valueCols, col => fields[col].Trim());
                    if (!values.All(v => v == ""))
                        foreach (int keyCol in keyCols)
                        {
                            string key = fields[keyCol].Trim();
                            if (key != "")
                                keyToValueMap[key] = values;
                        }
                }
            }
            Console.WriteLine("Initiated {0} annotator with {1} items from {2}", annotType, keyToValueMap.Count, aPath);
        }

    }

    class kgXrefAnnotator : SimpleAnnotator
    {
        private static string annotationFilename = "kgXref.txt";

        public kgXrefAnnotator(StrtGenome genome)
            : base(genome, annotationFilename, new int[] {1, 4}, new int[] {7, 2}, "descriptions")
        { }

        public override void Annotate(ref Transcript t)
        {
            string[] values;
            if (t.Description == "")
            {
                bool hit = false;
                foreach (string name in t.Name.Split(';'))
                    if (keyToValueMap.TryGetValue(name, out values))
                    {
                        t.Description = values[0];
                        t.UniProtAccession = values[1];
                        break;
                    }
                if (!hit && keyToValueMap.TryGetValue(t.GeneName, out values))
                {
                    t.Description = values[0];
                    t.UniProtAccession = values[1];
                }
            }
        }
    }

    class RefLinkAnnotator : SimpleAnnotator
    {
        private static string annotationFilename = "refLink.txt";

        public RefLinkAnnotator(StrtGenome genome)
            : base(genome, annotationFilename, new int[] { 0, 2 }, new int[] { 1, 6 }, "EntrezId & alt. descriptions")
        { }

        public override void Annotate(ref Transcript t)
        {
            string[] values;
            if (t.Description == "" || t.EntrezID == "")
            {
                bool hit = false;
                foreach (string name in t.Name.Split(';'))
                    if (keyToValueMap.TryGetValue(name, out values))
                    {
                        if (t.Description == "")
                            t.Description = values[0];
                        if (t.EntrezID == "")
                            t.EntrezID = values[1];
                        hit = true;
                        break;
                    }
                if (!hit && keyToValueMap.TryGetValue(t.GeneName, out values))
                {
                    if (t.Description == "")
                        t.Description = values[0];
                    if (t.EntrezID == "")
                        t.EntrezID = values[1];
                }
            }
        }
    }

    class AliasAnnotator
    {
        private string annotationFilename = "kgSpAlias.txt";
        private Dictionary<string, string> UniProtAcc2GOAliases = new Dictionary<string, string>();

        public AliasAnnotator(StrtGenome genome)
        {
            string aliasPath = Path.Combine(genome.GetOriginalGenomeFolder(), annotationFilename);
            if (!File.Exists(aliasPath))
                Console.WriteLine("Please download {0} from UCSC/goldenPath and rerun to get gene/protein synonyms.", aliasPath);
            using (StreamReader r = aliasPath.OpenRead())
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (line == "" || line.StartsWith("!") || line.StartsWith("#"))
                        continue;
                    string[] fields = line.Split('\t');
                    string UniProtAcc = fields[1].Trim();
                    string alias = fields[2].Trim();
                    if (UniProtAcc == "" || alias == "")
                        continue;
                    if (alias.EndsWith("_MOUSE")) alias = alias.Replace("_MOUSE", "");
                    if (alias.EndsWith("_HUMAN")) alias = alias.Replace("_HUMAN", "");
                    alias = alias.Replace("'", "-prime");
                    if (UniProtAcc2GOAliases.ContainsKey(UniProtAcc))
                    {
                        if (!UniProtAcc2GOAliases[UniProtAcc].Contains(alias))
                            UniProtAcc2GOAliases[UniProtAcc] += ";" + alias;
                    }
                    else
                        UniProtAcc2GOAliases[UniProtAcc] = alias;
                }
            }
            Console.WriteLine("Initiated alias annotator with synonyms for {0} genes from {1}", UniProtAcc2GOAliases.Count, aliasPath);
        }

        public void Annotate(ref Transcript t)
        {
            string aliases;
            if (t.UniProtAccession != null && UniProtAcc2GOAliases.TryGetValue(t.UniProtAccession, out aliases))
            {
                foreach (string alias in aliases.Split(';'))
                {
                    if (alias != t.Name && alias != t.GeneName && !alias.EndsWith("Rik"))
                        t.TranscriptAnnotations.Add(new TranscriptAnnotation(null, null, "synonym", alias, ""));
                }
            }
        }
    }

    class GeneAssociationAnnotator
    {
        private string goaFilepattern = "gene_association*";
        private Dictionary<string, string> geneNameToGOTerms = new Dictionary<string, string>();
        private GeneOntology go;

        public GeneAssociationAnnotator(StrtGenome genome)
        {
            string goOBOPath = Path.Combine(Props.props.GenomesFolder, C1Props.props.GeneOntologySubPath);
            if (File.Exists(goOBOPath))
                go = GeneOntology.FromFile(goOBOPath);
            else
                Console.WriteLine("Please download {0} from GeneOntology and rerun to get GO annotations.", goOBOPath);
            string[] annotationPaths = Directory.GetFiles(genome.GetOriginalGenomeFolder(), goaFilepattern);
            foreach (string path in annotationPaths)
            {
                using (StreamReader r = path.OpenRead())
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        if (line == "" || line.StartsWith("!") || line.StartsWith("#"))
                            continue;
                        string[] fields = line.Split('\t');
                        string geneName = fields[2].Trim();
                        string term = fields[4].Trim();
                        if (geneName == "" || term == "")
                            continue;
                        GoTerm gt = go.GetTerm(term);
                        if (gt == null || gt.Name == "cellular_component" || gt.Name == "biological_process" || gt.Name == "molecular_function")
                            continue;
                        if (geneNameToGOTerms.ContainsKey(geneName) && !geneNameToGOTerms[geneName].Contains(term))
                            geneNameToGOTerms[geneName] += ";" + term;
                        else
                            geneNameToGOTerms[geneName] = term;
                    }
                }
                Console.WriteLine("Initiated transcript annotator with GO terms for {0} genes from {1}",
                                  geneNameToGOTerms.Count, path);
            }
        }

        public void AnnotateFromGeneAssociation(ref Transcript t)
        {
            string terms;
            if (geneNameToGOTerms.TryGetValue(t.GeneName, out terms))
            {
                if (go != null)
                {
                    GoTerm goTerm;
                    foreach (string term in terms.Split(';'))
                    {
                        if ((goTerm = go.GetTerm(term)) != null)
                            t.TranscriptAnnotations.Add(new TranscriptAnnotation(null, null, "GO", term.Replace("GO:", ""), goTerm.Name));
                    }
                }
            }
        }
    }

    class BiosystemsAnnotator
    {
        private readonly static string bsid2InfoFile = "biosystems/bsid2info.sed";
        private readonly static string biosystemsFile = "biosystems/biosystems_gene";

        private List<Pair<int, int>> entrezIDToBiosystem = new List<Pair<int, int>>();
        private Biosystems biosystems = null;

        public BiosystemsAnnotator(StrtGenome genome)
        { 
            string bsid2InfoPath = PathHandler.ExistsOrGz(Path.Combine(Props.props.GenomesFolder, bsid2InfoFile));
            string genePath = PathHandler.ExistsOrGz(Path.Combine(Props.props.GenomesFolder, biosystemsFile));
            if (bsid2InfoPath == null || genePath == null)
            {
                Console.WriteLine("Download {0} and {1} from NCBI Biosystems and save under {2} and rerun to get pathway annotations.",
                                  bsid2InfoFile, biosystemsFile, Props.props.GenomesFolder);
                return;
            }
            biosystems = Biosystems.FromFile(bsid2InfoPath);
            ReadBiosystemsGene(genePath);
            Console.WriteLine("Initiated transcript annotator with {0} pathway associations from {1}",
                               entrezIDToBiosystem.Count, genePath);
        }

        private void ReadBiosystemsGene(string genePath)
        {
            using (StreamReader r = genePath.OpenRead())
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    string[] fields = line.Split('\t');
                    int bsid = int.Parse(fields[0].Trim());
                    int entrezId = int.Parse(fields[1].Trim());
                    entrezIDToBiosystem.Add(new Pair<int, int>(entrezId, bsid));
                }
            }
            entrezIDToBiosystem.Sort(EntrezIDComparer);
        }

        private static int EntrezIDComparer(Pair<int, int> p1, Pair<int, int> p2)
        {
            return p1.First.CompareTo(p2.First);
        }

        public void AnnotateFromBiosystems(ref Transcript t)
        {
            if (biosystems == null || t.EntrezID == null || t.EntrezID == "")
                return;
            int e = int.Parse(t.EntrezID);
            int idx = entrezIDToBiosystem.FindIndex(p => p.First == e);
            if (idx >= 0)
            {
                Dictionary<string, List<Biosystem>> sysBySource = new Dictionary<string, List<Biosystem>>();
                Biosystem b;
                Pair<int, int> p = entrezIDToBiosystem[idx];
                while (p.First == e)
                {
                    int bsid = p.Second;
                    if ((b = biosystems.GetSystem(bsid)) != null)
                    {
                        if (!sysBySource.ContainsKey(b.Source))
                            sysBySource[b.Source] = new List<Biosystem>();
                        if (!sysBySource[b.Source].Any(v => v.Accession == b.Accession))
                            sysBySource[b.Source].Add(b);
                    }
                    if (++idx >= entrezIDToBiosystem.Count)
                        break;
                    p = entrezIDToBiosystem[idx];
                }
                foreach (KeyValuePair<string, List<Biosystem>> sbb in sysBySource)
                {
                    foreach (Biosystem bs in sbb.Value)
                        t.TranscriptAnnotations.Add(new TranscriptAnnotation(null, null, bs.Source, bs.Accession, bs.Name));
                }
            }
        }
    }

    /// <summary>
    /// Data derived from NCBI biosystems ftp, file "bsid2info.sed"
    /// </summary>
    public class Biosystem
    {
        public string BsId { get; set; }
        public string Source { get; set; }
        public string Accession { get; set; }
        public string Name { get; set; }
        //public string Type { get; set; }
        //public string TaxonomyScope { get; set; }
        //public string TaxId { get; set; }
        //public string Description { get; set; }

        public Biosystem(string bsid, string source, string acc, string name, string type, string scope, string taxId, string descr)
        {
            this.BsId = bsid;
            this.Source = source;
            this.Accession = acc;
            this.Name = name;
            //this.Type = type;
            //this.TaxonomyScope = scope;
            //this.TaxId = taxId;
            //this.Description = descr;
        }

        public static Biosystem FromBs2IdLine(string line)
        {
            string[] fields = line.Split('\t');
            string bsid = fields[0].Trim();
            string source = fields[1].Trim();
            string acc = fields[2].Trim();
            if (source == "KEGG")
            {
                Match m = Regex.Match(acc, "^[A-Za-z]+([0-9]+)$");
                if (m.Success)
                    acc = m.Groups[1].Value;
            }
            else if (source == "REACTOME")
                acc = acc.Replace("REACT_", "");
            else if (source == "WikiPathways")
                acc = acc.Replace("WP", "");
            if (fields.Length == 8)
                return new Biosystem(bsid, source, acc, fields[3].Trim(),
                                        fields[4].Trim(), fields[5].Trim(), fields[6].Trim(), fields[7].Trim());
            else
                return new Biosystem(bsid, source, acc, fields[3].Trim(), "N/A", "N/A", "N/A", "");
        }
    }

    public class Biosystems
    {
        Dictionary<int, Biosystem> bsidToSystem = new Dictionary<int, Biosystem>();

        public Biosystems()
        { }

        public Biosystem GetSystem(int bsid)
        {
            Biosystem b = null;
            bsidToSystem.TryGetValue(bsid, out b);
            return b;
        }

        public static Biosystems FromFile(string file)
        {
            Biosystems bs = new Biosystems();
            using (StreamReader r = file.OpenRead())
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (line == "" || line.StartsWith("#") || line.StartsWith("!"))
                        continue;
                    Biosystem b = Biosystem.FromBs2IdLine(line);
                    bs.bsidToSystem[int.Parse(b.BsId)] = b;
                }
            }
            return bs;
        }
    }
}
