using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics;
using Linnarsson.Dna;
using System.IO;
using Linnarsson.Mathematics.SortSearch;
using System.Runtime.Serialization.Formatters.Binary;
using Linnarsson.Utilities;
using System.Runtime.Serialization;

namespace Linnarsson.Dna
{
	public class GenomeAnnotations
	{
		
		/// <summary>
		/// A dictionary of annotations indexed by chromosome name
		/// </summary>
		public Dictionary<string, QuickAnnotationMap> FwQuickAnnotations { get; set; }
		public Dictionary<string, QuickAnnotationMap> RevQuickAnnotations { get; set; }
		public ContigCoordinateConverter Converter { get; set; }
		public List<string> GenesOfInterest { get; set; }
        public Dictionary<string, int> GeneLengths = new Dictionary<string, int>();

		/// <summary>
		/// The actual chromosome sequences
		/// </summary>
		public Dictionary<string,DnaSequence> ChromosomeSequences { get; set; }

		private GenomeAnnotations(ContigCoordinateConverter converter)
		{
			Converter = converter;
			FwQuickAnnotations = new Dictionary<string, QuickAnnotationMap>();
			RevQuickAnnotations = new Dictionary<string, QuickAnnotationMap>();
			ChromosomeSequences = new Dictionary<string, DnaSequence>();
			GenesOfInterest = new List<string>();
			GenesOfInterest.Add("Sox2");
			GenesOfInterest.Add("Actb");
			GenesOfInterest.Add("Nanog");
			GenesOfInterest.Add("Klf4");
			GenesOfInterest.Add("Calb1");
			GenesOfInterest.Add("Rnr2");
			GenesOfInterest.Add("Tmpo");
			GenesOfInterest.Add("Trpm6");
			GenesOfInterest.Add("Pou5f1");
			GenesOfInterest.Add("Rnr1");
			GenesOfInterest.Add("ND1");
			GenesOfInterest.Add("COX2");
			GenesOfInterest.Add("Vcam1");
			GenesOfInterest.Add("Zfp42");
			GenesOfInterest.Add("Fgf2r");
			GenesOfInterest.Add("Nt5e");
			GenesOfInterest.Add("Runx2");
			GenesOfInterest.Add("Taz");
			GenesOfInterest.Add("Osx");
			GenesOfInterest.Add("Twist");
			GenesOfInterest.Add("Ap1");
			GenesOfInterest.Add("Sox9");
			GenesOfInterest.Add("Sox6");
			GenesOfInterest.Add("Sox5"); 
			GenesOfInterest.Add("Bmp2");
			GenesOfInterest.Add("Smad1");
			GenesOfInterest.Add("Smad4");
		}

		public void Save(string fileName)
		{
			var writer = new BinaryWriter(File.OpenWrite(fileName));
			writer.Write(FwQuickAnnotations.Count);
			foreach(var kvp in FwQuickAnnotations)
			{
				writer.Write(kvp.Key);
				kvp.Value.Serialize(writer);
			}
			writer.Write(RevQuickAnnotations.Count);
			foreach(var kvp in RevQuickAnnotations)
			{
				writer.Write(kvp.Key);
				kvp.Value.Serialize(writer);
			}
			writer.Write(ChromosomeSequences.Count);
			foreach(var kvp in ChromosomeSequences)
			{
				writer.Write(kvp.Key);
				kvp.Value.Serialize(writer);
			}
			writer.Close();
		}

		public static GenomeAnnotations Load(string filename, ContigCoordinateConverter converter, bool loadSequences)
		{
			var startTime = DateTime.Now;
			var reader = new BinaryReader(File.OpenRead(filename));
			GenomeAnnotations result = new GenomeAnnotations(converter);
			Background.Message("Loading annotations...");
			int length = reader.ReadInt32();
			for(int i = 0; i < length; i++)
			{
				result.FwQuickAnnotations[reader.ReadString()] = QuickAnnotationMap.Deserialize(reader);
			}
			length = reader.ReadInt32();
			for(int i = 0; i < length; i++)
			{
				result.RevQuickAnnotations[reader.ReadString()] = QuickAnnotationMap.Deserialize(reader);
			}
			length = reader.ReadInt32();
            for (int i = 0; i < length; i++)
            {
                result.ChromosomeSequences[reader.ReadString()] = DnaSequence.Deserialize(reader, !loadSequences);
            }

			reader.Close();
			Background.Message("");
			Console.WriteLine("Index loaded in " + (DateTime.Now - startTime).ToString());
			return result;
		}

		/// <summary>
		/// Rebuild the annotated genome index
		/// </summary>
		public static GenomeAnnotations Build(string GenomeFolder, ContigCoordinateConverter converter)
		{
			GenomeAnnotations result = new GenomeAnnotations(converter);
			result.BuildIndex(GenomeFolder);
			return result;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="GenomeFolder"></param>
		/// <param name="groupLabel">Such as "C57BL/6J"</param>
		private void BuildIndex(string genomeFolder)
		{
			long countNts = 0;
			if(Background.CancellationPending) return;

	
			string[] genomeFiles = Directory.GetFiles(genomeFolder, "*.gbk.gz");
			foreach(string chr in Converter.GetChromosomeNames())
			{
				FwQuickAnnotations[chr] = new QuickAnnotationMap(30000);
				RevQuickAnnotations[chr] = new QuickAnnotationMap(30000);
			}

			// Prepare for mapping repeats to the QuickAnnotations
			Background.Progress(0);
			Background.Message("Indexing repeats...");
			string maskFile = Path.Combine(genomeFolder, "masking_coordinates.gz");
			int countRepeats = 0;
			Dictionary<string, List<RepeatMaskRecord>> repeats = new Dictionary<string, List<RepeatMaskRecord>>();
			if(File.Exists(maskFile))
			{
				Console.WriteLine("Indexing repeat mask file...");
				DateTime start = DateTime.Now;
				var reader = maskFile.OpenRead();
				while(true)
				{
					string line = reader.ReadLine();
					if(line == null) break;
					string[] rec = line.Split('\t');

                    try
                    {
                        if (!repeats.ContainsKey(rec[0])) repeats[rec[0]] = new List<RepeatMaskRecord>();
                        repeats[rec[0]].Add(new RepeatMaskRecord { Contig = rec[0], Start = int.Parse(rec[1]), End = int.Parse(rec[2]), Name = rec[3] });
                        countRepeats++;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error at: " + line);
                        Console.WriteLine(e.Message);
                    }
				}
				Console.WriteLine(countRepeats + " repeats indexed in " + (DateTime.Now - start).ToString());
			}
			else Console.WriteLine("WARNING: repeat mask file not found!");

			// Set up the chromosome sequences
			foreach(var chr in Converter.GetChromosomeNames()) ChromosomeSequences[chr] = DnaSequence.Create(Converter.GetChromosomeLength(chr));

			int nFiles = genomeFiles.Length;
			Background.Progress(0);
			Background.Message("Indexing annotations...");
			int nDone = 0;
			int countExons = 0;
			foreach(string file in genomeFiles)
			{
				Console.WriteLine(": " + Path.GetFileName(file));
				foreach(GenbankRecord gbr in GenbankFile.Stream(file))
				{
					countNts += gbr.SequenceLength;
					string chr = Converter.GetChromosome(gbr.AccessionVersion);
					if(chr == null) continue;
					string ctg = gbr.AccessionVersion;

					// Add repeats (to both strands, since we lack orientation information for repeats)
					if(repeats.ContainsKey(gbr.AccessionVersion))
					{
						foreach(RepeatMaskRecord rept in repeats[gbr.AccessionVersion])
						{
                            string officialName = "repeat_" + rept.Name;

                            // Measure repeat lengths (total)
                            if (!GeneLengths.ContainsKey(officialName)) GeneLengths[officialName] = 0;
                            GeneLengths[officialName] += rept.End - rept.Start;

                            FwQuickAnnotations[chr].Add(new Interval<string>(Converter.Convert(ctg, rept.Start), Converter.Convert(ctg, rept.End), officialName));
                            RevQuickAnnotations[chr].Add(new Interval<string>(Converter.Convert(ctg, rept.Start), Converter.Convert(ctg, rept.End), officialName));
						}
					}

					foreach(GenbankFeature ft in gbr.Features)
					{
						if(ft.Name == "mRNA" || (ft.Name == "gene" && gbr.MoleculeTopology == "circular")) // track all genes on the mitochondrion
						{
							var exons = ft.GetLocationAsInterval();
                            string officialName = ft.GetQualifier("gene");
                            if (ft.Name == "gene" && gbr.MoleculeTopology == "circular") officialName = "mt_" + officialName;

                            // Measure gene lengths
                            if (!GeneLengths.ContainsKey(officialName)) GeneLengths[officialName] = 0;
                            foreach (var exon in exons) GeneLengths[officialName] += (int)exon.Length;

							for(int i = 0; i < exons.Count; i++)
							{
								// Add the exon to the quick annotations
                                if (ft.Strand == DnaStrand.Forward) FwQuickAnnotations[chr].Add(new Interval<string>(Converter.Convert(ctg, exons[i].Start), Converter.Convert(ctg, exons[i].End), officialName));
                                else RevQuickAnnotations[chr].Add(new Interval<string>(Converter.Convert(ctg, exons[i].Start), Converter.Convert(ctg, exons[i].End), officialName));
								countExons++;
							}
						}
						else if(ft.Name == "gene" && GenesOfInterest.Contains(ft.GetQualifier("gene")))
						{
                            // This section is for making gene images
							var exons = ft.GetLocationAsInterval();
							for(int i = 0; i < exons.Count; i++)
							{
								// Add the exon to the quick annotations
								if(ft.Strand == DnaStrand.Forward) FwQuickAnnotations[chr].Add(new Interval<string>(Converter.Convert(ctg, exons[i].Start), Converter.Convert(ctg, exons[i].End), "gene_" + ft.GetQualifier("gene")));
								else RevQuickAnnotations[chr].Add(new Interval<string>(Converter.Convert(ctg, exons[i].Start), Converter.Convert(ctg, exons[i].End), "gene_" + ft.GetQualifier("gene")));
							}
						}
					}

					// Copy the sequence into the right place on the genome
					DnaSequence chrSeq = ChromosomeSequences[chr];
					int start = Converter.Convert(ctg, 1) - 1; // make it zero-based
					for(int i = 0; i < gbr.Sequence.Count; i++)
					{
						chrSeq[i + start] = gbr.Sequence[i];
					}

					Background.Message(gbr.AccessionVersion + " done, total of " + countNts + " nts indexed");
					Background.Progress(nDone * 100 / nFiles);

					if(Background.CancellationPending) return;
				}
				nDone++;
			}
			Console.WriteLine(countExons + " exons indexed.");
			Console.WriteLine(countNts + " nucleotides indexed (counting one strand only).");
			Console.WriteLine("Indexing completed.");
		}
	}


	public class RepeatMaskRecord
	{
		public string Contig { get; set; }
		public int Start { get; set; }
		public int End { get; set; }
		public string Name { get; set; }
	}
}
