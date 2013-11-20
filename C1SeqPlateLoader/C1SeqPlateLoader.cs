using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Strt;
using C1;

namespace C1SeqPlateLoader
{
    public class C1SeqPlateLoader
    {
        private ProjectDB pdb;
        private C1DB cdb;

        public C1SeqPlateLoader()
        {
            pdb = new ProjectDB();
            cdb = new C1DB();
        }

        public void LoadC1Plate(string plateOrChip)
        {
            List<Cell> cells;
            string seqPlateFile = C1Props.props.C1SeqPlateFilenamePattern.Replace("*", plateOrChip);
            seqPlateFile = Path.Combine(C1Props.props.C1SeqPlatesFolder, seqPlateFile);
            if (File.Exists(seqPlateFile))
                cells = ReadSeqPlateFile(seqPlateFile, plateOrChip);
            else
                cells = cdb.GetCellsOfChip(plateOrChip);
            cdb.AssignCellSeqPlateWell(cells);
            string layoutFile = ConstructLayoutFile(plateOrChip, cells);
            InsertNewProject(cells, layoutFile);
        }

        private string ConstructLayoutFile(string plate, List<Cell> cells)
        {
            string layoutFile = PathHandler.GetSampleLayoutPath(plate);
            using (StreamWriter writer = new StreamWriter(layoutFile))
            {
                writer.WriteLine("SampleId\tSpecies\tC1Chip\tC1ChipWell");
                foreach (Cell cell in cells)
                {
                    writer.WriteLine("{0}\t{1}\t{2}\t{3}", cell.PlateWell, cell.Species, cell.Chip, cell.ChipWell);
                }
            }
            return layoutFile;
        }

        private List<Cell> ReadSeqPlateFile(string mixFile, string plate)
        {
            List<Cell> cells = new List<Cell>();
            using (StreamReader r = new StreamReader(mixFile))
            {
                string line = r.ReadLine();
                while (line != null && !line.StartsWith("#") && !line.Contains("plate"))
                {
                    string[] fields = line.Split('\t');
                    string chip = fields[0].Trim();
                    string chipWell = string.Format("{0}{1:00}", fields[1].Trim(), int.Parse(fields[2]));
                    string plateWell = string.Format("{0}{1:00}", fields[3].Trim(), int.Parse(fields[4]));
                    Cell cell = cdb.GetCellFromChipWell(chip, chipWell);
                    cell.Plate = plate;
                    cell.PlateWell = plateWell;
                    cells.Add(cell);
                    line = r.ReadLine();
                }
            }
            return cells;
        }

        /// <summary>
        /// Insert a new project into STRT pipeline
        /// </summary>
        /// <param name="cells">full data of C1 cells that make up the project</param>
        private void InsertNewProject(List<Cell> cells, string layoutFile)
        {
            HashSet<string> speciess = new HashSet<string>();
            HashSet<string> tissues = new HashSet<string>();
            HashSet<string> chips = new HashSet<string>();
            HashSet<string> protocols = new HashSet<string>();
            foreach (Cell c in cells)
            {
                string s = c.Species.ToLower();
                if (s == "mouse" || s.StartsWith("mus")) s = "Mm";
                if (s == "human" || s.StartsWith("homo")) s = "Hs";
                speciess.Add(s);
                chips.Add(c.Chip);
                protocols.Add(c.StrtProtocol);
            }
            string chip = string.Join(" / ", chips.ToArray());
            string tissue = string.Join("/", tissues.ToArray());
            string plate = cells[0].Plate;
            string species = string.Join("/", speciess.ToArray());
            string protocol = string.Join(" / ", protocols.ToArray());
            ProjectDescription pd = new ProjectDescription("", cells[0].Operator, cells[0].PI,
                chip, DateTime.Now, plate, "", species, tissue,
                "single cell", "C1", "", protocol, C1Props.props.C1StandardBarcodeSet, "", layoutFile,
                "", C1Props.props.SpikeMoleculeCount);
            new ProjectDB().InsertNewProject(pd);
        }

        private string VerifyUniquePlateId(string chipFolder, string plateId)
        {
            int dupNo = 1;
            string newPlateId = plateId;
            while (pdb.GetProjectColumn("plateid", newPlateId, "platereference").Count > 0)
            {
                newPlateId = plateId + "_" + (++dupNo).ToString();
            }
            if (dupNo > 1)
            {
                Console.WriteLine("{0} WARNING: In {1}: Duplicated chipId, plateId will be set to {2}.",
                                    DateTime.Now, chipFolder, newPlateId);
            }
            return newPlateId;
        }

    }
}
