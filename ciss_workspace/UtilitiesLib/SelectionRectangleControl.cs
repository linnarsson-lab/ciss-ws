using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Linnarsson.Utilities
{
	public partial class SelectionRectangleControl : UserControl, ICaptureLossNotify
	{
		protected virtual void OnBeginSelection(bool shiftDown) {}
		protected virtual void OnEndSelection(Rectangle selection) { }
		protected virtual void OnSelecting(Rectangle selection) { }
		protected virtual void OnSelectionCancelled() { }

		public SelectionRectangleControl()
		{
			InitializeComponent();

			// Set up mouse capture
			CaptureLossNotifyWindow win = new CaptureLossNotifyWindow(this);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			if(!selecting) return;
			Rectangle rect = makeSelectionrectangle();
			e.Graphics.DrawRectangle(new Pen(Color.Gold, 1), rect);
			e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(128, Color.Gold)), rect);	
		}

		private bool selecting;
		private Point ptBeginSel, ptEndSel;
		public void OnLostCapture()
		{
			selecting = false;
			OnSelectionCancelled();
			Invalidate();
		}
		private Rectangle makeSelectionrectangle()
		{
			return new Rectangle(Math.Min(ptBeginSel.X, ptEndSel.X),
								Math.Min(ptBeginSel.Y, ptEndSel.Y),
								Math.Abs(ptEndSel.X - ptBeginSel.X),
								Math.Abs(ptEndSel.Y - ptBeginSel.Y));

		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if(e.Button == MouseButtons.Left)
			{
				ptBeginSel = ptEndSel = new Point(e.X, e.Y);
				selecting = true;
				OnBeginSelection(ModifierKeys == Keys.Shift);
				Invalidate();
			}
		}
		protected override void OnMouseMove(MouseEventArgs e)
		{
			if(selecting)
			{
				ptEndSel = new Point(e.X, e.Y);
				OnSelecting(makeSelectionrectangle());
				Invalidate();
			}
		}
		protected override void OnMouseUp(MouseEventArgs e)
		{
			if(selecting)
			{
				selecting = false;
				OnEndSelection(makeSelectionrectangle());
				Invalidate();
			}
		}
	}
}
