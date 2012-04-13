using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Utilities;
using System.IO;

namespace Linnarsson.Dna
{
    public class SampleLayoutFileException : ApplicationException
    {
        public SampleLayoutFileException(string msg)
            : base(msg)
        { }
    }
    
    public class PlateLayout
    {
        public string Filename;
        public Dictionary<string, string> SpeciesIdBySampleId = new Dictionary<string, string>(); // All speciesIds are lower case
        private Dictionary<string, object> SpeciesIds = new Dictionary<string, object>(); // All speciesIds are lower case
        private Dictionary<string, object> SpeciesAbbrevs = new Dictionary<string, object>(); // All speciesAbbrevs are lower case
        private Dictionary<string, string[]> AnnotationsBySampleId = new Dictionary<string, string[]>();
        private Dictionary<string, int> AnnotationIndexes = new Dictionary<string, int>();
        public int Length { get { return SpeciesIdBySampleId.Count; } }

        public string[] GetAnnotations()
        {
            return AnnotationIndexes.Keys.ToArray();
        }
        public string GetSampleAnnotation(string annotation, string sampleId)
        {
            return AnnotationsBySampleId[sampleId][AnnotationIndexes[annotation]];
        }

        public string[] GetSpeciesIds()
        {
            return SpeciesIds.Keys.ToArray();
        }
        public string[] GetSpeciesAbbrevs()
        {
            return SpeciesAbbrevs.Keys.ToArray();
        }

        public PlateLayout(string plateLayoutPath)
        {
            Filename = plateLayoutPath;
            int sampleIdIdx = -1, speciesIdIdx = -1;
            int annotationIdx = 0;
            string[] genomeAbbrevs =  Array.ConvertAll(StrtGenome.GetGenomes(), (g) => g.Abbrev.ToLower());
            StreamReader reader = plateLayoutPath.OpenRead();
            string line = reader.ReadLine();
            while (line.StartsWith("#")) line = reader.ReadLine();
            string[] hFields = line.Split('\t');
            for (int colIdx = 0; colIdx < hFields.Length; colIdx++)
            {
                string title = hFields[colIdx];
                if (title.ToLower() == "sampleid") sampleIdIdx = colIdx;
                else if (title.ToLower() == "species") speciesIdIdx = colIdx;
                else AnnotationIndexes[title] = annotationIdx++;
            }
            if (sampleIdIdx == -1 || speciesIdIdx == -1)
                throw new SampleLayoutFileException("Header line of " + plateLayoutPath + " misses SampleId and/or Species column");
            line = reader.ReadLine();
            while (line != null && line != "")
            {
                if (!line.StartsWith("#"))
                {
                    string[] fields = line.Split('\t');
                    string sampleId = fields[sampleIdIdx].Trim();
                    if (SpeciesIdBySampleId.ContainsKey(sampleId))
                        throw new SampleLayoutFileException("Duplicated sampleId in " + plateLayoutPath);
                    string speciesId = fields[speciesIdIdx].ToLower().Trim();
                    try
                    {
                        speciesId = StrtGenome.GetGenome(speciesId).Abbrev;
                    }
                    catch (ArgumentException)
                    {
                        speciesId = "empty";
                    }
                    SpeciesIdBySampleId[sampleId] = speciesId;
                    SpeciesIds[speciesId] = null;
                    foreach (string genomeAbbrev in genomeAbbrevs)
                        if (genomeAbbrev == speciesId)
                            SpeciesAbbrevs[speciesId] = null;
                    List<string> otherFields = new List<string>();
                    for (int colIdx = 0; colIdx < fields.Length; colIdx++)
                        if (colIdx != sampleIdIdx && colIdx != speciesIdIdx)
                            otherFields.Add(fields[colIdx]);
                    AnnotationsBySampleId[sampleId] = otherFields.ToArray();
                }
                line = reader.ReadLine();
            }
            reader.Close();
            if (SpeciesAbbrevs.Count == 0)
                throw new SampleLayoutFileException("No parseable species in " + plateLayoutPath + 
                                                    ". Use two-letter abbrevation or full latin name (e.g. 'Hs' or 'Homo sapiens')");
        }

    }
}
