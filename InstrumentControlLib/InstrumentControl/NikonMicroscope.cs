#region Using directives

using System;
using System.Collections.Generic;
using System.IO;
using Linnarsson.Utilities;


#endregion

namespace Linnarsson.InstrumentControl
{
	public enum Objective { PlanFluor10 = 1, PlanFluor20 = 2, PlanFluor40 = 3, PlanApo20 = 4, PlanApo40 = 5, PlanApo63 = 6 }
	public enum FilterBlock
	{
		DAPI = 0, 
		FITC = 1,
		TRITC = 2, 
		Cy3 = 3, 
		TexasRed = 4,
		Cy5 = 5,
		Brightfield = 6
	}

	public interface IReadonlyMicroscope
	{
		void Disconnect();
		FilterBlock Filter { get; }
		event NikonMicroscope.MicroscopeChangedHandler FilterChanged;
		Objective Objective { get; }
		int OffsetLensPosition { get; }
		OpticalPath OpticalPath { get; }
		event NikonMicroscope.MicroscopeChangedHandler OpticalPathChanged;
		PfsStatus PfsStatus { get; }
		event NikonMicroscope.MicroscopeChangedHandler PfsStatusChanged;
		Position3D<double> Position { get; }
		event NikonMicroscope.MicroscopeChangedHandler PositionChanged;
	}
	/// <summary>
	/// This class provides high-level control of the microscope, the Märzhauser stage and the Uniblitz shutter.
	/// Note that the camera can also control the Uniblitz shutter directly through a TTL trigger. Camera, peltier
    /// and flowcell (=MSP9250 dispenser) will be separate.
	/// </summary>
	[Serializable]
    public class NikonMicroscope : IReadonlyMicroscope
    {
		private NikonConnection nikonConnection;
		private MarzhauserConnection marzhauserConnection;
		public Objective Objective { get; set; }
		public FilterBlock Filter { get; set; }

		public void NextObjective()
		{
			if(Objective == Objective.PlanApo63) Objective = Objective.PlanFluor10;
			else Objective = (Objective)(Objective + 1);
			nikonConnection.SelectObjective((int)Objective, false);
			if(ObjectiveChanged != null) ObjectiveChanged();
		}

		public void PreviousObjective()
		{
			if(Objective == Objective.PlanFluor10) Objective = Objective.PlanApo63;
			else Objective = (Objective)(Objective - 1);
			nikonConnection.SelectObjective((int)Objective, false);
			if(ObjectiveChanged != null) ObjectiveChanged();
		}

		public void NextFilter()
		{
			if(Filter == FilterBlock.Brightfield) Filter = FilterBlock.DAPI;
			else Filter = (FilterBlock)(Filter + 1);
			nikonConnection.SelectFilterBlock((int)Filter);
			if(FilterChanged != null) FilterChanged();
		}

		public void PreviousFilter()
		{
			if(Filter == FilterBlock.DAPI) Filter = FilterBlock.Brightfield;
			else Filter = (FilterBlock)(Filter - 1);
			nikonConnection.SelectFilterBlock((int)Filter);
			if(FilterChanged != null) FilterChanged();
		}

		private Position3D<double> m_Position;
		/// <summary>
		/// Gets the corrent stage position. You cannot set the stage position directly by setting this property;
		/// use the Goto() method instead. 
		/// </summary>
		/// <value></value>
		public Position3D<double> Position
		{
			get { return m_Position; }
		}

		private OpticalPath m_OpticalPath;
		public OpticalPath OpticalPath
		{
			get { return m_OpticalPath; }
			set
			{
				nikonConnection.SetOpticalPath(value);
				m_OpticalPath = value;
			}
		}

		private PfsStatus m_PfsStatus = PfsStatus.Unknown;
		public PfsStatus PfsStatus
		{
			get { return m_PfsStatus; }
			set { m_PfsStatus = value; }
		}

        private int m_OffsetLensPosition;
        public int OffsetLensPosition
        {
            get { return m_OffsetLensPosition; }
        }
		public bool Connected { get; set; }
	
		public NikonMicroscope()
		{
			Objective = Objective.PlanFluor10;
			Filter = FilterBlock.Brightfield;
			m_OpticalPath = OpticalPath.Eye;
			m_Position = new Position3D<double>(54.5, 14.5, 0); // This is just a dummy for debugging purposes
		}

		/// <summary>
		/// Connect to the microscope and stage. Normally, you shouldn't call this method directly, but instead
		/// call Connect() on Instrument, which will also connect to the camera and liquid handling station.
		/// </summary>
		/// <param name="microscopePort"></param>
		/// <param name="stagePort"></param>
		/// <param name="skipCalibration"></param>
		public void Connect(HardwareConfiguration config)
		{
			// Establish a connection
			try
			{
				nikonConnection = new NikonConnection(config.MicroscopePort, false);
				nikonConnection.Open();
			}
			catch(Exception e)
			{
				throw new IOException("Unable to connect to Nikon microscope", e);
			}

			try
			{
                marzhauserConnection = new MarzhauserConnection(config.MarzhauserPort, config.MarzhauserBaudrate);
                marzhauserConnection.Open();
			}
			catch (Exception e)
			{
				nikonConnection.Close();
				throw new IOException("Unable to connect to Märzhäuser XY-stage", e);
			}
			Connected = true;
        }


		public delegate void MicroscopeChangedHandler();

		public event MicroscopeChangedHandler ObjectiveChanged;
		public event MicroscopeChangedHandler FilterChanged;
		public event MicroscopeChangedHandler OpticalPathChanged;
		public event MicroscopeChangedHandler PositionChanged;
		public event MicroscopeChangedHandler PfsStatusChanged;

		/// <summary>
		/// Refresh the status of this instance by asking the microscope, stage and camera for their current settings.
		/// </summary>
		public void Refresh()
		{
			if (nikonConnection == null || marzhauserConnection == null) return;
            
			Objective newObj = (Objective)nikonConnection.GetCurrentObjective();
			if(newObj != Objective)
			{
				Objective = newObj;
				if(ObjectiveChanged != null) ObjectiveChanged();
			}

			FilterBlock newFB = (FilterBlock)nikonConnection.GetCurrentFilterBlock();
			if(newFB != Filter)
			{
				Filter = newFB;
				if(FilterChanged != null) FilterChanged();
			}
			OpticalPath newOP = nikonConnection.GetOpticalPath();
			if(newOP != this.OpticalPath)
			{
				m_OpticalPath = newOP;
				if(OpticalPathChanged != null) OpticalPathChanged();
			}

			PfsStatus newPs = PfsStatus.NotInstalled;
			int offset = 0;

			newPs = nikonConnection.GetPfsStatus();
			offset = nikonConnection.GetOffsetLensPosition();

			if(newPs != PfsStatus || offset != OffsetLensPosition)
			{
				m_OffsetLensPosition = offset;
				m_PfsStatus = newPs;
				if(PfsStatusChanged != null) PfsStatusChanged();
			}

			marzhauserConnection.Refresh();
			double StepsPerMm = 20000f; // stepper motor steps per millimeter in Z axis (focus drive)
			Position3D<double> newPos = new Position3D<double>(marzhauserConnection.X, marzhauserConnection.Y, nikonConnection.GetFocusPosition() / StepsPerMm);
			if(newPos != Position)
			{
				m_Position = newPos;
				if(PositionChanged != null) PositionChanged();
			}
		}



		/// <summary>
		/// Go to an absolute position. 
		/// </summary>
		/// <param name="pos"></param>
		/// <param name="focusRes"></param>
		public void Goto(Position3D<double> pos)
		{
			marzhauserConnection.Goto(pos.X, pos.Y);

			if (PfsStatus == PfsStatus.Disabled || PfsStatus == PfsStatus.Waiting)
			{
				double StepsPerMm = 20000f; // stepper motor steps per millimeter in Z axis (focus drive)
				if (Math.Abs(Position.Z - pos.Z) > 0.5)
				{
					nikonConnection.SetFocusResolution(FocusResolution.Coarse);
				}
				else if (Math.Abs(Position.Z - pos.Z) > 0.05)
				{
					nikonConnection.SetFocusResolution(FocusResolution.Middle);
				}
				else
				{
					nikonConnection.SetFocusResolution(FocusResolution.Fine);
				}
				nikonConnection.SetFocus((int)(pos.Z * StepsPerMm));
			}

			m_Position = pos;
		}

        public void GotoXY(double x, double y)
        {
            if(Connected) marzhauserConnection.Goto(x,y);
			m_Position = new Position3D<double>(x,y,0);
		}
	    public void MoveXY(double x, double y)
        {
            marzhauserConnection.Move(x, y);
        }

		/// <summary>
		/// Move relative to the current position.
		/// </summary>
		/// <param name="relPos">Interpreted as a relative position</param>
		/// <param name="focusRes"></param>
		public void Move(Position3D<double> relPos)
		{
			if(relPos.Z != 0)
			{
				double StepsPerMm = 20000f; // stepper motor steps per millimeter in Z axis (focus drive)
				if(Math.Abs(relPos.Z) > 0.5)
				{
					nikonConnection.SetFocusResolution(FocusResolution.Coarse);
				}
				else if(Math.Abs(relPos.Z) > 0.05)
				{
					nikonConnection.SetFocusResolution(FocusResolution.Middle);
				}
				else
				{
					nikonConnection.SetFocusResolution(FocusResolution.Fine);
				}
				if(relPos.Z > 0) nikonConnection.IncreaseFocus((int)(relPos.Z * StepsPerMm));
				if(relPos.Z < 0) nikonConnection.DecreaseFocus((int)(-relPos.Z * StepsPerMm));
			}
			if(relPos.X != 0 || relPos.Y != 0)
			{
				marzhauserConnection.Move(relPos.X, relPos.Y);
			}
			m_Position = new Position3D<double>(Position.X + relPos.X, Position.Y + relPos.Y, Position.Z + relPos.Z);
		}

		/// <summary>
		/// Wait for an operation to finish. Currently only waits for stage motions, since the
		/// microscope parts are controlled synchronously.
		/// </summary>
		public void Wait()
		{
			marzhauserConnection.Wait();
		}

		public void EnableJoystick()
		{
			marzhauserConnection.EnableJoystick();
		}

		public void Disconnect()
		{
			if(!Connected) return;
			if(nikonConnection != null) nikonConnection.Close();
			if(marzhauserConnection != null) marzhauserConnection.Close();
			Connected = false;
		}

		public void ResetStage()
		{
			marzhauserConnection.Reset();
		}

        public void SetPfsMode(PfsMode mode)
        {
            nikonConnection.PfsControl(mode);
        }

        public void SetPfsOffset(int i)
        {
            nikonConnection.MoveOffsetLens(i);
			m_OffsetLensPosition = i;
        }

        public void MoveOffsetLensToSurface()
        {
            nikonConnection.MoveOffsetLensToSurface();
        }

        public void MoveOffsetLensToInitialPosition()
        {
            nikonConnection.MoveOffsetLensToInitialPosition();
        }

		public void StageDelay(int delay)
		{
			marzhauserConnection.Delay(delay);
		}
	}
}
