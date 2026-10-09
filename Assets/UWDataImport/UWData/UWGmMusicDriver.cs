using System;
using MeltySynth;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The game's MT-32 music (UW*.XMI) on a General MIDI synthesizer: MeltySynth (MIT) with a
	/// soundfont - the one that ships with the game is cut from FluidR3_GM (MIT) by
	/// Tools/UWSoundFontTrim.
	///
	/// THE PIECES ARE WRITTEN FOR THE MT-32, not for General MIDI (read out 2026-10-08): every
	/// TIMB chunk asks for bank 0 only, the MT-32's built-in timbres, by MT-32 program number;
	/// parts on channels 2-9, the rhythm on 10, no SysEx. So the program numbers go through
	/// Mt32ToGm, a table of our own made from the timbre names in the MT-32's control ROM -
	/// the result is close, not the original's sound. The rhythm keys of the pieces (35-75) lie
	/// where General MIDI put the same drums, so channel 10 passes unchanged.
	///
	/// Two MT-32 habits are carried over (both from Munt's source, Part.cpp and Synth.cpp):
	/// its pan runs the other way round than General MIDI's, and its pitch bend spans twelve
	/// semitones, not two (only "Fleeing", UW13, bends at all).
	///
	/// The XMIDI controllers 110-120 are the Miles driver's own and never reach the device -
	/// above all 120, which General MIDI reads as All Sound Off.
	/// </summary>
	public class UWGmMusicDriver : IUWRenderingMidiDriver
	{
		private const int DrumChannel = 9;

		private const int BendSemitones = 12;

		/// <summary>
		/// MT-32 program (the timbre names as the control ROM has them) to General MIDI
		/// program, both counted from 0. Our own choice by name; where the MT-32 has a sound
		/// General MIDI lacks, the nearest in character.
		/// </summary>
		public static readonly byte[] Mt32ToGm =
		{
			//  AcouPiano1-3, ElecPiano1-4, Honkytonk
			0, 1, 0, 4, 5, 4, 5, 3,
			//  Elec Org 1-4, Pipe Org 1-3, Accordion
			16, 17, 18, 16, 19, 19, 20, 21,
			//  Harpsi 1-3, Clavi 1-3, Celesta 1-2
			6, 6, 6, 7, 7, 7, 8, 8,
			//  Syn Brass1-4, Syn Bass 1-4
			62, 63, 62, 63, 38, 39, 38, 39,
			//  Fantasy, Harmo Pan, Chorale, Glasses, Soundtrack, Atmosphere, Warm Bell, Funny Vox
			88, 89, 52, 98, 97, 99, 98, 85,
			//  Echo Bell, Ice Rain, Oboe 2001, Echo Pan, DoctorSolo, Schooldaze, BellSinger, SquareWave
			102, 96, 68, 75, 80, 9, 91, 80,
			//  Str Sect 1-3, Pizzicato, Violin 1-2, Cello 1-2
			48, 49, 48, 45, 40, 40, 42, 42,
			//  Contrabass, Harp 1-2, Guitar 1-2, Elec Gtr 1-2, Sitar
			43, 46, 46, 24, 25, 26, 27, 104,
			//  Acou Bass1-2, Elec Bass1-2, Slap Bass1-2, Fretless 1-2
			32, 32, 33, 34, 36, 37, 35, 35,
			//  Flute 1-2, Piccolo 1-2, Recorder, Panpipes, Sax 1-2
			73, 73, 72, 72, 74, 75, 64, 65,
			//  Sax 3-4, Clarinet 1-2, Oboe, Engl Horn, Bassoon, Harmonica
			66, 67, 71, 71, 68, 69, 70, 22,
			//  Trumpet 1-2, Trombone 1-2, Fr Horn 1-2, Tuba, Brs Sect 1
			56, 56, 57, 57, 60, 60, 58, 61,
			//  Brs Sect 2, Vibe 1-2, Syn Mallet, Wind Bell, Glock, Tube Bell, Xylophone
			61, 11, 11, 98, 112, 9, 14, 13,
			//  Marimba, Koto, Sho, Shakuhachi, Whistle 1-2, BottleBlow, BreathPipe
			12, 107, 20, 77, 78, 78, 76, 76,
			//  Timpani, MelodicTom, Deep Snare, Elec Perc1-2, Taiko, Taiko Rim, Cymbal
			47, 117, 118, 118, 118, 116, 115, 119,
			//  Castanets, Triangle, Orche Hit, Telephone, Bird Tweet, OneNoteJam, WaterBells, JungleTune
			115, 112, 55, 124, 123, 118, 98, 108
		};

		private readonly Synthesizer mOSynth;

		public int SampleRate { get; }

		/// <summary>Loads the soundfont; throws if it cannot be read.</summary>
		public UWGmMusicDriver(string psSoundFontPath, int piSampleRate)
		{
			SampleRate = piSampleRate;
			mOSynth = new Synthesizer(psSoundFontPath, piSampleRate);
			Reset();
		}

		/// <summary>Overall level, 1 = MeltySynth's own.</summary>
		public float MasterVolume
		{
			get { return mOSynth.MasterVolume; }
			set { mOSynth.MasterVolume = value; }
		}

		public void Reset()
		{
			mOSynth.Reset();

			for (int liChannel = 0; liChannel < 16; liChannel++)
				fSetBendRange(liChannel, BendSemitones);
		}

		public void AllNotesOff()
		{
			mOSynth.NoteOffAll(false);
		}

		public void Handle(int piStatus, int piData1, int piData2)
		{
			int liChannel = piStatus & 0x0F;
			int liCommand = piStatus & 0xF0;

			switch (liCommand)
			{
				case 0xC0:
					// The MT-32's rhythm part ignores a program change; General MIDI would switch kits.
					if (liChannel == DrumChannel)
						return;

					mOSynth.ProcessMidiMessage(liChannel, 0xC0, Mt32ToGm[piData1 & 0x7F], 0);
					return;

				case 0xB0:
					if (piData1 >= 110 && piData1 <= 120)
						return;

					if (piData1 == 10)
						piData2 = 127 - piData2;

					break;
			}

			mOSynth.ProcessMidiMessage(liChannel, liCommand, piData1, piData2);
		}

		/// <summary>The next piCount stereo samples into the two buffers, from their start.</summary>
		public void Render(float[] pfLeft, float[] pfRight, int piCount)
		{
			mOSynth.Render(new Span<float>(pfLeft, 0, piCount), new Span<float>(pfRight, 0, piCount));
		}

		/// <summary>RPN 0 (pitch bend sensitivity) to whole semitones, then the RPN closed again.</summary>
		private void fSetBendRange(int piChannel, int piSemitones)
		{
			mOSynth.ProcessMidiMessage(piChannel, 0xB0, 101, 0);
			mOSynth.ProcessMidiMessage(piChannel, 0xB0, 100, 0);
			mOSynth.ProcessMidiMessage(piChannel, 0xB0, 6, piSemitones);
			mOSynth.ProcessMidiMessage(piChannel, 0xB0, 38, 0);
			mOSynth.ProcessMidiMessage(piChannel, 0xB0, 101, 127);
			mOSynth.ProcessMidiMessage(piChannel, 0xB0, 100, 127);
		}
	}
}
