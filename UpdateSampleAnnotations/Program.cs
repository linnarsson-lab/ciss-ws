﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Linnarsson.Dna;
using Linnarsson.Strt;

namespace UpdateSampleAnnotations
{
    class UpdateSampleAnnotationsProgram
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                Console.WriteLine("Usage:\nmono UpdateSampleAnnotations.exe STRTRESULTFOLDER/FILE\n\n" + 
                                  "The MABLAB_annotations.tab file will be updated with latest stored annotation data.");
            }
            else
            {
                string resultFolder = args[0];
                WriteMatlabSampleAnnotations(resultFolder);
            }
        }

        private static string resultFolderMatch = "_([^_]+)_([^_]+)_[A-Z]+_[0-9]+_[0-9]+$";

        private static void WriteMatlabSampleAnnotations(string resultFolderOrAnnotFile)
        {
            bool isFolder = Directory.Exists(resultFolderOrAnnotFile);
            string resultFolder = isFolder ? resultFolderOrAnnotFile : Path.GetDirectoryName(resultFolderOrAnnotFile);
            string projectFolder = Path.GetDirectoryName(resultFolder);
            string projectName = Path.GetFileName(projectFolder);
            string annotFile = isFolder ? GetOldAnnotationFile(resultFolderOrAnnotFile, projectName) : resultFolderOrAnnotFile;
            string[] selectedWells = GetSelectedWells(annotFile);
            Match m = Regex.Match(resultFolder, resultFolderMatch);
            string barcodesName = m.Groups[1].Value;
            string speciesArg = m.Groups[2].Value;
            string sampleLayoutPath = PathHandler.GetSampleLayoutPath(projectFolder);
            Barcodes barcodes = Barcodes.GetBarcodes(barcodesName);
            int[] selectedBcIndexes = new int[selectedWells.Length];
            for (int idx = 0; idx < selectedWells.Length; idx++)
            {
                int selIdx = barcodes.GetBcIdxFromWellId(selectedWells[idx]);
                if (selIdx == -1)
                    throw new Exception("ERROR: Could not match a Well in old annotation file to a barcode: " + selectedWells[idx] +
                                        "\n  Has the barcode file (" + barcodesName + ") changed, or is the old annotation file erronous?");
                selectedBcIndexes[idx] = selIdx;
            }
            PlateLayout sampleLayout = PlateLayout.GetPlateLayout(projectName, sampleLayoutPath);
            Console.WriteLine("SampleLAyout filename:" + sampleLayout.Filename);
            barcodes.SetSampleLayout(sampleLayout);
            Console.WriteLine("#Annotations:" + barcodes.GetAnnotationTitles().Count);
            File.Move(annotFile, annotFile + ".old");
            StreamWriter annotWriter = new StreamWriter(annotFile);
            SampleAnnotationWriter.WriteSampleAnnotationLines(annotWriter, barcodes, projectName, 0, false, selectedBcIndexes);
            annotWriter.Close();
        }

        private static string GetOldAnnotationFile(string resultFolder, string projectName)
        {
            string[] annotFiles = Directory.GetFiles(resultFolder, projectName + "_MATLAB_annotations.tab");
            if (annotFiles.Length == 0)
                throw new Exception("Can not find a previous MATLAB_annotation file in " + resultFolder);
            if (annotFiles.Length > 1)
                throw new Exception("There are several MATLAB_annotation files in " + resultFolder + ". Please specify one.");
            string annotFile = annotFiles[0];
            return annotFile;
        }

        private static string[] GetSelectedWells(string oldAnnotFile)
        {
            StreamReader reader = new StreamReader(oldAnnotFile);
            string[] selectedWells = null;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("Well"))
                {
                    selectedWells = line.Substring(5).Trim().Split('\t');
                    break;
                }
            }
            reader.Close();
            return selectedWells;
        }
    }
}
