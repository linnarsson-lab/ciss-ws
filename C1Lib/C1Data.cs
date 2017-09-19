using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.C1
{
    public class Chip
    {
        public int? id { get; set; }
        public string chipid { get; set; }                  // "1234345-123"
        public string strtprotocol { get; set; }            // "v1.11"
        public DateTime datedissected { get; set; }
        public DateTime datecollected { get; set; }
        public string species { get; set; }                 // "Ms" or "Hs"
        public string strain { get; set; }                  // "C57/bl6"
        public string donorid { get; set; }                 // "129"
        public string age { get; set; }                     // "e14.5"
        public string sex { get; set; }                     // 'M' or 'F' or 'M+F'
        public string weight { get; set; }                  // "17"
        public string tissue { get; set; }                  // "cortex" or "HeLa"
        public string treatment { get; set; }
        public int spikemolecules { get; set; }
        public int? aaaprojectid { get; set; }
        public int aaaclientid { get; set; }
        public int aaacontactid { get; set; }
        public int aaamanagerid { get; set; }
        public string comments { get; set; }
        public List<Cell> cells { get; set; }

        public Chip(int? id, string chipid, string strtprotocol, DateTime datedissected, DateTime datecollected,
                    string species, string strain, string donorid, string age, string sex, string weight, 
                    string tissue, string treatment, int spikes,
                    int? aaaprojectid, int aaaclientid, int aaacontactid, int aaamanagerid, string comments)
        {
            this.id = id;
            this.chipid = chipid;
            this.strtprotocol = strtprotocol;
            this.datedissected = datedissected;
            this.datecollected = datecollected;
            this.species = species;
            this.strain = strain;
            this.donorid = donorid;
            this.age = age;
            this.sex = sex.ToUpper();
            this.weight = weight;
            this.tissue = tissue;
            this.treatment = treatment;
            this.spikemolecules = spikes;
            this.aaaprojectid = aaaprojectid;
            this.aaaclientid = aaaclientid;
            this.aaacontactid = aaacontactid;
            this.aaamanagerid = aaamanagerid;
            this.comments = comments;
            this.cells = new List<Cell>();
        }
    }

    public class Cell
    {
        public int? id { get; set; }
        public int aaachipid { get; set; }
        public int aaasampleid { get; set; }
        public string chipwell { get; set; }                // Well on C1 chip, e.g. "A02"
        public string subwell { get; set; }                 // Picked row and col on Wafergen 10x10 subsquare, e.g. "0400"
        public string platewell { get; set; }               // Well on sequencing plate, e.g. "B04"
        public double diameter { get; set; }
        public double area { get; set; }
        public int red { get; set; }
        public int green { get; set; }
        public int blue { get; set; }
        public bool valid { get; set; }                     // True if the cell is not excluded (visual inspection)
        public List<CellImage> cellImages { get; set; }
        public List<CellAnnotation> cellAnnotations { get; set; }

        public Cell(int? id, int aaachipid, int aaasampleid, string chipwell, string platewell,
                    double diameter, double area, int red, int green, int blue, bool valid, string subwell)
        {
            this.id = id;
            this.aaachipid = aaachipid;
            this.aaasampleid = aaasampleid;
            this.chipwell = chipwell;
            this.subwell = subwell;
            this.platewell = platewell;
            this.diameter = diameter;
            this.area = area;
            this.red = red;
            this.green = green;
            this.blue = blue;
            this.valid = valid;
            this.cellImages = new List<CellImage>();
            this.cellAnnotations = new List<CellAnnotation>();
        }
    }

    public class Detection 
    {
        public static int No = 0;
        public static int Yes = 1;
        public static int Unknown = 2;
    };

    public class CellImage
    {
        public int? id { get; set; }
        public int? aaacellid { get; set; }
        public string Reporter { get; set; }                 // channel used for imaging
        public string Marker { get; set; }                   // "TH-GFP"
        public int Detection { get; set; }             // was this marker detected?
        public string RelativePath { get; set; }             // Location of image file

        public CellImage(int? id, int? aaacellid, string reporter, string marker, int detection, string relPath)
        {
            this.id = id;
            this.aaacellid = aaacellid;
            this.Reporter = reporter;
            this.Marker = marker;
            this.Detection = detection;
            this.RelativePath = relPath;
        }
    }

    public class CellAnnotation
    {
        public int? id { get; set; }
        public int aaacellid { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }

        public CellAnnotation(int? id, int aaacellid, string name, string value)
        {
            this.id = id;
            this.aaacellid = aaacellid;
            this.Name = name;
            this.Value = value;
        }
    }

    public class ExprBlob
    {
        public string aaacellid { get; set; }
        public int TranscriptomeID { get; set; }
        public byte[] Blob { get; set; }

        public ExprBlob(int nValues)
        {
            Blob = new byte[4 * nValues];
        }

        public void SetBlobValue(int blobIdx, int value)
        {
            byte[] recBytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) // Keep data big-endian
                Array.Reverse(recBytes, 0, 4);
            Array.Copy(recBytes, 0, Blob, 4 * blobIdx, 4);
        }
        public void ClearBlob()
        {
            Array.Clear(Blob, 0, Blob.Length);
        }
    }

    public class Expression
    {
        public string aaacellid { get; set; }
        public int TranscriptID { get; set; }
        /// <summary>
        /// Uniquely mapped reads
        /// </summary>
        public int UniqueReads { get; set; }
        /// <summary>
        /// Uniquely mapped molecules
        /// </summary>
        public int UniqueMolecules { get; set; }
        /// <summary>
        /// Including multireads of transcript origin
        /// </summary>
        public int Reads { get; set; }
        /// <summary>
        /// Molecules including these from multireads of transcript origin
        /// </summary>
        public int Molecules { get; set; }

        public Expression()
        { }
        public Expression(string aaacellid, int transcriptId, int minReads, int minMols, int maxReads, int maxMols)
        {
            this.aaacellid = aaacellid;
            this.TranscriptID = transcriptId;
            this.UniqueReads = minReads;
            this.UniqueMolecules = minMols;
            this.Reads = maxReads;
            this.Molecules = maxMols;
        }
    }

    public class Transcriptome
    {
        public int? TranscriptomeID { get; set; }
        public string Name { get; set; }                      // "mm10_aENSE"
        public string Organism { get; set; }
        public string Source { get; set; }                    // "ENSEMBL"
        public string GenomeFolder { get; set; }
        public string Description { get; set; }
        public DateTime BuildDate { get; set; }
        public string BuilderVersion { get; set; }            // "28"
        public string AnnotationVersion { get; set; }         // "43"
        public DateTime AnalysisDate { get; set; }

        public Transcriptome(int? transcriptomeId, string name, string organism, string source, string genomeFolder, string description,
                             DateTime buildDate, string builderVersion, DateTime analysisDate, string annotationVersion)
        {
            this.TranscriptomeID = transcriptomeId;
            this.Name = name;
            this.Organism = organism;
            this.Source = source;
            this.GenomeFolder = genomeFolder;
            this.Description = description;
            this.BuildDate = buildDate;
            this.BuilderVersion = builderVersion;
            this.AnalysisDate = analysisDate;
            this.AnnotationVersion = annotationVersion;
        }
        public override string ToString()
        {
            return string.Format("Transcriptome(ID={0}, Name={1}, Organism={2}, Source={3}, GenomeFolder={4}, Description={5}, " +
                                 "BuildDate={6}, BuilderVersion={7}, AnnotationVersion={8}, AnalysisDate={9})",
                                 TranscriptomeID, Name, Organism, Source, GenomeFolder, Description,
                                 BuildDate, BuilderVersion, AnnotationVersion, AnalysisDate);
        }
    }

    public class Transcript
    {
        public int? TranscriptID { get; set; }
        public int TranscriptomeID { get; set; }
        public string Name { get; set; }             // "ENSMUS232243"
        public string Type { get; set; }             // "miRNA", "pseudogene", "coding"
        public string GeneName { get; set; }                // "JunB"
        public string UniqueGeneName { get; set; }          // "JunB_v2"
        public string EntrezID { get; set; }
        public string Description { get; set; }
        public string Chromosome { get; set; }              // "chrX"
        public int Start { get; set; }                      // 1-based, includes any 5' extension
        public int End { get; set; }                        // Inclusive, 1-based
        public int Length { get; set; }                     // For an mRNA, the length of the transcript, not locus
        public char Strand { get; set; }                    // '+', '-', or '0'
        public int Extension5Prime { get; set; }            // 5' extension of first exon for detection of missed CAP sites.
        public string ExonStarts { get; set; }              // 0-based comma-separated "refFlat.txt"/psl line
        public string ExonEnds { get; set; }                // 0-based exclusive comma-separated "refFlat.txt"/psl line
        public int ExprBlobIdx { get; set; }                // Used when storing expr data as blob
        public List<TranscriptAnnotation> TranscriptAnnotations { get; set; }
        public string UniProtAccession { get; set; }        // Temporary for cross-correlation between annotation files
        public string StartToCloseCutSites { get; set; }    // Comma-delimited list of distances from CAP to (PvuI) cut sites

        public Transcript(string name, string type, string geneName, string uniqueGeneName,
                          string entrezId, string description, string chromosome, int start, int end, int length,
                          char strand, int extension5Prime, string exonStarts, string exonEnds)
            : this(null, 0, 0, name, type, geneName, uniqueGeneName, entrezId, description, chromosome, start, end, length,
                   strand, extension5Prime, exonStarts, exonEnds, "")
        { }

        public Transcript(int? transcriptId, int transcriptomeId, int exprBlobIdx, 
                          string name, string type, string geneName, string uniqueGeneName,
                          string entrezId, string description, string chromosome, int start, int end, int length,
                          char strand, int extension5Prime, string exonStarts, string exonEnds, string startToCloseCutSites)
        {
            this.TranscriptID = transcriptId;
            this.TranscriptomeID = transcriptomeId;
            this.ExprBlobIdx = exprBlobIdx;
            this.Name = name;
            this.Type = type;
            this.GeneName = geneName;
            this.UniqueGeneName = uniqueGeneName;
            this.EntrezID = entrezId;
            this.Description = description;
            this.Chromosome = chromosome;
            this.Start = start;
            this.End = end;
            this.Length = length;
            this.Strand = strand;
            this.Extension5Prime = extension5Prime;
            this.ExonStarts = exonStarts;
            this.ExonEnds = exonEnds;
            this.StartToCloseCutSites = startToCloseCutSites;
            this.TranscriptAnnotations = new List<TranscriptAnnotation>();
        }
        public override string ToString()
        {
            return string.Format("Transcript(ID={0}, TranscriptomeID={14}, ExprBlobIdx={15}, Name={1}, Type={2}, " +
                                 "GeneName={3}, EntrezID={4}, Description={5}, Chromosome={6}, " +
                                 "Start={7}, End={8}, Length={9}, Strand={10}, Extension5Prime={11}, StartToCloseCutSite=({16})\n" +
                                 "ExonStarts={12}\nExonEnds={13})",
                                 TranscriptID, Name, Type, GeneName, EntrezID, Description, Chromosome,
                                 Start, End, Length, Strand, Extension5Prime, ExonStarts, ExonEnds, TranscriptomeID,
                                 ExprBlobIdx, StartToCloseCutSites);
        }

    }

    public class TranscriptAnnotation
    {
        public int? TranscriptAnnotationID { get; set; }
        public int? TranscriptID { get; set; }
        public string Source { get; set; }                    // "go-process"
        public string Value { get; set; }                   // "GO:0034355"
        public string Description { get; set; }                 // "transcription"

        public TranscriptAnnotation(int? transcriptAnnotationId, int? transcriptId, string name, string value, string comment)
        {
            this.TranscriptAnnotationID = transcriptAnnotationId;
            this.TranscriptID = transcriptId;
            this.Source = name;
            this.Value = value;
            this.Description = comment;
        }
        public override string ToString()
        {
            return string.Format("TranscriptAnnotation(ID={0}, TranscriptID={1}, Source={2}, Value={3}, Description={4})",
                                 TranscriptAnnotationID, TranscriptID, Source, Value, Description);
        }
    }
}
