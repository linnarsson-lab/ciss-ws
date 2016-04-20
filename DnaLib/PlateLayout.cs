using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Utilities;
using System.IO;
using Linnarsson.C1;

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
        protected HashSet<string> m_BuildIds = new HashSet<string>(); // Distinct builds corresponding to abbrevs in SpeciesIdBySampleId
        public string[] BuildIds { get { return m_BuildIds.ToArray(); } }
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

        /// <summary>
        /// Get a valid speciesId, e.g. 'mm' out of a user-specified species or build Id. Also add to buildIds HashSet.
        /// </summary>
        /// <param name="speciesId"></param>
        /// <returns></returns>
        protected string ParseSpeciesOrBuildId(string speciesId)
        {
            try
            {
                StrtGenome g = StrtGenome.GetGenome(speciesId.ToLower().Trim());
                speciesId = g.Abbrev;
                m_BuildIds.Add(g.Build);
            }
            catch (ArgumentException)
            {
                speciesId = "empty";
            }
            return speciesId;
        }

        /// <summary>
        /// Reads plate layout from DB if props.InsertCellDBData == true, else or on failure read from layoutpath.
        /// Return null if no layout could be found.
        /// </summary>
        /// <param name="plateid"></param>
        /// <param name="layoutpath"></param>
        /// <returns></returns>
        public static PlateLayout GetPlateLayout(string plateid, string layoutpath)
        {
            PlateLayout plateLayout = null;
            if (Props.props.InsertCellDBData)
            {
                try
                {
                    plateLayout = new C1PlateLayout(plateid);
                    Console.WriteLine("Plate layout was read from C1 Database.");
                }
                catch (SampleLayoutFileException)
                { }
            }
            else if (File.Exists(layoutpath))
            {
                plateLayout = new FilePlateLayout(layoutpath);
                Console.WriteLine("Plate layout was read from " + layoutpath);
            }
            else if (layoutpath != "")
                Console.WriteLine("WARNING: Can not find layout file " + layoutpath);
            return plateLayout;
        }
    }

    public class C1PlateLayout : PlateLayout
    {
        /// <summary>
        /// Constructor that will read cell data from sample/cell metadata database
        /// </summary>
        /// <param name="plateId"></param>
        public C1PlateLayout(string plateId)
        {
            IDB db = DBFactory.GetProjectDB();
            db.GetCellAnnotations(plateId, out AnnotationsBySampleId, out AnnotationIndexes);
            int spIdx = AnnotationIndexes["species"];
            int validIdx = AnnotationIndexes["valid"];
            foreach (KeyValuePair<string, string[]> p in AnnotationsBySampleId)
            {
                string speciesId = ParseSpeciesOrBuildId(p.Value[spIdx]);
                SpeciesIdBySampleId[p.Key] = (p.Value[validIdx] == "Y")? speciesId : "empty";
            }
            if (m_BuildIds.Count == 0)
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
                else if (title == "merge") title = "Merge";
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
                    string speciesId = ParseSpeciesOrBuildId(fields[speciesIdIdx]);
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
            if (m_BuildIds.Count == 0)
                throw new SampleLayoutFileException("No parseable species in " + plateLayoutPath + 
                                                    ". Use two-letter abbrevation or full latin name (e.g. 'Hs' or 'Homo sapiens')");
        }
    }
}
