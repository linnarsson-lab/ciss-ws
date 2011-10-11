using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Utilities;

namespace Junction_lables_simulator_console
{
    class Program
    {
        static void Main(string[] args)
        {
            Simulator smtr = new Simulator();
            var p = new Options() {
                { "seq=", "Select the Genome/Chromosome sequence files -For example Chr21 from hg18", (v) => smtr.refSeqFilepath = v },
                { "SNP=", "Select the SNP file(s) for the same Genome/Chromosome i.e. chr21 URN sample", (v) => smtr.snpfilepath = v },
                { "Erro=", "Select the Illumina file(s) for the introducing error", (v) => smtr.IlluminaSeqError = v },
                { "nm:", "Number of molecules", (v) => smtr.num_molecule = int.Parse(v) },
                { "lm:", "Length of each molecules (kb). Given no will be multiplied by 1KB", (v) => smtr.molecule_len = long.Parse(v) },
                { "prob:", "Insert transposon with probability =1/", (var) => smtr.tsp_probability = double.Parse(var) },
                { "per:", "Discard % of the fragment", (v) => smtr.Discard_percentage = int.Parse(v) },
                { "rl:", "Read lenth in bp", (v) => smtr.readlen = int.Parse(v) },
                { "reads:", "Total reads in million bp. Given no will be multiplied by 1M", (v) => smtr.Reads_in_million  = int.Parse(v) },
                //{ "rebuild", "Rebuild all, overwriting any existing files", (v) => lpp.Rebuild = true }
            };
            foreach (var x in p.Parse(args)) ;
            if (smtr.refSeqFilepath == null || smtr.snpfilepath == null)
            {
                Console.WriteLine("Usage: mono Junction_lables_simulator_console.exe [OPTIONS]");
                Console.WriteLine();
                p.WriteOptionDescriptions(Console.Out);
                return;
            }

            Console.WriteLine("Junction Lables Simulator");
            smtr.analyze();
            


        }
    }
}
