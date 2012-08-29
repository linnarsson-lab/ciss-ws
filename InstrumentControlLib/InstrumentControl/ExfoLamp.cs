using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Threading;
using System.IO;

namespace Linnarsson.InstrumentControl
{
	public enum IntensityLevel	{Zero, Eightth, Quarter, Half, Full}
	[Flags]
	enum UnitStatus { Alarm = 1, Lamp = 2, Shutter = 4, Home = 8, LampReady = 16, Lock = 32 }

	public delegate void LampPropertyChanged();

	public interface IReadonlyLamp
	{
		bool Connected { get; }
		IntensityLevel Intensity { get; }
		event LampPropertyChanged LampChanged;
		int LampHours { get; }
		bool ShutterOpen { get; }
	}

	[Serializable]
	public class ExfoLamp : IReadonlyLamp
	{
		private SerialPort ExfoLampPort;
		private DateTime previousCommandTime = DateTime.MinValue;

		private IntensityLevel m_Intensity;
		public IntensityLevel Intensity
		{
			get { return m_Intensity; }
			set { m_Intensity = value; SetIntensityLevel(value); }
		}

		private int m_LampHours;
		public int LampHours
		{
			get { return m_LampHours; }
		}

		private bool m_ShutterOpen;
		public bool ShutterOpen
		{
			get { return m_ShutterOpen; }
		}
	
		public event LampPropertyChanged LampChanged;

		public void Refresh()
		{
            if (ExfoLampPort == null || !ExfoLampPort.IsOpen) return;
			int oldLH = LampHours;
			m_LampHours = GetLampHours();

			IntensityLevel oldIL = Intensity;
			m_Intensity = GetIntensityLevel();

			bool oldShutter = ShutterOpen;
			m_ShutterOpen = (getUnitStatus() & UnitStatus.Shutter) == UnitStatus.Shutter;

			if(LampChanged != null && (oldIL != Intensity || oldLH != LampHours || oldShutter != ShutterOpen))
			{
				LampChanged();
			}
		}

		public ExfoLamp()
		{
		}

		public bool Connected { get; set; }

		private void Throttle(int delayMilliseconds)
		{
			//TimeSpan lag = DateTime.Now - previousCommandTime;
			//previousCommandTime = DateTime.Now;

			//if(lag.TotalMilliseconds > delayMilliseconds) return;
			//else Thread.Sleep(delayMilliseconds - (int)lag.TotalMilliseconds);
			Thread.Sleep(delayMilliseconds);
		}

		private void CheckAnswer(string answer)
		{
			if(answer== "") return;  //OK is an empty NewLine.
			else if (answer == "e")	throw new Exception("Syntax error in communication with Exfo Lamp");
			else throw new Exception("Serial port communication error with Exfo Lamp");
		}
		public void Connect(string comport)
		{
			try
			{
				ExfoLampPort = new System.IO.Ports.SerialPort(comport, 9600, Parity.None, 8, StopBits.One);
				ExfoLampPort.Close();
				ExfoLampPort.Open();
				Connected = true;
				ExfoLampPort.ReadTimeout = 1000;
				ExfoLampPort.WriteTimeout = 500;
				ExfoLampPort.NewLine = "\r";
				ExfoLampPort.WriteLine("tt");
				string str = ExfoLampPort.ReadLine();
				CheckAnswer(str);
			}
			catch(Exception e)
			{
				if(Connected)
				{
					ExfoLampPort.Close();
					Connected = false;
				}
				throw new IOException("Unable to connect to EXFO XCite lamp", e);
			}
		}

		public IntensityLevel GetIntensityLevel()
		{
			if(Connected)
			{
				Throttle(50); // pre-write delay
				ExfoLampPort.WriteLine("ii");
				string str = ExfoLampPort.ReadLine();
				int level = int.Parse(str);
				if(0 <= level && level <= 4)
				{
					return (IntensityLevel)level;
				}
				else
				{
					CheckAnswer(str);
					return IntensityLevel.Zero;
				}
			}
			return IntensityLevel.Zero;

		}
		public void SetIntensityLevel(IntensityLevel level)
		{
			if(Connected)
			{
				int v = (int)level;
				ExfoLampPort.WriteLine("i" + v.ToString());
				string str = ExfoLampPort.ReadLine();
				CheckAnswer(str);
				Throttle(600); // post-write delay
			}
		}
		public void SetExposureTime(int expTime)
		{
			if (Connected)
			{
				int v = (int) ((expTime +50)/ (double) 100);//Add 50 ms extra.
				v = Math.Max(v, 2);//Minimum is 0.2s
				v = Math.Min(9999, v);//Maximum is 999.9s
				string val = v.ToString();
				val = String.Format("{0:0000}", v); //Make 20 to 0020.
				ExfoLampPort.WriteLine("c" + val);
				string str = ExfoLampPort.ReadLine();
				CheckAnswer(str);
			}
		}
		public void RunTimedExposure()
		{
			if (Connected)
			{
				ExfoLampPort.WriteLine("oo");
				string str = ExfoLampPort.ReadLine();
				//Throttle(250); // post-read delay
				CheckAnswer(str);
			}
		}
		public void TurnLampOff()
		{
			if(Connected)
			{
				ExfoLampPort.WriteLine("ss");
				string str = ExfoLampPort.ReadLine();
				//Throttle(250); // post-read delay
				CheckAnswer(str);
			}
		}
		public void TurnLampOn()
		{
			if(Connected)
			{
				ExfoLampPort.WriteLine("bb");
				string str = ExfoLampPort.ReadLine();
				//Throttle(250); // post-read delay
				CheckAnswer(str);
			}
		}


		public int GetLampHours()
		{
			if(Connected)
			{
				Throttle(50); // pre-write delay
				ExfoLampPort.WriteLine("hh");
				string str = ExfoLampPort.ReadLine();
				int hours = int.Parse(str);
				if(0 <= hours && hours <= 9999)
				{
					return hours;
				}
				else
				{
					CheckAnswer(str);
					return 0;
				}
			}
			return 0;

		}
		private UnitStatus getUnitStatus()
		{
			if(Connected)
			{
				Throttle(50); // pre-write delay
				ExfoLampPort.WriteLine("uu");
				string result = ExfoLampPort.ReadLine();
				return (UnitStatus)int.Parse(result);
			}
			return UnitStatus.Alarm;
		}

		public void OpenShutter()
		{
			if(Connected)
			{
				ExfoLampPort.WriteLine("mm");
				string str = ExfoLampPort.ReadLine();
				Throttle(250); // post-read delay
				CheckAnswer(str);
			}
		}
		public void CloseShutter()
		{
			if(Connected)
			{
				ExfoLampPort.WriteLine("zz");
				string str = ExfoLampPort.ReadLine();
				Throttle(250); // post-read delay
				CheckAnswer(str);
			}
		}
		public void LockInstrumentPanel()
		{
			if(Connected)
			{
				ExfoLampPort.WriteLine("ll");
				string str = ExfoLampPort.ReadLine();
				Throttle(1000); // post-read delay
				CheckAnswer(str);
			}
		}
		public void UnlockInstrumentPanel()
		{
			if(Connected)
			{
				Throttle(50); // pre-write delay
				ExfoLampPort.WriteLine("nn");
				string str = ExfoLampPort.ReadLine();
				CheckAnswer(str);
			}

		}

		public void Disconnect()
		{
			if(!Connected) return;
			if(ExfoLampPort != null && ExfoLampPort.IsOpen)
			{
				ExfoLampPort.Close();
			}
			Connected = false;
		}
	}
}