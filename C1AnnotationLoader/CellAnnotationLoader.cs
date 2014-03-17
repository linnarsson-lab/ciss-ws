using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using C1;

namespace C1AnnotationLoader
{
    public class CellAnnotationLoader
    {
        public void Process(string tabFile)
        {
            C1DB db = new C1DB();
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
                            db.InsertOrUpdateCellAnnotation((int)cell.CellID, key, value);
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
