using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;
using Linnarsson.Dna;
using Linnarsson.Mathematics.SortSearch;
using Linnarsson.Mathematics;
using Linnarsson.Strt;
using Linnarsson.Utilities;

namespace SilverBullet
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();

			// Ensure we can get feedback from deep down
            Background.Register(lblStatus, toolStripProgressBar1);
		}

		private void extractToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if(Background.IsBusy)
			{
				if(MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
			}
            List<string> laneArgs = new List<string>();
            RunsLanesDialog rld = new RunsLanesDialog();
            if (rld.ShowDialog() == DialogResult.OK)
                laneArgs = rld.laneArgs;
            string barcodeSet = SelectBarcodeSet();
            if (rld.projectName != "" && barcodeSet != null && laneArgs.Count > 0)
            {
                SetupMapper(barcodeSet);
                Background.RunAsync(() => { mapper.Extract(rld.projectName, laneArgs); Console.WriteLine("Done."); });
			}
		}
	
		private void Form1_Load(object sender, EventArgs e)
		{
			Console.WriteLine("SilverBullet single-cell transcriptome mapping tool\r\nVersion 1.0, (C) Sten Linnarsson 2012\r\n");
		}

		private void interruptToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if(Background.IsBusy)
			{
				Console.WriteLine("Cancelling...");
				Background.Cancel();
			}
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			if(Background.IsBusy)
			{
				Console.WriteLine("Cancelling...");
				Background.Cancel();
			}
		}

		// Use a common mapper object, so the index can be reused
		StrtReadMapper mapper;

        private void runBowtieToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Background.IsBusy)
            {
                if (MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
            }
            string projectFolder = SelectProject();
            string barcodeSet = "v4";
            if (projectFolder != null)
            {
                Dictionary<string, bool> bcSets = new Dictionary<string, bool>();
                foreach (string dirName in Directory.GetDirectories(projectFolder, "*Extracted_"))
                {
                    Match m = Regex.Match(dirName, ".*Extracted_([^_]+)_");
                    if (m.Success) bcSets[m.Groups[1].Value] = true;
                }
                if (bcSets.Count > 1)
                    barcodeSet = SelectBarcodeSet();
                else if (bcSets.Count == 1)
                    barcodeSet = bcSets.Keys.ToArray()[0];
                GenomeDialog gd = new GenomeDialog();
                gd.ShowDialog();
                GeneVariantsDialog gvd = new GeneVariantsDialog();
                gvd.ShowDialog();
                gd.Genome.GeneVariants = gvd.AnalyzeAllGeneVariants;
                SetupMapper(barcodeSet);
                Background.RunAsync(() =>
                {
                    Background.Message("Mapping...");
                    mapper.Map(projectFolder, gd.Genome);
                    Background.Message("Ready");
                    Console.WriteLine("Done.");
                });
            }
        }

        private void annotateFromBowtieToolStripMenuItem_Click(object sender, EventArgs e)
        {
			if(Background.IsBusy)
			{
				if(MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
			}
			// Locate the extraction folder
			FolderBrowserDialog fbd = new FolderBrowserDialog();
			fbd.Description = "Locate the extraction folder";
			fbd.ShowNewFolderButton = false;
			fbd.SelectedPath = Props.props.ProjectsFolder;
			if(fbd.ShowDialog() == DialogResult.OK)
			{
				GenomeDialog gd = new GenomeDialog();
				gd.ShowDialog();
                GeneVariantsDialog gvd = new GeneVariantsDialog();
                gvd.ShowDialog();
				string mapFolder = fbd.SelectedPath;
				SetupMapper(null);
				Background.RunAsync( () =>
				{
                    Background.Message("Annotating...");
                    try
                    {
                        gd.Genome.GeneVariants = gvd.AnalyzeAllGeneVariants;
                        string projectFolder = Path.GetDirectoryName(mapFolder);
                        mapper.Annotate(mapFolder, gd.Genome);
                    }
                    catch (Exception exp)
                    {
                        Console.WriteLine("ERROR: " + exp);
                    }
					Background.Message("Ready.");
					Background.Progress(100);
					Console.WriteLine("Done.");
				});
			}
		}

		private void buildIndexToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if(Background.IsBusy)
			{
				if(MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
			}
			GenomeDialog gd = new GenomeDialog();
			gd.ShowDialog();

			// Locate the extraction folder
			SetupMapper(null);
			Background.RunAsync(() =>
			{
                Background.Message("Building index...");
                try
                {
                    mapper.BuildJunctionsAndIndex(gd.Genome);
                }
				catch (Exception exp)
				{
                    Console.WriteLine("*** Error in Form1.buildIndexToolStripMenuItem_Click:" + exp.Message);
				}
				Background.Message("Ready");
				Background.Progress(100);
				Console.WriteLine("Done.");
			});
		}

		private void SetupMapper(string barcodeSet)
		{
            if (barcodeSet != null)
                Props.props.BarcodesName = barcodeSet;
            mapper = new StrtReadMapper(Props.props);
		}

		private void peekAtgzFileToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenFileDialog ofd = new OpenFileDialog();
			if(ofd.ShowDialog() == DialogResult.OK)
			{
				var file = ofd.FileName.OpenRead();
				for(int i = 0; i < 100; i++)
				{
					Console.WriteLine(file.ReadLine());
				}
				file.Close();
			}
		}

		private void mergeAndNormalizeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			MergeTool m = new MergeTool();
			if(m.ShowDialog() == DialogResult.OK)
			{
				SaveFileDialog sfd = new SaveFileDialog();
				if(sfd.ShowDialog() == DialogResult.OK)
				{
					// Do the merge
					Background.RunAsync(() =>
						{
							TableMerger tm = new TableMerger(m);
							foreach(string file in m.InputFiles) tm.AddTable(file,true);
							tm.Save(sfd.FileName);
						});
				}
			}
		}

		private void splitByBarcodeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if(Background.IsBusy)
			{
				if(MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
			}
            string projectFolder = SelectProject();
			if(projectFolder != null)
			{
                string barcodeSet = SelectBarcodeSet();
                SetupMapper(barcodeSet);
                Background.RunAsync(() => {
                    Background.Message("Splitting...");
                    mapper.Split(projectFolder);
                    Background.Message("Ready");
                    Console.WriteLine("Done.");
                });
			}
		}

        private void barcodeStatsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Background.IsBusy)
            {
                if (MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
            }
            string projectFolder = SelectProject();
            if (projectFolder!= null)
            {
                string barcodeSet = SelectBarcodeSet();
                SetupMapper(barcodeSet);
                Background.RunAsync(() => {
                    Background.Message("Calculating stats...");
                    mapper.BarcodeStats(projectFolder);
                    Background.Message("Ready");
                    Console.WriteLine("Done.");
                });
            }
        }

        private void buildSplicedExonsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Background.IsBusy)
            {
                if (MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
            }
            GenomeDialog gd = new GenomeDialog();
            gd.ShowDialog();
            SetupMapper(null);
            Background.RunAsync(() =>
            {
                Background.Message("Building junctions...");
                try
                {
                    mapper.BuildJunctions(gd.Genome);
                }
                catch (Exception exp)
                {
                    Console.WriteLine("*** Error in Form1.buildSplicedExonsToolStripMenuItem_Click: " + exp);
                }
                Background.Message("Ready");
                Background.Progress(100);
                Console.WriteLine("Done.");
            });

        }

        private static string SelectBarcodeSet()
        {
            string barcodeSet = null;
            BarcodeSetDialog bsd = new BarcodeSetDialog();
            if (bsd.ShowDialog() == DialogResult.OK)
                barcodeSet = bsd.barcodeSet;
            return barcodeSet;
        }

        private static string SelectProject()
        {
            return SelectProject("Locate the project folder");
        }
        private static string SelectProject(string msg)
        {
            string selectedFolder = null;
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.SelectedPath = Props.props.ProjectsFolder;
            fbd.Description = msg;
            fbd.ShowNewFolderButton = false;
            if (fbd.ShowDialog() == DialogResult.OK)
                selectedFolder = fbd.SelectedPath;
            return selectedFolder;
        }

        private void updateAnnotationsToolStripMenuItem_Click(object sender, EventArgs e)
        {
			if(Background.IsBusy)
			{
				if(MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
			}
            GenomeDialog gd = new GenomeDialog();
            gd.ShowDialog();
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Locate errors file in an Annotation folder for build "
                         + gd.Genome.GetBowtieMainIndexName();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                Background.RunAsync(() =>
                {
                    SetupMapper(null);
                    try
                    {
                        mapper.UpdateSilverBulletGenes(gd.Genome, ofd.FileName);
                    }
                    catch (ArgumentException exc)
                    {
                        Console.WriteLine("Error in Form1.updateAnnotationsToolStripMenuItem_Click: " + exc);
                    }
                });
            }
        }

        private void importFastaFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Background.IsBusy)
            {
                if (MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
            }
			OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Locate a fasta sequence file you want to import into the STRT pipeline";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string fastaFile = ofd.FileName;
                string barcodeSet = SelectBarcodeSet();
                TextDialog nameDialog = new TextDialog();
                nameDialog.ShowDialog();
                string projectName = nameDialog.value;
                SetupMapper(barcodeSet);
                mapper.ConvertToReads(fastaFile, projectName, 25, 50);
            }
        }

        private void synthesizeReadsFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Background.IsBusy)
            {
                if (MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
            }
            Console.WriteLine("Will make synthetic read data including SNPs, random mutations etc.");
            TextDialog readIdDialog = new TextDialog("Name for the synthetic data:");
            readIdDialog.ShowDialog();
            string outputId = readIdDialog.value;
            string barcodeSet = SelectBarcodeSet();
            GenomeDialog gd = new GenomeDialog();
            gd.ShowDialog();
            GeneVariantsDialog gvd = new GeneVariantsDialog();
            gvd.ShowDialog();
            gd.Genome.GeneVariants = gvd.AnalyzeAllGeneVariants;
            Background.RunAsync(() => 
                {
                    Background.Message("Synthesizing reads...");
                    SyntReadMaker srm = new SyntReadMaker(Barcodes.GetBarcodes(barcodeSet), gd.Genome);
                    Console.WriteLine(srm.SettingsString());
                    srm.SynthetizeReads(outputId);
                    Background.Message("Ready");
                });
        }

        private void sortMapFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Background.IsBusy)
            {
                if (MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
            }
			OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Locate a .map file you want to sort";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string mapFile = ofd.FileName;
                BowtieMapFileSorter s = new BowtieMapFileSorter();
                Background.RunAsync(() =>
                 {
                     Background.Message("Sorting...");
                     s.SortMapFile(mapFile);
                     Background.Message("Ready");
                 });
            }
        }

        private void dumpTranscriptsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Background.IsBusy)
            {
                if (MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
            }
            Console.WriteLine("Will construct synthetic exon/splc reads with given barcode set from the transcripts of a genome.");
            BarcodeSetDialog bsd = new BarcodeSetDialog();
            bsd.ShowDialog();
            Barcodes bc = Barcodes.GetBarcodes(bsd.barcodeSet);
            GenomeDialog gd = new GenomeDialog();
            gd.ShowDialog();
            GeneVariantsDialog gvd = new GeneVariantsDialog();
            gvd.ShowDialog();
            mapper = new StrtReadMapper(Props.props);
            gd.Genome.GeneVariants = gvd.AnalyzeAllGeneVariants;
            Console.WriteLine("Read lengths will be 44bp. All splices are made.");
			SaveFileDialog ofd = new SaveFileDialog();
            ofd.Title = "Specify a FastQ file to save the reads to.";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string fastaFile = ofd.FileName;
                Background.RunAsync(() =>
                {
                    Background.Message("Writing fastQ file...");
                    mapper.DumpTranscripts(bc, gd.Genome, 44, 1, 0, fastaFile, true, 3, 10);
                    Background.Message("Ready");
                });
            }
        }

        private void analyzeMapSNPsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Background.IsBusy)
            {
                if (MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
            }
            Console.WriteLine("Will search for potential SNP positions in a Bowtie .map output file.");
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.DefaultExt = "map";
            ofd.Multiselect = true;
            ofd.Title = "Locate .map file to analyze.";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                BarcodeSetDialog bsd = new BarcodeSetDialog();
                bsd.ShowDialog();
                Barcodes bc = Barcodes.GetBarcodes(bsd.barcodeSet);
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = Path.Combine(Path.GetDirectoryName(ofd.FileNames[0]), "snp.data");
                sfd.Title = "Specify output file to write SNP locations to.";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    Background.RunAsync(() =>
                    {
                        Background.Message("Finding SNPs...");
                        MapFileSnpFinder mfsf = new MapFileSnpFinder(bc);
                        List<string> files = ofd.FileNames.ToList();
                        mfsf.ProcessMapFiles(files);
                        mfsf.WriteToFile(sfd.FileName);
                        Background.Message("Ready");
                    });
                }
            }
        }

	}
}
