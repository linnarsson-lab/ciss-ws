using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Linnarsson.Utilities;
using System.Threading;

namespace Linnarsson.InstrumentControl
{

	public interface IReadonlyInstrument
	{
		bool Connected { get; }
		HardwareConfiguration HardwareConfig { get;  }
		IReadonlyLamp Lamp { get;  }
		IReadonlyMicroscope Microscope { get;  }
		IReadonlyTemperatureControl Peltier { get;  }
		ZaberActuator CellPicker { get; }
		event EventHandler<EventArgs> InstrumentStatusChanged;
		event EventHandler<EventArgs> ProjectOpenedOrClosed;
	}

	public class Instrument : IReadonlyInstrument
	{
		private static Instrument m_Instance;
		private static Mutex mutex = new Mutex();


		/// <summary>
		/// Provides read-only access to the instrument, which is safe for multithreaded use. 
		/// To obtain threadsafe read/write access, use the Execute() method instead.
		/// </summary>
		public static IReadonlyInstrument Readonly { 
			get 
			{
				if(m_Instance == null) m_Instance = new Instrument();
				return m_Instance; 
			} 
		}

		/// <summary>
		/// Executes a method with exclusive (thread-safe) access to the instrument.
		/// If the instrument is used by another thread, the call returns false immediately
		/// </summary>
		/// <param name="method"></param>
		/// <returns></returns>
		public static bool Execute(Action<Instrument> method)
		{
			return Execute(method, 0);
		}

		/// <summary>
		/// Executes a method with exclusive (thread-safe) access to the instrument.
		/// If the instrument is used by another thread, the call waits up to the given timeout
		/// </summary>
		/// <param name="method"></param>
		/// <param name="timeoutMilliseconds">The length of the timeout, or -1 to wait indefinitely.</param>
		/// <returns>false if the method failed to execute (did not obtain a lock, or instrument not connected)</returns>
		public static bool Execute(Action<Instrument> method, int timeoutMilliseconds)
		{
			if (!m_Instance.Connected) return false;
			if(!mutex.WaitOne(timeoutMilliseconds, false)) return false;
			try
			{
				method(m_Instance);
			}
			catch(Exception exp)
			{
				mutex.ReleaseMutex();
				throw exp;
			}
			return true;
		}

		public static bool Connect()
		{
			if (!mutex.WaitOne(0, false)) return false;
			try
			{
				m_Instance.connect();
			}
			catch (Exception exp)
			{
				mutex.ReleaseMutex();
				throw exp;
			}
			return true;
		}

		public string ConfigDirectory = "C:\\InstrumentData\\Config";

		public NikonMicroscope Microscope { get; set; }
		public ZaberActuator CellPicker { get; set; }
		public ExfoLamp Lamp { get; set; }
		public TemperatureControl Peltier { get; set; }
		public HardwareConfiguration HardwareConfig { get; set; }
		public List<PointF> Locations { get; set; }
		public PickerSettings PickerSettings { get; set; }
		public string CurrentRun { get; set; }
		private bool m_Connected = false;
		public bool Connected { get { return m_Connected; } }

        static Instrument()
		{
			m_Instance = new Instrument();
		}

		private void connect()
		{
			if(Connected) return;
			try
			{
                Console.Write("Connecting to microscope...");
	            Microscope.Connect(HardwareConfig);
                Console.WriteLine(" Ok.");
				Console.Write("Connecting to cell picker...");
				CellPicker.Startup(HardwareConfig.CellPickerPort);
				Console.WriteLine(" Ok.");

				Console.Write("Connecting to Peltier controller...");
				Peltier.Connect(HardwareConfig.PeltierPort);
				Console.WriteLine(" Ok.");
				Console.Write("Connecting to illuminator...");
				Lamp.Connect(HardwareConfig.ExfoLampPort);
				Console.WriteLine(" Ok.");
                Console.WriteLine("Instrument connected.");
            }
			catch (Exception e)
			{
				Lamp.Disconnect();
				Peltier.Disconnect();
				Microscope.Disconnect();
				throw e;
			}
			m_Connected = true;
			Refresh();
		}

		private Instrument()
		{
			Locations = new List<PointF>();
			LoadConfiguration();
			Microscope = new NikonMicroscope();
			Microscope.ObjectiveChanged += new NikonMicroscope.MicroscopeChangedHandler(HandleStatusChanged);
			Microscope.FilterChanged += new NikonMicroscope.MicroscopeChangedHandler(HandleStatusChanged);
			Microscope.OpticalPathChanged += new NikonMicroscope.MicroscopeChangedHandler(HandleStatusChanged);
			Microscope.PfsStatusChanged += new NikonMicroscope.MicroscopeChangedHandler(HandleStatusChanged);
			Microscope.PositionChanged += new NikonMicroscope.MicroscopeChangedHandler(HandleStatusChanged);

			ZaberActuatorConnection zc = new ZaberActuatorConnection();
			CellPicker = new ZaberActuator(zc, 1);

			Peltier = new TemperatureControl(ConfigDirectory);
			Peltier.TemperaturePropertyChanged += new TemperaturePropertyChangedHandler(HandleStatusChanged);
			Lamp = new ExfoLamp();
			Lamp.LampChanged += new LampPropertyChanged(HandleStatusChanged);
		}

		void ProjectManager_ProjectOpened(object sender, EventArgs e)
		{
			ProjectOpenedOrClosed(this, new EventArgs());
		}

		private void HandleStatusChanged()
		{
			InstrumentStatusChanged(this, new EventArgs());
		}

		public void LoadConfiguration()
		{
			if(File.Exists(Path.Combine(ConfigDirectory, "Hardware.xml"))) HardwareConfig = SimpleXmlSerializer.FromXmlFile<HardwareConfiguration>(Path.Combine(ConfigDirectory, "Hardware.xml"));
			else HardwareConfig = new HardwareConfiguration();

			if(File.Exists(Path.Combine(ConfigDirectory, "PickerSettings.txt"))) PickerSettings = PickerSettings.Load(Path.Combine(ConfigDirectory, "PickerSettings.txt"));
			else PickerSettings = new PickerSettings();

        }

		public void SaveConfiguration()
		{
			// Save the optical and camera settings
			SimpleXmlSerializer.ToXmlFile(Path.Combine(ConfigDirectory, "Hardware.xml"), HardwareConfig);
			PickerSettings.Save(Path.Combine(ConfigDirectory, "PickerSettings.txt"));
        }


		DateTime lastRefresh = DateTime.MinValue;
		public void Refresh()
		{
			// Throttle refreshes to max two per second
			if(Connected && (DateTime.Now - lastRefresh) > new TimeSpan(0,0,0,0,500))
			{
				lastRefresh = DateTime.Now;
				lock(this)
				{
					CellPicker.Refresh();
					Microscope.Refresh();
					Peltier.Refresh();
					Lamp.Refresh();
				}
			}
		}



		~Instrument()
		{
			Disconnect();
		}

		public void Disconnect()
		{
			if(!Connected) return;
            Console.Write("Disconnecting...");
			Lamp.Disconnect();
			Microscope.Disconnect();
			Peltier.Disconnect();
			m_Connected = false;
            Console.WriteLine(" Ok.");
		}


		#region IReadonlyInstrument Members

		IReadonlyLamp IReadonlyInstrument.Lamp
		{
			get { return Lamp; }
		}

		IReadonlyMicroscope IReadonlyInstrument.Microscope
		{
			get { return Microscope; }
		}

		IReadonlyTemperatureControl IReadonlyInstrument.Peltier
		{
			get { return Peltier; }
		}

		#endregion

		#region IReadonlyInstrument Members


		public event EventHandler<EventArgs> InstrumentStatusChanged = delegate { };
		public event EventHandler<EventArgs> ProjectOpenedOrClosed = delegate { };
		#endregion



	}
}
