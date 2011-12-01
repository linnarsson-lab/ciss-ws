using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using System.IO;

namespace ErrorCorrection_console
{
    public class recordCount
    {
        public string refSeqFilepath { get; set; }
        public recordCount()
        { }
        public void Count()
        {
            FastQFile fq = FastQFile.Load(refSeqFilepath, 64);
            Console.WriteLine (fq.Records.Count().ToString());
        }
    }
}
