using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Lineage;
using System.IO;
using Linnarsson.Utilities;

namespace LineageTool
{
	class Program
	{
		static void Main(string[] args)
		{
			LineagePreprocessor lpp = new LineagePreprocessor();

			var p = new Options() {
				{ "conf=", "Config file (two columns without header: file name and sample id)", (v) => lpp.ConfigFile = v },
				{ "ref=", "Path to reference FASTA file (.dat will be made if not found)", (v) => lpp.ReferenceFastaPath = v },
                { "mr:", "Minimum number of reads required to make a call (>= 0; default 10)", (v) => lpp.MinimumTotalReads = int.Parse(v) },
                { "mf:", "Minimum flanking sequence on the other side of the repeat (>= 0; default 4)", (v) => lpp.MinimumFlank = int.Parse(v) },
                { "fe:", "Minimum fraction of reads in highest peak (0-1; default 0.5)", (var) => lpp.MinimumFractionExplained = float.Parse(var) },
				{ "mosaik:", "Path to Mosaik bin folder (default: /data/sequtils/mosaik-aligner/bin)", (v) => lpp.MosaikPath = v },
				{ "phylip:", "Path to Mosaik bin folder (default: /data/sequtils/mosaik-aligner/bin)", (v) => lpp.PhylipPath = v },
				{ "rebuild", "Rebuild all, overwriting any existing files", (v) => lpp.Rebuild = true }
			};
			foreach(var x in p.Parse(args));
			if(lpp.ConfigFile == null || lpp.ReferenceFastaPath == null)
			{
				Console.WriteLine("Usage: mono LineageTool.exe [OPTIONS]");
				Console.WriteLine();
				p.WriteOptionDescriptions(Console.Out);
				return;			
			}

			Console.WriteLine("LineageTool by Sten Linnarsson 2010-06-21.");


			lpp.Analyze();

		}
	}
}
