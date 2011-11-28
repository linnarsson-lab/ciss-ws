﻿using System;
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
                { "Err=", "Select the error output file", (v) => ErrH.errorFilepath = v },
                { "Ecor=", "Select the error Corrected file", (v) => ErrH.errorCorrectedFilepath = v },
            };
            foreach (var x in p.Parse(args)) ;
            if (ErrH.refSeqFilepath == null || ErrH.errorFilepath== null )
            {
                Console.WriteLine("Usage: mono ErrorCorrection_console.exe [OPTIONS]");
                Console.WriteLine();
                p.WriteOptionDescriptions(Console.Out);
                return;
            }

            Console.WriteLine("Junction Lables Simulator Error Correction");
            ErrH.ErrorCorrection();
        }
    }
}
