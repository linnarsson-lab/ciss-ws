using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Utilities;

namespace ErrorCorrection_console
{
    class Program
    {
        static void Main(string[] args)
        {
            ErrorHandler ErrH = new ErrorHandler();
            var p = new Options() {
                { "seq=", "Select the FasQ file with error ", (v) => ErrH.refSeqFilepath = v },
                
            };
            foreach (var x in p.Parse(args)) ;
            if (ErrH.refSeqFilepath == null )
            {
                Console.WriteLine("Usage: mono ErrorCorrection_console.exe [OPTIONS]");
                Console.WriteLine();
                p.WriteOptionDescriptions(Console.Out);
                return;
            }

            Console.WriteLine("Junction Lables Simulator");
            ErrH.ErrorCorrection();
        }
    }
}
