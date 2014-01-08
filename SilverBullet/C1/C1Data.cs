using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace C1
{
    public class Cell
    {
        public int? CellID { get; set; }
        public string Chip { get; set; }                    // "1234345-123"
        public string ChipWell { get; set; }                // Well on C1 chip, e.g. "A02"
        public string Plate { get; set; }                   // Sequencing plate, e.g. "C1-243"
        public string PlateWell { get; set; }                // Well on sequencing plate, e.g. "B04"
        public string StrtProtocol { get; set; }            // "STRT c1 v1.11"
        public DateTime DateDissected { get; set; }
        public DateTime DateCollected { get; set; }
        public string Species { get; set; }                 // "mouse" or "human"
        public string DonorID { get; set; }                 // "129"
        public string Weight { get; set; }                 // "350g"
        public string Strain { get; set; }                  // "C57/Bl6"
        public string Age { get; set; }                     // "E14.5"
        public char Sex { get; set; }                       // 'M' or 'F'
        public string Tissue { get; set; }
        public string Treatment { get; set; }
        public double Diameter { get; set; }
        public double Area { get; set; }
        public string PI { get; set; }
        public string Operator { get; set; }
        public string Scientist { get; set; }
        public string Comments { get; set; }
        public int Red { get; set; }
        public int Green { get; set; }
        public int Blue { get; set; }
        public List<CellImage> cellImages { get; set; }

        public Cell(int? cellId, string chip, string chipWell, string plate, string plateWell,
                    string strtProtocol, DateTime dateDissected, DateTime dateCollected, string species, string strain,
                    string donorID, string age, char sex, string tissue, string treatment,
                    double diameter, double area, string PI, string op, string sci, string comments,
                    int red, int green, int blue, string weight)
        {
            this.CellID = cellId;
            this.Chip = chip;
            this.ChipWell = chipWell;
            this.Plate = plate;
            this.PlateWell = plateWell;
            this.StrtProtocol = strtProtocol;
            this.DateDissected = dateDissected;
            this.DateCollected = dateCollected;
            this.Species = species;
            this.Strain = strain;
            this.DonorID = donorID;
            this.Age = age;
            this.Sex = sex;
            this.Tissue = tissue;
            this.Treatment = treatment;
            this.Diameter = diameter;
            this.Area = area;
            this.PI = PI;
            this.Operator = op;
            this.Scientist = sci;
            this.Comments = comments;
            this.Red = red;
            this.Green = green;
            this.Blue = blue;
            this.Weight = weight;
            this.cellImages = new List<CellImage>();
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
        public int? CellImageID { get; set; }
        public int? CellID { get; set; }
        public string Reporter { get; set; }                 // channel used for imaging
        public string Marker { get; set; }                   // "TH-GFP"
        public int Detection { get; set; }             // was this marker detected?
        public string RelativePath { get; set; }             // Location of image file

        public CellImage(int? cellImageId, int? cellId, string reporter, string marker, int detection, string relPath)
        {
            this.CellImageID = cellImageId;
            this.CellID = cellId;
            this.Reporter = reporter;
            this.Marker = marker;
            this.Detection = detection;
            this.RelativePath = relPath;
        }
    }

    public class CellAnnotation
    {
        public int? CellAnnotationID { get; set; }
        public int CellID { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }

        public CellAnnotation(int? cellAnnotationId, int cellId, string name, string value)
        {
            this.CellAnnotationID = cellAnnotationId;
            this.CellID = cellId;
            this.Name = name;
            this.Value = value;
        }
    }

    public class ExprBlob
    {
        public int CellID { get; set; }
        public int TranscriptomeID { get; set; }
        public byte[] Blob { get; set; }

        public ExprBlob(int cellId, int transcriptomeId, byte[] blob)
        {
            CellID = cellId;
            TranscriptomeID = transcriptomeId;
            Blob = blob;
        }
    }

    public class Expression
    {
        public string CellID { get; set; }
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
        public Expression(string cellId, int transcriptId, int minReads, int minMols, int maxReads, int maxMols)
        {
            this.CellID = cellId;
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
        public string StartToCloseCutSite { get; set; }

        public Transcript(string name, string type, string geneName, string uniqueGeneName,
                          string entrezId, string description, string chromosome, int start, int end, int length,
                          char strand, int extension5Prime, string exonStarts, string exonEnds)
            : this(null, 0, 0, name, type, geneName, uniqueGeneName, entrezId, description, chromosome, start, end, length,
                   strand, extension5Prime, exonStarts, exonEnds)
        { }

        public Transcript(int? transcriptId, int transcriptomeId, int exprBlobIdx, 
                          string name, string type, string geneName, string uniqueGeneName,
                          string entrezId, string description, string chromosome, int start, int end, int length,
                          char strand, int extension5Prime, string exonStarts, string exonEnds)
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
                                 ExprBlobIdx, StartToCloseCutSite);
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
