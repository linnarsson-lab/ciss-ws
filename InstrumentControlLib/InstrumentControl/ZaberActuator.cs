using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using Linnarsson.Utilities;
using System.Threading;

namespace Linnarsson.InstrumentControl
{
	public class ZaberDeviceChangedEventArgs : EventArgs
	{
		public ZaberCommand Command { get; set; }
		public int Data { get; set; }
		public int Device { get; set; }
	}

	public interface IReadonlyZaberActuator
	{
	    int Position { get; }
	}

	public enum ZaberCommand
	{
		Reset = 0,
		Home = 1,
		Renumber = 2,
		ConstantSpeedTracking = 8,
		LimitActive = 9,
		ManualMoveTracking = 10,
		StoreCurrentPosition = 16,
		ReturnStoredPosition = 17,
		MoveToStoredPosition = 18,
		MoveAbsolute = 20,
		MoveRelative = 21,
		MoveAtConstantSpeed = 22,
		Stop = 23,
		ReadOrWriteMemory = 35,
		RestoreSettings = 36,
		SetMicrostepResolution = 37,
		SetRunningCurrent = 38,
		SetHoldCurrent = 39,
		SetDeviceMode = 40,
		SetTargetSpeed = 42,
		SetAcceleration = 43,
		SetMaximumRange = 44,
		SetCurrentPosition = 45,
		SetMaximumRelativeMove = 46,
		SetHomeOffset = 47,
		SetAliasNumber = 48,
		SetLockState = 49,
		ReturnDeviceId = 50,
		ReturnFirmwareVersion = 51,
		ReturnPowerSupplyVoltage = 52,
		ReturnSetting = 53,
		ReturnStatus = 54,
		EchoData = 55,
		ReturnCurrentPosition = 60,
		Error = 255
	}

	public class ZaberActuatorConnection
	{
		SerialPort port;
		Dictionary<int, ZaberActuator> devices = new Dictionary<int, ZaberActuator>();

		public ZaberActuatorConnection()
		{}
		
		public void Connect(string portName, int device, ZaberActuator handler)
		{
			devices[device] = handler;
			if(port != null && port.IsOpen) return;

			port = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
			port.Handshake = Handshake.None;
			port.ReadTimeout = 1000;
			port.Open();

			// Renumber all devices
			Write(0, ZaberCommand.Renumber, 0);

			// Set up the read loop
			Background.RunAsync(ReadLoop);
		}

		public void ReadLoop()
		{
			byte[] buffer = new byte[6];
			while (true)
			{
				buffer[0] = 0;
				buffer[1] = 0;
				buffer[2] = 0;
				buffer[3] = 0;
				buffer[4] = 0;
				buffer[5] = 0;

				try
				{
					if (port.BytesToRead > 0) port.Read(buffer, 0, Math.Min(6, port.BytesToRead));
					else
					{
						Thread.Sleep(100);
						continue;
					}
				}
				catch (TimeoutException)
				{
					continue;
				}
				int reply;
				reply = (buffer[5] << 24) | (buffer[4] << 16) | (buffer[3] << 8) | buffer[2];
				if (buffer[1] == (int)ZaberCommand.Error)
				{
					Console.WriteLine("CellPicker error: " + reply);
				}
				if (devices.ContainsKey(buffer[0]))
				{
					devices[buffer[0]].NotifyDeviceChanged(
						new ZaberDeviceChangedEventArgs
						{
							Command = (ZaberCommand)buffer[1],
							Data = reply,
							Device = buffer[0]
						});
				}
			}
		}
		private DateTime lastWrite = DateTime.MinValue;
		/// <summary>
		/// 
		/// </summary>
		/// <param name="device"></param>
		/// <param name="cmd"></param>
		/// <param name="data"></param>
		/// <param name="reply"></param>
		/// <returns>false if the command failed</returns>
		public void Write(int device, ZaberCommand cmd, int data)
		{
			if ((DateTime.Now - lastWrite).TotalMilliseconds < 100) Thread.Sleep(100);
			lastWrite = DateTime.Now;

			byte[] buffer = new byte[6];
			buffer[5] = (byte)((data & (0xFF << 24)) >> 24);
			buffer[4] = (byte)((data & (0xFF << 16)) >> 16);
			buffer[3] = (byte)((data & (0xFF << 8)) >> 8);
			buffer[2] = (byte)(data & 0xFF);
			buffer[1] = (byte)cmd;
			buffer[0] = (byte)device;

			port.Write(buffer, 0, 6);
		}
	}

	public class ZaberActuator : IReadonlyZaberActuator
	{
		private int DeviceNumber { get; set; }
		private ZaberActuatorConnection Connection { get; set; }
		private int position;
		public event EventHandler<ZaberDeviceChangedEventArgs> DeviceChanged = delegate { };

		public ZaberActuator(ZaberActuatorConnection connection, int device)
		{
			DeviceNumber = device;
			Connection = connection;

		}



		public void Move(int dz)
		{
			Connection.Write(DeviceNumber, ZaberCommand.MoveRelative, dz);
			GetPosition();
		}

		public void Goto(int z)
		{
			Connection.Write(DeviceNumber, ZaberCommand.MoveAbsolute, z);
			GetPosition();
		}

		public void Wait(int z, int dz)
		{
			while (Math.Abs(Position - z) > dz)
			{
				GetPosition();
				Thread.Sleep(500);
			}
		}

		public void GetPosition()
		{
			Connection.Write(DeviceNumber, ZaberCommand.ReturnCurrentPosition, 0);
		}

		public void Refresh()
		{
			GetPosition();
		}

		public void Startup(string portName)
		{
			Connection.Connect(portName, DeviceNumber, this);
			// Set max speed (4 mm/s)
			Connection.Write(DeviceNumber, ZaberCommand.SetTargetSpeed, 4000);
			Thread.Sleep(1000);
		}

		public int Position
		{
			get { return position; }
		}


		public void SetPosition(int p)
		{
			Connection.Write(DeviceNumber, ZaberCommand.SetCurrentPosition, p);
		}

		internal void NotifyDeviceChanged(ZaberDeviceChangedEventArgs e)
		{

			if (e.Command == ZaberCommand.Home || e.Command == ZaberCommand.SetCurrentPosition || e.Command == ZaberCommand.ReturnCurrentPosition)
			{
				position = e.Data;
			}

			DeviceChanged(this, e);
		}

		public void Home()
		{
			Connection.Write(DeviceNumber, ZaberCommand.Home, 0);
		}
	}
}
