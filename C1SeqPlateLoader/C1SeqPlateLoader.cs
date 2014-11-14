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
    /// In both cases, the chip(s) must already be registered in the Sanger database.
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

        public void LoadC1SeqPlate(string mixplateOrChipid, string barcodesSet)
        {
            List<Cell> plateOrderedCells;
            string mixPlateFile = C1Props.props.C1SeqPlateFilenamePattern.Replace("*", mixplateOrChipid);
            mixPlateFile = Path.Combine(C1Props.props.C1SeqPlatesFolder, mixPlateFile);
            if (File.Exists(mixPlateFile))
                plateOrderedCells = ReadSeqPlateMixFile(mixPlateFile);
            else
                plateOrderedCells = ReadChipPlateCellsFromDB(mixplateOrChipid);
            string plateid = C1Props.C1ProjectPrefix + mixplateOrChipid;
            Dictionary<int, Chip> chipsById = pdb.GetChipsById(plateOrderedCells);
            string layoutFilename = ConstructLayoutFile(plateid, plateOrderedCells, chipsById);
            pdb.UpdatePlateWellOfCells(plateOrderedCells);
            InsertNewProject(plateid, chipsById, layoutFilename, barcodesSet);
        }

        private List<Cell> ReadChipPlateCellsFromDB(string mixplateOrChipid)
        {
            List<Cell> cells;
            cells = pdb.GetCellsOfChip(mixplateOrChipid);
            if (cells.Count == 0)
                throw new Exception(string.Format("ERROR: No cells for chip {0} in DB. Register/reload the chip(s) on Sanger DB web page.", mixplateOrChipid));
            HashSet<string> emptyWells = ReadExcludeFile(mixplateOrChipid);
            foreach (Cell cell in cells)
            {
                cell.platewell = cell.chipwell;
                cell.empty = emptyWells.Contains(cell.chipwell);
            }
            return cells;
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

        private List<Cell> ReadSeqPlateMixFile(string mixFile)
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
                    string chipid = fields[0].Trim();
                    string chipWell = string.Format("{0}{1:00}", fields[1].Trim(), int.Parse(fields[2]));
                    string plateWell = string.Format("{0}{1:00}", fields[3].Trim(), int.Parse(fields[4]));
                    Cell cell = pdb.GetCellFromChipWell(chipid, chipWell);
                    if (cell == null)
                        throw new Exception(string.Format("ERROR: Chip {0} not in DB. Register/reload the chip in Sanger DB web page.", chipid));
                    cell.platewell = plateWell;
                    cells.Add(cell);
                }
            }
            return cells;
        }

        private string ConstructLayoutFile(string seqPlateName, List<Cell> plateOrderedCells, Dictionary<int, Chip> chipsById)
        {
            string layoutFile = PathHandler.GetSampleLayoutPath(seqPlateName);
            string projectPath = Path.GetDirectoryName(layoutFile);
            string layoutFilename = Path.GetFileName(layoutFile);
            if (!Directory.Exists(projectPath))
                Directory.CreateDirectory(projectPath);
            using (StreamWriter writer = new StreamWriter(layoutFile))
            {
                writer.WriteLine("SampleId\tSpecies\tC1Chip\tC1ChipWell\tSpikeMolecules\tDiameter\tArea\tRed\tGreen\tBlue");
                foreach (Cell cell in plateOrderedCells)
                {
                    Chip chip = chipsById[cell.jos_aaachipid];
                    string species = cell.empty ? "empty" : chip.species;
                    writer.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}",
                                     cell.platewell, species, chip.chipid, cell.chipwell, chip.spikemolecules,
                                     cell.diameter, cell.area, cell.red, cell.green, cell.blue);
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

        /// <summary>
        /// Insert a new project into STRT pipeline
        /// </summary>
        /// <param name="plateid">The new plate id</param>
        /// <param name="chipsById">More than one if it is a mix plate</param>
        /// <param name="layoutFile">path to the layout file</param>
        /// <param name="barcodesSet">barcode set of the new plate</param>
        private void InsertNewProject(string plateid, Dictionary<int, Chip> chipsById, string layoutFile, string barcodesSet)
        {
            HashSet<string> chipids = new HashSet<string>();
            HashSet<string> speciess = new HashSet<string>();
            HashSet<string> tissues = new HashSet<string>();
            HashSet<string> protocols = new HashSet<string>();
            HashSet<string> comments = new HashSet<string>();
            int jos_aaaclientid = -1, jos_aaacontactid = -1, jos_aaamanagerid = -1, spikemolecules = -1;
            foreach (Chip c in chipsById.Values)
            {
                chipids.Add(c.chipid);
                speciess.Add(c.species);
                tissues.Add(c.tissue);
                protocols.Add(c.strtprotocol);
                comments.Add(c.comments);
                jos_aaaclientid = (jos_aaaclientid < 0 || jos_aaaclientid == c.jos_aaaclientid)? c.jos_aaaclientid : 0;
                jos_aaacontactid = (jos_aaacontactid < 0 || jos_aaacontactid == c.jos_aaacontactid) ? c.jos_aaacontactid : 0;
                jos_aaamanagerid = (jos_aaamanagerid < 0 || jos_aaamanagerid == c.jos_aaamanagerid) ? c.jos_aaamanagerid : 0;
                if (c.spikemolecules > 0)
                    spikemolecules = (spikemolecules == -1) ? c.spikemolecules : 0; // Set to 0 if different values in chips of a mix plate
            }
            string chipid = string.Join(" / ", chipids.ToArray());
            string tissue = string.Join(" / ", tissues.ToArray());
            string comment = string.Join(" / ", comments.ToArray());
            string species = string.Join("/", speciess.ToArray());
            string protocol = string.Join(" / ", protocols.ToArray());
            if (spikemolecules == -1)
                spikemolecules = C1Props.props.SpikeMoleculeCount;
            ProjectDescription pd = new ProjectDescription(jos_aaacontactid, jos_aaamanagerid, jos_aaaclientid,
                chipid, DateTime.Now, plateid, "", species, tissue,
                "single cell", "C1", "", protocol, barcodesSet, "", layoutFile,
                comment, spikemolecules);
            pd.nSeqCycles = C1Props.props.C1RequiredSeqCycles;
            pd.nIdxCycles = C1Props.props.C1RequiredIdxCycles;
            pd.nPairedCycles = 0;
            pd.seqPrimer = C1Props.props.C1SeqPrimer;
            pd.idxPrimer = C1Props.props.C1IdxPrimer;
            pd.pairedPrimer = null;
            pdb.InsertOrUpdateProject(pd);
        }

    }
}
