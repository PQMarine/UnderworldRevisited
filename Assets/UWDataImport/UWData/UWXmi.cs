using System.Collections.Generic;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A piece of music in XMI format (Extended MIDI of the Miles Sound library) from the
	/// "sound" folder. The game documentation only says "TODO tear exult engine apart" about it -
	/// but the format itself is an ordinary IFF and well enough known.
	///
	/// Structure:
	///   FORM XDIR   contains INFO with the number of sequences
	///   CAT  XMID   contains one FORM XMID per sequence with
	///                 TIMB  the instruments used (program/bank per entry)
	///                 EVNT  the events
	///
	/// Two things distinguish XMI from ordinary MIDI, and both are
	/// handled here:
	/// - The delay before an event is stored as a series of 0x7F bytes plus one
	///   remainder byte, not as the usual variable length.
	/// - A note-on carries its DURATION directly after it; there is no separate note-off.
	///
	/// This class reads the structure and the events. It plays nothing itself - playback
	/// is UWXmiSequencer driving UWAdlibMusicDriver with the AdLib timbres from UW.AD
	/// (the MT-32 timbres in UW.MT are not used).
	/// </summary>
	public class UWXmi
	{
		public struct Timbre
		{
			public byte Patch;

			public byte Bank;
		}

		public struct Note
		{
			/// <summary>Start time in XMI ticks since the start of the sequence.</summary>
			public int StartTick;

			/// <summary>Duration in ticks - in XMI part of the note-on event.</summary>
			public int DurationTicks;

			public byte Channel;

			public byte Key;

			public byte Velocity;
		}

		/// <summary>
		/// A MIDI event for the sequencer (UWXmiSequencer). A note-on with
		/// duration becomes two: the note-on and a note-off at start plus duration. A
		/// meta event carries status 0xFF, the type in Data1 and, for tempo (0x51), the value
		/// in Tempo.
		/// </summary>
		public struct Event
		{
			public int Tick;

			public byte Status;

			public byte Data1;

			public byte Data2;

			public int Tempo;
		}

		public class Sequence
		{
			public List<Timbre> Timbres = new List<Timbre>();

			public List<Note> Notes = new List<Note>();

			/// <summary>All events in time order, note-offs before everything else on the same
			/// tick - since 2026-09-11, for playback.</summary>
			public List<Event> Events = new List<Event>();

			/// <summary>Length in ticks, derived from the last event.</summary>
			public int LengthTicks;
		}

		private readonly List<Sequence> mOSequences = new List<Sequence>();

		public IReadOnlyList<Sequence> Sequences => mOSequences;

		public bool IsLoaded { get; private set; }

		public UWXmi(string psFileName)
		{
			if (!File.Exists(psFileName))
				return;

			byte[] lyData = File.ReadAllBytes(psFileName);

			// Collect all FORM XMID chunks; each one is a sequence.
			for (int liCursor = 0; liCursor + 12 <= lyData.Length; liCursor++)
			{
				if (!fIsTag(lyData, liCursor, "FORM") || !fIsTag(lyData, liCursor + 8, "XMID"))
					continue;

				int liLength = fReadBigEndian32(lyData, liCursor + 4);
				Sequence lOSequence = fReadSequence(lyData, liCursor + 12, System.Math.Min(liCursor + 8 + liLength, lyData.Length));

				if (lOSequence != null)
					mOSequences.Add(lOSequence);
			}

			IsLoaded = mOSequences.Count > 0;
		}

		private static Sequence fReadSequence(byte[] pyData, int piStart, int piEnd)
		{
			Sequence lOSequence = new Sequence();
			int liCursor = piStart;

			while (liCursor + 8 <= piEnd)
			{
				string lsTag = fReadTag(pyData, liCursor);
				int liLength = fReadBigEndian32(pyData, liCursor + 4);
				int liPayload = liCursor + 8;

				if (lsTag == "TIMB")
				{
					// First word is the count, then two bytes each for program and bank.
					if (liPayload + 1 < pyData.Length)
					{
						int liCount = pyData[liPayload] | (pyData[liPayload + 1] << 8);

						for (int liIndex = 0; liIndex < liCount; liIndex++)
						{
							int liOffset = liPayload + 2 + (liIndex * 2);

							if (liOffset + 1 >= pyData.Length)
								break;

							lOSequence.Timbres.Add(new Timbre
							{
								Patch = pyData[liOffset],
								Bank = pyData[liOffset + 1]
							});
						}
					}
				}
				else if (lsTag == "EVNT")
				{
					fReadEvents(pyData, liPayload, System.Math.Min(liPayload + liLength, piEnd), lOSequence);
				}

				// IFF chunks are padded to an even length.
				liCursor = liPayload + liLength + (liLength & 1);
			}

			return lOSequence;
		}

		private static void fReadEvents(byte[] pyData, int piStart, int piEnd, Sequence pOSequence)
		{
			int liCursor = piStart;
			int liTick = 0;

			// For the time order: note-offs first, then the rest in file order.
			List<KeyValuePair<long, Event>> lOOrdered = new List<KeyValuePair<long, Event>>();
			int liSerial = 0;

			void fAdd(Event pOEvent, bool pbNoteOff)
			{
				long liKey = ((long)pOEvent.Tick << 24) | ((pbNoteOff ? 0L : 1L) << 23) | (uint)(liSerial++ & 0x7FFFFF);
				lOOrdered.Add(new KeyValuePair<long, Event>(liKey, pOEvent));
			}

			while (liCursor < piEnd)
			{
				byte lyByte = pyData[liCursor];

				// Delay: any number of 0x7F, then a value below 0x80 as the remainder.
				if (lyByte < 0x80)
				{
					liTick += lyByte;
					liCursor++;

					while (lyByte == 0x7F && liCursor < piEnd && pyData[liCursor] < 0x80)
					{
						lyByte = pyData[liCursor];
						liTick += lyByte;
						liCursor++;
					}

					continue;
				}

				int liStatus = lyByte;
				liCursor++;

				if (liStatus == 0xFF)
				{
					// Meta event: type, then length as a variable-length number.
					if (liCursor >= piEnd)
						break;

					int liMetaType = pyData[liCursor++];
					int liMetaLength = fReadVariableLength(pyData, ref liCursor, piEnd);

					if (liMetaType == 0x51 && liMetaLength == 3 && liCursor + 2 < pyData.Length)
					{
						fAdd(new Event
						{
							Tick = liTick,
							Status = 0xFF,
							Data1 = 0x51,
							Tempo = (pyData[liCursor] << 16) | (pyData[liCursor + 1] << 8) | pyData[liCursor + 2]
						}, false);
					}

					liCursor += liMetaLength;

					if (liMetaType == 0x2F)
					{
						fAdd(new Event { Tick = liTick, Status = 0xFF, Data1 = 0x2F }, false);
						break;
					}

					continue;
				}

				int liCommand = liStatus & 0xF0;
				byte lyChannel = (byte)(liStatus & 0x0F);

				if (liCommand == 0x90)
				{
					// Note-on: key, velocity, then the duration as a variable-length number. That
					// is exactly the XMI peculiarity - there is no note-off.
					if (liCursor + 1 >= piEnd)
						break;

					byte lyKey = pyData[liCursor++];
					byte lyVelocity = pyData[liCursor++];
					int liDuration = fReadVariableLength(pyData, ref liCursor, piEnd);

					pOSequence.Notes.Add(new Note
					{
						StartTick = liTick,
						DurationTicks = liDuration,
						Channel = lyChannel,
						Key = lyKey,
						Velocity = lyVelocity
					});

					fAdd(new Event { Tick = liTick, Status = (byte)liStatus, Data1 = lyKey, Data2 = lyVelocity }, false);
					fAdd(new Event { Tick = liTick + liDuration, Status = (byte)(0x80 | lyChannel), Data1 = lyKey, Data2 = 0 }, true);

					if (liTick + liDuration > pOSequence.LengthTicks)
						pOSequence.LengthTicks = liTick + liDuration;
				}
				else if (liCommand == 0xC0 || liCommand == 0xD0)
				{
					// Program change and channel pressure have one data byte.
					if (liCursor < piEnd)
						fAdd(new Event { Tick = liTick, Status = (byte)liStatus, Data1 = pyData[liCursor] }, false);

					liCursor++;
				}
				else if (liCommand == 0xF0)
				{
					// System exclusive - length as a variable-length number.
					int liLength = fReadVariableLength(pyData, ref liCursor, piEnd);
					liCursor += liLength;
				}
				else
				{
					// Everything else has two data bytes.
					if (liCursor + 1 < piEnd)
					{
						fAdd(new Event
						{
							Tick = liTick,
							Status = (byte)liStatus,
							Data1 = pyData[liCursor],
							Data2 = pyData[liCursor + 1]
						}, false);
					}

					liCursor += 2;
				}
			}

			lOOrdered.Sort((pOLeft, pORight) => pOLeft.Key.CompareTo(pORight.Key));

			foreach (KeyValuePair<long, Event> lOPair in lOOrdered)
				pOSequence.Events.Add(lOPair.Value);
		}

		private static int fReadVariableLength(byte[] pyData, ref int piCursor, int piEnd)
		{
			int liResult = 0;

			while (piCursor < piEnd)
			{
				byte lyByte = pyData[piCursor++];
				liResult = (liResult << 7) | (lyByte & 0x7F);

				if ((lyByte & 0x80) == 0)
					break;
			}

			return liResult;
		}

		private static bool fIsTag(byte[] pyData, int piOffset, string psTag)
		{
			if (piOffset + psTag.Length > pyData.Length)
				return false;

			for (int liIndex = 0; liIndex < psTag.Length; liIndex++)
			{
				if (pyData[piOffset + liIndex] != psTag[liIndex])
					return false;
			}

			return true;
		}

		private static string fReadTag(byte[] pyData, int piOffset)
		{
			char[] lcTag = new char[4];

			for (int liIndex = 0; liIndex < 4; liIndex++)
				lcTag[liIndex] = (char)pyData[piOffset + liIndex];

			return new string(lcTag);
		}

		/// <summary>IFF lengths are stored most significant byte first.</summary>
		private static int fReadBigEndian32(byte[] pyData, int piOffset)
		{
			return (pyData[piOffset] << 24) | (pyData[piOffset + 1] << 16)
				| (pyData[piOffset + 2] << 8) | pyData[piOffset + 3];
		}
	}
}
