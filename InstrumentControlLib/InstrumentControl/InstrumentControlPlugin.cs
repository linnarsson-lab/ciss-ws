using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.InstrumentControl
{
	public abstract class InstrumentControlPlugin
	{
		public abstract void Execute(Instrument instr);

		public InstrumentControlPlugin() { }
	}
}
