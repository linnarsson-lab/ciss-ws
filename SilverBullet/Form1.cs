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
			Console.WriteLine("SilverBullet single-cell transcriptome mapping tool\r\nVersion 1.0, (C) Sten Linnarsson 2009\r\n");
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

		private void qCAnalysisToolStripMenuItem_Click(object sender, EventArgs e)
		{
            MessageBox.Show("This function is now a separate application.");
			//QcForm qcf = new QcForm();
			//qcf.Show();
		}

        private void runBowtieToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Background.IsBusy)
            {
                if (MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
            }
            string projectFolder = SelectProject();
            string barcodeSet = "v1";
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
                    mapper.Map(projectFolder, gd.Genome);
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
                    try
                    {
                        gd.Genome.GeneVariants = gvd.AnalyzeAllGeneVariants;
                        string projectFolder = Path.GetDirectoryName(mapFolder);
                        mapper.Annotate(mapFolder, gd.Genome);
                    }
                    catch (NoAnnotationsFileFoundException nafe)
                    {
                        Console.WriteLine("ERROR: " + nafe.Message);
                    }
                    catch (ChromosomeMissingException ce)
                    {
                        Console.WriteLine(ce.Message);
                        Console.WriteLine("Make sure that the proper fasta/genbank file is in the genomes directory.");
                    }
                    catch (NoMapFilesFoundException me)
                    {
                        Console.WriteLine(me.Message);
                        Console.WriteLine("You may have forgotten to run Bowtie with the proper settings.");
                    }
                    //catch (Exception exp)
                    //{
                    //    Console.WriteLine("Error in Form1.annotateFromBowtieToolStripMenuItem_Click: " + exp);
                    //}
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
                try
                {
                    mapper.BuildJunctionsAndIndex(gd.Genome, "");
                }
				catch (Exception exp)
				{
                    Console.WriteLine("*** Error in Form1.buildIndexToolStripMenuItem_Click:" + exp.Message);
				}
				Background.Message("Ready.");
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

		private void annotateFromWiggleToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if(Background.IsBusy)
			{
				if(MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
			}

			// Locate the extraction folder
			FolderBrowserDialog fbd = new FolderBrowserDialog();
			fbd.Description = "Locate the extraction folder (with *.wig files)";
			fbd.ShowNewFolderButton = false;
			if(fbd.ShowDialog() == DialogResult.OK)
			{
				GenomeDialog gd = new GenomeDialog();
				gd.ShowDialog();

				string wigFolder = fbd.SelectedPath;
				SetupMapper(null);
				Background.RunAsync(() =>
				{
					try
					{
						mapper.AnnotateFromWiggles(wigFolder, gd.Genome);
					}
                    catch (NoAnnotationsFileFoundException)
                    {
                        Console.WriteLine("No index was found (use Tools->Build Index)");
                    }
                    catch (NoMapFilesFoundException)
                    {
                        Console.WriteLine("No .map files were found (use Pipeline->Run Bowtie)");
                    }
                    catch (Exception exp)
					{
						Console.WriteLine("*** Error in Form1.annotateFromWiggleToolStripMenuItem_Click: " + exp.Message);
					}
					Background.Message("Ready.");
					Background.Progress(100);
					Console.WriteLine("Done.");
				});
			}
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
                Background.RunAsync(() => { mapper.Split(projectFolder); Console.WriteLine("Done."); });
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
                Background.RunAsync(() => { mapper.BarcodeStats(projectFolder); Console.WriteLine("Done."); });
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
                if (true) //try
                {
                    mapper.BuildJunctions(gd.Genome);
                }
                //catch (Exception exp)
                {
                //    Console.WriteLine("*** Error in Form1.buildSplicedExonsToolStripMenuItem_Click: " + exp);
                }
                Background.Message("Ready.");
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
                         + gd.Genome.GetBowtieIndexName();
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
			SaveFileDialog ofd = new SaveFileDialog();
            ofd.Title = "Specify a FASTA file to save the synthetic reads to.";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string fastaFile = ofd.FileName;
                string barcodeSet = SelectBarcodeSet();
                GenomeDialog gd = new GenomeDialog();
                gd.ShowDialog();
                GeneVariantsDialog gvd = new GeneVariantsDialog();
                gvd.ShowDialog();
                SetupMapper(barcodeSet);
                gd.Genome.GeneVariants = gvd.AnalyzeAllGeneVariants;
                Console.WriteLine("Read lengths will be mostly 50bp , but some reads down to 46 bp.");
                Background.RunAsync(() => 
                   { mapper.SynthetizeReads(gd.Genome, fastaFile); } );
            }
        }

        private void rerunAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Background.IsBusy)
            {
                if (MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
            }
            ProjectDB pdb = new ProjectDB();
            foreach (ProjectDescription pd in pdb.GetProjectDescriptions())
            {
                Console.WriteLine("Updating {0}...", pd.projectName);
                Props.props.BarcodesName = pd.barcodeSet;
                mapper = new StrtReadMapper(Props.props);
                mapper.Extract(pd);
                mapper.MapAndAnnotateWithLayout(pd.ProjectFolder, pd.defaultSpecies, Props.props.AnalyzeAllGeneVariants);
            }
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
                 { s.SortMapFile(mapFile); });
            }
        }

        private void dumpTranscriptsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Background.IsBusy)
            {
                if (MessageBox.Show("A previous task has not yet completed. Do you wish to proceed anyway?", "Conflicting task", MessageBoxButtons.YesNo) == DialogResult.No) return;
            }
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
            ofd.Title = "Specify a FQ file to save the reads to.";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string fastaFile = ofd.FileName;
                Background.RunAsync(() =>
                { mapper.DumpTranscripts(bc, gd.Genome, 44, 1, 0, fastaFile, true, 3, 10); });
            }
        }

	}
}
