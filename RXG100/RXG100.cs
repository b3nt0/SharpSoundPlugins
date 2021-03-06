﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using AudioLib.TF;
using AudioLib;
using SharpSoundDevice;
using AudioLib.Modules;
using AudioLib.SplineLut;

namespace RXG100Sim
{
	public class RXG100 : IAudioDevice
	{
		public const int P_CHANNEL	= 0;
	
		public const int P_INPUT_A	= 1;
		public const int P_GAIN_A	= 2;
		public const int P_VOL_A	= 3;
		public const int P_BASS_A	= 4;
		public const int P_MID_A	= 5;
		public const int P_TREBLE_A	= 6;
		public const int P_PRES_A	= 7;

		public const int P_INPUT_B	= 8;
		public const int P_GAIN_B	= 9;
		public const int P_VOL_B	= 10;
		public const int P_BOOST_B	= 11;
		public const int P_BASS_B	= 12;
		public const int P_MID_B	= 13;
		public const int P_TREBLE_B	= 14;
		public const int P_PRES_B	= 15;

		public const int NUM_PARAMS	= 16;

		public static string[] ParameterNames = new String[] { 
			"Channel", 
			"Input", 
			"Gain", 
			"Volume", 
			"Bass", 
			"Mid", 
			"Treble",
			"Presence",

			"Input", 
			"Gain", 
			"Volume", 
			"Boost",
			"Bass", 
			"Mid", 
			"Treble",
			"Presence"
		};

		// ------- Declare modules for processing ----------

		private Highpass1 HighpassInput;

		// Channel A
		private TF1 TF1A;
		private SplineInterpolator Stage1A;
		private TF2 TF2A;
		private SplineInterpolator Stage2A;
		private Highpass1 PostVolumeHpA;
		private TFPres TFPresA;
		private Tonestack TonestackA;

		// Channel B
		private TF1 TF1B;
		private TF12 TF1xB;
        private SplineInterpolator Stage1B;
        private TF2 TF2B;
		private SplineInterpolator Stage2B;
		private SplineInterpolator ClipperZenerB;
		private SplineInterpolator ClipperDiodeB;
		private TFVolume TFVolumeB;
		private TFPres TFPresB;
		private Tonestack TonestackB;

		// Hipass filters
		private Highpass1 h3;
		private Highpass1 hipassZenerB;

		private Lowpass1 LowpassOutput;

		Editor Editor;

		// --------------- IAudioDevice Properties ---------------

		DeviceInfo DevInfo;

		public int DeviceId { get; set; }
		public DeviceInfo DeviceInfo { get { return this.DevInfo; } }
		public Parameter[] ParameterInfo { get; private set; }
		public Port[] PortInfo { get; private set; }
		public int CurrentProgram { get; private set; }
		public IHostInfo HostInfo { get; set; }

		// --------------- Necessary Parameters ---------------

		public double Samplerate;

		public RXG100()
		{
			Samplerate = 48000;
			DevInfo = new DeviceInfo();
			ParameterInfo = new Parameter[NUM_PARAMS];
			PortInfo = new Port[2];
		}

		public void InitializeDevice()
		{
			DevInfo.DeviceID = "Low Profile - RXG100";
#if DEBUG
			DevInfo.DeviceID = DevInfo.DeviceID + " - Dev";
#endif
			DevInfo.Developer = "Valdemar Erlingsson";
			DevInfo.EditorHeight = 0;
			DevInfo.EditorWidth = 0;
			DevInfo.HasEditor = true;
			DevInfo.Name = "RXG100 Amp Simulator";
			DevInfo.ProgramCount = 1;
			DevInfo.Type = DeviceType.Effect;
			DevInfo.Version = 1100;
			DevInfo.VstId = DeviceUtilities.GenerateIntegerId(DevInfo.DeviceID);

			PortInfo[0].Direction = PortDirection.Input;
			PortInfo[0].Name = "Stereo Input";
			PortInfo[0].NumberOfChannels = 2;

			PortInfo[1].Direction = PortDirection.Output;
			PortInfo[1].Name = "Stereo Output";
			PortInfo[1].NumberOfChannels = 2;
			
			for(int i = 0; i < ParameterInfo.Length; i++)
			{
				var p = new Parameter();
				p.Display = "0.0";
				p.Index = (uint)i;
				p.Name = ParameterNames[i];
				p.Steps = 0;
				p.Value = 0.0;
				ParameterInfo[i] = p;
			}

			HighpassInput = new Highpass1((float)Samplerate);

			// Channel A
			TF1A = new TF1(Samplerate);
			Stage1A = new SplineInterpolator(Data.Splines.Stage2_2TF);

			TF2A = new TF2(Samplerate);
			Stage2A = new SplineInterpolator(Data.Splines.Stage2_2TF);

			PostVolumeHpA = new Highpass1((float)Samplerate);
			TFPresA = new TFPres(Samplerate);
			TonestackA = new Tonestack((float)Samplerate);

			// Channel B
			TF1B = new TF1((float)Samplerate);
			TF1xB = new TF12((float)Samplerate);
			Stage1B = new SplineInterpolator(Data.Splines.Stage1CapSimulatedTF);
			

            TF2B = new TF2((float)Samplerate);
			Stage2B = new SplineInterpolator(Data.Splines.Stage2SimulatedTF);

			ClipperZenerB = new SplineInterpolator(Data.Splines.ZenerTF);
			ClipperDiodeB = new SplineInterpolator(Data.Splines.D1N914TF);

			TFVolumeB = new TFVolume((float)Samplerate);
			TFPresB = new TFPres((float)Samplerate);
			TonestackB = new Tonestack((float)Samplerate);

			h3 = new Highpass1((float)Samplerate);
			hipassZenerB = new Highpass1((float)Samplerate);
			LowpassOutput = new Lowpass1((float)Samplerate);
			TonestackA.setComponents(0.200e-9f, 0.022e-6f, 0.02e-6f, 1e3f, 500e3f, 47e3f, 500e3f, 20e3f, 500e3f);
			TonestackB.setComponents(0.200e-9f, 0.022e-6f, 0.02e-6f, 1e3f, 500e3f, 47e3f, 500e3f, 20e3f, 500e3f);
			SetSampleRate(Samplerate);

			Editor = new Editor();
			Editor.Instance = this;
			DevInfo.EditorHeight = Editor.Height;
			DevInfo.EditorWidth = Editor.Width;

			for(int i = 0; i < ParameterInfo.Length; i++)
			{
				SetParam(i, 0.51);
			}
		}

		public void DisposeDevice() { }

		public void Start() { }

		public void Stop() { }

		public void SetSampleRate(double samplerate)
		{
			this.Samplerate = samplerate;

			HighpassInput.Fs = Samplerate;

			// Channel A
			TF1A.Fs = Samplerate;
			TF2A.Fs = Samplerate;
			PostVolumeHpA.Fs = Samplerate;
			TFPresA.Fs = Samplerate;
			TonestackA.Fs = Samplerate;

			// Channel B
			TF1B.Fs = Samplerate;
			TF1xB.Fs = Samplerate;
			TF2B.Fs = Samplerate;
			TFVolumeB.Fs = Samplerate;
			TFPresB.Fs = Samplerate;
			TonestackB.Fs = Samplerate;

			h3.Fs = Samplerate;
			hipassZenerB.Fs = Samplerate;

			LowpassOutput.Fs = Samplerate;

			Update(null);
		}

		private void Update(int? parameter)
		{
			if (parameter == null)
			{
				HighpassInput.SetParam(Highpass1.P_FREQ, 180);
				h3.SetParam(Highpass1.P_FREQ, 59.45f);
				hipassZenerB.SetParam(Highpass1.P_FREQ, 8);
				LowpassOutput.SetParam(Lowpass1.P_FREQ, 10000);

				// Channel A ------------------------------------------------------
				TF1A.Update();
				TF2A.SetParam(TF2.P_USE_R3, 0.0);
			}

			if (parameter == null || parameter == P_GAIN_A)
				TF2A.SetParam(TF2.P_GAIN, Utils.ExpResponse(ParameterInfo[P_GAIN_A].Value));

			if (parameter == null)
			{
				PostVolumeHpA.SetParam(Highpass1.P_FREQ, 30);
				//Stage1A.Bias = -0.5f;
				Stage1A.Bias = -0.14f;
				Stage2A.Bias = -0.15f;
			}

			if (parameter == null || parameter == P_BASS_A || parameter == P_MID_A || parameter == P_TREBLE_A)
			{
				TonestackA.SetParam(Tonestack.P_BASS, Utils.ExpResponse(ParameterInfo[P_BASS_A].Value));
				TonestackA.SetParam(Tonestack.P_MID, ParameterInfo[P_MID_A].Value);
				TonestackA.SetParam(Tonestack.P_TREBLE, ParameterInfo[P_TREBLE_A].Value);
			}

			if (parameter == null || parameter == P_PRES_A)
				TFPresA.SetParam(TFPres.P_PRES, ParameterInfo[P_PRES_A].Value);

			// Channel B ------------------------------------------------------

			if (parameter == null)
			{
				TF1B.Update();
				TF2B.SetParam(TF2.P_USE_R3, 1.0);
				TF1xB.Update();
			}

			if (parameter == null || parameter == P_GAIN_B)
				TF2B.SetParam(TF2.P_GAIN, Utils.ExpResponse(ParameterInfo[P_GAIN_B].Value));

			if (parameter == null || parameter == P_VOL_B)
				TFVolumeB.SetParam(TFVolume.P_VOL, Utils.ExpResponse(ParameterInfo[P_VOL_B].Value));

			if (parameter == null)
			{
                Stage1B.Bias = 0.47 - 0.5f;
				Stage2B.Bias = 0.63 - 0.5f;
			}

			if (parameter == null || parameter == P_BASS_B || parameter == P_MID_B || parameter == P_TREBLE_B)
			{
				TonestackB.SetParam(Tonestack.P_BASS, Utils.ExpResponse(ParameterInfo[P_BASS_B].Value));
				TonestackB.SetParam(Tonestack.P_MID, ParameterInfo[P_MID_B].Value);
				TonestackB.SetParam(Tonestack.P_TREBLE, ParameterInfo[P_TREBLE_B].Value);
			}

			if (parameter == null || parameter == P_PRES_B)
				TFPresB.SetParam(TFPres.P_PRES, ParameterInfo[P_PRES_B].Value);

			if (Editor != null)
				Editor.UpdateParameters();
		}

		public void SetParam(int param, double value)
		{
			ParameterInfo[param].Value = value;
			ParameterInfo[param].Display = String.Format("{0:0.00}", value);
			Update(param);
		}

		public void SetCurrentProgram(int program)
		{
			CurrentProgram = program;
			Update(null);
		}

		public void ProcessSample(double[][] input, double[][] output, uint bufferSize)
		{
			double[] signal = input[0];
			HighpassInput.ProcessInPlace(signal);

			// Channel A
			if (ParameterInfo[P_CHANNEL].Value <= 0.5)
			{
				var inputGain = Utils.ExpResponse(ParameterInfo[P_INPUT_A].Value);
				Utils.GainInPlace(signal, inputGain);

				TF1A.ProcessInPlace(signal);
				Stage1A.ProcessInPlace(signal);

				TF2A.ProcessInPlace(signal);
				Stage2A.ProcessInPlace(signal);

				var volume = Utils.ExpResponse(ParameterInfo[P_VOL_A].Value) * 0.333; // match loudness of channels
				Utils.GainInPlace(signal, volume);

				PostVolumeHpA.ProcessInPlace(signal);
				TonestackA.ProcessInPlace(signal);
				TFPresA.ProcessInPlace(signal);
			}

			// Channel B
			if (ParameterInfo[P_CHANNEL].Value > 0.5f)
			{			
				var inputGain = Utils.ExpResponse(ParameterInfo[P_INPUT_B].Value) * 5;
				Utils.GainInPlace(signal, inputGain);

				TF1B.ProcessInPlace(signal);
				TF1xB.ProcessInPlace(signal);

                Stage1B.ProcessInPlace(signal);

				TF2B.ProcessInPlace(signal);
				Stage2B.ProcessInPlace(signal);

				hipassZenerB.ProcessInPlace(signal);
				ClipperZenerB.ProcessInPlace(signal);

				if (ParameterInfo[P_BOOST_B].Value > 0.5f)
				{
					ClipperDiodeB.ProcessInPlace(signal);
					Utils.GainInPlace(signal, 6);
				}

				TFVolumeB.ProcessInPlace(signal);
				TonestackB.ProcessInPlace(signal);
				TFPresB.ProcessInPlace(signal);
			}

			LowpassOutput.ProcessInPlace(signal);

			// prevent horrible noise in case something goes wrong
			Utils.SaturateInPlace(signal, -4, 4);

			// copy data to outBuffer
			for (int i = 0; i < bufferSize; i++)
			{
				output[0][i] = signal[i];
				output[1][i] = signal[i];
			}
		}

		public void ProcessSample(IntPtr input, IntPtr output, uint inChannelCount, uint outChannelCount, uint bufferSize)
		{
			// not used
		}

		public void OpenEditor(IntPtr parentWindow)
		{
			Editor.UpdateParameters();
			DeviceUtilities.DockWinFormsPanel(Editor, parentWindow);
		}

		public void CloseEditor() { }

		public bool SendEvent(Event ev)
		{
			if (ev.Type == EventType.Parameter)
			{
				SetParam((int)ev.EventIndex, (double)ev.Data);
				return true;
			}

			return false;
		}

		public void SetProgramData(Program program, int index)
		{
			try
			{
				DeviceUtilities.DeserializeParameters(ParameterInfo, program.Data);
				Update(null);
			}
			catch(Exception)
			{
				System.Windows.Forms.MessageBox.Show("Unable to load preset");
			}
		}

		public Program GetProgramData(int index)
		{
			var output = new Program();
			output.Data = DeviceUtilities.SerializeParameters(ParameterInfo);
			output.Name = "";
			return output;
		}

		public void HostChanged()
		{
			var samplerate = HostInfo.SampleRate;
			if (samplerate != this.Samplerate)
				SetSampleRate(samplerate);
		}
	}
}

