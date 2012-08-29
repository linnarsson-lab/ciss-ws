using System;
using System.ComponentModel;
using System.Collections.Generic;
using Linnarsson.Persistence;
using Linnarsson.Mathematics;
using System.Drawing;
using Linnarsson.CommandEngine;
using System.IO;

namespace Linnarsson.ImageAnalysis
{
    [Serializable]
    
    [DisplayName("AnalyzeBubbles")]
    [Description("Searches for bubbles in the image")]
    [Category("Debug Module")]
    public class AnalyzeBubblesCommand : ImageAnalysisCommand
    {
        public bool m_AllImages;
        [Description("True for all, false for only first images.")]
        public bool AllImages
        {
            get { return m_AllImages; }
            set { m_AllImages = value; }
        }

        public bool m_FilterFeatures;
        [Description("True to zero out measurements for features under bubbles. Requires that AllImages is true.")]
        public bool FilterFeatures
        {
            get { return m_FilterFeatures; }
            set { m_FilterFeatures = value; }
        }

        private int m_MinBubbleSize = 25;
        [Description("Minimum size in pixels of a bubble.")]
        public int MinBubbleSize
        {
            get { return m_MinBubbleSize; }
            set { m_MinBubbleSize = value; }
        }

        private bool m_ReportToFile;
        [Description("Set to true to get a report in the Features folder.")]
        public bool ReportToFile
        {
            get { return m_ReportToFile; }
            set { m_ReportToFile = value; }
        }

        public override void Execute(ImageAnalysisData id)
        {
            if (!AllImages && !id.IsFirstImage)
                return;
            id.CurrentImage.EnsureImageIsLoaded();
            BubbleMasker bm = new BubbleMasker();
            bm.MinDiameter = MinBubbleSize;
            List<Bubble> bubbles = bm.FindBubbles(id.CurrentImage.Matrix);
            if (bubbles.Count >= 1 && ReportToFile)
            {
                foreach (Bubble bc in bubbles)
                {
                    string alignmentFile = id.RemoteServer.GetImageAlignmentFile(id.Location);
                    string bubbleFile = alignmentFile.Replace(".alignment", ".bubbles");
                    bool exists = File.Exists(bubbleFile);
                    StreamWriter fs = new StreamWriter(bubbleFile, true);
                    if (!exists)
                        fs.WriteLine("X\tY\tXWidth\tYWidth");
                    ImageInfo ii = id.CurrentImage;
                    fs.WriteLine("{0}\t{1}\t{2}\t{3}",
                                 bc.xCenter, bc.yCenter, bc.xSpan, bc.ySpan);
                    fs.Close();
                }
            }
            if (FilterFeatures && bubbles.Count >= 1 && AllImages)
            { // Only filter measurement probes
                int[,] bioPixels = id.CurrentImage.Matrix.BioPixels;
                for (int feature = 0; feature < bioPixels.GetLength(0); feature++)
                {
                    int x = bioPixels[feature, 1];
                    int y = bioPixels[feature, 2];
                    foreach (Bubble bc in bubbles)
                    {
                        if (bc.Contains(y, x))
                        {
                            bioPixels[feature, 0] = 0;
                            break;
                        }
                    }
                }
            }
        }
    }
}