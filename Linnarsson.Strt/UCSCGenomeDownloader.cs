﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.NetworkInformation;
using System.IO;
using System.IO.Compression;
using Linnarsson.Dna;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;

namespace Linnarsson.Strt
{
    public class UCSCGenomeDownloader
    {
        string goldenPathGenomes = "ftp://hgdownload.cse.ucsc.edu/goldenPath";
        string loginName = "anonymous";
        string password = Props.props.FailureReportAndAnonDownloadEmail;
        string gencodeMatcher = "wgEncodeGencodeCompV([0-9]+).txt.gz";

        /// <summary>
        /// Downloads the most up-to-date chromosomes, refFlat, MART annotations, and repeat mask files for a 
        /// species into the proper subfolder under GenomesFolder.
        /// </summary>
        /// <param name="latinSpeciesName">UCSC latin directory name, e.g. "Mus musculus", or a UCSC abbreviation, e.g. "mm"</param>
        public void DownloadGenome(string latinSpeciesName)
        {
            string abbrev, threeName;
            ParseSpecies(latinSpeciesName, out threeName, out abbrev);
            List<string> goldenPathMatches = GetMatchingGoldenPathDirs(abbrev, threeName);
            foreach (string buildName in goldenPathMatches)
            {
                string speciesURL = goldenPathGenomes + "/" + buildName;
                string destDir = Path.Combine(Path.Combine(Props.props.GenomesFolder, buildName), "genome");
                if (!Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
                try
                {
                    DownloadMartAnnotations(abbrev, destDir);
                    DownloadSpeciesGenome(speciesURL, buildName, destDir);
                    Console.WriteLine("Downloaded {0} from {1} to {2}", buildName, goldenPathGenomes, destDir);
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine("***Error: " + e);
                    Console.WriteLine("*** Looking for an older build ***");
                }
            }
            Console.WriteLine("ERROR: Could not find a build at {0} for {1} {2}!", goldenPathGenomes, abbrev, threeName);
        }

        private List<string> GetMatchingGoldenPathDirs(string abbrev, string threeName)
        {
            List<string> allSpecies = ListFiles(goldenPathGenomes);
            List<string> goldenPathMatches = new List<string>();
            foreach (string speciesSubdir in allSpecies)
            {
                string dir = speciesSubdir.ToLower();
                if (dir == threeName || dir == abbrev)
                {
                    goldenPathMatches.Add(speciesSubdir);
                    break;
                }
                if (Regex.IsMatch(dir, "^" + abbrev + "[0-9]+$") || Regex.IsMatch(dir, "^" + threeName + "[0-9]+$"))
                    goldenPathMatches.Add(speciesSubdir);
            }
            if (goldenPathMatches.Count == 0)
                throw new FileNotFoundException("No genome for " + abbrev + " " + threeName + " at " + goldenPathGenomes + ".");
            goldenPathMatches.Sort();
            goldenPathMatches.Reverse();
            return goldenPathMatches;
        }

        public void ParseSpecies(string name, out string threeName, out string abbrev)
        {
            abbrev = threeName = "";
            Match m = Regex.Match(name, "^[A-Z][A-a-z][0-9]+$");
            if (m.Success)
                abbrev = name;
            m = Regex.Match(name, "^[A-Za-z]{6}[0-9]+$");
            if (m.Success)
                threeName = name;
            m = Regex.Match(name, "^([A-Za-z]+)[_ ]([A-Za-z]+)$");
            if (m.Success)
            {
                abbrev = m.Groups[1].Value.Substring(0, 1) + m.Groups[2].Value.Substring(0, 1);
                threeName = m.Groups[1].Value.Substring(0, 3) + m.Groups[2].Value.Substring(0, 3);
            }
            abbrev = abbrev.ToLower();
            threeName = threeName.ToLower();
        }

        private void DownloadSpeciesGenome(string speciesURL, string buildName, string destDir)
        {
            string databaseURL = speciesURL + "/database";
            List<string> downloadFilenames = GetNamesOfAnnotationFiles(databaseURL);
            if (downloadFilenames.Count == 0)
                throw new FileNotFoundException("Can not find required database files at UCSC: " + speciesURL);
            DownloadFiles(databaseURL, destDir, downloadFilenames);
            string refFilePath = Path.Combine(destDir, "refFlat.txt.gz");
            if (File.Exists(refFilePath))
                GunzipFile(refFilePath);
            string chrURL = speciesURL + "/chromosomes";
            downloadFilenames.Clear();
            foreach (string chrFile in ListFiles(chrURL))
                if (chrFile.StartsWith("chr") && !chrFile.Contains("random") && !chrFile.Contains("Un"))
                    downloadFilenames.Add(chrFile);
            if (downloadFilenames.Count == 0)
                throw new FileNotFoundException("Can not find any chromosome files at UCSC: " + chrURL);
            DownloadFiles(chrURL, destDir, downloadFilenames);
            foreach (string file in downloadFilenames)
                if (file.EndsWith("gz"))
                    GunzipFile(Path.Combine(destDir, file));
        }

        private List<string> GetNamesOfAnnotationFiles(string databaseURL)
        {
            List<string> downloadFilenames = new List<string>();
            List<string> gencodeFiles = new List<string>();
            List<int> gencodeVersions = new List<int>();
            foreach (string file in ListFiles(databaseURL))
            {
                if (file.Contains("refFlat.txt") || file.Contains("refLink.txt") || file.Contains("kgXref.txt")
                    || file.Contains("chromInfo.txt") || file.Contains("kgSpAlias.txt") || file.Contains("rmsk.txt")
                    || file.Contains("README") || file.Contains("knownGene.txt"))
                    downloadFilenames.Add(file);
                if (Regex.Match(file, gencodeMatcher).Success)
                {
                    gencodeFiles.Add(file);
                    gencodeVersions.Add(int.Parse(Regex.Match(file, gencodeMatcher).Groups[1].Value));
                }
            }
            if (gencodeFiles.Count > 0)
            {
                Sort.QuickSort(gencodeVersions, gencodeFiles);
                string latestGencodeFile = gencodeFiles.Last();
                downloadFilenames.Add(latestGencodeFile);
                string attrsFile = latestGencodeFile.Replace("Comp", "Attrs");
                downloadFilenames.Add(attrsFile);
            }
            return downloadFilenames;
        }

        private static void GunzipFile(string inFilePath)
        {
            Console.WriteLine("Decompressing " + inFilePath);
            string outFilePath = inFilePath.Substring(0, inFilePath.Length - 3);
            byte[] buffer = new byte[1024 * 1024];
            using (FileStream inFile = new FileInfo(inFilePath).OpenRead())
            {
                using (FileStream outFile = File.Create(outFilePath))
                {
                    using (GZipStream decompress = new GZipStream(inFile, CompressionMode.Decompress))
                    {
                        int numRead;
                        while ((numRead = decompress.Read(buffer, 0, buffer.Length)) != 0)
                        {
                            outFile.Write(buffer, 0, numRead);
                        }
                    }
                }
            }
            File.Delete(inFilePath);
        }

        private List<string> ListFiles(string FTPAddress)
        {
            if (!FTPAddress.EndsWith("/"))
                FTPAddress = FTPAddress + "/";
            FtpWebRequest request = FtpWebRequest.Create(FTPAddress) as FtpWebRequest;
            request.Method = WebRequestMethods.Ftp.ListDirectory;
            request.Credentials = new NetworkCredential(loginName, password);
            request.UsePassive = true;
            request.UseBinary = true;
            FtpWebResponse response = request.GetResponse() as FtpWebResponse;
            Stream responseStream = response.GetResponseStream();
            List<string> files = new List<string>();
            StreamReader reader = new StreamReader(responseStream);
            while (!reader.EndOfStream)
            {
                files.Add(reader.ReadLine());
            }
            reader.Close();
            responseStream.Close();
            response.Close();
            return files;
        }

        /// <summary>
        /// Downloads VEGA and ENSEMBL annotations from BioMart database.
        /// </summary>
        /// <param name="abbrev">Species, e.g. "mm" or "hg"</param>
        /// <param name="downloadFolder">Optional destination folder. If "", downloads to genome folder of latest build for species</param>
        public void DownloadMartAnnotations(string abbrev, string downloadFolder)
        {
            Dictionary<string, string> threeNameToMartName = new Dictionary<string,string>() {
                {"gg", "ggallus"}, {"bt", "btaurus"}, {"hg", "hsapiens"}, {"hs", "hsapiens"}, {"mm", "mmusculus"}, 
                {"ce", "celegans"}, {"dm", "dmelanogaster"}, {"dr", "drerio"}, {"xl", "xlaevis"},
                {"cf", "cfamiliaris"}, {"xt", "xtropicalis"}, {"sc", "scerevisiae"}, {"pt", "ptroglodytes"} };

            string queryPattern = "http://www.biomart.org/biomart/martservice?query=" +
                                  "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                                  "<!DOCTYPE Query>" +
                                  "<Query virtualSchemaName = \"default\" formatter = \"TSV\" header = \"1\" uniqueRows = \"0\" count = \"\" datasetConfigVersion = \"0.6\" >" +
                                  "<Dataset name = \"{0}_gene_{1}\" interface = \"default\" >" +
                                  "<Attribute name = \"{1}_gene_id\" />" +
                                  "<Attribute name = \"{1}_transcript_id\" />" +
                                  "<Attribute name = \"exon_chrom_start\" />" +
                                  "<Attribute name = \"exon_chrom_end\" />" +
                                  "<Attribute name = \"rank\" />" +
                                  "<Attribute name = \"start_position\" />" +
                                  "<Attribute name = \"end_position\" />" +
                                  "<Attribute name = \"chromosome_name\" />" +
                                  "<Attribute name = \"strand\" />" +
                                  "<Attribute name = \"external_gene_id\" />" +
                                  "<Attribute name = \"gene_biotype\" />" +
                                  "</Dataset>" +
                                  "</Query>";
            if (!threeNameToMartName.ContainsKey(abbrev)) return;
            string speciesId = threeNameToMartName[abbrev];
            if (downloadFolder == "")
            {
                string[] matches = Directory.GetDirectories(Props.props.GenomesFolder, abbrev + "*");
                Array.Sort(matches);
                matches.Reverse();
                downloadFolder = Path.Combine(matches[0], "genome");
            }
            WebClient webClient = new WebClient();
            foreach (string db in new string[] { "vega", "ensembl" })
            {
                string source = string.Format(queryPattern, speciesId, db);
                string strtName = (db == "ensembl") ? "ENSE" : db.ToUpper();
                string dest = Path.Combine(downloadFolder, strtName + "_mart_export.txt");
                Console.WriteLine("Reading " + dest + "...");
                for (int tryNo = 1; tryNo <= 5; tryNo++)
                {
                    try
                    {
                        webClient.DownloadFile(source, dest);
                        break;
                    }
                    catch (Exception)
                    { }
                }
                if (!File.Exists(dest) || new FileInfo(dest).Length < 1000)
                {
                    Console.WriteLine("Could not download " + abbrev + " BioMart annotations for " + db);
                    if (File.Exists(dest)) File.Delete(dest);
                }
                else
                {
                    Console.WriteLine("Downloaded " + abbrev + " BioMart annotations for " + db + " to " + dest);
                }
            }
        }

        private void DownloadFiles(string FTPAddress, string downloadFolder, List<string> filenames)
        {
            WebClient webClient = new WebClient();
            foreach (string fileName in filenames)
            {
                string source = FTPAddress + "/" + fileName;
                string dest = Path.Combine(downloadFolder, fileName);
                Console.WriteLine("Reading " + source + " to " + dest);
                for (int tryNo = 1; tryNo <= 5; tryNo++)
                {
                    try
                    {
                        webClient.DownloadFile(source, dest);
                        break;
                    }
                    catch (Exception e)
                    {
                        if (tryNo == 5)
                            throw e;
                    }
                }
            }
        }
    }
}
