using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace C1
{
    public class Cell
    {
        public int? CellID { get; set; }
        public string Plate { get; set; }                   // "1423934" or "L243"
        public string Well { get; set; }                    // "A04"
        public string StrtProtocol { get; set; }            // "STRT c1 v1.11"
        public DateTime DateCollected { get; set; }
        public string Species { get; set; }                 // "mouse" or "human"
        public string Strain { get; set; }                  // "C57/Bl6"
        public string Age { get; set; }                     // "E14.5"
        public char Sex { get; set; }                       // 'M' or 'F'
        public string Tissue { get; set; }
        public string Treatment { get; set; }
        public double Diameter { get; set; }
        public double Area { get; set; }
        public string PI { get; set; }
        public string Operator { get; set; }
        public string Comments { get; set; }
        public List<CellImage> cellImages { get; set; }

        public Cell(int? cellId, string plate, string well, double diameter, double area, string PI, string op, string comments)
        {
            this.CellID = cellId;
            this.Plate = plate;
            this.Well = well;
            this.Diameter = diameter;
            this.Area = area;
            this.PI = PI;
            this.Operator = op;
            this.Comments = comments;
            this.cellImages = new List<CellImage>();
        }
    }

    public class CellImage
    {
        public int? CellImageID { get; set; }
        public int? CellID { get; set; }
        public string Reporter { get; set; }                 // channel used for imaging
        public string Marker { get; set; }                   // "TH-GFP"
        public bool Detection { get; set; }                  // true if stain was detected
        public string RelativePath { get; set; }             // Location of image file

        public CellImage(int? cellImageId, int? cellId, string reporter, string marker, bool detection, string relPath)
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
    }

    public class Transcript
    {
        public int? TranscriptID { get; set; }
        public int TranscriptomeID { get; set; }
        public string Name { get; set; }             // "ENSMUS232243"
        public string Type { get; set; }             // "miRNA", "pseudogene", "coding"
        public string GeneName { get; set; }                // "JunB"
        public string Description { get; set; }
        public string Chromosome { get; set; }              // "chrX"
        public int Start { get; set; }                      // According to build source - excludes any 5' extension. 1-based
        public int End { get; set; }                        // Inclusive, 1-based
        public int Length { get; set; }                     // For an mRNA, the length of the transcript, not locus
        public char Strand { get; set; }                    // '+', '-', or '0'
        public int Extension5Prime { get; set; }            // Extension of first exon for detection of missed CAP sites.
        public string ExonStarts { get; set; }              // Formatted as a comma-separated "refFlat.txt"/psl line
        public string ExonEnds { get; set; }                // Formatted as a comma-separated "refFlat.txt"/psl line

        public Transcript(int? transcriptId, int transcriptomeId, string name, string type, string geneName, string description,
                       string chromosome, int start, int end, int length, char strand, int extension5Prime, string exonStarts, string exonEnds)
        {
            this.TranscriptID = transcriptId;
            this.TranscriptomeID = transcriptomeId;
            this.Name = name;
            this.Type = type;
            this.GeneName = geneName;
            this.Description = description;
            this.Chromosome = chromosome;
            this.Start = start;
            this.End = end;
            this.Length = length;
            this.Strand = strand;
            this.Extension5Prime = extension5Prime;
            this.ExonStarts = exonStarts;
            this.ExonEnds = exonEnds;
        }
    }

    public class FeatureAnnotation
    {
        public int? FeatureAnnotationID { get; set; }
        public int FeatureID { get; set; }
        public string Name { get; set; }                    // "go-process"
        public string Value { get; set; }                   // "GO:0034355"
        public string Comment { get; set; }                 // "transcription"

        public FeatureAnnotation(int? featureAnnotationId, int featureId, string name, string value, string comment)
        {
            this.FeatureAnnotationID = featureAnnotationId;
            this.FeatureID = featureId;
            this.Name = name;
            this.Value = value;
            this.Comment = comment;
        }
    }
}
