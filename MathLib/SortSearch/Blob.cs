using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Mathematics.SortSearch
{
		/// <summary>
	/// Represents an array capable of storing a very large number of 33-bit values
	/// </summary>
	public unsafe class Blob33
	{
		public readonly long Length;
		uint* mainWords;	// array of 32-bit words
		byte* extraBits;	// packed array of one extra bit per word

		public long this[long index]
		{
			get
			{	
				// get the upper 32 bits
				long result = ((long)*(mainWords + index)) << 1;

				// add the lower 33rd bit
				long offset = index >> 3;
				byte shift = (byte)(index % 8);
				byte bit = (byte)((*(extraBits + offset) & (1 << shift)) >> shift);
				result |= bit;
				return result;
			}
			set
			{
				// store the upper 32 bits
				*(mainWords + index) = (uint)(value >> 1);

				// store the lower 33rd bit
				long offset = index >> 3;
				byte shift = (byte)(index % 8);
				byte bit = (byte)((value & 1) << shift);
			
				// put the extra bit in the right place
				byte mask = (byte)~(1 << shift);
				*(extraBits + offset) = (byte)((*(extraBits + offset) & mask) | bit);
			}
		}

		public void Exchange(long i, long j)
		{
			long temp = this[i];
			this[i] = this[j];
			this[j] = temp;
		}

		public Blob33(long length)
		{
			Length = length;
			mainWords = (uint *)Marshal.AllocHGlobal((IntPtr)(length * sizeof(uint)));
			extraBits = (byte*)Marshal.AllocHGlobal((IntPtr)(length / sizeof(byte) + 1));
		}

		~Blob33()
		{
			Marshal.FreeHGlobal((IntPtr)mainWords);
			Marshal.FreeHGlobal((IntPtr)extraBits);
		}




		public void Serialize(BinaryWriter writer)
		{
			int reportFreq = (int)(Length / 100);
			writer.Write(Length);
			for(long ix = 0; ix < Length; ix++)
			{
				writer.Write(*(mainWords + ix));
				if(ix % reportFreq == 0) Background.Progress((int)(ix * 100 / Length / 2));
			}
			for(long ix = 0; ix < Length / sizeof(byte) + 1; ix++)
			{
				writer.Write(*(extraBits + ix));
				if(ix % reportFreq == 0) Background.Progress((int)(ix * 100 / Length / 2) + 50);
			}

		}

		public static Blob33 Deserialize(BinaryReader reader)
		{
			long length = reader.ReadInt64();
			int reportFreq = (int)(length / 100);
			Blob33 result = new Blob33(length);

			for(long ix = 0; ix < length; ix++)
			{
				*(result.mainWords + ix) = reader.ReadUInt32();
				if(ix % reportFreq == 0) Background.Progress((int)(ix * 100 / length / 2));
			}
			for(long ix = 0; ix < length / sizeof(byte) + 1; ix++)
			{
				*(result.extraBits + ix) = reader.ReadByte();
				if(ix % reportFreq == 0) Background.Progress((int)(ix * 100 / length / 2) + 50);
			}
			return result;
		}

	}
	/// <summary>
	/// Represents an array capable of storing a very large number of 2-bit values
	/// </summary>
	public unsafe class Blob2
	{
		public long Capacity { get; private set; }
		public long Length { get; private set; }
		byte* words;	// array of 8-bit words


		public byte this[long index]
		{
			get
			{
				// extract the two bits
				long offset = index >> 2;
				byte shift = (byte)((index % 4) * 2);
				byte mask = (byte)(3 << shift);

				return (byte)((words[offset] & mask) >> shift);
			}
			set
			{
				// no bounds or value checking here, for performance reasons

				// locate the two bits
				long offset = index >> 2;
				byte shift = (byte)((index % 4) * 2);
				byte mask = (byte)~(3 << shift);

				// put them in place
				*(words + offset) = (byte)((*(words + offset) & mask) | (value << shift));
			}
		}

		public void Append(byte item)
		{
			if(Capacity <= Length)
			{
				Capacity *= 2;
				var temp = (byte*)Marshal.AllocHGlobal((IntPtr)(Capacity / 4 + 1));
				for(long i = 0; i < Length/4+1; i++)
				{
					*(temp + i) = *(words + i);
				}
				Marshal.FreeHGlobal((IntPtr)words);
				words = temp;
			}
			this[Length++]=item;
		}

		public void Exchange(long i, long j)
		{
			byte temp = this[i];
			this[i] = this[j];
			this[j] = temp;
		}

		public Blob2(long capacity)
		{
			Length = 0;
			Capacity = capacity;
			words = (byte*)Marshal.AllocHGlobal((IntPtr)(Capacity / 4 + 1));
		}

		~Blob2()
		{
			Marshal.FreeHGlobal((IntPtr)words);
		}


		public void Serialize(BinaryWriter writer)
		{
			writer.Write(Length);
			for(long ix = 0; ix < Length/4 + 1; ix++)
			{
				writer.Write(*(words + ix));
			}
		}

		public static Blob2 Deserialize(BinaryReader reader)
		{
			long length = reader.ReadInt64();
			int reportFreq = (int)( length / 100);
			Blob2 result = new Blob2(length);
			result.Length = length;
			for(long ix = 0; ix < length/4 + 1; ix++)
			{
				*(result.words + ix) = reader.ReadByte();
				if(ix % reportFreq == 0) Background.Progress((int)(ix * 100 / (length/4+1)));
			}
			return result;
		}

	}
}
