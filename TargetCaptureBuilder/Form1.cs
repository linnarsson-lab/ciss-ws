using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Linnarsson.Dna;
using System.Text.RegularExpressions;
using System.IO;
using Linnarsson.Utilities;

namespace TargetCaptureBuilder
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			histogram.Clear();
			RestrictionEnzyme MseI = RestrictionEnzymes.MseI;
			DnaSequence polyG = new ShortDnaSequence("GGGGGGGGGG");
			DnaSequence polyC = new ShortDnaSequence("CCCCCCCCCC");
			DnaSequence polyA = new ShortDnaSequence("AAAAAAAAAA");
			DnaSequence polyT = new ShortDnaSequence("TTTTTTTTTT");
            
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.Multiselect = true;
			if(ofd.ShowDialog() != DialogResult.OK) return;
            
			SaveFileDialog sfd = new SaveFileDialog();
			if(sfd.ShowDialog() != DialogResult.OK) return;
			var result = sfd.FileName.OpenWrite();
            char polyType = ' ';    // to get the type of poly repeats
            result.WriteLine("Sequence" + "\t" + "Seq length" + "\t" + "%GC" + "\t" + "G/C/A/T Length" + "\t" + "leftDistance" + "\t" + "rightDistance" + "\t" + "PolyType" + "\t" + "Chromosome Name" + "\t" + "Start Position in Chr");

			long totalLength = 0;
			int countFrags = 0;
			foreach(string fname in ofd.FileNames)
			{
				FastaFile ff = FastaFile.Load(fname);
                foreach(FastaRecord rec in ff.Records)
				{
					List<int> MseISites = new List<int>();
					totalLength += rec.Sequence.Count;
                    string ChrName = rec.HeaderLine; // assuming Chromosome name will be in the header line of the record
                     
                     
					// Find all MseI sites
					int offset = (int)rec.Sequence.Match(MseI.Sequence, 0);
					do
					{
						MseISites.Add(offset);
                        
						offset = (int)rec.Sequence.Match(MseI.Sequence, offset + 1);
                        
					} while(offset > 0 && offset < rec.Sequence.Count);
                    
					// Find all polyG sites & write the corresponding MseI fragments
					int m = 0;
                    offset = (int)rec.Sequence.Match(polyG, 0);
                   
					do
					{
                        while(m < MseISites.Count && MseISites[m] < offset) m++;
                        if(m >= MseISites.Count) break;
                        polyType = 'G';
                        if (Report(result, rec.Sequence.SubSequence(MseISites[m - 1] + 1, MseISites[m] - MseISites[m - 1]), offset - MseISites[m - 1] - 1, polyType, ChrName, offset )) countFrags++;
                        //MessageBox.Show(offset.ToString());
                        offset = (int)rec.Sequence.Match(polyG, MseISites[m]);
                          
					} while(offset > 0 && offset < rec.Sequence.Count);

					// Find all polyC sites & write the corresponding MseI fragments
					m = 0;
					offset = (int)rec.Sequence.Match(polyC, 0);
					do
					{
						while(m < MseISites.Count && MseISites[m] < offset) m++;
						if(m >= MseISites.Count) break;
                        polyType = 'C';
                        if (Report(result, rec.Sequence.SubSequence(MseISites[m - 1] + 1, MseISites[m] - MseISites[m - 1]), offset - MseISites[m - 1] - 1, polyType, ChrName, offset  )) countFrags++;
                        offset = (int)rec.Sequence.Match(polyC, MseISites[m]);
                        
					} while(offset > 0 && offset < rec.Sequence.Count);

                    // Find all polyA sites & write the corresponding MseI fragments
                    m = 0;
                    offset = (int)rec.Sequence.Match(polyA, 0);
                    do
                    {
                        while (m < MseISites.Count && MseISites[m] < offset) m++;
                        if (m >= MseISites.Count) break;
                        polyType = 'A';
                        if (Report(result, rec.Sequence.SubSequence(MseISites[m - 1] + 1, MseISites[m] - MseISites[m - 1]), offset - MseISites[m - 1] - 1, polyType, ChrName, offset )) countFrags++;
                        offset = (int)rec.Sequence.Match(polyA, MseISites[m]);
                        
                    } while (offset > 0 && offset < rec.Sequence.Count);

                    // Find all polyT sites & write the corresponding MseI fragments
                    m = 0;
                    offset = (int)rec.Sequence.Match(polyT, 0);
                    do
                    {
                        while (m < MseISites.Count && MseISites[m] < offset) m++;
                        if (m >= MseISites.Count) break;
                        polyType = 'T';
                        if (Report(result, rec.Sequence.SubSequence(MseISites[m - 1] + 1, MseISites[m] - MseISites[m - 1]), offset - MseISites[m - 1] - 1, polyType, ChrName, offset )) countFrags++;
                        offset = (int)rec.Sequence.Match(polyT, MseISites[m]);
                        
                    } while (offset > 0 && offset < rec.Sequence.Count);

				}
				Console.WriteLine("Fragments: " + countFrags.ToString() + " length: " + (totalLength / 1e6).ToString() + " Mbp");
				ff = null;
			}
			foreach(var kvp in histogram)
			{
				Console.WriteLine(kvp.Key + "\t" + kvp.Value);
			}
			result.Close();
            MessageBox.Show("Programme executed successfully") ;
		}

		SortedDictionary<int, int> histogram = new SortedDictionary<int, int>();
		public bool Report(StreamWriter result, DnaSequence seq, int leftDistance, char polyType, string ChrName, int OFFSET)
		{
			if(seq.Count < 75 || seq.Count > 600) return false;
			int temp = leftDistance;
			byte nt = seq[temp];
			while(seq[temp] == nt) temp++;
			int gLength = temp - leftDistance;
			if(gLength > 40) return false;
			if(!histogram.ContainsKey(gLength)) histogram[gLength] = 1;
			else histogram[gLength] = histogram[gLength] + 1;
			int rightDistance = (int)(seq.Count - leftDistance - gLength);
			if(leftDistance > 65 && rightDistance > 65) return false;
			if(leftDistance < 20 || rightDistance < 20) return false;

			// Sequence Length GC GLength LeftFlank RightFlank
			result.WriteLine(seq.ToString() + "\t" + seq.Count + "\t" + seq.CountCases(IupacEncoding.GC)*100/seq.Count + "\t" + gLength + "\t" + leftDistance + "\t" + rightDistance + "\t" + polyType + "\t" + ChrName + "\t" + (OFFSET - leftDistance +1 )  );
			return true;
		}

		private void Form1_Load(object sender, EventArgs e)
		{

		}

		private void button2_Click(object sender, EventArgs e)
		{
			
			// Load the file, reformat for Mathematica
			OpenFileDialog ofd = new OpenFileDialog();
			if(ofd.ShowDialog() == DialogResult.OK)
			{
				var samples = new Dictionary<string, string>();
				samples["ID_REF"] = "Probe";
				samples["IDENTIFIER"] = "Gene";
				StreamReader reader = new StreamReader(ofd.FileName);
				while(true)
				{
					string line = reader.ReadLine();
					if(line.StartsWith("#GSM"))
					{
						string id = line.Substring(1, 8);
						string tissue = line.Split(new string[] {"src: "}, StringSplitOptions.None)[1];
						samples[id] = tissue;
					}
					if(line.StartsWith("!dataset_table_begin"))
					{
						// Reader the header line
						line = reader.ReadLine();
						SaveFileDialog sfd = new SaveFileDialog();
						if(sfd.ShowDialog() == DialogResult.OK)
						{
							StreamWriter writer = new StreamWriter(sfd.FileName);
							string[] headers = line.Split('\t');
							foreach(string hdr in headers)
							{
								writer.Write(samples[hdr]);
								writer.Write('\t');
							}
							writer.WriteLine();
							while(true)
							{
								line = reader.ReadLine();
								if(line == "!dataset_table_end")
								{
									writer.Close();
									break;
								}
								writer.WriteLine(line);
							}
						}
						reader.Close();
						return;
					}
				}
			}
		}

		private void button3_Click(object sender, EventArgs e)
		{
			OpenFileDialog ofd = new OpenFileDialog();
			Dictionary<char, int[]> table = new Dictionary<char, int[]>();
			table['.'] = new int[50];
			table['0'] = new int[50];
			table['1'] = new int[50];
			table['2'] = new int[50];
			table['3'] = new int[50];
			if(ofd.ShowDialog() == DialogResult.OK)
			{
				StreamReader sr = new StreamReader(ofd.FileName);
				int count = 0;
				sr.ReadLine();
				while(true)
				{
					sr.ReadLine();
					string line = sr.ReadLine();
					count++;
					if(count < 2500000) continue;
					if(count > 3000000) break;
					for(int i = 1; i < line.Length - 1; i++)
					{
						table[line[i]][i]++;	
					} 
				}
				Console.WriteLine(".\t0\t1\t2\t3");
				for(int j = 0; j < 50; j++)
				{
					Console.Write(table['.'][j]);
					Console.Write('\t');
					Console.Write(table['0'][j]);
					Console.Write('\t');
					Console.Write(table['1'][j]);
					Console.Write('\t');
					Console.Write(table['2'][j]);
					Console.Write('\t');
					Console.Write(table['3'][j]);
					Console.WriteLine();
				}
			}
		}

		private void button4_Click(object sender, EventArgs e)
		{
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.Multiselect = true;
			if(ofd.ShowDialog() == DialogResult.OK)
			{
				Dictionary<string, int[]> stats = new Dictionary<string, int[]>();
				foreach(string fname in ofd.FileNames)
				{
					var file = fname.OpenRead();

					while(true)
					{
						if(file.ReadLine() == null) break;
						string seq = file.ReadLine();
						file.ReadLine();
						file.ReadLine();

						int ix = seq.IndexOf("CCCCCCCCCC");
						if(ix > 11)
						{
							string key = seq.Substring(ix - 12, 12);
							int len = 0;
							while(ix < seq.Length && seq[ix] == 'C')
							{
								len++; ix++;
							}

							if(!stats.ContainsKey(key))
							{
								stats[key] = new int[50];
							}
							stats[key][len]++;
						}
					}
					file.Close();
				}

				foreach(var kvp in stats)
				{
					int sum = kvp.Value.Sum();
					if(sum > 10)
					{
						StringBuilder sb = new StringBuilder();
						foreach(int x in kvp.Value) sb.Append(x.ToString() + "\t");
						Console.WriteLine(sb.ToString());
                       // MessageBox.Show(sb.ToString());  
					}
				}
			}

		}

		private void button5_Click(object sender, EventArgs e)
		{
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.Title = "Select a fasta file containing the targets";
			if(ofd.ShowDialog() == DialogResult.OK)
			{
				var targets = FastaFile.Load(ofd.FileName);
				var output = (Path.Combine(Path.GetDirectoryName(ofd.FileName),Path.GetFileNameWithoutExtension(ofd.FileName) + "_tiling.fa")).OpenWrite();
				TmCalculator tm = new TmCalculator();
				foreach(var rec in targets.Records)
				{
					int ix = 0;
					int len = 20;
					while(ix + len < rec.Sequence.Count)
					{
						DnaSequence seq = rec.Sequence.SubSequence(ix, len);
						while(tm.GetTm(seq, 0.000001, 0.000001, 0.1) < 50)
						{
							len++;
							if(ix + len == rec.Sequence.Count) break;
							seq = rec.Sequence.SubSequence(ix, len);
						}
						output.WriteLine(">" + rec.Identifier + " \\start=" + ix.ToString() + " \\length=" + len.ToString());
						output.WriteLine(seq.ToString());
						ix += len;
					}
				}
				output.Close();
			}
		}


	}
}
