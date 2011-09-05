using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Collections.Generic;



namespace Linnarsson.Mathematics.Automata
{
	class RegexCacheItem
	{
		public RegularExpression Regex { get; set; }
		public string String { get; set; }
	}

	class RegexCache
	{
		private Dictionary<int, List<RegexCacheItem>> cache = new Dictionary<int, List<RegexCacheItem>>();
		private List<Triplet<int, int, bool>> sublanguages = new List<Triplet<int, int, bool>>();

		public RegularExpression FindCreateLiteral(string literal)
		{
			StringBuilder sb = new StringBuilder();
			foreach(char c in literal)
			{
				if((int)c >= 128) throw new ArgumentOutOfRangeException("Literal regex cannot contain characters outside 7-bit ASCII range");
				if(RegularExpression.REGEX_CHARS.Contains(c.ToString())) sb.Append("\\");
				sb.Append(c);
			}

			RegularExpression result = FindCreate(sb.ToString());
			result.IsLiteral = true;
			result.LiteralString = literal;
			return result;
		}

		public RegularExpression FindCreate(string regex)
		{
			int key = regex.GetHashCode();
			if(cache.ContainsKey(key))
			{
				foreach(RegexCacheItem item in cache[key]) if(item.String == regex) return item.Regex;
			}
			RegularExpression re = new RegularExpression(regex);
			if(!cache.ContainsKey(key)) cache[key] = new List<RegexCacheItem>();
			cache[key].Add(new RegexCacheItem { Regex = re, String = regex });
			return re;
		}

		public bool IsSublanguage(RegularExpression a, RegularExpression b)
		{
			foreach(Triplet<int, int, bool> pair in sublanguages)
			{
				if(pair.First == a.GetHashCode() && pair.Second == b.GetHashCode()) return pair.Third;
			}
			bool isSub = false;
			if(b.IsLiteral) isSub = a.Matches(b.LiteralString);
			else isSub = a.DFA.Difference(b.DFA).IsEmpty;
			sublanguages.Add(new Triplet<int, int, bool>(a.GetHashCode(), b.GetHashCode(), isSub));
			return isSub;
		}
	}

	public class RegularExpression
	{
		internal Nfa NFA;
		internal Dfa DFA;
		public string Regex { get; set; }
		internal bool IsLiteral;
		public string LiteralString { get; set; }
		internal const string REGEX_CHARS = "-()[]*+.?|\\";
		private static RegexCache cache;

		public override string ToString()
		{
			return Regex;
		}

		public bool IsEmpty
		{
			get { return DFA.IsEmpty; }
		}

		static RegularExpression()
		{
			cache = new RegexCache();
		}

		private RegularExpression()
		{

		}

		internal RegularExpression(string regex)
		{
			Regex = regex;
			if(regex == "") NFA = Nfa.EmptyString();
			else NFA = ParseRegex(new CursorTextReader(new StringReader(regex)));
			DFA = NFA.Determinize();
		}

		public static RegularExpression Create(string regex)
		{
			return cache.FindCreate(regex);
		}

		public static RegularExpression Literal(string literal)
		{
			return cache.FindCreateLiteral(literal);
		}

		public static RegularExpression AnyString()
		{
			return cache.FindCreate(".*");
		}

		public RegularExpression Concatenate(RegularExpression other)
		{
			if(Regex == "") return other;
			if(other.Regex == "") return this;
			return cache.FindCreate("(" + Regex + ")(" + other.Regex + ")");
			//RegularExpression regex = new RegularExpression();
			//regex.NFA = NFA.Copy();
			//regex.NFA.Concatenate(other.NFA.Copy());
			//regex.DFA = regex.NFA.Determinize();
			//regex.Regex = "(" + Regex + ")(" + other.Regex + ")";
			//return regex;
		}

		public RegularExpression Union(RegularExpression other)
		{
			if(Regex == "") return other;
			if(other.Regex == "") return this;
			return cache.FindCreate("(" + Regex + ")|(" + other.Regex + ")");
			//RegularExpression regex = new RegularExpression();
			//regex.NFA = NFA.Copy();
			//regex.NFA.Alternate(other.NFA.Copy());
			//regex.DFA = regex.NFA.Determinize();
			//regex.Regex = "(" + Regex + ")|(" + other.Regex + ")";
			//return regex;
		}
		
		public bool Matches(string input)
		{
			DFA.Restart();
			for(int ix = 0; ix < input.Length; ix++) DFA.Step(input[ix]);
			return DFA.IsAccept;
		}

		public bool IsSublanguageOf(RegularExpression other)
		{
			/* This is the basis for a strongly typed regex type with subtyping. 
			 * 
			 * The method may not look too complex, but it took great effort and a lot of
			 * thinking to get to the point where it is now working (2008-03-17 17:59). 
			 * About one year was needed to get here (not full time), and several conceptual
			 * leaps inspired by Benjamin Pierce's and John Hopcrofts' books among others.
			 */
			//return DFA.Difference(other.DFA).IsEmpty;

			return cache.IsSublanguage(this, other);
		}

		/// <summary>
		/// Determines if the two regexes accept exactly the same language
		/// </summary>
		/// <param name="other"></param>
		/// <returns>True if the languages accepted are the same</returns>
		public bool IsEquivalentTo(RegularExpression other)
		{
			return DFA.IsEquivalentTo(other.DFA);
		}

		private Nfa ParseRegex(CursorTextReader input)
		{
			return ParseRegex(input, 0);
		}

		private Nfa ParseRegex(CursorTextReader input, int precedence)
		{
			Nfa fsa = ParseQuantifiedAtom(input);
			if(fsa == null) throw new RegexException("Empty or invalid regex", input.Position);

			char? c = input.Peek();
			while(c != null)
			{
				switch(c)
				{

					case '|':
						if(precedence >= 1) return fsa;
						else
						{
							input.Read();
							Nfa right = ParseRegex(input, 1);
							fsa.Alternate(right);
						}
						break;
					case ')':
						return fsa;
					default: // concatenation
						if(precedence >= 2) return fsa;
						else
						{
							Nfa next = ParseQuantifiedAtom(input);
							fsa.Concatenate(next);
						}
						break;
				}
				c = input.Peek();
			}
			return fsa;
		}

		private Nfa ParseQuantifiedAtom(CursorTextReader input)
		{
			Nfa fsa = ParseAtom(input);
			char? c = input.Peek();
			if(c == null) return fsa;
			switch(c)
			{
				case '*':
					input.Read();
					fsa.KleeneClosure();
					break;
				case '+':
					input.Read();
					Nfa copy = fsa.Copy();
					copy.KleeneClosure();
					fsa.Concatenate(copy);
					break;
				case '?':
					input.Read();
					fsa.Optional();
					break;
			}
			return fsa;
		}

		private Nfa ParseAtom(CursorTextReader input)
		{
			char? c = input.Read();
			if(c == null) throw new RegexException("Empty regex atom", input.Position);

			switch(c)
			{
				case '(':
					Nfa fsa = ParseRegex(input, 0);
					if(input.Peek() != ')') throw new RegexException("Expected ')'", input.Position);
					input.Read();
					return fsa;
				case '[':
					Nfa fsa2 = ParseCharClass(input);
					return fsa2;
				case '.':
					return Nfa.AnyCharacter();
				case '\\':
					c = input.Read();
					if(c == null) throw new RegexException("Escape character '\\' cannot be at end of regex", input.Position);
					return Nfa.SingleCharacter((char)c);
			}
			if(REGEX_CHARS.Contains(c.ToString())) throw new RegexException("Regex atom cannot be a regex operator (did you forget an escape character?)", input.Position);

			return Nfa.SingleCharacter((char)c);
		}

		private Nfa ParseCharClass(CursorTextReader input)
		{
			char? c = input.Read();
			if(c == null) throw new RegexException("Missing closing bracket", input.Position);
			bool negated = (c == '^');
			if(negated)
			{
				c = input.Read();
				if(c == null) throw new RegexException("Missing closing bracket", input.Position);
			}

			HashSet<char> set = new HashSet<char>();

			while(c != ']')
			{
				if(c == '\\')
				{
					c = input.Read();
					if(c == null) throw new RegexException("Escape character '\\' cannot be at end of regex", input.Position);
				}
				set.Add((char)c);

				c = input.Read();
				if(c == null) throw new RegexException("Missing closing bracket", input.Position);
			}
			return Nfa.CharacterClass(set, negated);
		}
	}

	public class RegexException : Exception
	{
		public int Position { get; set; }
		public RegexException(string message, int pos)
			: base(message)
		{
			Position = pos;
		}
	}

	public class CursorTextReader
	{
		private TextReader InnerTextReader { get; set; }
		private int m_Position = 0;
		public int Position { get { return m_Position; } }

		public CursorTextReader(TextReader tr)
		{
			InnerTextReader = tr;
			m_Position = 0;
		}

		public char? Read()
		{
			int n = InnerTextReader.Read();
			if (n == -1) return null;
			m_Position++;
			return (char)n;
		}
		public char? Peek()
		{
			int n = InnerTextReader.Peek();
			if (n == -1) return null;
			return (char)n;
		}
	}
}
