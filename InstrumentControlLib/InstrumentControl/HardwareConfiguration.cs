using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.InstrumentControl
{
    public class HardwareConfiguration
    {
		private bool m_HasCamera;
		public bool HasCamera
		{
			get { return m_HasCamera; }
			set { m_HasCamera = value; }
		}
	
		private bool m_HasPfs;
		public bool HasPfs
		{
			get { return m_HasPfs; }
			set { m_HasPfs = value; }
		}
	
        private string m_MicroscopePort;
        public string MicroscopePort
        {
            get { return m_MicroscopePort; }
            set { m_MicroscopePort = value; }
        }

        private string m_MarzhauserPort;
        public string MarzhauserPort
        {
            get { return m_MarzhauserPort; }
            set { m_MarzhauserPort = value; }
        }

		private int m_MarzhauserBaudrate;
		public int MarzhauserBaudrate
		{
			get { return m_MarzhauserBaudrate; }
			set { m_MarzhauserBaudrate = value; }
		}
	
        private string m_AutosamplerPort;
        public string AutosamplerPort
        {
            get { return m_AutosamplerPort; }
            set { m_AutosamplerPort = value; }
        }

        private string m_PeltierPort;
        public string PeltierPort
        {
            get { return m_PeltierPort; }
            set { m_PeltierPort = value; }
        }

		private string m_CellPickerPort;
		public string CellPickerPort
		{
			get { return m_CellPickerPort; }
			set { m_CellPickerPort = value; }
		}
		
		private string m_ExfoLampPort;
        public string ExfoLampPort
        {
            get { return m_ExfoLampPort; }
            set { m_ExfoLampPort = value; }
        }

		private PickerSettings m_PickerSettings;
		public PickerSettings PickerSettings
		{
			get { return m_PickerSettings; }
			set { m_PickerSettings = value; }
		}
		
    }
}
