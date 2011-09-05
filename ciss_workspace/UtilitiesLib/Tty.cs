using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Utilities
{
	public enum TtyAttribute
	{
		None = 0,
		Bold = 1,
		Underscore = 4,
		Blink = 5,
		Reverse = 7,
		Concealed = 8,

		ForegroundBlack = 30,
		ForegroundRed = 31,
		ForegroundGreen = 32,
		ForegroundYellow = 33,
		ForegroundBlue = 34,
		ForegroundMagenta = 35,
		ForegroundCyan = 36,
		ForegroundWhite = 37,

		BackgroundBlack = 40,
		BackgroundRed = 41,
		BackgroundGreen = 42,
		BackgroundYellow = 43,
		BackgroundBlue = 44,
		BackgroundMagenta = 45,
		BackgroundCyan = 46,
		BackgroundWhite = 47,
	}

	public class Tty
	{
		private static void Escape(string code)
		{
			Console.Write((char)27);
			Console.Write("[");
			Console.Write(code);
		}

		public static void Up()
		{
			Escape("1A");
		}

		public static void Up(int n)
		{
			Escape(n.ToString() + "A");
		}

		public static void Down()
		{
			Escape("1B");
		}

		public static void Down(int n)
		{
			Escape(n.ToString() + "B");
		}

		public static void Left()
		{
			Escape("1D");
		}

		public static void Left(int n)
		{
			Escape(n.ToString() + "D");
		}
		public static void Right()
		{
			Escape("1C");
		}

		public static void Right(int n)
		{
			Escape(n.ToString() + "C");
		}
		public static void SaveCursorPosition()
		{
			Escape("s");
		}
		public static void RestoreCursorPosition()
		{
			Escape("u");
		}
		public static void EraseDisplay()
		{
			Escape("2J");
		}
		public static void EraseLine()
		{
			Escape("K");
		}
		public static void LineWrap(bool enabled)
		{
			if (enabled) Escape("=7h");
			else Escape("=7l");
		}
		public static void CursorVisible(bool enabled)
		{
			if (enabled) Escape("?25h");
			else Escape("?25I");
		}

		public static void SetAttribute(TtyAttribute attr)
		{
			Escape(((byte)attr).ToString() + "m");
		}
	}
}
