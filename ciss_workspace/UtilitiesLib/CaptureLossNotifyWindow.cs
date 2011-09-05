using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Linnarsson.Utilities
{
	public interface ICaptureLossNotify
	{
		void OnLostCapture();
		IntPtr Handle { get;  }
	}

	public class CaptureLossNotifyWindow : NativeWindow
	{
		public ICaptureLossNotify Control { get; set; }

		public CaptureLossNotifyWindow(ICaptureLossNotify ctrl)
		{
			Control = ctrl;
			AssignHandle(ctrl.Handle);
		}

		protected override void WndProc(ref Message m)
		{
			if(m.Msg == 533) Control.OnLostCapture();
			base.WndProc(ref m);
		}
	}
}
