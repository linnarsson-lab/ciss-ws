#region Using directives

using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.IO;

#endregion

namespace Linnarsson.InstrumentControl
{
	[System.Serializable]
	public class NikonException : System.ApplicationException
	{
		public NikonException() { }
		public NikonException(string message) : base( message ) { }
		public NikonException(string message, System.Exception inner) : base( message, inner ) { }
		public NikonException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base( info, context ) { }
	}

	public enum NikonCommandState { BeforeSending, WaitingForQ, WaitingForO, WaitingForA, Completed }
	public enum OpticalPath { Eye = 1, Eye20RightPort80 = 2, BottomPort = 3, Eye20FrontPort80 = 4, LeftPort = 5 }
	public enum FocusResolution { Coarse, Middle, Fine }
	public enum MotionSpeed { Slow, Medium, Fast }
	public enum PfsStatus { NotInstalled = -2, Unknown = -1, Waiting = 1, LEDOver = 2, LEDUnder = 3, Running = 4, Search = 5, Search2 = 10, JP = 50, Disabled = 90 }
	public enum PfsMode { On = 1, Wait = 2, Search = 3 }

	[Serializable]
	internal class NikonCommand
	{
		private NikonCommandState m_State;
		public NikonCommandState State
		{
			get { return m_State; }
			set { m_State = value; }
		}

		private char m_Command;
		public char Command
		{
			get { return m_Command; }
			set { m_Command = value; }
		}

		private string m_OperationName;
		public string OperationName
		{
			get { return m_OperationName; }
			set { m_OperationName = value; }
		}

		private string m_AdditionalData;
		public string AdditionalData
		{
			get { return m_AdditionalData; }
			set { m_AdditionalData = value; }
		}

		private string m_ReturnedData;
		public string ReturnedData
		{
			get { return m_ReturnedData; }
			set { m_ReturnedData = value; }
		}

        private List<string> extendedCommands = new List<string>(new string[] {
                "OLA",
                "OLB",
                "OLD",
                "OLI",
                "OLG",
                "OLR",
                "OLE",
                "OLH",
                "OLC",
                "OLF",
                "AFG",
                "AFR",
                "FES",
                "FER",
                "MER",
                "ILM",
                "ILR",
                "WES",
                "WER",
                "MTS",
                "MTR",
                "OBJ",
                "OBR",
                "VEN",
                "SUM",
                "PNM"
            });
		public NikonCommand(char cmd, string opName) : this(cmd, opName, "") { }
		public NikonCommand(char cmd, string opName, string data)
		{
			Command = cmd;
			OperationName = opName;
			AdditionalData = data;
			m_State = NikonCommandState.BeforeSending;
		}

		public override string ToString()
		{
            string prefix = Command.ToString();
            if (extendedCommands.Contains(OperationName)) prefix = "cTRN1" + Command.ToString();
			return prefix + OperationName + AdditionalData + "\r";
		}

		public void NotifySent()
		{
			switch(Command)
			{
				case 'f':
					State = NikonCommandState.WaitingForQ;
					break;
				case 'c':
					State = NikonCommandState.WaitingForO;
					break;
				case 'r':
					State = NikonCommandState.WaitingForA;
					break;
			}
		}

		public void HandleResult(string result)
		{
            if (extendedCommands.Contains(OperationName))
            {
                if (!result.StartsWith("oTRN1")) throw new IOException("Unexpected reply from microscope: " + result);
                result = result.Substring(5);
            }
            if (result[0] == 'n')
			{
				State = NikonCommandState.Completed;
				throw new NikonException("Instrument error: " + result);
			}
			switch(State)
			{
				case NikonCommandState.WaitingForA:
					if(result[0] != 'a') throw new NikonException("Expected return code 'a'.");
					ReturnedData = result.Substring(4).TrimEnd();
					State = NikonCommandState.Completed;
					break;
				case NikonCommandState.WaitingForO:
					if(result[0] != 'o') throw new NikonException("Expected return code 'o'.");
					State = NikonCommandState.Completed;
					break;
				case NikonCommandState.WaitingForQ:
					if(result[0] != 'q') throw new NikonException("Expected return code 'q'.");
					State = NikonCommandState.WaitingForO;
					break;
			}
		}
	}

	/// <summary>
	/// Represents a low-level interface to the Nikon control box. The methods are synchronous and not
	/// thread-safe, due to the serial nature of the underlying RS232 protocol. All method calls will block
	/// until the operation is completed (or an error occurs). This class implements only the functions
	/// relevant to the Nikon motorized microscope acquired Spring 2005 at Global Genomics.
	/// </summary>
	[Serializable]
	internal class NikonConnection
	{
		private SerialPort port;
		private char stdCommandType = 'c';
		private static StringBuilder history = new StringBuilder();


		public NikonConnection(string portName, bool verifyCommands)
		{
			port = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One);
			port.ReadTimeout = 500;
			if(verifyCommands) stdCommandType = 'f';
			else stdCommandType = 'c';
		}

		public void Open()
		{
			port.Open();
			port.NewLine = "\r\n";
		}

		/// <summary>
		/// Sends a NikonCommand as a synchronous operation. The method will block until the operation is completed.
		/// </summary>
		/// <param name="cmd"></param>
		public void SendCommand(NikonCommand cmd)
		{
			//history.Append(cmd.ToString());
			port.Write(cmd.ToString());
			cmd.NotifySent();
			try
			{
				while (!(cmd.State == NikonCommandState.Completed))
				{
					string result = port.ReadLine();
					cmd.HandleResult(result);
				}
			}
			catch (TimeoutException te)
			{
				Console.WriteLine("Timeout error talking to microscope: " + te.Message);
			}
		}

		public void NextObjective(bool linkCondenser)
		{
			NikonCommand cmd = new NikonCommand(stdCommandType, "RCW", linkCondenser ? "1" : "0");
			SendCommand(cmd);
		}

		public void PreviousObjective(bool linkCondenser)
		{
			NikonCommand cmd = new NikonCommand(stdCommandType, "RCC", linkCondenser ? "1" : "0");
			SendCommand(cmd);
		}

		public void SelectObjective(int position, bool linkCondenser)
		{
			string data = linkCondenser ? "1" : "0";
			if(position >= 0 && position < 6) data += (position + 1).ToString();
			else throw new InvalidOperationException("Objective must be 0-5");

			NikonCommand cmd = new NikonCommand(stdCommandType, "RDM", data);
			SendCommand(cmd);
		}

		public int GetCurrentObjective()
		{
			NikonCommand cmd = new NikonCommand('r', "RAR");
			SendCommand(cmd);
			return int.Parse(cmd.ReturnedData) - 1;
		}

        public void PfsControl(PfsMode mode)
        {
            NikonCommand cmd = new NikonCommand('c', "AFG", ((int)mode).ToString());
            SendCommand(cmd);

        }

		public PfsStatus GetPfsStatus()
		{
            NikonCommand cmd = new NikonCommand('r', "AFR");
            SendCommand(cmd);
            return (PfsStatus)(int.Parse(cmd.ReturnedData));
		}

        public void MoveOffsetLens(int absPosition)
        {
            NikonCommand cmd = new NikonCommand(stdCommandType, "OLA", absPosition.ToString());
            SendCommand(cmd);
        }
        public void MoveOffsetLensRelative(int relPosition)
        {
            NikonCommand cmd = new NikonCommand(stdCommandType, "OLB", relPosition.ToString());
            SendCommand(cmd);
        }
        public void MoveOffsetLensToSurface()
        {
            NikonCommand cmd = new NikonCommand(stdCommandType, "OLD");
            SendCommand(cmd);
        }

        public void MoveOffsetLensToInitialPosition()
        {
            NikonCommand cmd = new NikonCommand(stdCommandType, "OLI");
            SendCommand(cmd);
        }       
        
        public int GetSurfacePosition()
        {
            NikonCommand cmd = new NikonCommand('r', "OLE");
            SendCommand(cmd);
            return int.Parse(cmd.ReturnedData);
        }
        public int GetOffsetLensPosition()
        {
            NikonCommand cmd = new NikonCommand('r', "OLR");
            SendCommand(cmd);
            return int.Parse(cmd.ReturnedData);
        }

		public void SetOpticalPath(OpticalPath position)
		{
			int pos = (int)position;
			if(pos < 1 || pos > 5) throw new InvalidOperationException("Invalid optical path");
			NikonCommand cmd = new NikonCommand(stdCommandType, "PDM", pos.ToString());
			SendCommand(cmd);
		}
		public OpticalPath GetOpticalPath()
		{
			NikonCommand cmd = new NikonCommand('r', "PAR");
			SendCommand(cmd);
			return (OpticalPath)(int.Parse(cmd.ReturnedData));
		}

		//public void InsertBottomPrism()
		//{
		//    NikonCommand cmd = new NikonCommand(stdCommandType, "PPS", "1");
		//    SendCommand(cmd);
		//}
		//public void RemoveBottomPrism()
		//{
		//    NikonCommand cmd = new NikonCommand(stdCommandType, "PPS", "0");
		//    SendCommand(cmd);
		//}
		//public bool IsBottomPrismInserted()
		//{
		//    NikonCommand cmd = new NikonCommand('r', "PPR");
		//    SendCommand(cmd);
		//    return int.Parse(cmd.ReturnedData) == 1;
		//}
		public void NextFilterBlock()
		{
			NikonCommand cmd = new NikonCommand(stdCommandType, "HCW", "0");
			SendCommand(cmd);
		}
		public void PreviousFilterBlock()
		{
			NikonCommand cmd = new NikonCommand(stdCommandType, "HCC", "0");
			SendCommand(cmd);
		}
		public void SelectFilterBlock(int position)
		{
			string data;
			if(position >= 0 && position < 6) data = "1" + (position + 1).ToString();
			else throw new InvalidOperationException("Filter must be 0-5");

			NikonCommand cmd = new NikonCommand(stdCommandType, "HDM", data);
			SendCommand(cmd);
		}
		public int GetCurrentFilterBlock()
		{
			NikonCommand cmd = new NikonCommand('r', "HAR");
			SendCommand(cmd);
			return int.Parse(cmd.ReturnedData) - 1;
		}

		public void IncreaseFocus(int count)
		{
			NikonCommand cmd = new NikonCommand(stdCommandType, "SUC", count.ToString());
			SendCommand(cmd);
		}
		public void DecreaseFocus(int count)
		{
			NikonCommand cmd = new NikonCommand(stdCommandType, "SDC", count.ToString());
			SendCommand(cmd);
		}

		public void SetFocus(int count)
		{
			if(count < 0) count = 0;
			if(count > 200000) throw new NikonException("Attempting to move Z axis beyond 200000");
			NikonCommand cmd = new NikonCommand('c', "SMV", count.ToString()+",9");
			//NikonCommand cmd = new NikonCommand(stdCommandType, "SMV", count.ToString());
			SendCommand(cmd);
		}
		public int GetFocusPosition()
		{
			NikonCommand cmd = new NikonCommand('r', "SPR");
			SendCommand(cmd);
			return int.Parse(cmd.ReturnedData.Substring(4));
		}
		/// <summary>
		/// Coarse (100 µm/rotation), Middle (50 µm/rotation) or Fine (25 µm/rotation)
		/// </summary>
		/// <param name="res"></param>
		public void SetFocusResolution(FocusResolution res)
		{
			NikonCommand cmd = new NikonCommand('c', "SJS", ((int)res).ToString());
			SendCommand(cmd);
		}
		public FocusResolution GetFocusResolution()
		{
			NikonCommand cmd = new NikonCommand('r', "SJR");
			SendCommand(cmd);
			return (FocusResolution)int.Parse(cmd.ReturnedData);
		}
		/// <summary>
		/// Sets the current focus level as the vertical maximum for rotating the objective revolver. By
		/// setting a stopper some distance below the specimen, one can avoid crashing objectives.
		/// </summary>
		public void SetObjectiveRotationStopper()
		{
			NikonCommand cmd = new NikonCommand(stdCommandType, "SLS", "1");
			SendCommand(cmd);
		}
		public void RemoveObjectiveRotationStopper()
		{
			NikonCommand cmd = new NikonCommand(stdCommandType, "SLS", "0");
			SendCommand(cmd);
		}
		public bool IsObjectiveRotationStopperOn()
		{
			NikonCommand cmd = new NikonCommand('r', "SLR");
			SendCommand(cmd);
			return int.Parse(cmd.ReturnedData.Substring(4)) == 1;
		}
		/// <summary>
		/// Turn the lamp on or off.
		/// </summary>
		/// <param name="on"></param>
		public void SetLamp(bool on)
		{
			NikonCommand cmd = new NikonCommand(stdCommandType, "LMS", on ? "1" : "0");
			SendCommand(cmd);
		}
		public bool IsLampOn()
		{
			NikonCommand cmd = new NikonCommand('r', "LSR");
			SendCommand(cmd);
			return int.Parse(cmd.ReturnedData.Substring(4)) == 1;
		}
		/// <summary>
		/// Set the lamp voltage between 3.0 and 12.0 in 0.1 increments.
		/// </summary>
		/// <param name="voltage"></param>
		public void SetLampVoltage(float voltage)
		{
			if(voltage < 3.0f) voltage = 3.0f;
			if(voltage > 12.0f) voltage = 12.0f;
			string data = String.Format("{0:#.#}", voltage);
			NikonCommand cmd = new NikonCommand('c', "LMC", data);
			SendCommand(cmd);
		}
		public float GetLampVoltage()
		{
			NikonCommand cmd = new NikonCommand('r', "LVR");
			SendCommand(cmd);
			return float.Parse(cmd.ReturnedData.Substring(4));
		}
		public bool IsLampControlEnabled()
		{
			NikonCommand cmd = new NikonCommand('r', "LER");
			SendCommand(cmd);
			return int.Parse(cmd.ReturnedData.Substring(4)) == 1;
		}
		/// <summary>
		/// Give lamp control to microscope or pad/pc.
		/// </summary>
		/// <param name="who">true=pad/pc, false=microscope</param>
		public void SetLampControlTarget(bool who)
		{
			NikonCommand cmd = new NikonCommand('c', "LCS", who ? "1" : "0");
			SendCommand(cmd);
		}

		/// <summary>
		/// Check who has control of the lamp (true = pad/pc, false = microscope)
		/// </summary>
		/// <returns></returns>
		public bool IsLampControlledByPC()
		{
			NikonCommand cmd = new NikonCommand('r', "LCR");
			SendCommand(cmd);
			return int.Parse(cmd.ReturnedData.Substring(4)) == 1;
		}

		/// <summary>
		/// Open the Uniblitz shutter. Assumes the Uniblitz shutter is mounted as shutter #1.
		/// </summary>
		public void OpenUniblitzShutter()
		{
			NikonCommand cmd = new NikonCommand(stdCommandType, "DSC", "1");
			SendCommand(cmd);
		}
		/// <summary>
		/// Closes the Uniblitz shutter. Assumes the Uniblitz shutter is mounted as shutter #1.
		/// </summary>
		public void CloseUniblitzShutter()
		{
			NikonCommand cmd = new NikonCommand(stdCommandType, "DSC", "2");
			SendCommand(cmd);
		}


		public void Close()
		{
			port.Close();
		}



    }
}
