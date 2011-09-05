using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Xml;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
using System.IO.Compression;
using Linnarsson.Utilities;
using Linnarsson.Mathematics.SortSearch;
using Linnarsson.Mathematics;

namespace Linnarsson.Dna
{
	/// <summary>
	/// Represents the data found in a Genbank header. 
	/// </summary>
	[Serializable]
	public class GenbankHeader
	{
		public string FileName;
		public string ReleaseDate;
		public string ReleaseNumber;
		public string Description;
		public int NumberOfEntries;
		public int NumberOfLoci;
		public int NumberOfBases;
	}

	/// <summary>
	/// The header of a genbank record. 
	/// </summary>
	[Serializable]
	public class GenbankRecord
	{
		public int GI { get; set; }
		public string Accession { get; set; }
		public int Version { get; set; }

        public string AccessionVersion
        {
            get { return Accession + '.' + Version; }
        }

		public string LocusName { get; set; }
		public int SequenceLength { get; set; }
		public string MoleculeType { get; set; }
		public string MoleculeTopology { get; set; }
		public string GenbankDivision { get; set; }
		public string ModificationDate { get; set; }
		public string Definition { get; set; }
		public string Keywords { get; set; }
		public string Source { get; set; }
		public string Organism { get; set; }
		public List<GenbankFeature> Features { get; set; }

		public DnaSequence Sequence { get; set; }

		public GenbankRecord()
		{
			Features = new List<GenbankFeature>();
		}
	}

	[Serializable]
	public class GenbankFeature
	{
		public string Name { get; set; }
		public string Location { get; set; }
        public List<GenbankQualifier> Qualifiers;

		public DnaStrand Strand
		{
			get { return Location.StartsWith("complement(") ? DnaStrand.Reverse : DnaStrand.Forward; }
		}

		public List<Interval> GetLocationAsInterval()
		{
			List<Interval> result = new List<Interval>();
			string temp = Location;

			// Strip complement operator
			if(temp.StartsWith("complement(")) temp = temp.Substring(11, temp.Length - 12);
			if(temp.StartsWith("join("))
			{
				// Strip join operator
				temp = temp.Substring(5, temp.Length - 6);
				string[] intervals = temp.Split(',');
				foreach(string s in intervals) result.Add(GetInterval(s));
			}
			else result.Add(GetInterval(temp));
			return result;
		}

		private Interval GetInterval(string s)
		{
			if(!s.Contains("..")) return new Interval(long.Parse(s), long.Parse(s));
			if(s.StartsWith("..")) return new Interval(long.MinValue, long.Parse(s.Substring(2)));
			if(s.EndsWith("..")) return new Interval(long.Parse(s.TrimEnd('.')), long.MaxValue);
			string[] edges = s.Split(new string[] { ".." }, StringSplitOptions.None);
			return new Interval(long.Parse(edges[0].Trim('<', '>')), long.Parse(edges[1].Trim('<', '>')));
		}

		public GenbankFeature()
		{
			Qualifiers = new List<GenbankQualifier>();
		}

		public string GetQualifier(string qname)
		{
			foreach(var q in Qualifiers) if(q.Name == qname) return q.Value;
			return null;
		}
	}

	[Serializable]
	public class GenbankQualifier
	{
		public string Name { get; set; }
		public string Value { get; set; }
	}

	/// <summary>
	/// Represents a Genbank file. Instances can be created, filled with data and then saved in
	/// genbank format using Save(); or can be obtained by loading an existing genbank file using the
	/// static method Load().
	/// </summary>
	public class GenbankFile
	{
		/// <summary>
		/// Represents the genbank file header, or null if there was no header
		/// </summary>
		public GenbankHeader Header { get; set; }
		public List<GenbankRecord> Records { get; set; }

		public GenbankFile()
		{
			Records = new List<GenbankRecord>();
		}

		/// <summary>
		/// Parses a complete genbank file, loading all records into memory.
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static GenbankFile Load(string path)
		{
			return Load(path, (GenbankRecord gbr) => true);
		}

		/// <summary>
		/// Parses a genbank file and retains only those records for which the predicate
		/// returns true. The predicate will be called with each complete record (incl 
		/// the sequence), so can be used e.g. to filter based on the sequence.
		/// </summary>
		/// <param name="path"></param>
		/// <param name="filterPredicate"></param>
		/// <returns></returns>
		public static GenbankFile Load(string path, Func<GenbankRecord,bool> filterPredicate)
		{
			LineReader stream = new LineReader(path.OpenRead());
			GenbankFile gbf = new GenbankFile();

			gbf.Header = ParseGenbankHeader(stream);
			while(ParseRecord(gbf, stream, filterPredicate)) ;
			stream.Close();
			return gbf;
		}

		public static IEnumerable<GenbankRecord> Stream(string path)
		{
			LineReader stream = new LineReader(path.OpenRead());
			GenbankFile gbf = new GenbankFile();

			gbf.Header = ParseGenbankHeader(stream);
			while(ParseRecord(gbf, stream, (gbr) => { return true; }))
			{
				yield return gbf.Records[0];
				gbf.Records.Clear();
			}
			stream.Close();
		}

		#region Parser
		private static GenbankHeader ParseGenbankHeader(LineReader stream)
		{
			if(stream.PeekLine().StartsWith("LOCUS")) return null;

			string line = stream.ReadLine();
			GenbankHeader gbh = new GenbankHeader();

			gbh.FileName = line.Substring(0, 11);
			line = stream.ReadLine();								// line 2
			gbh.ReleaseDate = line.Trim();
			stream.ReadLine();										// line 3
			line = stream.ReadLine();								// line 4
			gbh.ReleaseNumber = line.Substring(47, 5);
			stream.ReadLine();										// line 5
			line = stream.ReadLine();								// line 6
			gbh.Description = line.Trim();
			stream.ReadLine();										// line 7
			line = stream.ReadLine();								// line 8
			gbh.NumberOfEntries = int.Parse(line.Substring(39, 8));
			gbh.NumberOfLoci = int.Parse(line.Substring(0, 8));
			gbh.NumberOfBases = int.Parse(line.Substring(15, 11));
			stream.ReadLine();										// line 9
			stream.ReadLine();										// line 10

			return gbh;
		}

		private static bool ParseRecord(GenbankFile gbf, LineReader stream, Func<GenbankRecord, bool> filterPredicate)
		{
			if (stream.AtEOF) return false;
			string line = stream.PeekLine();	// Now line = 'LOCUS ...' or null if EOF or sometimes "" at the end of a file
			while (line != null && line == "")
			{
				stream.ReadLine();
				line = stream.PeekLine();
			}
			if (line == null) return false;

			GenbankRecord gbr = ParseRecordHeader(stream); // Leaves line pointing to 'FEATURES...' or 'ORIGIN'
			line = stream.PeekLine();
            if (line.StartsWith("FEATURES"))
            {
				stream.ReadLine();
	    		while (ParseFeature(gbr, stream)) ;
            }

			gbr.Sequence = ParseSequence(stream);
			if(filterPredicate(gbr)) gbf.Records.Add(gbr);

			return true;
		}

		private static GenbankRecord ParseRecordHeader(LineReader stream)
		{
			GenbankRecord gbr = new GenbankRecord();
			gbr.Features = new List<GenbankFeature>();
			string headerKey = "";
			string line = stream.PeekLine();

			// We're at the locus line
			// LOCUS       SCU49845     5028 bp    DNA             PLN       21-JUN-1999

			while(true)
			{
				StringBuilder sb = new StringBuilder(160);
				sb.Append(line.Substring(12));
				sb.Append("\r\n");
				headerKey = line.Substring(0,12).TrimEnd();
				if(headerKey == "FEATURES" || headerKey == "ORIGIN") break;
				stream.ReadLine();
				line = stream.PeekLine();
				while(line == "" || line[0] == ' ')
				{
					if(line == "") 
					{
						sb.Append("\r\n");
						stream.ReadLine();
						line = stream.PeekLine();
						continue;
					}
					sb.Append(line.Substring(12));
					sb.Append("\r\n");
					stream.ReadLine();
					line = stream.PeekLine();
				}
				string content = sb.ToString();
				switch(headerKey)
				{
					case "LOCUS":
						//Positions  Contents
						//---------  --------
						//01-05      'LOCUS'
						//06-12      spaces
						//13-28      Locus name
						//29-29      space
						//30-40      Length of sequence, right-justified
						//41-41      space
						//42-43      bp
						//44-44      space
						//45-47      spaces, ss- (single-stranded), ds- (double-stranded), or
						//           ms- (mixed-stranded)
						//48-53      NA, DNA, RNA, tRNA (transfer RNA), rRNA (ribosomal RNA), 
						//           mRNA (messenger RNA), uRNA (small nuclear RNA), snRNA,
						//           snoRNA. Left justified.
						//54-55      space
						//56-63      'linear' followed by two spaces, or 'circular'
						//64-64      space
						//65-67      The division code (see Section 3.3)
						//68-68      space
						//69-79      Date, in the form dd-MMM-yyyy (e.g., 15-MAR-1991)

						// split out the data from the locus string
                        int spPos = 16;
                        while (content[spPos] != ' ') spPos++;
						gbr.LocusName = content.Substring(0, spPos).Trim();
                        string lenChars = content.Substring(spPos + 1, 27 - spPos);
                        while ("bp ".Contains(lenChars.Substring(lenChars.Length - 1, 1)))
                            lenChars = lenChars.Substring(0, lenChars.Length - 1);
						gbr.SequenceLength = int.Parse(lenChars.Trim());
                        if (content.Length >= 43)
							gbr.MoleculeType = content.Substring(32, 9).Trim();
                        if (content.Length >= 52)
							gbr.MoleculeTopology = content.Substring(43, 8).Trim();
                        if (content.Length >= 56)
							gbr.GenbankDivision = content.Substring(52, 3);
                        if (content.Length >= 66)
							gbr.ModificationDate = content.Substring(56, 11);
						break;
					case "DEFINITION":
						gbr.Definition = content.Trim();
						break;
					case "ACCESSION":
						gbr.Accession = content.Split(null, 2)[0];
						if(gbr.Accession == "-") gbr.Accession = gbr.LocusName;
						break;
					case "VERSION":
						string [] versionItems = content.Split(null);
						gbr.Version = int.Parse(versionItems[0].Split('.')[1]);
						gbr.GI = int.Parse(versionItems[2].Substring(3));
						break;
					case "KEYWORDS":
						gbr.Keywords = content.Trim();
						break;
					case "SOURCE":
						gbr.Source = content;
						gbr.Organism = content.Split(new char[] { '\r', '\n' })[2];
						break;
					case "REFERENCE":
						break;
					case "COMMENT":
						break;
				}
			}
			return gbr;
		}

		private static bool ParseFeature(GenbankRecord gbr, LineReader stream)
		{
			string line = stream.PeekLine();
			// Check for the BASE COUNT or ORIGIN line
			// just in case there are no features
			// BASE COUNT     1510 a   1074 c    835 g   1609 t
			// ORIGIN
			if(line[0] == 'B' || line[0] == 'O') return false;

			GenbankFeature f = new GenbankFeature();
			// Parse the FEATURE line and any continuation lines
			f.Name = line.Substring(5,16).Trim();
			StringBuilder sb = new StringBuilder(160);
			sb.Append(line.Substring(21));
			stream.ReadLine();
			line = stream.PeekLine();
			while(line[5] == ' ' && line[21] != '/')
			{
				sb.Append(line.Substring(21));
				stream.ReadLine();
				line = stream.PeekLine();
			}	
			f.Location = sb.ToString();
			// we're now on the first qualifier line, or next feature, or beyond the last feature

			if(line[0] == 'B' || line[0] == 'O' || line[21] != '/') // No qualifiers, and line points to next feature 
			{
				return true;
			}
			// Now parse all the qualifiers on the feature
			do 
			{
				GenbankQualifier q = new GenbankQualifier();
				sb = new StringBuilder(160);
				sb.Append(line.Substring(21)); // add the first qualifier line
				sb.Append("\r\n");
				stream.ReadLine();
				for(line = stream.PeekLine();line.Trim() == "" /* blank lines are illegal but present in NCBI files */ || (line[0] != 'B' && line[0] != 'O' && line[5] == ' ' && line[21] != '/'); stream.ReadLine(), line = stream.PeekLine())
				{
					sb.Append(line.Substring(21)); // add any subsequent qualifier lines
					sb.Append("\r\n");
				} // Now we're at a '/', a 'BASE COUNT' or the next feature
				// We need to split out the qualifier name as well as the value
				string qNameVal = sb.ToString();
				int firstSeparator = qNameVal.IndexOf('=');
				if(firstSeparator != -1)
				{
					q.Name = qNameVal.Substring(1,firstSeparator - 1);
					q.Value = qNameVal.Substring(firstSeparator + 1).Trim('"', ' ', '\r', '\n');
				}
				else
				{
					q.Name = qNameVal;
					q.Value = "";
				}
                f.Qualifiers.Add(q);
			}				
			while(line[0] == ' ' && line[21] == '/'); // parse additional qualifiers

            gbr.Features.Add(f);

			return true;
		}

		private static DnaSequence ParseSequence(LineReader stream)
		{
			// When entering, next line = 'BASE COUNT' or line = 'ORIGIN'
			// We skip the base counts, since we could get them from the sequence anyway
			stream.ReadLine();
			string line = stream.ReadLine();
			if(line[0] == 'B') line = stream.ReadLine(); // and we skip past the 'ORIGIN' line
			DnaSequence sb = new LongDnaSequence();
			while(line != null && line[0] != '/')
			{
				string seq = line.Substring(10);
				string [] items = seq.Split(' ');
				foreach(string item in items)
				{
					sb.Append(item);
				}
				line = stream.ReadLine();
			}
			
			// Now we're either at the first line of the next record, or at EOF
			return sb;
		}
		#endregion
	}
}