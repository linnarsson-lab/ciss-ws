using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Strt
{
    public interface IStrtReport
    {
        string ToXml(int indent);
    }

    class DataPoint : Dictionary<string, object>, IStrtReport
    {
        public DataPoint():base() {}
        public DataPoint(int capacity):base(capacity) {}

        public string ToXml(int indent)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(new string(' ', indent) + "<point ");
            foreach (string k in this.Keys)
                sb.Append(k + "=\"" + this[k].ToString() + "\" ");
            sb.Append("/>\n");
            return sb.ToString();
        }
    }

    class Curve : List<DataPoint>, IStrtReport
    {
        string title;
        string cls;

        public Curve(string title, string cls) : base() { this.title = title; this.cls = cls; }
        public Curve(string title, string cls, IEnumerable<DataPoint> collection) : base(collection) { this.title = title; this.cls = cls; }
        public Curve(string title, string cls, int capacity) : base(capacity) { this.title = title; this.cls = cls; }

        public string ToXml(int indent)
        {
            string spaces = new string(' ', indent);
            StringBuilder sb = new StringBuilder();
            sb.Append(spaces + "<curve legend=\"" + title + "\" class=\"" + cls + "\">n");
            foreach (DataPoint p in this)
                sb.Append(p.ToXml(indent + 2));
            sb.Append(spaces + "</curve>\n");
            return sb.ToString();
        }
    }

    public class ReadReport : IStrtReport
    {
        int totalCount = 0;
        List<DataPoint> dataPoints = new List<DataPoint>();

        public ReadReport(int totalCount)
        {
            this.totalCount = totalCount;
        }

        public void Add(string label, int count)
        {
            double fraction = (totalCount > 0)? count/(double)totalCount : 0.0;
            Add(label, count, fraction);
        }
        public void Add(string label, int count, double fraction)
        {
            dataPoints.Add(new DataPoint { {"type", label}, {"count", count}, {"fraction", fraction } });
        }

        public string ToXml(int indent)
        {
            string spaces = new string(' ', indent);
            StringBuilder sb = new StringBuilder();
            sb.Append(spaces + "<reads>\n");
            sb.Append(spaces + "  <title>Read distribution by categories</title>\n");
            foreach (DataPoint p in dataPoints)
                sb.Append(p.ToXml(indent + 2));
            sb.Append(spaces + "</reads>\n");
            return sb.ToString();
        }
    }

    public class SenseAntisenseReport : IStrtReport
    {
        List<DataPoint> dataPoints = new List<DataPoint>();

        public void Add(string label, int totLength, int senseCount, int antisenseCount)
        {
            dataPoints.Add(new DataPoint { {"feature", label}, {"length", totLength}, {"senseCount", senseCount}, {"antisenseCount", antisenseCount} });
        }

        public string ToXml(int indent)
        {
            string spaces = new string(' ', indent);
            StringBuilder sb = new StringBuilder();
            sb.Append(spaces + "<senseantisense>\n");
            sb.Append(spaces + "  <title>Sense and Antisense reads by feature type</title>\n");
            foreach (DataPoint p in dataPoints)
                sb.Append(p.ToXml(indent + 2));
            sb.Append(spaces + "</reads>\n");
            return sb.ToString();
        }
    }

    public class SpikeReport : IStrtReport
    {
        List<DataPoint> dataPoints = new List<DataPoint>();

        public void Add(string label, int level, int stdev)
        {
            dataPoints.Add(new DataPoint { { "feature", label }, { "level", level }, { "stdev", stdev } });
        }

        public string ToXml(int indent)
        {
            string spaces = new string(' ', indent);
            StringBuilder sb = new StringBuilder();
            sb.Append(spaces + "<spikes>\n");
            sb.Append(spaces + "  <title>Normalized spike means and standard deviations</title>\n");
            foreach (DataPoint p in dataPoints)
                sb.Append(p.ToXml(indent + 2));
            sb.Append(spaces + "</spikes>\n");
            return sb.ToString();
        }
    }

    public class HitProfileReport : IStrtReport
    {
        List<Curve> curves = new List<Curve>();

        public void NewCurve(string curveId, string curveClass)
        {
            curves.Add(new Curve(curveId, curveClass));
        }
        public void AddToCurve(double position, double frequency)
        {
            curves[curves.Count - 1].Add(new DataPoint { { "position", position }, { "frequency", frequency } });
        }

        public string ToXml(int indent)
        {
            string spaces = new string(' ', indent);
            StringBuilder sb = new StringBuilder();
            sb.Append(spaces + "<hitprofile>\n");
            sb.Append(spaces + "  <title>5'->3' hit profile of transcript classes</title>\n");
            foreach (Curve c in curves)
                sb.Append(c.ToXml(indent + 2));
            sb.Append(spaces + "</hitprofile>\n");
            return sb.ToString();
        }
    }

    public class VariationByReadReport : IStrtReport
    {
        List<Curve> curves = new List<Curve>();

        public void NewCurve(string curveId, string curveClass)
        {
            curves.Add(new Curve(curveId, curveClass));
        }
        public void AddToCurve(double readCount, double CV)
        {
            curves[curves.Count - 1].Add(new DataPoint { { "totalreads", readCount }, { "cv", CV } });
        }

        public string ToXml(int indent)
        {
            string spaces = new string(' ', indent);
            StringBuilder sb = new StringBuilder();
            sb.Append(spaces + "<variationbyreads>\n");
            sb.Append(spaces + "  <title>Median %CV as function of read count at various transcript levels</title>\n");
            foreach (Curve c in curves)
                sb.Append(c.ToXml(indent + 2));
            sb.Append(spaces + "</variationbyreads>\n");
            return sb.ToString();
        }
    }

    public class StrtReport
    {
        List<IStrtReport> reports = new List<IStrtReport>();

        public void AddReport(IStrtReport report)
        {
            reports.Add(report);
        }

        public string ToXml()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\n");
            sb.Append("<strtreport>\n");
            foreach (IStrtReport report in reports)
                sb.Append(report.ToXml(2));
            sb.Append("</strtreport>\n");
            return sb.ToString();
        }

    }
}
