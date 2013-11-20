using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace C1SeqPlateLoader
{
    class Program
    {
        static void Main(string[] args)
        {
            string plateOrChip = args[0];
            new C1SeqPlateLoader().LoadC1Plate(plateOrChip);
        }

    }
}
