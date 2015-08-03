using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.C1;
using Linnarsson.Dna;

namespace C1AnnotationLoader
{
    public class CellAnnotationLoader
    {
        /// <summary>
        /// Parse cell annotations from tabFile and insert into cells10k database.
        /// tabFile consists of lines of:
        /// chip TAB well TAB key1 TAB value1 [TAB key2 TAB value2...]
        /// Comment lines start with '#'
        /// </summary>
        /// <param name="tabFile"></param>
        public void Process(string tabFile)
        {
            ProjectDB db = new ProjectDB();
            using (StreamReader reader = new StreamReader(tabFile))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("#"))
                        continue;
                    string[] fields = line.Split('\t');
                    string chip = fields[0].Trim();
                    string chipWell = fields[1].Trim();
                    for (int i = 2; i < fields.Length; i += 2)
                    {
                        string key = fields[i].Trim();
                        string value = fields[i + 1].Trim();
                        try
                        {
                            Cell cell = db.GetCellFromChipWell(chip, chipWell);
                            CellAnnotation ca = new CellAnnotation(null, cell.id.Value, key, value);
                            db.InsertOrUpdateCellAnnotation(ca);
                        }
                        catch (IndexOutOfRangeException)
                        {
                            Console.WriteLine("Can not find cell in DB: {0} {1}", chip, chipWell);
                        }
                    }
                }
            }
        }
    }
}
