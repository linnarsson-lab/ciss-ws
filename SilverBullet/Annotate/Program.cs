using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Linnarsson.Strt;
using Linnarsson.Dna;
using Linnarsson.Mathematics;
using Linnarsson.Utilities;

namespace Annotate
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 4 || args[0].StartsWith("-"))
            {
                Console.WriteLine("Usage: mono Annotate.exe <projectName> <barcodeSet> <species> <all|single>");
                Console.WriteLine("Example: mono Annotate.exe L124 v2 Mm single < BowtieOutputFile.map");
                Console.WriteLine("Input in bowtie output (.map) format is taken from standard input.");
            }
            else
            {
                try
                {
                    Annotate(args);
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERROR in Annotate.exe: " + e.Message);
                }
            }
        }

        private static void Annotate(string[] args)
        {
            {
                Props props = Props.props;
                string projectFolder = PathHandler.GetRootedProjectFolder(args[0]);
                props.BarcodesName = args[1];
                string[] speciesAbbrevs = new string[] { args[2] };
                bool analyzeAllGeneVariants = (args[3].ToLower().StartsWith("a")) ? true : false;
                string sampleLayoutPath = PathHandler.GetSampleLayoutPath(projectFolder);
                if (File.Exists(sampleLayoutPath))
                {
                    PlateLayout sampleLayout = new PlateLayout(sampleLayoutPath);
                    props.Barcodes.SetSampleLayout(sampleLayout);
                    speciesAbbrevs = sampleLayout.GetSpeciesAbbrevs();
                }
                foreach (string speciesAbbrev in speciesAbbrevs)
                    Annotate(props, projectFolder, speciesAbbrev, analyzeAllGeneVariants);
            }
        }

        private static void Annotate(Props props, string projectFolder, string speciesAbbrev, bool analyzeAllGeneVariants)
        {
            StrtGenome genome = StrtGenome.GetGenome(speciesAbbrev, analyzeAllGeneVariants);
            string outputFolder = Path.Combine(projectFolder, "QuickAnnotation_" + DateTime.Now.ToPathSafeString());
            ProcessAnnotation(props, genome, projectFolder, outputFolder);
		}

        private static void ProcessAnnotation(Props props, StrtGenome genome, string projectFolder, string outputFolder)
        {
            AbstractGenomeAnnotations annotations = new UCSCGenomeAnnotations(props, genome);
            annotations.Load();
            TranscriptomeStatistics ts = new TranscriptomeStatistics(annotations, props);
            BowtieMapFile bmf = new BowtieMapFile(props.Barcodes);
            foreach (BowtieMapRecord[] recs in bmf.ReadBlocks(Console.In, 10))
                ts.Add(recs, 1);
            ts.SampleStatistics();
            Console.WriteLine("Saving to {0}...", outputFolder);
            Directory.CreateDirectory(outputFolder);
            string sampleName = Path.GetFileName(projectFolder);
            ReadCounter readCounter = new ReadCounter();
            string extractedSummaryPath = PathHandler.GetGlobalExtractionSummaryPath(projectFolder);
            readCounter.AddSummaryTabfile(extractedSummaryPath);
            ts.Save(readCounter, Path.Combine(outputFolder, sampleName));
            //PDFReportCreator.AddPDFReport(outputFolder);
        }

    }
}
