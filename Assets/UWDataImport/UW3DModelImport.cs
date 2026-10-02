using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport.UWData;

namespace UWDataImport
{
    /// <summary>
    /// Reads the 3D models that the original has baked into UW.EXE (door frame, bridge,
    /// bench, chest, shrine/ankh, ...) and turns their bytecode into vertex/face lists.
    ///
    /// Written 2026-09-15 from the model interpreter of UW.EXE itself (handler disassembly of
    /// the jump table at seg051:2738; handler addresses below are seg004 offsets). The
    /// original runs a model as a small program with the data segment set to seg051:
    /// `lodsw; xchg ax,bx; jmp [bx+2738h]`. Vertex slots live at DS:1620 + 8 * i, and every
    /// vertex operand is already i * 8. Offsets of branch and skip targets are relative to the
    /// end of the operand word. Coordinates are plain model units (256 = one tile), word order
    /// X, Y, Z with Z = height.
    ///
    /// For a static mesh every branch is taken: sort nodes (06, 0C, 0E, 10) run both lists,
    /// the camera and register skips (14, 16, 58, 5E-68) do not skip, and a jump (48) right
    /// at the end of such a skipped region is the "else" part and runs as well - the original
    /// uses these pairs for faces seen from both sides. Rotations (BA and relatives) and the
    /// pivot shift (4A) are not applied; the runtime vectors v128 and v256 (built by the
    /// object renderer) count as zero, vertices built with v256 are marked as reaching up to
    /// the ceiling.
    ///
    /// The model offset table (64 words) is at file 0x4CCD0 (seg051:28A0); offsets are relative
    /// to seg051:2938 (file 0x4CD68). Like the colour records these offsets are verified
    /// against the GOG build only.
    /// </summary>
    public class UW3DModelImport
    {
        private const int TableStart = 0x0004ccd0;
        private const int CodeBase = 0x0004cd68;
        private const int ModelCount = 64;
        private const int MaxCallDepth = 32;
        private const int MaxSteps = 200000;

        /// <summary>Slots of the runtime vectors v128 (portcullis opening) and v256 (ceiling),
        /// see opcode 8C.</summary>
        private const int RuntimeVector128 = 128;
        private const int RuntimeVector256 = 256;

        /// <summary>
        /// Colour opcodes (BC, D4) do not store a palette colour directly but the address of a
        /// colour register in the exe's data segment (DS). UW.EXE fills these registers before
        /// running a model's bytecode (seg032_2DCA_597, called for 3D models from
        /// RenderingObjects_seg032_2DCA_5): it emits "store word" opcodes that write the model's
        /// colours into register n at DS:[237C] - 18h + 2n, i.e. 0x2920 + 2n. So
        /// "(address - 0x2920) / 2" is the register number.
        /// </summary>
        private const int ColorRegisterBase = 0x2920;

        /// <summary>
        /// The per-model colour records in UW.EXE: DS:060A, 4 bytes per model (flags, then up
        /// to three palette indices; the number of colours is flags &amp; 7), 30 models. In the
        /// GOG build the data segment starts at file offset 0x5DCC0, so the table is at file
        /// 0x5E2CA (the object-to-model table at DS:0682 follows directly). Read at runtime, so
        /// no game data lives in the source.
        /// Flag bits beyond the count are not evaluated here: 0x08 ceiling vector, 0x10 object
        /// texture, 0x20 register 0 taken from a resource, 0x40 pitch in register 5,
        /// 0x80 animated colours (moongate).
        /// </summary>
        private const int ColorRecordsStart = 0x0005e2ca;
        private const int ColorRecordCount = 30;

        /// <summary>Palette indices per model index, filled from UW.EXE by the constructor
        /// (see ColorRecordsStart). Empty for models without a colour record.</summary>
        private readonly byte[][] mModelColors = new byte[ModelCount][];

        private readonly byte[] mData;

        public UW3DModelImport(string psExePath)
        {
            mData = File.ReadAllBytes(psExePath);

            for (int liModel = 0; liModel < ModelCount; liModel++)
            {
                int liRecord = ColorRecordsStart + (liModel * 4);

                if (liModel >= ColorRecordCount || liRecord + 4 > mData.Length)
                {
                    mModelColors[liModel] = new byte[0];
                    continue;
                }

                int liCount = Math.Min(mData[liRecord] & 7, 3);
                byte[] laColors = new byte[liCount];
                Array.Copy(mData, liRecord + 1, laColors, 0, liCount);
                mModelColors[liModel] = laColors;
            }
        }

        /// <summary>Model indices; the object-to-model table of UW.EXE (DS:0682, 32 words
        /// indexed by object id - 0x150) confirms the mapping used by UWObjectSpawner.</summary>
        /// <summary>How many model slots the interpreter walks. Only about half of them carry
        /// geometry in uw1 (0x01 to 0x1C), the rest are empty - see Tools/UWSelfCheck.</summary>
        public const int Count = ModelCount;

        public static class ModelIndex
        {
            public const int DoorFrame = 0x01;
            public const int Bridge = 0x02;
            public const int Bench = 0x03;
            public const int SmallBoulder = 0x05;
            public const int MediumBoulder = 0x06;
            public const int LargeBoulder = 0x07;
            /// <summary>The projectile model - a thin body along its own
            /// Y axis (17 vertices, 17 faces, extents 0.1 x 0.4 x 0.1). The
            /// colour table listed it as "arrow" from the start, but it was never used until
            /// 2026-09-07 - projectiles flew as a flat sprite.</summary>
            public const int Arrow = 0x08;

            public const int Pillar = 0x0A;
            public const int Shrine = 0x0B;
            public const int Gravestone = 0x13;
            public const int Moongate = 0x17;
            public const int Table = 0x18;
            public const int Chest = 0x19;
            public const int Nightstand = 0x1A;
            public const int Barrel = 0x1B;
            public const int Chair = 0x1C;
        }

        /// <summary>
        /// The first (for most models the only) colour of a model's UW.EXE colour record -
        /// for places that do not parse real model bytecode but still need the same
        /// original colour of a model (e.g. the hand-built door leaf, whose narrow
        /// sides should carry the same colour as the door frame model (01)).
        /// </summary>
        public byte GetPrimaryPaletteIndex(int piModelIndex)
        {
            if (piModelIndex < 0 || piModelIndex >= ModelCount)
                return 0;

            byte[] laTable = mModelColors[piModelIndex];

            return (laTable != null && laTable.Length > 0) ? laTable[0] : (byte)0;
        }

        /// <summary>A vertex slot's value at one point of the execution.</summary>
        private struct SlotValue
        {
            public int X;
            public int Y;
            public int Z;
            public bool Ceiling;
        }

        /// <summary>A face as recorded during execution, with slot snapshots.</summary>
        private sealed class RawFace
        {
            public readonly List<int> Keys = new List<int>();
            public List<UWVector2> Uvs;
            public int TextureNumber = -1;
            public int ColorRegister;
            public int ShadeLevel;
            public bool IsGouraud;
        }

        /// <summary>Execution state of one model.</summary>
        private sealed class Machine
        {
            public readonly Dictionary<int, SlotValue> Slots = new Dictionary<int, SlotValue>();

            /// <summary>Distinct (slot, value) pairs in order of creation; faces refer to these.</summary>
            public readonly List<KeyValuePair<int, SlotValue>> Snapshots = new List<KeyValuePair<int, SlotValue>>();

            public readonly Dictionary<long, int> SnapshotIndex = new Dictionary<long, int>();

            public readonly List<RawFace> Faces = new List<RawFace>();

            public readonly Dictionary<int, byte> Shades = new Dictionary<int, byte>();

            /// <summary>Register of the flat colour (BC).</summary>
            public int ColorRegister;

            /// <summary>Shade level of the flat colour (BC, low byte of the second operand word).</summary>
            public int FlatShade;

            /// <summary>Register of the Gouraud base colour (D4) - a separate state in UW.EXE,
            /// BC does not change it.</summary>
            public int GouraudRegister;

            public bool Gouraud;
            public bool HaveCenter;
            public UWVector3 Center;
            public UWVector3 Extents;
            public int Steps;
            public bool Aborted;
            public readonly List<int> UnknownOpcodes = new List<int>();
        }

        public UW3DModel GetModel(int piModelIndex)
        {
            if (piModelIndex < 0 || piModelIndex >= ModelCount)
                return null;

            ushort liRelOffset = fReadUInt16(TableStart + (piModelIndex * 2));

            if (liRelOffset == 0)
                return null;

            int liStart = CodeBase + liRelOffset;

            if (liStart < 0 || liStart + 2 > mData.Length)
                return null;

            Machine lOMachine = new Machine();
            fRun(lOMachine, liStart, 0);

            return fBuildModel(piModelIndex, lOMachine);
        }

        /// <summary>Runs one list until its END (opcode 00).</summary>
        private void fRun(Machine pOMachine, int piPos, int piDepth)
        {
            if (piDepth > MaxCallDepth)
            {
                pOMachine.Aborted = true;
                return;
            }

            // ends of the skipped regions (conditional skips) that are currently open
            List<int> lOOpenRegions = new List<int>();

            while (!pOMachine.Aborted)
            {
                if (++pOMachine.Steps > MaxSteps || piPos + 2 > mData.Length)
                {
                    pOMachine.Aborted = true;
                    return;
                }

                for (int liAt = lOOpenRegions.Count - 1; liAt >= 0; liAt--)
                    if (piPos >= lOOpenRegions[liAt])
                        lOOpenRegions.RemoveAt(liAt);

                int liOp = fReadUInt16(piPos);
                int p = piPos + 2;

                switch (liOp)
                {
                    case 0x00: // 27D8 END
                        return;

                    case 0x02: // 2B16 store word (address, value) - register setup
                    case 0x04:
                    case 0x0A:
                    case 0xBE: // 2B04 copy a word through a pointer
                        piPos = p + 4;
                        break;

                    case 0x06: // 30AE sort node: three (normal, point) pairs, then list A and list B
                        fRun(pOMachine, fTarget(p + 12), piDepth + 1);
                        fRun(pOMachine, fTarget(p + 14), piDepth + 1);
                        piPos = p + 16;
                        break;

                    case 0x0C: // 30F6 / 3133 / 3170 two-axis sort nodes
                    case 0x0E:
                    case 0x10:
                        fRun(pOMachine, fTarget(p + 8), piDepth + 1);
                        fRun(pOMachine, fTarget(p + 10), piDepth + 1);
                        piPos = p + 12;
                        break;

                    case 0x6C:
                        fRun(pOMachine, fTarget(p + 4), piDepth + 1);
                        fRun(pOMachine, fTarget(p + 6), piDepth + 1);
                        piPos = p + 8;
                        break;

                    case 0x12: // 3036 call a sub-list
                        fRun(pOMachine, fTarget(p), piDepth + 1);
                        piPos = p + 2;
                        break;

                    case 0x50: // rotate by a constant angle and call (not applied)
                    case 0x70:
                    case 0x72:
                    case 0xBA: // 3C2B rotate by the angle in a register and call (doors)
                    case 0xC2:
                    case 0xC4:
                        fRun(pOMachine, fTarget(p + 2), piDepth + 1);
                        piPos = p + 4;
                        break;

                    case 0x48: // 3B5A jump; at the end of a skipped region it jumps over the "else" part
                    {
                        int liTarget = fTarget(p);
                        p += 2;
                        int liLast = lOOpenRegions.Count - 1;

                        if (liLast >= 0 && lOOpenRegions[liLast] == p)
                        {
                            lOOpenRegions[liLast] = liTarget;
                            piPos = p;
                        }
                        else
                        {
                            piPos = liTarget;
                        }

                        break;
                    }

                    case 0x14: // 305A run if [address] > value, else skip
                    case 0x16: // 3047 run if [address] < value, else skip
                    case 0x64: // 3F7B / 3F92 / 3FA9 camera side skips
                    case 0x66:
                    case 0x68:
                        lOOpenRegions.Add(p + 6 + fReadInt16(p));
                        piPos = p + 6;
                        break;

                    case 0x58: // 3FC0 skip a group facing away (three pairs)
                        lOOpenRegions.Add(p + 14 + fReadInt16(p));
                        piPos = p + 14;
                        break;

                    case 0x5E: // 3F00 / 3F29 / 3F52 the same for two axes
                    case 0x60:
                    case 0x62:
                        lOOpenRegions.Add(p + 10 + fReadInt16(p));
                        piPos = p + 10;
                        break;

                    case 0x7A: // 2BFE define vertex x, y, z, slot
                    case 0x4C: // define a runtime vector the same way
                        fSetSlot(pOMachine, fReadUInt16(p + 6), fReadInt16(p), fReadInt16(p + 2), fReadInt16(p + 4), false);
                        piPos = p + 8;
                        break;

                    case 0x82: // 2B3D define n consecutive vertices
                    {
                        int liCount = fReadUInt16(p);
                        int liFirst = fReadUInt16(p + 2);

                        for (int liAt = 0; liAt < liCount; liAt++)
                        {
                            int q = p + 4 + (6 * liAt);
                            fSetSlot(pOMachine, liFirst + (8 * liAt), fReadInt16(q), fReadInt16(q + 2), fReadInt16(q + 4), false);
                        }

                        piPos = p + 4 + (6 * liCount);
                        break;
                    }

                    case 0xB6: // one vertex with byte coordinates (* 32)
                        fSetSlot(pOMachine, mData[p + 3] * 8, (sbyte)mData[p] * 32, (sbyte)mData[p + 1] * 32, (sbyte)mData[p + 2] * 32, false);
                        piPos = p + 4;
                        break;

                    case 0xB8: // n vertices with byte coordinates
                    {
                        int liCount = mData[p];
                        int liFirst = mData[p + 1] * 8;

                        for (int liAt = 0; liAt < liCount; liAt++)
                        {
                            int q = p + 2 + (3 * liAt);
                            fSetSlot(pOMachine, liFirst + (8 * liAt), (sbyte)mData[q] * 32, (sbyte)mData[q + 1] * 32, (sbyte)mData[q + 2] * 32, false);
                        }

                        piPos = p + 2 + (3 * liCount);
                        break;
                    }

                    case 0x86: // 3441 dst = src + d along X (alias 2A)
                    case 0x2A:
                    case 0x88: // 328B along Z (alias 1C)
                    case 0x1C:
                    case 0x8A: // 34E4 along Y (alias 2C)
                    case 0x2C:
                    {
                        SlotValue lOValue = fGetSlot(pOMachine, fReadUInt16(p));
                        int liDelta = fReadInt16(p + 2);

                        if (liOp == 0x86 || liOp == 0x2A)
                            lOValue.X += liDelta;
                        else if (liOp == 0x88 || liOp == 0x1C)
                            lOValue.Z += liDelta;
                        else
                            lOValue.Y += liDelta;

                        fSetSlot(pOMachine, fReadUInt16(p + 4), lOValue.X, lOValue.Y, lOValue.Z, lOValue.Ceiling);
                        piPos = p + 6;
                        break;
                    }

                    case 0x90: // 358D / 352C / 35EE two-axis offsets xz / xy / yz
                    case 0x92:
                    case 0x94:
                    {
                        SlotValue lOValue = fGetSlot(pOMachine, fReadUInt16(p + 4));
                        int liDelta1 = fReadInt16(p);
                        int liDelta2 = fReadInt16(p + 2);

                        if (liOp == 0x90)
                        {
                            lOValue.X += liDelta1;
                            lOValue.Z += liDelta2;
                        }
                        else if (liOp == 0x92)
                        {
                            lOValue.X += liDelta1;
                            lOValue.Y += liDelta2;
                        }
                        else
                        {
                            lOValue.Y += liDelta1;
                            lOValue.Z += liDelta2;
                        }

                        fSetSlot(pOMachine, fReadUInt16(p + 6), lOValue.X, lOValue.Y, lOValue.Z, lOValue.Ceiling);
                        piPos = p + 8;
                        break;
                    }

                    case 0x26: // copy a vertex
                    {
                        SlotValue lOValue = fGetSlot(pOMachine, fReadUInt16(p + 2));
                        fSetSlot(pOMachine, fReadUInt16(p), lOValue.X, lOValue.Y, lOValue.Z, lOValue.Ceiling);
                        piPos = p + 4;
                        break;
                    }

                    case 0x8C: // 3BB1 dst = a + b (b is a runtime vector)
                    case 0xC6: // dst = a - b
                    {
                        SlotValue lOA = fGetSlot(pOMachine, fReadUInt16(p));
                        int liB = fReadUInt16(p + 2) / 8;
                        SlotValue lOB = liB == RuntimeVector128 || liB == RuntimeVector256 ? new SlotValue() : fGetSlot(pOMachine, liB * 8);
                        int liSign = liOp == 0x8C ? 1 : -1;
                        bool lbCeiling = lOA.Ceiling || (liOp == 0x8C && liB == RuntimeVector256);

                        fSetSlot(pOMachine, fReadUInt16(p + 4), lOA.X + (liSign * lOB.X), lOA.Y + (liSign * lOB.Y), lOA.Z + (liSign * lOB.Z), lbCeiling);
                        piPos = p + 6;
                        break;
                    }

                    case 0x78: // 4494 cull box: centre slot, half extents x, z, y, skip
                    {
                        if (!pOMachine.HaveCenter)
                        {
                            SlotValue lOCenter = fGetSlot(pOMachine, fReadUInt16(p));
                            pOMachine.Center = new UWVector3(lOCenter.X / 256f, lOCenter.Z / 256f, lOCenter.Y / 256f);
                            pOMachine.Extents = new UWVector3(2 * fReadInt16(p + 2) / 256f, 2 * fReadInt16(p + 4) / 256f, 2 * fReadInt16(p + 6) / 256f);
                            pOMachine.HaveCenter = true;
                        }

                        piPos = p + 10;
                        break;
                    }

                    case 0x84:
                        piPos = p + 2 + (2 * fReadUInt16(p));
                        break;

                    case 0x7E: // 2E73 polygon with the current fill mode (alias 22)
                    case 0x22:
                    {
                        int liCount = fReadUInt16(p);
                        RawFace lOFace = fNewFace(pOMachine);

                        for (int liAt = 0; liAt < liCount; liAt++)
                            lOFace.Keys.Add(fSnapshot(pOMachine, fReadUInt16(p + 2 + (2 * liAt)) / 8));

                        pOMachine.Faces.Add(lOFace);
                        piPos = p + 2 + (2 * liCount);
                        break;
                    }

                    case 0x2E: // palette colour
                        pOMachine.Gouraud = false;
                        pOMachine.FlatShade = 0;
                        piPos = p + 2;
                        break;

                    case 0x5C: // register colour plus offset
                        pOMachine.Gouraud = false;
                        pOMachine.FlatShade = 0;
                        piPos = p + 4;
                        break;

                    case 0xBC: // 7D9D flat colour from a register (address, shade level, add)
                        pOMachine.ColorRegister = (fReadUInt16(p) - ColorRegisterBase) / 2;
                        pOMachine.FlatShade = mData[p + 2];
                        pOMachine.Gouraud = false;
                        piPos = p + 4;
                        break;

                    case 0x44: // flat fill
                    case 0x40: // 3937 flat fill with another clip rectangle
                        pOMachine.Gouraud = false;
                        piPos = p;
                        break;

                    case 0xD6: // 38B9 Gouraud fill
                    case 0xD8:
                        pOMachine.Gouraud = true;
                        piPos = p;
                        break;

                    case 0xD4: // 2BC7 Gouraud base register and per-vertex shade bytes, padded to even
                    {
                        int liCount = fReadUInt16(p);
                        pOMachine.GouraudRegister = (fReadUInt16(p + 2) - ColorRegisterBase) / 2;

                        for (int liAt = 0; liAt < liCount; liAt++)
                        {
                            int q = p + 4 + (3 * liAt);
                            pOMachine.Shades[fReadUInt16(q) / 8] = mData[q + 2];
                        }

                        piPos = p + 4 + (3 * liCount);

                        if (((piPos - CodeBase) & 1) != 0)
                            piPos++;

                        break;
                    }

                    case 0xA0: // 5CED textured quad (texture, four vertex bytes)
                    case 0xA2:
                    case 0xA4: // mirrored in u
                    case 0xA6:
                    {
                        RawFace lOFace = fNewFace(pOMachine);
                        lOFace.TextureNumber = fReadUInt16(p);
                        lOFace.Uvs = new List<UWVector2>(4);
                        bool lbMirror = liOp == 0xA4 || liOp == 0xA6;

                        for (int liAt = 0; liAt < 4; liAt++)
                        {
                            lOFace.Keys.Add(fSnapshot(pOMachine, mData[p + 2 + liAt]));
                            float lfU = (liAt == 1 || liAt == 2) ? 1f : 0f;
                            float lfV = (liAt >= 2) ? 1f : 0f;
                            lOFace.Uvs.Add(new UWVector2(lbMirror ? 1f - lfU : lfU, lfV));
                        }

                        pOMachine.Faces.Add(lOFace);
                        piPos = p + 6;
                        break;
                    }

                    case 0xA8: // 5C7E textured polygon (texture, n, n * (vertex, u, v))
                    case 0xAA:
                    {
                        RawFace lOFace = fNewFace(pOMachine);
                        lOFace.TextureNumber = fReadUInt16(p);
                        piPos = fReadTexturedVertices(pOMachine, lOFace, p + 2);
                        break;
                    }

                    case 0xB4: // 5C5E / 5C6A textured polygon with the renderer's current texture
                    case 0xCE:
                        piPos = fReadTexturedVertices(pOMachine, fNewFace(pOMachine), p);
                        break;

                    case 0xB2: // select the current texture slot
                        piPos = p + 2;
                        break;

                    case 0x4A: // 3B6E pivot shift (not applied)
                        piPos = p + 6;
                        break;

                    default:
                        pOMachine.UnknownOpcodes.Add(liOp);
                        pOMachine.Aborted = true;
                        return;
                }
            }
        }

        /// <summary>n, then n * (vertex word, u word, v word); u and v as fractions of 65535.</summary>
        private int fReadTexturedVertices(Machine pOMachine, RawFace pOFace, int piPos)
        {
            int liCount = fReadUInt16(piPos);
            pOFace.Uvs = new List<UWVector2>(liCount);

            for (int liAt = 0; liAt < liCount; liAt++)
            {
                int q = piPos + 2 + (6 * liAt);
                pOFace.Keys.Add(fSnapshot(pOMachine, fReadUInt16(q) / 8));
                pOFace.Uvs.Add(new UWVector2(fReadUInt16(q + 2) / 65535f, fReadUInt16(q + 4) / 65535f));
            }

            pOMachine.Faces.Add(pOFace);
            return piPos + 2 + (6 * liCount);
        }

        private static RawFace fNewFace(Machine pOMachine)
        {
            return new RawFace
            {
                ColorRegister = pOMachine.Gouraud ? pOMachine.GouraudRegister : pOMachine.ColorRegister,
                ShadeLevel = pOMachine.Gouraud ? 0 : pOMachine.FlatShade,
                IsGouraud = pOMachine.Gouraud
            };
        }

        private static SlotValue fGetSlot(Machine pOMachine, int piOperand)
        {
            SlotValue lOValue;
            return pOMachine.Slots.TryGetValue(piOperand / 8, out lOValue) ? lOValue : new SlotValue();
        }

        private static void fSetSlot(Machine pOMachine, int piOperand, int piX, int piY, int piZ, bool pbCeiling)
        {
            int liSlot = piOperand / 8;
            pOMachine.Slots[liSlot] = new SlotValue { X = piX, Y = piY, Z = piZ, Ceiling = pbCeiling };

            // every definition is kept as a snapshot, so that vertices nobody draws still exist
            fSnapshot(pOMachine, liSlot);
        }

        /// <summary>Index of the (slot, current value) pair, created on first use.</summary>
        private static int fSnapshot(Machine pOMachine, int piSlot)
        {
            SlotValue lOValue;

            if (!pOMachine.Slots.TryGetValue(piSlot, out lOValue))
                lOValue = new SlotValue();

            long liKey = ((long)piSlot << 50) ^ ((long)(lOValue.X & 0xFFFF) << 34) ^ ((long)(lOValue.Y & 0xFFFF) << 18)
                ^ ((long)(lOValue.Z & 0xFFFF) << 2) ^ (lOValue.Ceiling ? 1L : 0L);

            int liIndex;

            if (!pOMachine.SnapshotIndex.TryGetValue(liKey, out liIndex))
            {
                liIndex = pOMachine.Snapshots.Count;
                pOMachine.Snapshots.Add(new KeyValuePair<int, SlotValue>(piSlot, lOValue));
                pOMachine.SnapshotIndex[liKey] = liIndex;
            }

            return liIndex;
        }

        /// <summary>
        /// Builds the result: vertices in the order of their slot numbers (a slot with several
        /// values keeps them in the order of creation; unreferenced earlier values of a slot
        /// are dropped), positions in tiles (256 units = 1), faces with their colours from the
        /// model's colour record.
        /// </summary>
        private UW3DModel fBuildModel(int piModelIndex, Machine pOMachine)
        {
            UW3DModel lOModel = new UW3DModel();
            lOModel.Center = pOMachine.Center;
            lOModel.Extents = pOMachine.Extents;
            lOModel.UnknownNodeIds.AddRange(pOMachine.UnknownOpcodes);

            // which snapshots are used: referenced by a face, or the final value of their slot
            bool[] lbUsed = new bool[pOMachine.Snapshots.Count];

            foreach (RawFace lOFace in pOMachine.Faces)
                foreach (int liKey in lOFace.Keys)
                    lbUsed[liKey] = true;

            foreach (KeyValuePair<int, SlotValue> lOSlot in pOMachine.Slots)
                lbUsed[fSnapshot(pOMachine, lOSlot.Key)] = true;

            List<int> lOOrder = new List<int>();

            for (int liAt = 0; liAt < lbUsed.Length; liAt++)
                if (liAt < pOMachine.Snapshots.Count && lbUsed[liAt])
                    lOOrder.Add(liAt);

            // stable order by slot number (List.Sort is not stable, so compare the index too)
            lOOrder.Sort((a, b) =>
            {
                int liBySlot = pOMachine.Snapshots[a].Key.CompareTo(pOMachine.Snapshots[b].Key);
                return liBySlot != 0 ? liBySlot : a.CompareTo(b);
            });

            Dictionary<int, int> lODense = new Dictionary<int, int>();

            foreach (int liKey in lOOrder)
            {
                KeyValuePair<int, SlotValue> lOSnapshot = pOMachine.Snapshots[liKey];
                int liIndex = lOModel.Vertices.Count;
                lODense[liKey] = liIndex;
                lOModel.Vertices.Add(new UWVector3(lOSnapshot.Value.X / 256f, lOSnapshot.Value.Y / 256f, lOSnapshot.Value.Z / 256f));

                if (lOSnapshot.Value.Ceiling)
                    lOModel.CeilingVertexIndices.Add(liIndex);

                byte lyShade;

                if (pOMachine.Shades.TryGetValue(lOSnapshot.Key, out lyShade))
                    lOModel.VertexDarkValues[liIndex] = lyShade;
            }

            foreach (RawFace lORaw in pOMachine.Faces)
            {
                UW3DModelFace lOFace = new UW3DModelFace
                {
                    Uvs = lORaw.Uvs,
                    TextureNumber = lORaw.TextureNumber,
                    IsGouraud = lORaw.IsGouraud,
                    ShadeLevel = lORaw.ShadeLevel,
                    ColorIndex = fResolveSlotColor(piModelIndex, lORaw.ColorRegister),
                    AltColorIndex = fResolveSlotColor(piModelIndex, lORaw.ColorRegister + 1)
                };

                foreach (int liKey in lORaw.Keys)
                    lOFace.VertexIndices.Add(lODense[liKey]);

                lOModel.Faces.Add(lOFace);
            }

            return lOModel;
        }

        /// <summary>Palette index of a colour register of the model's colour record, with
        /// modulo wrap-around (also for register + 1 at the end of the record).
        /// Gouraud faces (D6) carry the outline/edge colour darkened via LIGHT.DAT
        /// (register + 1, AltColorIndex), all other faces the base colour - confirmed on the
        /// barrel and, with the record order from UW.EXE, on the chest (register 0 = brown body
        /// on the Gouraud faces, register 1 = dark outline).</summary>
        private byte fResolveSlotColor(int piModelIndex, int piRegister)
        {
            if (piModelIndex < 0 || piModelIndex >= ModelCount)
                return 0;

            byte[] laTable = mModelColors[piModelIndex];

            if (laTable == null || laTable.Length == 0)
                return 0;

            int liIndex = ((piRegister % laTable.Length) + laTable.Length) % laTable.Length;
            return laTable[liIndex];
        }

        private int fTarget(int piOperand)
        {
            return piOperand + 2 + fReadInt16(piOperand);
        }

        private ushort fReadUInt16(int piPos)
        {
            return piPos >= 0 && piPos + 1 < mData.Length ? (ushort)(mData[piPos] | (mData[piPos + 1] << 8)) : (ushort)0;
        }

        private short fReadInt16(int piPos)
        {
            return (short)fReadUInt16(piPos);
        }
    }
}
