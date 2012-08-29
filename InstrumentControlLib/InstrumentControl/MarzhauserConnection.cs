#region Using directives

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.IO.Ports;
using System.IO;
using System.Threading;

#endregion

namespace Linnarsson.InstrumentControl
{
	
	[System.Serializable]
	public class MarzhauserException : System.ApplicationException
	{
		//
		// For guidelines regarding the creationg of new exception types, see
		//    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
		// and
		//    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
		//

		public MarzhauserException() { }
		public MarzhauserException(string message) : base( message ) { }
		public MarzhauserException(string message, System.Exception inner) : base( message, inner ) { }
		public MarzhauserException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base( info, context ) { }
	}
	[Serializable]
	internal class MarzhauserConnection
	{
		private SerialPort port;

		private double m_X;
		/// <summary>
		/// Get the X position in millimeters from the stage home
		/// </summary>
		/// <value></value>
		public double X
		{
			get { return m_X; }
		}

		private double m_Y;
		/// <summary>
		/// Get the Y position in millimeters from the stage home
		/// </summary>
		/// <value></value>
		public double Y
		{
			get { return m_Y; }
		}

		private double m_Velocity = 100;
		/// <summary>
		/// Sets the velocity in mm/s. Range: 0.000 015 26 mm/s (=15.26 nm/s) - 180 mm/s.
		/// </summary>
		/// <value></value>
		public double Velocity
		{
			get { return m_Velocity; }
			set {
				port.Write(value.ToString() + " sv ");
				m_Velocity = value; 
			}
		}

		private double m_Acceleration;
		/// <summary>
		/// Sets the acceleration in mm/s^2. Range: 0 - 2400 mm/s^2.
		/// </summary>
		/// <value></value>
		public double Acceleration
		{
			get { return m_Acceleration; }
			set {
				port.Write(value.ToString() + " setaccel ");
				m_Acceleration = value;
			}
		}

		private double m_JoystickVelocity;
		public double JoystickVelocity
		{
			get { return m_JoystickVelocity; }
			set {
				port.Write(value.ToString() + " setjoyspeed ");
				m_JoystickVelocity = value; 
			}
		}

		private double m_JoystickHighVelocity;
		public double JoystickHighVelocity
		{
			get { return m_JoystickHighVelocity; }
			set
			{
				port.Write(value.ToString() + " setjoybspeed ");
				m_JoystickHighVelocity = value;
			}
		}

		private double m_JoystickAcceleration;
		public double JoystickAcceleration
		{
			get { return m_JoystickAcceleration; }
			set {
				port.Write(value.ToString() + " setmanaccel ");
				m_JoystickAcceleration = value;
			}
		}

        private string m_StageIdentity;
        public string StageIdentity
        {
            get { return m_StageIdentity; }
            set { m_StageIdentity = value; }
        }
	

		public MarzhauserConnection(string portName, int baudrate)
		{
			port = new SerialPort(portName, baudrate, Parity.None, 8, StopBits.One);
			port.Handshake = Handshake.XOnXOff;
		}

        private bool CheckError(int expected)
        {
            port.Write("ge ");
            return int.Parse(port.ReadLine().Trim()) == expected;
        }
        private bool CheckError() { return CheckError(0); }

        private bool CheckStatus(int expected)
        {
            port.Write("st ");
            return int.Parse(port.ReadLine().Trim()) == expected;
        }
        private bool CheckStatus() { return CheckStatus(0); }

        private void BlockingStatusCheck()
        {
            port.Write("0 0 r ");
            if(!CheckError()) throw new IOException("Communication error with motorized stage!");
            if(!CheckStatus()) throw new IOException("Communication error with motorized stage!");
            port.Write("p ");
            port.ReadLine();
        }

		public void Open()
		{
			port.Open();
			port.NewLine = "\n"; // used for receiving commands
		}
        
        public void Reset()
        {
            port.Write("reset ");
            Thread.Sleep(5000);
            port.Write("getlimit ");
            port.ReadLine();
            port.ReadLine();

			// Set the pitch correctly
			port.Write("1 1 setpitch ");
			port.Write("1 2 setpitch ");

            // Set up some sensible defaults
			Velocity = 25;
            Acceleration = 100;
			JoystickVelocity = 10;
            JoystickHighVelocity = 25;
            JoystickAcceleration = 100;

            // Calibrate
            port.Write("cal "); // this will move to the home positions and set the zero value for each axis
            BlockingStatusCheck();
            port.Write("rm ");  // this will move to the max positons and set the max value of each axis
            BlockingStatusCheck();

            // Go to the center position and enable the joystick
            port.Write("60 50 m ");
            BlockingStatusCheck();
            EnableJoystick();
            BlockingStatusCheck();
        }

		/// <summary>
		/// Moves to an absolute position given in Units. Range is 0 - 16383 mm. 
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public void Goto(double x, double y)
		{
			port.Write(String.Format("{0} {1} move ", x, y));
			// Enable the joystick
			port.Write("1 j ");
			m_X = x;
			m_Y = y;
		}

		/// <summary>
		/// Moves relative to the current position.
		/// </summary>
		/// <param name="x">The distance in mm.</param>
		/// <param name="y">The distance in mm.</param>
		public void Move(double x, double y)
		{
			port.Write(String.Format("{0} {1} rmove ", x, y));
			// Enable the joystick
			port.Write("1 j ");
			m_X += x;
			m_Y += y;
		}

		/// <summary>
		/// Wait for a move to be completed.
		/// </summary>
		public void Wait()
		{
            BlockingStatusCheck();
		}

		/// <summary>
		/// Refreshes only the position, not any of the unit settings etc.
		/// </summary>
		public void Refresh()
		{
			port.Write("p ");
			string[] reply = port.ReadLine().Trim().Split(' ');
			if (reply.Length == 3)
			{
				m_X = double.Parse(reply[0]);
				m_Y = double.Parse(reply[2]);
			}
			else if (reply.Length == 4) // Strangely, this sometimes happens
			{
				m_X = double.Parse(reply[0]);
				m_Y = double.Parse(reply[3]);
			}
		}

		internal void EnableJoystick()
		{
			port.Write("1 j ");
		}

		public void Close()
		{
			port.Close();
		}

		public void Delay(int delay)
		{
			port.Write(String.Format("{0} 0 wt ", delay*4));
		}
	}
}
