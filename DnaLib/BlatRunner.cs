using System.Diagnostics;
using System.IO;

namespace Linnarsson.Dna
{ 
    public class BlatRunner
    {
        public static PslParser Align(string queryFile, string databaseFile)
        {
            return Align(queryFile, databaseFile, "");
        }

        public static PslParser Align(string queryFile, string databaseFile, string options)
        {
            string outputFile = Path.GetTempFileName();
            string args = string.Format(@"""{0}"" ""{1}"" {2} ""{3}""", databaseFile, queryFile, options, outputFile);
            Process p = Process.Start("blat.exe", args);
            p.WaitForExit();
            PslParser pp = new PslParser(outputFile);
            File.Delete(outputFile);
            return pp;
        }
    }
}
