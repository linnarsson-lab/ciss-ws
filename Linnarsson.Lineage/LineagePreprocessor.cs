using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Utilities;
using Linnarsson.Dna;
using System.IO;
using Linnarsson.Mathematics;
using System.Diagnostics;
using Linnarsson.Mathematics.Automata;

namespace Linnarsson.Lineage
{
	public class LineagePreprocessor
	{
		/// <summary>
		/// filename -> sampleId (from config file)
		/// </summary>
		public Dictionary<string, string> SampleIdPerFileName = new Dictionary<string, string>();
		public string MosaikPath { get; set; }
		public string PhylipPath { get; set; }
		string ReferenceDatPath;
		public string ReferenceFastaPath { get; set; }
		public string ConfigFile { get; set; }
		public float MinimumFractionExplained { get; set; }
		public int MinimumFlank { get; set; }
		public int MinimumTotalReads { get; set; }
		public string BuildFolder { get; set; }
		public bool Rebuild { get; set; }

		public LineagePreprocessor()
		{
			BuildFolder = "Build_" + DateTime.Now.ToPathSafeString();
			if(!Directory.Exists(BuildFolder)) Directory.CreateDirectory(BuildFolder);
			MinimumFractionExplained = 0.5f;
			MinimumTotalReads = 10;
			MinimumFlank = 4;
			MosaikPath = "/data/sequtils/mosaik-aligner/bin";
			PhylipPath = "/data/sequtils/phylip-3.69/exe";
			Rebuild = false;
		}

		public void Analyze()
		{
			if(!Directory.Exists(MosaikPath))
			{
				Console.WriteLine("Mosaik folder not found at " + MosaikPath);
				return;
			}
			if(!Directory.Exists(PhylipPath))
			{
				Console.WriteLine("Phylip folder not found at " + MosaikPath);
				return;
			}
			if(!File.Exists(ReferenceFastaPath))
			{
				Console.WriteLine("Reference .fa file not found at " + ReferenceFastaPath);
				return;
			}

			ReferenceDatPath = Path.ChangeExtension(ReferenceFastaPath, ".dat");
			Console.WriteLine("Using Mosaik at: " + MosaikPath);
			Console.WriteLine("Using Phylip at: " + PhylipPath);
			Console.WriteLine("Using reference .dat at: " + ReferenceDatPath);
			Console.WriteLine("Using reference .fa at: " + ReferenceFastaPath);

			if(!File.Exists(ReferenceDatPath))
			{
				Console.WriteLine("Reference .dat file not found; building it...");
				if(!MosaikBuildRef()) return;
			} 

			DetermineSampleIds(ConfigFile);
			Console.WriteLine("Found " + SampleIdPerFileName.Count + " samples.");
			if(SampleIdPerFileName.Count == 0) return;

			// Write out all the settings to the build folder
			var configFile = Path.Combine(BuildFolder, "settings.txt").OpenWrite();
			configFile.WriteLine("-mosaik = " + MosaikPath);
			configFile.WriteLine("-phylip = " + PhylipPath);
			configFile.WriteLine("-ref = " + ReferenceFastaPath);
			configFile.WriteLine("-fe = " + MinimumFractionExplained);
			configFile.WriteLine("-mf = " + MinimumFlank);
			configFile.WriteLine("-mr = " + MinimumTotalReads);
			configFile.WriteLine();
			foreach(var kvp in SampleIdPerFileName)
			{
				configFile.WriteLine(kvp.Key + "\t" + kvp.Value);
			}
			configFile.Close();

			Console.WriteLine("*** Building (MosaikBuild)");
			if(!MosaikBuild()) return;
			Console.WriteLine("*** Building complete.");

			Console.WriteLine("*** Aligning (MosaikAligner)");
			if(!MosaikAlign()) return;
			Console.WriteLine("*** Aligning complete.");

			Console.WriteLine("*** Sorting (MosaikSort)");
			if(!MosaikSort()) return;
			Console.WriteLine("*** Sorting complete.");

			Console.WriteLine("*** Assembling (MosaikAssembler)");
			if(!MosaikAssemble()) return;
			Console.WriteLine("*** Assembling complete.");

			Console.WriteLine("*** Making consensus calls");
			ConsensusCall();
			Console.WriteLine("*** Consensus calls complete.");

			Console.WriteLine("*** Calculating distance matrix");
			DistanceMatrix();
			Console.WriteLine("*** Distance matrix complete.");

			Console.WriteLine("*** Building tree");
			if(!BuildTree()) return; 
			Console.WriteLine("*** Tree complete.");

			Console.WriteLine();
			Console.WriteLine("All done, and here's the summary:");
			Console.WriteLine();

			Console.WriteLine("Sample\tFile\tReads\tTargets\tCoverage");
			foreach(var s in SampleIdPerFileName)
			{
				if(!ReadsPerSample.ContainsKey(s.Value)) Console.WriteLine(s.Value + "\t" + s.Key + "\t0\t0\t0");
				Console.WriteLine(s.Value + "\t" + s.Key + "\t" + ReadsPerSample[s.Value] + "\t" + TargetsPerSample[s.Value] + "\t" + ReadsPerSample[s.Value] / (float)TargetsPerSample[s.Value]);
			}

			Console.WriteLine();

			string tree = File.ReadAllText(Path.Combine(BuildFolder, "PHYLIP_outfile.txt"));
			Console.WriteLine(tree);
			
		}

		public void DetermineSampleIds(string configFile)
		{
			Console.WriteLine("Parsing config file: " + configFile);
			var file = configFile.OpenRead();
			while(true)
			{
				string line = file.ReadLine();
				if(line == null) break;
				if(line == "") continue;
				if(line.Trim().StartsWith("#")) continue;
				string[] items = line.Split('\t');
				if(items.Length != 2)
				{
					Console.WriteLine("Skipping one line: " + line);
					Console.WriteLine("(not tab-separated, or contained more than two columns)");
					continue;
				}
				if(!File.Exists(items[0]))
				{
					Console.WriteLine("File not found (" + items[0] + ") - skipped.");
					continue;
				}
				foreach(char c in Path.GetInvalidFileNameChars())
				{
					if(items[1].Contains(c))
					{
						Console.WriteLine("SampleId '" + items[1] + "' contains invalid characters (use only alphanumeric and underscore) - skipped.");
					}
				}

				SampleIdPerFileName[items[0]] = items[1];
			}
		}

		public bool MosaikBuild()
		{
			// Convert each file to Mosaik format
			// /sequtils/mosaik/bin/MosaikBuild -q s_1_qseq_AGCGAG.fq -out s_1_qseq_AGCGAG.dat -st illumina -p AGCGAG
			foreach(var file in SampleIdPerFileName.Keys)
			{
				string qFile = file;
				string outFile = SampleIdPerFileName[file] + ".dat";
				string arguments = String.Format("-q {0} -out {1} -st illumina -p {2} -sam {2}", qFile, outFile, SampleIdPerFileName[file]);
				string cmd = Path.Combine(MosaikPath, "MosaikBuild");

				// Check if the file has already been converted
				if((!Rebuild) && File.Exists(outFile)) 
				{
					//Console.WriteLine(outFile + " [File already built - skipping]");
					continue;
				}

				Console.WriteLine(qFile + " -> " + outFile + ":\t" + cmd + " " + arguments);

				var call = new CmdCaller(cmd, arguments, false);
				if(call.ExitCode != 0)
				{
					Console.WriteLine("Failed to run MosaikBuild on {0}. ExitCode={1}:", qFile, call.ExitCode);
					return false;
				}

			}
			return true;
		}
		public bool MosaikBuildRef()
		{
			// Convert ref file to Mosaik format

			string arguments = String.Format("-fr {0} -oa {1}", ReferenceFastaPath, ReferenceDatPath);
			string cmd = Path.Combine(MosaikPath, "MosaikBuild");

			Console.WriteLine(ReferenceFastaPath + " -> " + ReferenceDatPath + ":\t" + cmd + " " + arguments);

			var call = new CmdCaller(cmd, arguments, false);
			if(call.ExitCode != 0)
			{
				Console.WriteLine("Failed to run MosaikBuild on {0}. ExitCode={1}:", ReferenceFastaPath, call.ExitCode);
				return false;
			}
			return true;
		}

		public bool MosaikAlign()
		{
			// /sequtils/mosaik/bin/MosaikAligner -in s_1_qseq_AGCGAG.dat -out s_1_qseq_AGCGAG_aligned.dat -ia /data/genomes/mm9/lineage/polyg.dat -hs 15 -mm 12 -act 35 -p 3
			foreach(var sample in SampleIdPerFileName.Values)
			{
				string qFile = sample + ".dat";	// input file in Mosaik .dat format
				string outFile = sample + "_aligned.dat";
				string arguments = String.Format("-in {0} -out {1} -ia {2} -hs 15 -mm 12 -act 35 -p 28", qFile, outFile, ReferenceDatPath);
				string cmd = Path.Combine(MosaikPath, "MosaikAligner");

				// Check if the file has already been converted
				if ((!Rebuild) && File.Exists(outFile))
				{
					//Console.WriteLine(outFile + " [File already aligned - skipping]");
					continue;
				}

				Console.WriteLine(qFile + " -> " + outFile + ":\t" + cmd + " " + arguments);

				var call = new CmdCaller(cmd, arguments, false);
				if(call.ExitCode != 0)
				{
					Console.WriteLine("Failed to run MosaikAligner on {0}. ExitCode={1}:", qFile, call.ExitCode);
					return false;
				}
			}
					return true;
	}

		public bool MosaikSort()
		{
			// /sequtils/mosaik/bin/MosaikSort -in s_1_qseq_AGCGAG_aligned.dat -out s_1_qseq_AGCGAG_sorted.dat
			foreach(var sample in SampleIdPerFileName.Values)
			{
				string qFile = sample + "_aligned.dat";	// aligned input file in Mosaik .dat format
				string outFile = sample + "_sorted.dat";
				string arguments = String.Format("-in {0} -out {1}", qFile, outFile);
				string cmd = Path.Combine(MosaikPath, "MosaikSort");

				// Check if the file has already been converted
				if ((!Rebuild) && File.Exists(outFile))
				{
					//Console.WriteLine(outFile + " [File already sorted - skipping]");
					continue;
				}

				Console.WriteLine(qFile + " -> " + outFile + ":\t" + cmd + " " + arguments);

				var call = new CmdCaller(cmd, arguments, false);
				if(call.ExitCode != 0)
				{
					Console.WriteLine("Failed to run MosaikSort on {0}. ExitCode={1}:", qFile, call.ExitCode);
					return false;
				}
			}
					return true;
	}

		public bool MosaikAssemble()
		{
			// /sequtils/mosaik/bin/MosaikAssembler -in s_1_qseq_AGCGAG_sorted.dat -out s_1_qseq_AGCGAG_assembled –f ace
			foreach(var sample in SampleIdPerFileName.Values)
			{
				string qFile = sample + "_sorted.dat";	// aligned input file in Mosaik .dat format
				string outFolder = sample + "_ace";
				string outFileRoot = sample + "_ace/" + sample;
				string arguments = String.Format("-in {0} -ia {1} -out {2} -f ace", qFile, ReferenceDatPath, outFileRoot);
				string cmd = Path.Combine(MosaikPath, "MosaikAssembler");

				// Check if the file has already been converted
				if ((!Rebuild) && Directory.Exists(outFolder))
				{
					//Console.WriteLine(outFolder + " [File already assembled - skipping. WARNING: only based on existence of ace folder.]");
					continue;
				}
				else Directory.CreateDirectory(outFolder);
				Console.WriteLine(qFile + " -> " + outFolder + ":\t" + cmd + " " + arguments);

				var call = new CmdCaller(cmd, arguments, false);
				if(call.ExitCode != 0)
				{
					Console.WriteLine("Failed to run MosaikAssembler on {0}. ExitCode={1}:", qFile, call.ExitCode);
					return false;
				}

			}
					return true;
		}

		/// <summary>
		///  target name -> index in CallMatrix
		/// </summary>
		Dictionary<string, int> Targets = new Dictionary<string, int>();

		/// <summary>
		///  sample name -> index in CallMatrix
		/// </summary>
		Dictionary<string, int> Samples = new Dictionary<string, int>();

		/// <summary>
		///  2D matrix of calls, indexed by [sampleIndex, targetIndex]
		/// </summary>
		ConsensusCall[,] CallMatrix;
		Dictionary<string, int> TargetsPerSample = new Dictionary<string, int>();
		Dictionary<string, int> ReadsPerSample = new Dictionary<string, int>();

		public void ConsensusCall()
		{
			// Set things up for easy access
			int i = 0;
			foreach(var s in SampleIdPerFileName.Values)
			{
				Samples[s] = i++;
			}			

			i = 0;
			foreach(var ff in FastaFile.Stream(ReferenceFastaPath))
			{
				Targets[ff.Identifier] = i++;
			}
			CallMatrix = new ConsensusCall[SampleIdPerFileName.Count, i];

			// Scan all the ace folders
			foreach(var sample in SampleIdPerFileName.Values)
			{
				string aceFolder = sample + "_ace";

				if(!Directory.Exists(aceFolder))
				{
					Console.WriteLine(aceFolder + " - folder not found, skipping.");
					continue;
				}
				Console.WriteLine(aceFolder + " -> consensus calls");

				foreach(var file in Directory.GetFiles(aceFolder, "*.ace"))
				{
					int startOfSampleName = file.LastIndexOf(sample) + sample.Length + 1;
					string target = file.Substring(startOfSampleName, file.Length - startOfSampleName - 4);
					ConsensusCallOne(sample, target, file);
				}
			}

			// Dump the consensus call matrix
			var consensusFile = Path.Combine(BuildFolder,"histogram.tab").OpenWrite();
			StringBuilder sb = new StringBuilder();
			for(int j = 0; j < 50; j++)
			{
				sb.Append("0\t");
			}
			sb.Length -= 1;
			string emptyHisto = sb.ToString();
			consensusFile.WriteLine("POLYG\tSample\tTotalReads\tCallReadCount\tFractionExplained\tCallLength\tCall\tNumCalledPerTarget\tTargetMutated");
			foreach(var t in Targets)
			{
				// Figure out of the target has a valid mutation
				int call = 0;
				bool hasMutation = false;
				int numCalls = 0;
				foreach (var s in Samples)
				{
					if (CallMatrix[s.Value, t.Value] != null)
					{
						if (CallMatrix[s.Value, t.Value].Call != 0)
						{
							numCalls++;
							if (call != 0 && call != CallMatrix[s.Value, t.Value].Call)
							{
								// We have two distinct calls
								if(!hasMutation) hasMutation = true;
							}
							call = CallMatrix[s.Value, t.Value].Call;
						}
					}
				}
				foreach(var s in Samples)
				{//			return TotalReads + "\t" + CallReadCount + "\t" + FractionExplained + "\t" + CallLength;
					if(CallMatrix[s.Value, t.Value] != null) consensusFile.WriteLine(t.Key + "\t" + s.Key + "\t" + CallMatrix[s.Value, t.Value].ToString() + "\t" + numCalls.ToString() +"\t" + (hasMutation ? "yes" : "no") + "\t" + CallMatrix[s.Value, t.Value].HistogramString());
					else consensusFile.WriteLine(t.Key + "\t" + s.Key + "\t\t\t\t\t\t0\tno\t" + emptyHisto);
				}
			}
			consensusFile.Close();
		}

		/// <summary>
		/// Make consensus calls and print them to stdout
		/// </summary>
		/// <param name="file"></param>
		public void ConsensusCallOne(string sample, string target, string file)
		{
			if(!TargetsPerSample.ContainsKey(sample)) TargetsPerSample[sample] = 0;
			TargetsPerSample[sample]++;

			int polyStart = -1, polyEnd = -1;
			char polyNt = 'X';
			int j = 0;
			Dictionary<string, int> calls = new Dictionary<string, int>();
			AceRecord reference = null;
			// Read file in phrap ACE format
			foreach(var record in PhrapAceFile.Stream(file))
			{
				if(record.Header == ".MosaikReference")
				{
					// Get pointers to the first and last character in the homopolymer (may be *)
					polyStart = findLongestHomopolymerStart(record.Sequence);
					polyEnd = findLongestHomopolymerEnd(record.Sequence);
					polyNt = record.Sequence[polyStart];
					reference = record;
					j = 0;
					while(polyNt == '*') polyNt = record.Sequence[polyStart + j++];
				}
				else
				{
					// Scan the polymer from the appropriate direction
					if(record.IsForwardStrand)
					{
						int len = 0;
						for(int i = polyStart - record.Offset; i < record.Sequence.Length; i++)
						{
							if(i < 0) break;	// Cannot make an accurate call when the read doesn't span the repeat
							if(record.Sequence[i] != '*')
							{
								if(record.Sequence[i] != polyNt)
								{
									// Now verify that the read extends at least MinimumFlank on the other side (and is accurate)
									int N = 0;
									for(; i < record.Sequence.Length; i++)
									{
										if(record.Sequence[i] != '*' && record.Sequence[i] == reference.Sequence[i + record.Offset]) N++;
										if(N >= MinimumFlank) break;
									}
									// Make a call only if the flank is long enough
									if(N >= MinimumFlank) calls[record.Header] = len;
									break;
								}
								len++;
							}						
						}
					}
					else
					{
						int len = 0;
						for(int i = polyEnd - record.Offset; i >= 0; i--)
						{
							if(i >= record.Sequence.Length) break;	// Cannot make an accurate call when the read doesn't span the repeat
							if(record.Sequence[i] != '*')
							{
								if(record.Sequence[i] != polyNt)
								{
									// Now verify that the read extends at least MinimumFlank on the other side (and is accurate)
									int N = 0;
									for(; i >= 0; i--)
									{
										if(record.Sequence[i] != '*' && record.Sequence[i] == reference.Sequence[i + record.Offset]) N++;
										if(N >= MinimumFlank) break;
									}
									if(N >= MinimumFlank) calls[record.Header] = len; 
									break;
								}
								len++;
							}
						}
					}

				}
			}
			// Make histogram
			int[] histo = new int[50];
			int totalCount = 0;
			foreach(var call in calls) 
			{
				if(call.Value < 50) histo[call.Value]++; 
				else Console.WriteLine("Unexpectedly long polymer: " + call.Key + " " + call.Value.ToString());
				totalCount ++;
			}
			if(!ReadsPerSample.ContainsKey(sample)) ReadsPerSample[sample] = 0;
			ReadsPerSample[sample] += totalCount;
			// Find peak
			ScoreTracker<int, int> peakTracker = new ScoreTracker<int, int>();
			for(int i = 0; i < histo.Length; i++)
			{
				peakTracker.Examine(histo[i], i);
			}
			// Make the consensus call
			ConsensusCall cc = new ConsensusCall
			{
				TotalReads = totalCount,
				CallLength = peakTracker.MaxItem,
				CallReadCount = peakTracker.MaxScore,
				Histogram = histo,
				MinimumTotalReads = MinimumTotalReads,
				MinimumFractionExplained = MinimumFractionExplained
			};

			// When rebuilding, some files may not exist in the new reference database
			if (Samples.ContainsKey(sample) && Targets.ContainsKey(target))
			{
				CallMatrix[Samples[sample], Targets[target]] = cc;
				string callFolder = Path.Combine(BuildFolder, sample + "_calls");
				if (!Directory.Exists(callFolder)) Directory.CreateDirectory(callFolder);
				var writer = Path.Combine(callFolder, target + "_calls.txt").OpenWrite();
				string line = sample + "\t" + target + "\t" + cc.ToString();
				writer.WriteLine(line);
				//Console.WriteLine(line);
				foreach (var call in calls) writer.WriteLine(call.Key + "\t" + call.Value.ToString());
				writer.Close();
			}
		}

		// TODO: alternative method; find longest G-run (C-run) in each mapped read, then verify the two flanks

		private int findLongestHomopolymerEnd(string line)
		{
			int len = 0;
			char cur = 'X';

			ScoreTracker<int, int> tracker = new ScoreTracker<int, int>();
			for(int i = 0; i < line.Length; i++)
			{
				if(i > 0 && line[i] != cur && line[i] != '*')
				{
					tracker.Examine(len, i);
					len = 0;
					cur = line[i];
				}
				else
				{
					if(line[i] == cur) len++;
				}
			}
			return tracker.MaxItem - 1;
		}

		private int findLongestHomopolymerStart(string line)
		{
			int len = 0;
			char cur = 'X';

			ScoreTracker<int, int> tracker = new ScoreTracker<int, int>();
			for(int i = line.Length - 1; i >= 0; i--)
			{
				if(i > 0 && line[i] != cur && line[i] != '*')
				{
					tracker.Examine(len, i);
					len = 0;
					cur = line[i];
				}
				else
				{
					if(line[i] == cur) len++;
				}
			}
			return tracker.MaxItem + 1;
		}

		public void DistanceMatrix()
		{
			var fileD = "infile".OpenWrite();
			var fileC = Path.Combine(BuildFolder, "count_matrix.txt").OpenWrite();

			fileD.WriteLine("    " + Samples.Count);
			fileC.WriteLine("    " + Samples.Count);
			foreach(var s1 in Samples)
			{
				Console.Write(s1.Key.PadRight(10));
				int index1 = s1.Value;
				fileD.Write(s1.Key.PadRight(10));
				fileC.Write(s1.Key.PadRight(10));
				foreach(var s2 in Samples)
				{
					int index2 = s2.Value;
					if(index2 >= index1) break;
					DescriptiveStatistics ds = new DescriptiveStatistics();
					for(int i = 0; i < Targets.Count; i++)
					{
						if(CallMatrix[s1.Value, i] == null) continue;
						if(CallMatrix[s2.Value, i] == null) continue;
						float d = CallMatrix[s1.Value, i].DistanceTo(CallMatrix[s2.Value, i], MinimumFractionExplained, MinimumTotalReads);
						if(!float.IsNaN(d)) ds.Add(d);
					}
					fileD.Write(String.Format("{0:0.0000}", ds.Mean()*10).PadRight(8));
					fileC.Write(String.Format("{0}", ds.Count).PadRight(8));
					Console.Write(String.Format("{0:0.00}({1})", ds.Mean()*10, ds.Count).PadRight(12));
				}
				fileD.WriteLine();
				fileC.WriteLine();
				Console.WriteLine();
			}
			fileD.Close();
			fileC.Close();
		}

		public bool BuildTree()
		{
			// Use PHYLIP to build a tree
			string arguments = "";
			string cmd = Path.Combine(PhylipPath, "neighbor");
			Console.WriteLine(cmd + " " + arguments);
			
			// Remove the outfile and outtree files so they don't irritate PHYLIP
			File.Delete("outfile");
			File.Delete("outtree");

			// Call PHYLIP
			Process cmdProcess = new Process();
			ProcessStartInfo cmdStartInfo = new ProcessStartInfo();
			cmdStartInfo.FileName = cmd;
			cmdStartInfo.RedirectStandardInput = true;
			cmdStartInfo.UseShellExecute = false;
			cmdStartInfo.CreateNoWindow = true;

			cmdStartInfo.Arguments = arguments;

			cmdProcess.EnableRaisingEvents = true;
			cmdProcess.StartInfo = cmdStartInfo;
			cmdProcess.Start();

			// Send commands
			cmdProcess.StandardInput.WriteLine("N");
			cmdProcess.StandardInput.WriteLine("L");
			cmdProcess.StandardInput.WriteLine("Y");

			// Wait for exiting the process
			cmdProcess.WaitForExit();
			if(cmdProcess.ExitCode != 0)
			{
				Console.WriteLine("Failed to run PHYLIP on {0}. ExitCode={1}:", ReferenceFastaPath, cmdProcess.ExitCode);
				return false;
			}

			// Rename the output
			File.Move("outfile", Path.Combine(BuildFolder, "PHYLIP_outfile.txt"));
			File.Move("outtree", Path.Combine(BuildFolder, "PHYLIP_outtree.txt"));
			File.Move("infile", Path.Combine(BuildFolder, "PHYLIP_infile.txt"));

			return true;
		}
	}

	public class ConsensusCall
	{
		public int TotalReads { get; set; }
		public int CallLength { get; set; }
		public int CallReadCount { get; set; }
		public int[] Histogram { get; set; }
		public float MinimumFractionExplained{ get; set; }
		public int MinimumTotalReads { get; set; }


		public float FractionExplained 
		{ 
			get
			{
				return (CallReadCount) / (float)TotalReads;
			}  
		}

		public int Call
		{
			get
			{
				// No call if too few reads
				if (TotalReads < MinimumTotalReads)
				{
					return 0;
				}

				// No call if fractions too low
				if (this.FractionExplained < MinimumFractionExplained)
				{
					return 0;
				}

				return CallLength;
			}
		}

		public override string ToString()
		{
			return TotalReads + "\t" + CallReadCount + "\t" + FractionExplained + "\t" + CallLength + "\t" + Call;
		}

		public float DistanceTo(ConsensusCall other, float MinimumFractionExplained, int MinimumTotalReads)
		{
			if (this.Call == 0 || other.Call == 0) return float.NaN;
			if (this.Call == other.Call) return 0;
			else return 1;
		}

		public string HistogramString()
		{
			StringBuilder sb = new StringBuilder();
			for(int i = 0; i < Histogram.Length; i++)
			{
				sb.Append(Histogram[i]);
				sb.Append('\t');
			}
			sb.Length -= 1;
			return sb.ToString();
		}
	}
}
