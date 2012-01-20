using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Linnarsson.Dna
{
    /// <summary>
    /// All GFF3-derived record types have to keep this interface in order to be read and iterated from files
    /// </summary>
    public interface IGFF3Record
    {
        void InitFromLine(string line);
        IGFF3Record New();
    }

    public class GFF3Record : IGFF3Record
    {
        public string seqid;
        public string source;
        public string type;
        public int start; // 1-based
        public int end; // 1-based
        public double score;
        public char strand; // one of +-.?
        public string phase;
        public string attributes;

        public void Init(string seqid, string source, string type, int start, int end,
                         string score, char strand, string phase, string attributes)
        {
            this.seqid = seqid;
            this.source = source;
            this.type = type;
            this.start = start;
            this.end = end;
            this.score = (score == ".")? 0.0 : double.Parse(score);
            this.strand = strand;
            this.phase = phase;
            this.attributes = attributes;
        }

        /// <summary>
        /// Factory for new instances of this class
        /// </summary>
        /// <returns></returns>
        public virtual IGFF3Record New()
        {
            return new GFF3Record();
        }
        /// <summary>
        /// Parse mandatory fields. Calls a file-type dependent method to parse specialized attributes field
        /// </summary>
        /// <param name="line"></param>
        public virtual void InitFromLine(string line)
        {
            string[] f = line.Split('\t');
            Init(f[0], f[1], f[2], int.Parse(f[3]), int.Parse(f[4]), f[5], f[6][0], f[7], f[8]);
            ParseAttributes();
        }

        protected virtual void ParseAttributes()
        {
            // Add some parsing for standard GFF3 file
        }

    }

    public class GFF3CompatibleFile
    {
        /// <summary>
        /// Iterate through all records of a GFF3-type file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="typeGFF3Class">Use this class' New() method to create new record instances</param>
        /// <returns></returns>
        public static IEnumerable<IGFF3Record> Iterate(string path, IGFF3Record typeGFF3Class)
        {
            string line;
            using (StreamReader reader = new StreamReader(path))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    if (!line.StartsWith("##"))
                    {
                        IGFF3Record rec = typeGFF3Class.New();
                        try
                        {
                            rec.InitFromLine(line);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Parsing GFF3 line: " + line + " " + e + " " + e.StackTrace);
                        }
                        if (rec != null) yield return rec;
                    }
                }
            }
        }
    }

    public class GVFVariantEffect
    {
        public string effect;
        public int Variant_seq_index;
        public string feature;
        public string[] featureIDs;
        public GVFVariantEffect(string effect, int index, string feature, string[] featureIDs)
        {
            this.effect = effect;
            Variant_seq_index = index;
            this.feature = feature;
            this.featureIDs = featureIDs;
        }

        public bool IsTranscriptEffect()
        {
            return effect == "nc_transcript" || effect == "non_synonymous_codon" || effect == "synonymous_codon";
        }
    }

    public class GVFRecord : GFF3Record, IGFF3Record
    {
        public string ID;
        public string[] Variant_seq;
        public string Reference_seq;
        public int[] Variant_reads;
        public int Total_reads;
        public double[] Variant_freq;
        public List<GVFVariantEffect> variant_effects = new List<GVFVariantEffect>();

        public override IGFF3Record New()
        {
            return new GVFRecord();
        }

        /// <summary>
        /// Has the variant of this record any effect on a trancribed sequence?
        /// </summary>
        /// <returns></returns>
        public bool AnyTranscriptEffect()
        {
            return variant_effects.Any(ve => ve.IsTranscriptEffect());
        }

        protected override void ParseAttributes()
        {
            foreach (string attribute in attributes.Split(';'))
            {
                string[] keyValPair = attribute.Split('=');
                switch (keyValPair[0])
                {
                    case "ID":
                        ID = keyValPair[1];
                        break;
                    case "Variant_seq":
                        Variant_seq = keyValPair[1].Split(',');
                        break;
                    case "Reference_seq":
                        Reference_seq = keyValPair[1];
                        break;
                    case "Variant_reads":
                        string[] reads = keyValPair[1].Split(',');
                        Variant_reads = new int[reads.Length];
                        for (int i = 0; i < reads.Length; i++)
                            Variant_reads[i] = int.Parse(reads[i]);
                        break;
                    case "Total_reads":
                        Total_reads = int.Parse(keyValPair[1]);
                        break;
                    case "Variant_freq":
                        string[] freqs = keyValPair[1].Split(',');
                        Variant_freq = new double[freqs.Length];
                        for (int i = 0; i < freqs.Length; i++)
                            Variant_freq[i] = double.Parse(freqs[i]);
                        break;
                    case "Variant_effect":
                        string[] parts = keyValPair[1].Split(' ');
                        variant_effects.Add(new GVFVariantEffect(parts[0], int.Parse(parts[1]), parts[2], parts[3].Split(',')));
                        break;
                    default:
                        break;
                }
            }
        }
    }

}
