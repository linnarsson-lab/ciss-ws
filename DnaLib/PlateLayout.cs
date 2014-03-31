using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Utilities;
using System.IO;
using C1;

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
        protected HashSet<string> m_SpeciesIds = new HashSet<string>(); // Distinct abbreviations found in SpeciesIdBySampleId
        public string[] SpeciesIds { get { return m_SpeciesIds.ToArray(); } }
        protected Dictionary<string, string[]> AnnotationsBySampleId = new Dictionary<string, string[]>();
        protected Dictionary<string, int> AnnotationIndexes = new Dictionary<string, int>();
        public int Length { get { return SpeciesIdBySampleId.Count; } }

        public string[] GetAnnotations()
        {
            return AnnotationIndexes.Keys.ToArray();
        }
        public string GetSampleAnnotation(string annotation, string sampleId)
        {
            return AnnotationsBySampleId[sampleId][AnnotationIndexes[annotation]];
        }

        protected string ParseSpeciesId(string speciesId)
        {
            try
            {
                speciesId = StrtGenome.GetGenome(speciesId.ToLower().Trim()).Abbrev;
                m_SpeciesIds.Add(speciesId);
            }
            catch (ArgumentException)
            {
                speciesId = "empty";
            }
            return speciesId;
        }

        /// <summary>
        /// Reads a plate layout from specified file, or from database if project has the C1-prefix.
        /// Return null if no layout could be found.
        /// </summary>
        /// <param name="projectName"></param>
        /// <param name="sampleLayoutPath"></param>
        /// <returns></returns>
        public static PlateLayout GetPlateLayout(string projectName, string sampleLayoutPath)
        {
            PlateLayout sampleLayout = null;
            if (projectName.StartsWith(C1Props.C1ProjectPrefix))
                sampleLayout = new C1PlateLayout(projectName);
            else if (File.Exists(sampleLayoutPath))
                sampleLayout = new FilePlateLayout(sampleLayoutPath);
            return sampleLayout;
        }
    }

    public class C1PlateLayout : PlateLayout
    {
        /// <summary>
        /// Constructor that will read cell data from the C1 database
        /// </summary>
        /// <param name="plateId"></param>
        public C1PlateLayout(string plateId)
        {
            C1DB c1db = new C1DB();
            c1db.GetCellAnnotationsByPlate(plateId, out AnnotationsBySampleId, out AnnotationIndexes);
            if (AnnotationsBySampleId.Count == 0)
            {
                string chipId = plateId.Replace(C1Props.C1ProjectPrefix, "");
                c1db.GetCellAnnotationsByChip(chipId, out AnnotationsBySampleId, out AnnotationIndexes);
                if (AnnotationsBySampleId.Count == 0)
                    throw new SampleLayoutFileException("Can not extract any well/cell annotations for " + plateId + "  from C1 database.");
                Console.WriteLine("WARNING: Plate " + plateId + " has not been properly loaded. Assuming matching chip->plate wellIds.");
            }
            foreach (KeyValuePair<string, string[]> p in AnnotationsBySampleId)
            {
                string speciesId = ParseSpeciesId(p.Value[AnnotationIndexes["Species"]]);
                SpeciesIdBySampleId[p.Key] = speciesId;
            }
            if (m_SpeciesIds.Count == 0)
                throw new SampleLayoutFileException("No parseable species in database for " + plateId +
                                                    ". Change to two-letter abbrevation or full latin name (e.g. 'Hs' or 'Homo sapiens')");
            Filename = "C1 database";
        }
    }

    public class FilePlateLayout : PlateLayout
    {
        /// <summary>
        /// Constructor that reads the layout from a file
        /// </summary>
        /// <param name="plateLayoutPath"></param>
        public FilePlateLayout(string plateLayoutPath)
        {
            Filename = plateLayoutPath;
            int sampleIdIdx = -1, speciesIdIdx = -1;
            int annotationIdx = 0;
            StreamReader reader = plateLayoutPath.OpenRead();
            string line = reader.ReadLine();
            while (line.StartsWith("#")) line = reader.ReadLine();
            string[] hFields = line.Split('\t');
            for (int colIdx = 0; colIdx < hFields.Length; colIdx++)
            {
                string title = hFields[colIdx].Trim();
                if (title == "")
                    title = "Column" + (colIdx + 1).ToString();
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
                    string speciesId = ParseSpeciesId(fields[speciesIdIdx]);
                    SpeciesIdBySampleId[sampleId] = speciesId;
                    List<string> otherFields = new List<string>();
                    for (int colIdx = 0; colIdx < fields.Length; colIdx++)
                        if (colIdx != sampleIdIdx && colIdx != speciesIdIdx)
                            otherFields.Add(fields[colIdx]);
                    while (otherFields.Count < annotationIdx)
                        otherFields.Add("");
                    AnnotationsBySampleId[sampleId] = otherFields.ToArray();
                }
                line = reader.ReadLine();
            }
            reader.Close();
            if (m_SpeciesIds.Count == 0)
                throw new SampleLayoutFileException("No parseable species in " + plateLayoutPath + 
                                                    ". Use two-letter abbrevation or full latin name (e.g. 'Hs' or 'Homo sapiens')");
        }
    }
}
