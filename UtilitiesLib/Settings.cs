using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Linnarsson.Utilities
{
	public class Settings
	{
		/// <summary>
		/// True if all arguments were correctly parsed and all mandatory arguments were given
		/// </summary>
		public bool Valid { get; set; }

		/// <summary>
		/// List of all extra arguments, i.e. those not consumed by options
		/// </summary>
		public List<string> ExtraArguments { get; set; }

		// Keep track of which options are given (we use the tagString to track them, since this must be unique)
		private List<string> givenOptions = new List<string>();

		public Settings()
		{
			ExtraArguments = new List<string>();
			Valid = true; 
		}

		/// <summary>
		/// Handle one option, returning true if the extra argument was consumed
		/// </summary>
		/// <param name="tag"></param>
		/// <returns></returns>
		private bool _HandleOption(string tag, string extra)
		{
			// Find all the properties with Tag attributes
			var props = from p in this.GetType().GetProperties()
						select new { Property = p, Option = (OptionAttribute)p.GetCustomAttributes(typeof(OptionAttribute), false).FirstOrDefault()  };

			// Find those that have the tag
			var validProp = from p in props
							where p.Option != null && p.Option.Tags.Contains(tag)
							select p;

			// Verify that we found a single property
			if(validProp.Count() != 1) 
			{
				if(validProp.Count() == 0)
				{
					Console.WriteLine("ERROR: invalid option: " + tag);
					Valid = false;
					return false;
				}
				else
				{
					throw new InvalidProgramException("Two properties use the same option: " + tag);
				}
			}

			// Prepare to set the value of the property
			var theProp = validProp.First();

			// If this is a bool property, use the option as a flag (not consuming the extra argument)
			if (theProp.Property.PropertyType == typeof(bool))
			{
				theProp.Property.SetValue(this, true, null);
				givenOptions.Add(theProp.Option.TagString);
				return false;
			}

			// Otherwise, consume the extra argument and use it to set the property value
			if(extra == null)
			{
				Console.WriteLine("Missing argument for option: " + tag);
				Valid = false;
				return false;
			}
			var converter = TypeDescriptor.GetConverter(theProp.Property.PropertyType);
			try
			{
				theProp.Property.SetValue(this, converter.ConvertFromInvariantString(extra), null);
				givenOptions.Add(theProp.Option.TagString);
				return true;
			}
			catch (NotSupportedException)
			{
				Console.WriteLine("Cannot convert '" + extra + "' to a value of type " + theProp.Property.PropertyType);
				Valid = false;
				return true;
			}
		}

		/// <summary>
		/// After parsing all arguments, verify that we got all mandatory arguments, and set the value of defaults not given
		/// </summary>
		private void _Validate()
		{
			// Find all the properties with Tag attributes
			var props = from p in this.GetType().GetProperties()
						select new { Property = p, Option = (OptionAttribute)p.GetCustomAttributes(typeof(OptionAttribute), false).FirstOrDefault()  };

			// Find those properties that were not set
			var notSet = from p in props
 						 where p.Option != null && !givenOptions.Contains(p.Option.TagString)
						 select p;

			foreach(var p in notSet)
			{
				// If it has a default, set it
				if(p.Option.Default != null) _HandleOption(p.Option.Tags[0], p.Option.Default);
				else if(p.Property.PropertyType != typeof(bool) && !p.Option.Optional)
				{
					Console.WriteLine("Missing mandatory argument: " + p.Option.Tags[0]);
					Valid = false;
				}
			}
			var atts = this.GetType().GetCustomAttributes(typeof(UsageAttribute), false);
			if(atts.Length != 0)
			{
				UsageAttribute aa = atts[0] as UsageAttribute;
				if (ExtraArguments.Count < aa.MinExtra || ExtraArguments.Count > aa.MaxExtra)
				{
					Console.WriteLine("Wrong number of extra arguments (between " + aa.MinExtra + " and " + (aa.MaxExtra == int.MaxValue ? "infinity" : aa.MaxExtra.ToString()) + " expected)");
					foreach (var e in ExtraArguments) Console.WriteLine("     " + e);
					Valid = false;
				}
			}
			
			if(!Valid) _PrintUsage();
		}

		private void _PrintUsage()
		{
			Console.WriteLine();

			// Find all the properties with Tag attributes
			var props = from p in this.GetType().GetProperties()
						select new { Property = p, Option = (OptionAttribute)p.GetCustomAttributes(typeof(OptionAttribute), false).FirstOrDefault()  };
			
			var atts = this.GetType().GetCustomAttributes(typeof(UsageAttribute), false);
			if (atts.Length != 0)
			{
				UsageAttribute aa = atts[0] as UsageAttribute;
				Console.WriteLine("Usage: " + aa.Arguments);
			}
			else Console.WriteLine("Usage:");
			foreach(var p in props)
			{
				if (p.Option == null) continue;
				int numChars = 0;
				foreach(string t in p.Option.Tags)
				{
					if(t.Length == 1) 
					{
						Console.Write("-" + t + " ");
						numChars += 3;
					}
					else 
					{
						Console.Write("--" + t + " ");
						numChars += 3 + t.Length;
					}
				}
				Console.Write(new string(' ', 20 - numChars));
				if (p.Property.PropertyType != typeof(bool) && p.Option.Default == null && !p.Option.Optional) Console.Write("REQUIRED: ");
				Console.WriteLine(p.Option.Usage);
			}
			Console.WriteLine("   --help           Print this message");
		}

		/// <summary>
		/// You can override this method in derived classes in order to validate extra arguments. For
		/// example you could verify the existence of files, or use a regex to verify the argument etc.
		/// If the validation fails, write an error message to the console and set Valid = false
		/// </summary>
		/// <param name="arg">The text of the argument</param>
		/// <param name="index">Zero-based index of this argument among all the extra arguments (i.e. not counting options)</param>
		protected virtual void ValidateExtraArgument(string arg, int index)
		{

		}

		public bool Parse(string[] args)
		{
			int ix = 0;
			int extraIndex = 0;
			while (true)
			{
				if (ix == args.Length) break;
				string arg = args[ix];
				string extra = null;
				if (ix < args.Length - 1) extra = args[ix + 1];

				// Is it a long-form argument, or a single short-form argument?
				if (arg.StartsWith("-") && arg.Length == 2)
				{
					if (_HandleOption(arg.Substring(1), extra)) ix++;
				}
				else if (arg.StartsWith("--")) 
				{
					if (arg.Substring(2) == "help")
					{
						_PrintUsage();
						return false;
					}
					if(_HandleOption(arg.Substring(2), extra)) ix++;
				}
				else if (arg.StartsWith("-"))
				{
					foreach (char c in arg.Substring(1))
					{
						// Combined short-form tags can only be bool (any error is handled in the _HandleOption method)
						_HandleOption(c.ToString(), null);
					}
				}
				else
				{
					ValidateExtraArgument(arg, extraIndex);
					ExtraArguments.Add(arg);
					extraIndex++;
				}
				ix++;
			}

			_Validate();
			return Valid;
		}
		
	}

	[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
	public sealed class OptionAttribute : Attribute
	{
		// See the attribute guidelines at 
		//  http://go.microsoft.com/fwlink/?LinkId=85236
		readonly IList<string> tagList;
		internal readonly string TagString;

		/// <summary>
		/// Define the tags used with this property
		/// </summary>
		/// <param name="tags">tags separated by vertical bar |</param>
		public OptionAttribute(string tags)
		{
			var items = tags.Split('|');
			tagList = new List<string>(items).AsReadOnly();
			TagString = tags;
		}

		/// <summary>
		/// List of valid tags
		/// </summary>
		public IList<string> Tags
		{
			get { return tagList; }
		}

		private string m_Default;
		/// <summary>
		///  This is an optional argument, with the given default value
		/// </summary>
		public string Default { get { return m_Default; } set { m_Default = value; Optional = true; } }

		/// <summary>
		///  The usage string given when errors occur
		/// </summary>
		public string Usage { get; set; }

		/// <summary>
		/// This argument is optional, but its default value is not set, or is set in the constructor
		/// </summary>
		public bool Optional { get; set; }
	}

	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class UsageAttribute : Attribute
	{
		// See the attribute guidelines at 
		//  http://go.microsoft.com/fwlink/?LinkId=85236
		readonly string arguments;

		/// <summary>
		/// The usage string for this command
		/// </summary>
		/// <param name="arguments"></param>
		public UsageAttribute(string arguments)
		{
			this.arguments = arguments;
			MaxExtra = int.MaxValue;
		}

		public string Arguments
		{
			get { return arguments; }
		}

		/// <summary>
		/// Minimum number of extra arguments required
		/// </summary>
		public int MinExtra { get; set; }

		/// <summary>
		/// Maximum number of extra arguments allowed (0, 1, 2 etc or int.MaxValue for unlimited number)
		/// </summary>
		public int MaxExtra { get; set; }
	}

}
