using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Hosts of the player's vitals: a plain one that keeps what it is told, one that counts the
    /// calls, and the worn equipment of the regeneration checks.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>The player's surroundings for a tick, without any engine. Only the
        /// regeneration bits are interesting here; everything else is quiet.</summary>
        private sealed class FakeVitalsHost : IUWVitalsHost
        {
            public int Regeneration;

            public int FiveMinuteBlocks;

            /// <summary>Counts the five-minute blocks (an event of the vitals since cleanup step
            /// 3.6, 2026-10-17) and hands the vitals back.</summary>
            public UWPlayerVitals Watch(UWPlayerVitals pOVitals)
            {
                pOVitals.FiveMinuteBlockDue += () => FiveMinuteBlocks++;

                return pOVitals;
            }

            public int DungeonLevelIndex { get; set; }

            public int WornRegeneration => Regeneration;

            public bool IsMoving => false;

            public float MovementFraction => 0f;

            public bool IsInMotionState => false;

            public bool IsChargingAttack => false;

            /// <summary>The armour per body part a check sets (the damage pipeline, cleanup step 4.2).</summary>
            public int[] Armour = new int[4];

            public int GetArmourAt(int piPart) => piPart >= 0 && piPart < Armour.Length ? Armour[piPart] : 0;

            public int DamageTypeProof { get; set; }
        }

        /// <summary>The worn equipment for the status pass, without any engine: a real inventory
        /// model (the authority since cleanup step 3.6, 2026-10-17, when IUWEquipment went), filled
        /// slot by slot.</summary>
        private sealed class FakeEquipment
        {
            public readonly UWInventoryModel Model = new UWInventoryModel(() => null, () => null);

            public bool IsLeftHanded
            {
                get { return Model.IsLeftHanded; }
                set { Model.IsLeftHanded = value; }
            }

            public UWObject GetEquipped(UWArmorItemMap.BodySlot peSlot)
            {
                return Model.GetEquipped(peSlot);
            }

            public void Put(UWArmorItemMap.BodySlot peSlot, int piId, int piQuality)
            {
                Model.EquipSlots[(int)peSlot] = new UWObject((ushort)piId) { Quality = (ushort)piQuality };
            }

            /// <summary>An enchantment on the piece already in the slot: major and minor class
            /// sit in the quantity field, see UWArmourProtection.TryGetWearableEnchantment.
            /// </summary>
            public void Enchant(UWArmorItemMap.BodySlot peSlot, int piMajor, int piMinor)
            {
                UWObject lOItem = GetEquipped(peSlot);

                lOItem.IsEnchanted = true;
                lOItem.Quantity = (ushort)((piMajor << 4) | piMinor);
            }

            public void Enchant(UWArmorItemMap.BodySlot peSlot, int piId, int piMajor, int piMinor)
            {
                Put(peSlot, piId, UWPlayerCritterRow.MaxQuality);
                Enchant(peSlot, piMajor, piMinor);
            }
        }

        /// <summary>FakeVitalsHost with a count of the Lore resets.</summary>
        private sealed class CountingVitalsHost : IUWVitalsHost
        {
            public int Resets;

            public int StatusPasses;

            public int Shakes;

            public int LastShakeLevel = -1;

            /// <summary>Counts what the vitals tell (their events since cleanup step 3.6,
            /// 2026-10-17; callbacks of this host until then) and hands them back.</summary>
            public UWPlayerVitals Watch(UWPlayerVitals pOVitals)
            {
                pOVitals.CombatShakeDue += piLevel => { Shakes++; LastShakeLevel = piLevel; };
                pOVitals.StatusPassDue += () => StatusPasses++;
                pOVitals.IdentificationReset += () => Resets++;

                return pOVitals;
            }

            public int DungeonLevelIndex => 0;

            public int WornRegeneration => 0;

            public bool IsMoving => false;

            public float MovementFraction => 0f;

            public bool IsInMotionState { get; set; }

            public bool IsChargingAttack => false;

            /// <summary>The armour per body part a check sets (the damage pipeline, cleanup step 4.2).</summary>
            public int[] Armour = new int[4];

            public int GetArmourAt(int piPart) => piPart >= 0 && piPart < Armour.Length ? Armour[piPart] : 0;

            public int DamageTypeProof { get; set; }
        }
    }
}
