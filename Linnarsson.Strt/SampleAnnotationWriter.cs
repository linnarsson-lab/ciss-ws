using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    public class SampleAnnotationWriter
    {
        public static void WriteSampleAnnotationLines(StreamWriter matrixFile, Barcodes barcodes,
                                               string projectName, int nTabs, bool addColon, int[] selectedBcIndexes)
        {
            string colon = addColon ? ":" : "";
            String tabs = new String('\t', nTabs);
            matrixFile.Write("{0}Sample{1}", tabs, colon);
            foreach (int idx in selectedBcIndexes)
                matrixFile.Write("\t{0}_{1}", projectName, barcodes.GetWellId(idx));
            matrixFile.WriteLine();
            matrixFile.Write("{0}Well{1}", tabs, colon);
            foreach (int idx in selectedBcIndexes)
                matrixFile.Write("\t{0}", barcodes.GetWellId(idx));
            matrixFile.WriteLine();
            matrixFile.Write("{0}Barcode{1}", tabs, colon);
            foreach (int idx in selectedBcIndexes)
                matrixFile.Write("\t{0}", barcodes.Seqs[idx]);
            matrixFile.WriteLine();
            foreach (string annotation in barcodes.GetAnnotationTitles())
            {
                matrixFile.Write("{0}{1}{2}", tabs, annotation, colon);
                foreach (int idx in selectedBcIndexes)
                    matrixFile.Write("\t{0}", barcodes.GetAnnotation(annotation, idx));
                matrixFile.WriteLine();
            }
        }

    }
}
