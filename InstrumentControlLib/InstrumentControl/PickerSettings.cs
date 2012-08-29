using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Utilities;

namespace Linnarsson.InstrumentControl
{
	public class NextWellChangedEventArgs : EventArgs
	{
		public string NextWell { get; set; }
	}

	public class PickerSettings
	{
		public string ImageFolder { get; set; }

		// In each case, the Z value refers to the cell picker (Zaber device)
		public Position3D<double> PositionA03 { get; set; }
		public Position3D<double> PositionH12 { get; set; }
		public int ClearHeight { get; set; }
		public int WellDepth { get; set; }
		public int LowspeedZone { get; set; }

		public event EventHandler<NextWellChangedEventArgs> NextWellChanged = delegate { };

		public PickerSettings()
		{
			PositionA03 = new Position3D<double>();
			PositionH12 = new Position3D<double>();
			ClearHeight = 0;
			WellDepth = 0;
			Restart();
		}

		public void Save(string filename)
		{
			var file = filename.OpenWrite();
			file.WriteLine(PositionA03.X.ToString());
			file.WriteLine(PositionA03.Y.ToString());

			file.WriteLine(PositionH12.X.ToString());
			file.WriteLine(PositionH12.Y.ToString());

			file.WriteLine(ClearHeight.ToString());
			file.WriteLine(WellDepth.ToString());

			file.Close();
		}

		public static PickerSettings Load(string filename)
		{
			var file = filename.OpenRead();
			PickerSettings ps = new PickerSettings();
			ps.PositionA03 = new Position3D<double>(
				double.Parse(file.ReadLine()),
				double.Parse(file.ReadLine()),
				0);
			ps.PositionH12 = new Position3D<double>(
				double.Parse(file.ReadLine()),
				double.Parse(file.ReadLine()),
				0);
			ps.ClearHeight = int.Parse(file.ReadLine());
			ps.WellDepth = int.Parse(file.ReadLine());
			ps.LowspeedZone = ps.WellDepth;
			file.Close();
			return ps;
		}


		private int nextRow;
		private int nextCol;

		public string NextWell { get { return ToWellString(nextRow, nextCol);  } }

		public void Restart()
		{
			nextCol = 2;
			nextRow = 0;
			ImageFolder = null;
			NextWellChanged(this, new NextWellChangedEventArgs { NextWell = NextWell });
		}

		public void Set(int row, int col)
		{
			nextCol = col - 1;
			nextRow = row - 1;
			NextWellChanged(this, new NextWellChangedEventArgs { NextWell = NextWell });
		}

		public void DidPickCell()
		{
			nextRow++;
			if(nextRow == 8)
			{
				nextRow = 0;
				nextCol++;
				if(nextCol == 12)
				{
					nextCol = 0;
				}
			}
			NextWellChanged(this, new NextWellChangedEventArgs { NextWell = NextWell });
		}

		public Position3D<double> GetNextWellPosition() 
		{
			return new Position3D<double>(
				PositionA03.X + (PositionH12.X - PositionA03.X) * (nextCol-2) / 9,
				PositionA03.Y + (PositionH12.Y - PositionA03.Y) * nextRow / 7,
				WellDepth);
		}

		
		private string ToWellString(int r, int c)
		{
			return "ABCDEFGH"[r].ToString() + (c+1).ToString("00");
		}

		//public void SetCalibrationPoint(Instrument instr)
		//{
		//    double dX = instr.Microscope.Position.X - CalibrationPoint.X;
		//    double dY = instr.Microscope.Position.Y - CalibrationPoint.Y;
		//    double dZ = instr.CellPicker.Position - CalibrationPoint.Z;

		//    PositionA03 = new Position3D<double>(
		//        PositionA03.X + dX,
		//        PositionA03.Y + dY,
		//        PositionA03.Z + dZ);

		//    PositionH12 = new Position3D<double>(
		//        PositionH12.X + dX,
		//        PositionH12.Y + dY,
		//        PositionH12.Z + dZ);

		//    ClearHeight = (int)(ClearHeight + dZ);

		//    DishPositionDown = new Position3D<double>(
		//        DishPositionDown.X + dX,
		//        DishPositionDown.Y + dY,
		//        DishPositionDown.Z + dZ);

		//    CalibrationPoint = new Position3D<double>(
		//        instr.Microscope.Position.X,
		//        instr.Microscope.Position.Y,
		//        instr.CellPicker.Position);

		//    Console.WriteLine("Calibration point moved: dX = {0}, dY = {1}, dZ = {2}", dX, dY, dZ);
		//}
	}
}
