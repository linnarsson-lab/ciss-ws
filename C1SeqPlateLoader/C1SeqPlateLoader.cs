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
    /// <summary>
    /// Use to load or reload a sequencing plate from C1 cell data as a (new) sequencing project plate.
    /// Either a whole C1 chip can be loaded, or a mixed plate from several chips, in which case a
    /// mapping file must exist telling which chip well is loaded into each sequencing plate well.
    /// A PlateLayout file is constructed containing this mapping and the species of the cell in each well.
    /// </summary>
    public class C1SeqPlateLoader
    {
        private ProjectDB pdb;
        private C1DB cdb;
        private bool useExcluded = false;

        public C1SeqPlateLoader(bool useExcluded)
        {
            this.useExcluded = useExcluded;
            pdb = new ProjectDB();
            cdb = new C1DB();
        }

        public void LoadC1SeqPlate(string plateOrChip, string barcodesSet)
        {
            List<Cell> cells;
            string seqPlateFile = C1Props.props.C1SeqPlateFilenamePattern.Replace("*", plateOrChip);
            seqPlateFile = Path.Combine(C1Props.props.C1SeqPlatesFolder, seqPlateFile);
            string seqPlateName = C1Props.C1ProjectPrefix + plateOrChip;
            if (File.Exists(seqPlateFile))
            {
                cells = ReadSeqPlateMixFile(seqPlateFile, seqPlateName);
                if (cells.Count == 0)
                    throw new Exception(string.Format("ERROR: Could not find any 10kCells using plate mix file {0}.", seqPlateFile));
            }
            else
            {
                cells = cdb.GetCellsOfChip(plateOrChip);
                if (cells.Count == 0)
                    throw new Exception(string.Format("ERROR: Could not find any 10kCells in database for chip {0}.", plateOrChip));
                HashSet<string> emptyWells = ReadExcludeFile(plateOrChip);
                foreach (Cell c in cells)
                {
                    c.Plate = seqPlateName;
                    c.PlateWell = c.ChipWell;
                    if (emptyWells.Contains(c.ChipWell))
                        c.Species = "Empty";
                }
            }
            cdb.UpdateDBCellSeqPlateWell(cells);
            string layoutFilename = ConstructLayoutFile(seqPlateName, cells);
            InsertNewProject(cells, layoutFilename, barcodesSet);
        }

        private HashSet<string> ReadExcludeFile(string chip)
        {
            HashSet<string> emptyWells = new HashSet<string>();
            string chipFolderName = chip.Substring(0, 4) + "-" + chip.Substring(4);
            string chipFolder = Path.Combine(C1Props.props.C1RunsFolder, chipFolderName);
            string[] excludeFiles = Directory.GetFiles(chipFolder, C1Props.props.WellExcludeFilePattern);
            if (excludeFiles.Length == 1)
            {
                using (StreamReader reader = new StreamReader(excludeFiles[0]))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line == "" || line.StartsWith("#") || line.Contains("row"))
                            continue;
                        line = line.Trim();
                        char row = line[0];
                        int wellNo = int.Parse(line.Substring(line.Contains("\t") ? line.IndexOf('\t') : 1));
                        string well = string.Format("{0}{1:00}", row, wellNo);
                        emptyWells.Add(well);
                    }
                }
            }
            return emptyWells;
        }

        private string ConstructLayoutFile(string seqPlateName, List<Cell> cells)
        {
            string layoutFile = PathHandler.GetSampleLayoutPath(seqPlateName);
            string projectPath = Path.GetDirectoryName(layoutFile);
            string layoutFilename = Path.GetFileName(layoutFile);
            if (!Directory.Exists(projectPath))
                Directory.CreateDirectory(projectPath);
            using (StreamWriter writer = new StreamWriter(layoutFile))
            {
                writer.WriteLine("SampleId\tSpecies\tC1Chip\tC1ChipWell");
                foreach (Cell cell in cells)
                {
                    writer.WriteLine("{0}\t{1}\t{2}\t{3}", cell.PlateWell, cell.Species, cell.Chip, cell.ChipWell);
                }
            }
            try
            {
                File.Copy(layoutFile, Path.Combine(Props.props.UploadsFolder, layoutFilename), true);
            }
            catch (Exception)
            {
                Console.WriteLine("NOTE: Could not load layout to DB!");
                Console.WriteLine("Open Sanger DB and specify layout {0} for {1} under Samples/Edit!", layoutFile, seqPlateName);
                layoutFilename = "";
            }
            return layoutFilename;
        }

        private List<Cell> ReadSeqPlateMixFile(string mixFile, string seqPlateName)
        {
            List<Cell> cells = new List<Cell>();
            using (StreamReader r = new StreamReader(mixFile))
            {
                string line; 
                while ((line = r.ReadLine()) != null)
                {
                    if (line == "" || line.StartsWith("#") || line.Contains("plate"))
                        continue;
                    string[] fields = line.Split('\t');
                    string chip = fields[0].Trim();
                    string chipWell = string.Format("{0}{1:00}", fields[1].Trim(), int.Parse(fields[2]));
                    string plateWell = string.Format("{0}{1:00}", fields[3].Trim(), int.Parse(fields[4]));
                    Cell cell = cdb.GetCellFromChipWell(chip, chipWell);
                    cell.Plate = seqPlateName;
                    cell.PlateWell = plateWell;
                    cells.Add(cell);
                }
            }
            return cells;
        }

        /// <summary>
        /// Insert a new project into STRT pipeline
        /// </summary>
        /// <param name="cells">full data of C1 cells that make up the project</param>
        private void InsertNewProject(List<Cell> cells, string layoutFile, string barcodesSet)
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
                if (s.ToLower() != "empty") speciess.Add(s);
                chips.Add(c.Chip);
                protocols.Add(c.StrtProtocol);
                tissues.Add(c.Tissue);
            }
            string chip = string.Join(" / ", chips.ToArray());
            string tissue = string.Join("/", tissues.ToArray());
            string plate = cells[0].Plate;
            string species = string.Join("/", speciess.ToArray());
            string protocol = string.Join(" / ", protocols.ToArray());
            ProjectDescription pd = new ProjectDescription(cells[0].Scientist, cells[0].Operator, cells[0].PI,
                chip, DateTime.Now, plate, "", species, tissue,
                "single cell", "C1", "", protocol, barcodesSet, "", layoutFile,
                cells[0].Comments, C1Props.props.SpikeMoleculeCount);
            pd.nSeqCycles = C1Props.props.C1RequiredSeqCycles;
            pd.nIdxCycles = C1Props.props.C1RequiredIdxCycles;
            pd.nPairedCycles = 0;
            pd.seqPrimer = C1Props.props.C1SeqPrimer;
            pd.idxPrimer = C1Props.props.C1IdxPrimer;
            pd.pairedPrimer = null;
            new ProjectDB().InsertOrUpdateProject(pd);
        }

    }
}
