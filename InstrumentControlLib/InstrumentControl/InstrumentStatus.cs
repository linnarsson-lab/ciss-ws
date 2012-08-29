using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Linnarsson.InstrumentControl
{
	public class InstrumentStatus
	{
		public Channel Channel { get; set; }
		public Position3D<double> Position { get; set; }
		public double Temperature { get; set; }
		public DateTime Time { get; set; }
		public PfsStatus PfsStatus { get; set; }

		public InstrumentStatus(Instrument instr)
		{
			Channel = new Channel(instr);
			Position = instr.Microscope.Position;
			Temperature = instr.Peltier.MeasuredTemperature;
			Time = DateTime.Now;
			PfsStatus = instr.Microscope.PfsStatus;
		}
	}
}
