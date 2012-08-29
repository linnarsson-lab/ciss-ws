using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.IO;
using System.Threading;
using Linnarsson.Utilities;

namespace Linnarsson.InstrumentControl
{
	public delegate void TemperaturePropertyChangedHandler();
	public enum FanMode { Off, On, OnWhenCooling, OnWhenHeating, OnWhenCoolingHeating, FanRegulator }

	public interface IReadonlyTemperatureControl
	{
		TemperatureControlConfig Config { get; }
		bool Connected { get; }
		double D { get; }
		double I { get; }
		double MeasuredTemperature { get; }
		double P { get; }
		double Power { get; }
		bool Running { get; }
		double SetPoint { get; }
		event TemperaturePropertyChangedHandler TemperaturePropertyChanged;
	}

	[Serializable]
	public class TemperatureControl : IReadonlyTemperatureControl
	{
		private SerialPort TemperatureControlPort;
		public event TemperaturePropertyChangedHandler TemperaturePropertyChanged;

		private double m_SetPoint;
		public double SetPoint
		{
			get { return m_SetPoint; }
			set { Set("0", Math.Max(-10, Math.Min(75, value)).ToString()); m_SetPoint = Math.Max(-10, Math.Min(75, value)); }
		}
		private double m_Power;
		public double Power
		{
			get { return m_Power; }
		}
		private double m_P;
		public double P
		{
			get { return m_P;  }
			set { Set("1", value.ToString()); m_P = value; }
		}

		private double m_I;
		public double I
		{
			get { return m_I; }
			set { Set("2", value.ToString()); m_I = value; }
		}
		private double m_D;
		public double D
		{
			get { return m_D; }
			set { Set("3", value.ToString()); m_D = value; }
		}

		private double m_MeasuredTemperature;
		public double MeasuredTemperature
		{
			get {return m_MeasuredTemperature; }
		}


		private bool m_Running = false;
		public bool Running
		{
			get { return m_Running; }
		}

		public string Port { get; set; }

		public void SetFanMode(FanMode mode)
		{
			Set("23", ((int)mode).ToString());
		}
		public bool Connected { get; set; }

        private TemperatureControlConfig m_Config;
        public TemperatureControlConfig Config
        {
            get { return m_Config; }
            set { m_Config = value; }
        }
	
		public double CoolingILimit(double t)
		{
			return Math.Min(100, Math.Max(Config.CoolingMinILimit, Math.Abs(Config.CoolingILimitFactor * Config.CoolingILimit[t])));
		}
		public double CoolingILimit(double t, double coolingILimitFactor)
		{
			return Math.Min(100, Math.Max(Config.CoolingMinILimit, Math.Abs(coolingILimitFactor * Config.CoolingILimit[t])));
		}

		public double HeatingILimit(double t)
		{
            return Math.Min(100, Math.Max(Config.HeatingMinILimit, Math.Abs(Config.HeatingILimitFactor * Config.HeatingILimit[t])));
		}
		public double HeatingILimit(double t, double heatingILimitFactor)
		{
			return Math.Min(100, Math.Max(Config.HeatingMinILimit, Math.Abs(heatingILimitFactor * Config.HeatingILimit[t])));
		}
	
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="port"></param>
        public TemperatureControl(string configDir)
		{
			if(File.Exists(Path.Combine(configDir, "Peltier.xml"))) m_Config = SimpleXmlSerializer.FromXmlFile<TemperatureControlConfig>(Path.Combine(configDir, "Peltier.xml"));
			else m_Config = new TemperatureControlConfig();

		}
		public TemperatureControl()
		{
            this.m_Config = new TemperatureControlConfig();
		}

		public void Connect(string port)
		{
			try
			{
				initialize(port);
			}
			catch(Exception e)
			{
				if(Connected)
				{
					TemperatureControlPort.Close();
					Connected = false;
				}
				throw new IOException("Unable to connect to Peltier controller: " + e.Message, e);
			}
		}

		private void initialize(string port)
		{
			this.Port = port;
			TemperatureControlPort = new System.IO.Ports.SerialPort(Port, 115200, Parity.None, 8, StopBits.One);
			TemperatureControlPort.ReadTimeout = 1000;
			TemperatureControlPort.Handshake = Handshake.None;
			TemperatureControlPort.Open();
			Connected = true;
			this.Stop();
			Set("6", "100"); //Limits the output since the controll/power supply doesn't seem to manage 100%.
			//Steinhart-Hart coeff
			//Set("59", "1.129241e-3"); //Coeff A
			//Set("60", "2.341077e-4"); //Coeff B
			//Set("61", "8.775468e-8"); //Coeff C
			Set("59", Config.SteinhartHartCoefficients[0].ToString("#.######e0")); //Coeff A
			Set("60", Config.SteinhartHartCoefficients[1].ToString("#.######e0")); //Coeff B
			Set("61", Config.SteinhartHartCoefficients[2].ToString("#.######e0")); //Coeff C

			Set("16", "0");//Fan1 Does not exist. Always off.
			Set("23", "2");//Fan2 ON when cooling.
			Set("13", "6");//Turns controller into PID mode.
			Set("7", Config.DeadBand.ToString());//Set deadband to +/- 3% - default
			//Set("11", "0.7");//Sets heating gain to 0.7 for supercool
			Set("10", "1.0");

			Set("11", "1.0");//Sets heating gain to 0.7 for supercool

			// Set alarm values (which depend on the module power etc.)
			Set("47", Config.MaxCurrent.ToString());
		}
		public void Stop()
		{
			string str;
			TemperatureControlPort.Write("$Q\r\n");
			str = TemperatureControlPort.ReadLine();//Reads echo without ">" on first command
			str = TemperatureControlPort.ReadLine();//Reads "Stop"
			m_Running = false;
		}
		public void Run()
		{
			string str;
			TemperatureControlPort.Write("$W\r\n");
			str=TemperatureControlPort.ReadLine();//Reads echo;
			str=TemperatureControlPort.ReadLine();//Reads "Run"
			m_Running = true;
		}

		public void Refresh()
		{
            if (TemperatureControlPort == null || !TemperatureControlPort.IsOpen) return;
			double x = m_SetPoint;
			bool changed = false;

			try
			{
				m_SetPoint = Convert.ToDouble(Get("0"));
				if(x != m_SetPoint) changed = true;

				x = m_P;
				m_P = Convert.ToDouble(Get("1"));
				if(x != m_P) changed = true;

				x = m_I;
				m_I = Convert.ToDouble(Get("2"));
				if(x != m_I) changed = true;

				x = m_D;
				m_D = Convert.ToDouble(Get("3"));
				if(x != m_D) changed = true;

				x = m_Power;
				m_Power = Convert.ToDouble(Get("106"));
				if(x != m_Power) changed = true;

				x = m_MeasuredTemperature;
				m_MeasuredTemperature = Convert.ToDouble(Get("100"));
				if(x != m_MeasuredTemperature) changed = true;

				// trigger the event
				if(changed && TemperaturePropertyChanged != null) TemperaturePropertyChanged();
			}
			catch(FormatException) // Sometimes the supercool controller returns funky numbers, like "2e+2.123e+1"
			{
			}
		}


		private int maxRestarts = 100;
		private void Restart()
		{
			// This shouldn't happen except if there's an infinite loop somewhere or a very persistent error
			if (maxRestarts-- <= 0) throw new IOException("Failed repeatedly to restart the temp controller");
			if (TemperatureControlPort != null && TemperatureControlPort.IsOpen)
			{
				TemperatureControlPort.Close();
			}
			Thread.Sleep(1000);
			if(!m_Running)
			{
				Connect(Port);
				Stop();
			}
			else
			{
				Connect(Port);
				Run();
			}
		}

		public void SetMaxPower(int power)
		{
			if(power < 0 || power > 100) return;
			Set("6", power.ToString());
		}

		public void Set(string register, string value)
		{
			try
			{
				DoSet(register, value);
			}
			catch (Exception e)
			{
				Restart();
				Console.WriteLine("Peltier error: " + e.Message);
				DoSet(register, value);
			}
		}

		private void DoSet(string register, string value)
		{
			string sendCommand = "$R" + register + "=" + value + "\r\n";
			//Console.WriteLine(sendCommand);
			TemperatureControlPort.Write(sendCommand);
			string echo = TemperatureControlPort.ReadLine();
			if(!(echo+"\n" == "> " + sendCommand))
			{
				throw new Exception("Serial port communication error with temperature controller!");
			}
			string answer = TemperatureControlPort.ReadLine();
			if(answer.StartsWith("?"))
			{
				throw new Exception("Bad syntax in communication with temperature controller!");
			}

		}
		public string Get(string register)
		{
			try
			{
				return DoGet(register);
			}
			catch (Exception e)
			{
				Restart();
				Console.WriteLine("Peltier error: " + e.Message);
				return DoGet(register);
			}
		}
		public string DoGet(string register)
		{
			string sendCommand = "$R" + register + "?\r\n";
			//Console.WriteLine(sendCommand);
			string echo = "";
			int i = 0;
			TemperatureControlPort.Write(sendCommand);
			while(i < 10)
			{
				echo = TemperatureControlPort.ReadLine();
				if(!(echo + "\n" == "> " + sendCommand))
				{
					i = i+1;
					//echo = TemperatureControlPort.ReadLine();
					if (i==9)
					{
						throw new Exception("Serial port communication error with temperature controller!");
					}
				}
				else
				{
					i=10;
				}
			}
			string answer = TemperatureControlPort.ReadLine();
			if(answer.StartsWith("?"))
			{
				throw new Exception("Bad syntax in communication with temperature controller!");
			}
			//Console.WriteLine(answer);
			return answer.Replace("\r","");
		}
		///// <summary>
		///// General send commands for un-implemented parameters. Returns an empty string if a set command is issued;
		///// </summary>
		///// <param name="command"></param>
		///// <returns></returns>
		//public string Send(String command)
		//{
		//    string sendCommand = command + "\r\n";
		//    TemperatureControlPort.Write(sendCommand);
		//    // Checks that the echo is correct
		//    String echo = TemperatureControlPort.ReadLine();
		//    if (!(echo == "> " + command+"\r"))
		//    {
		//        throw new Exception("Serial port communication error with temperature controller!");
		//    }
		//    string answer = TemperatureControlPort.ReadLine();
		//    if(answer.StartsWith("?"))
		//    {
		//        throw new Exception("Bad syntax in communication with temperature controller!");
		//    }
		//    return answer.Replace("\r", "");
		//}
			
		~TemperatureControl()
		{
			//For some reason the serialport has been closed before the destructor of
			//TemperatureControl. This means that it has to be opened in order to stop
			//the controller before termination of the application. Ugly but necessary.
			//  /Ellef
			//if(TemperatureControlPort != null)
			//{
			//    if(!TemperatureControlPort.IsOpen)
			//    {
			//        TemperatureControlPort.Open();
			//    }
			//    Stop();//If not stopped the controller will continue.
			//    TemperatureControlPort.Close();
			//    System.Windows.Forms.MessageBox.Show("OK!");
			//}
		}

		public double GetMeasuredTemperature()
		{
			try
			{
				m_MeasuredTemperature = Convert.ToDouble(Get("100"));
			}
			catch(FormatException fe) // Sometimes the supercool controller returns funky numbers, like "2e+2.123e+1"
			{
				Console.WriteLine("Peltier temperature format error: " + fe);
			}

			return m_MeasuredTemperature;
		}

		public void Disconnect()
		{
			if(!Connected) return;
			if(TemperatureControlPort != null && TemperatureControlPort.IsOpen)
			{
				TemperatureControlPort.Close();
			}
			Connected = false;
		}

	}
}
