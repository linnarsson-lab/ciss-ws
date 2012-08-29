#region Using directives

using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Drawing.Design;
using System.Drawing;


#endregion

namespace Linnarsson.InstrumentControl
{
	[Serializable]
	public class Channel
	{
		private int m_ExposureTime = 1000;
		public int ExposureTime
		{
			get { return m_ExposureTime; }
			set { m_ExposureTime = value; }
		}

		private int m_Gain = 1;
		[DefaultValue(1)]
		public int Gain
		{
			get { return m_Gain; }
			set { m_Gain = value; }
		}

		private int m_BinSize = 1;
		[DefaultValue(1)]
		public int BinSize
		{
			get { return m_BinSize; }
			set { m_BinSize = value; }
		}

		private int m_BitDepth = 14;
		[DefaultValue(14)]
		public int BitDepth
		{
			get { return m_BitDepth; }
			set { m_BitDepth = value; }
		}

		private int m_ReadoutFrequencyMHz = 10;
		[DefaultValue(10)]
		public int ReadoutFrequencyMHz
		{
			get { return m_ReadoutFrequencyMHz; }
			set { m_ReadoutFrequencyMHz = value; }
		}

		private Rectangle m_ROI = new Rectangle(0, 0, 2048, 2048);
		public Rectangle ROI
		{
			get { return m_ROI; }
			set { m_ROI = value; }
		}

		private bool m_TTLOutput = true;
		[DefaultValue(true)]
		public bool TTLOutput
		{
			get { return m_TTLOutput; }
			set { m_TTLOutput = value; }
		}

		private int m_AverageFrames = 1;
		[DefaultValue(1)]
		public int AverageFrames
		{
			get { return m_AverageFrames; }
			set { m_AverageFrames = value; }
		}

		private bool m_AutoConvert16Bit = true;
		[DefaultValue(true)]
		public bool AutoConvert16Bit
		{
			get { return m_AutoConvert16Bit; }
			set { m_AutoConvert16Bit = value; }
		}
	
		private Objective m_Objective = Objective.PlanFluor20;
		public Objective Objective
		{
			get { return m_Objective; }
			set { m_Objective = value; }
		}

		private FilterBlock m_Filter = FilterBlock.Cy3;
		public FilterBlock Filter
		{
			get { return m_Filter; }
			set { m_Filter = value; }
		}

		private OpticalPath m_OpticalPath = OpticalPath.LeftPort;
		public OpticalPath OpticalPath
		{
			get { return m_OpticalPath; }
			set { m_OpticalPath = value; }
		}

		private IntensityLevel m_LampIntensity = IntensityLevel.Full;
		public IntensityLevel LampIntensity
		{
			get { return m_LampIntensity; }
			set { m_LampIntensity = value; }
		}
	

		/// <summary>
		/// Used only for XML deserialization
		/// </summary>
		public Channel() { }

		/// <summary>
		/// Create a memento that records the current camera and (optionally) optical settings
		/// </summary>
		/// <param name="m"></param>
		public Channel(IReadonlyInstrument m)
		{
			//m_ExposureTime = m.Camera.ExposureTime;
			//m_Gain = m.Camera.Gain;
			//m_BinSize = m.Camera.BinSize;
			//m_BitDepth = m.Camera.BitDepth;
			//m_ROI = m.Camera.ROI;
			//m_TTLOutput = m.Camera.TTLOutput;
			//m_AutoConvert16Bit = m.Camera.AutoConvert16Bit;
			//m_AverageFrames = m.Camera.AverageFrames;
			m_Objective = m.Microscope.Objective;
            m_Filter = m.Microscope.Filter;
            m_OpticalPath = m.Microscope.OpticalPath;
			m_LampIntensity = m.Lamp.Intensity;
			//m_ReadoutFrequencyMHz = m.Camera.ReadoutFrequencyMHz;
		}

		/// <summary>
		/// Apply the saved settings to the microscope and camera. 
		/// </summary>
		/// <param name="m"></param>
		public void Apply(Instrument m)
		{
			//m.Camera.ExposureTime = ExposureTime;
			//m.Camera.Gain = Gain;
			//m.Camera.BinSize = BinSize;
			//m.Camera.BitDepth = BitDepth;
			//m.Camera.ROI = ROI;
			//m.Camera.TTLOutput = TTLOutput;
			//m.Camera.AverageFrames = AverageFrames;
			//m.Camera.AutoConvert16Bit = AutoConvert16Bit;
			//m.Camera.ReadoutFrequencyMHz = ReadoutFrequencyMHz;
			m.Microscope.Filter = Filter;
			m.Microscope.Objective = Objective;
            m.Microscope.OpticalPath = this.OpticalPath;
			m.Lamp.Intensity = LampIntensity;
		}

		public override string ToString()
		{
			// make a string that represents deviations from default
			StringBuilder sb = new StringBuilder();
			sb.Append(ExposureTime);
			sb.Append(" ms; ");
			sb.Append(Filter);
			sb.Append("; ");
			sb.Append(Objective);
			return sb.ToString();
		}
	}
}
