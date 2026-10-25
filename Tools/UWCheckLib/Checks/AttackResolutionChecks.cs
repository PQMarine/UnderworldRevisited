using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.9 (2026-10-18): UWAttackResolution, the one do_attack / do_damage / do_miss
    /// (UnderworldExhumed combat/COMBAT.C) behind the creature's blow, the player's melee and a
    /// missile on the player. Pinned two ways: against the chains written out by hand, seed by seed,
    /// so the numbers and the order of the draws are the same - the chains the three paths ran
    /// themselves until step 4.9 (UWCritterCombat's steps for the creature, the player's former
    /// ComputeDamage with its critical), in the original's order since cleanup step 4.9b
    /// (2026-10-18, by the user's decision: a creature's part before its check, the player's weapon
    /// worn before the dice); and over the rule itself - the draws in their order, a thing never missed, the protection for the
    /// player only, the wear between the critical and the dice, the blocked sounds, the same side
    /// spared, the blow each outcome lands as, the missile's request and the poison.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckAttackResolution()
        {
            Check.Group("Attack resolution (UWAttackResolution: do_attack, frp_check, do_damage, do_miss)");

            fCheckAttackAgainstOldChains();
            fCheckAttackDraws();
            fCheckAttackSounds();
            fCheckAttackBlows();
            fCheckAttackMissile();
            fCheckAttackPoison();
        }

        /// <summary>Seed by seed, the outcome equals the chain written out by hand - in the original's
        /// order since cleanup step 4.9b (2026-10-18), the old chains of each path until then.</summary>
        private static void fCheckAttackAgainstOldChains()
        {
            const int Score = 12;
            const int Base = 9;
            const int Charge = 200;
            const int Flank = 2;
            const int Defence = 14;
            const int Armour = 3;
            const int Protection = 4;
            const int Part = UWArmourProtection.PartLegs;

            UWAttackerStats lOPlayer = new UWAttackerStats(UWCritterBrain.PlayerIndex, Score, Base, Charge, Flank, 0, false);
            UWObjectClassProperties.Critter lORow = fChainRow();
            int liOtherParts = 0;
            int liCreatureDiffers = 0;
            int liPlayerDefenderDiffers = 0;
            int liMeleeDiffers = 0;
            int liObjectDiffers = 0;
            int liCriticals = 0;
            int liMisses = 0;
            int liBadMisses = 0;
            int liDoorWears = 0;

            for (int liSeed = 1; liSeed <= 400; liSeed++)
            {
                // THE CREATURE'S BLOW IN THE ORIGINAL'S ORDER (since cleanup step 4.9b, 2026-10-18, by
                // the user's decision; until then these two pinned the chains of UWCritter.fStrikeCreature
                // - the check before the ninth and the part - and fStrikePlayer - the part before the
                // strong bonus): the ninth (AI.C:1155), the strong bonus (critter_attack), pickloc
                // (resolve_attack), frp_check, do_damage - written out by hand, a strong and a plain
                // creature in turn, on a creature and on the player.
                bool lbStrong = (liSeed & 1) == 0;

                UWRandom.UseSeed(liSeed);

                fOriginalCreatureBlow(lORow, lbStrong, Charge, Flank, ChainOwnFeet, ChainOwnHeight, ChainDefenderFeet,
                    ChainDefenderTop, 0, Defence, Armour, out UWSkillCheck.ResultEnum leOld, out int liOldPart,
                    out int liOldRolled, out int liOldDamage);

                int liOldNext = UWRandom.Next(1000);

                UWRandom.UseSeed(liSeed);

                UWAttackOutcome lONew = UWAttackResolution.CreatureBlow(lORow, true, 0, lbStrong, 5, Charge, Flank,
                    ChainOwnFeet, ChainOwnHeight, new UWFixedDefender(UWDefenderKind.Creature, Defence, Armour, Protection),
                    ChainDefenderFeet, ChainDefenderTop, out UWAttackerStats _);

                if (lONew.Result != leOld || lONew.Part != liOldPart || lONew.Rolled != liOldRolled
                    || lONew.Damage != liOldDamage || UWRandom.Next(1000) != liOldNext)
                    liCreatureDiffers++;

                if (lONew.Critical)
                    liCriticals++;

                if (!lONew.Hit)
                    liMisses++;

                if (lONew.Part != Part)
                    liOtherParts++;

                // The creature on the player: his protection off the score, the rolled damage to the
                // pipeline.
                UWRandom.UseSeed(liSeed);

                fOriginalCreatureBlow(lORow, lbStrong, Charge, Flank, ChainOwnFeet, ChainOwnHeight, ChainDefenderFeet,
                    ChainDefenderTop, Protection, Defence, 0, out leOld, out liOldPart, out liOldRolled, out liOldDamage);

                liOldNext = UWRandom.Next(1000);

                UWRandom.UseSeed(liSeed);

                lONew = UWAttackResolution.CreatureBlow(lORow, true, 0, lbStrong, 5, Charge, Flank, ChainOwnFeet,
                    ChainOwnHeight, new UWFixedDefender(UWDefenderKind.Player, Defence, Armour, Protection),
                    ChainDefenderFeet, ChainDefenderTop, out UWAttackerStats _);

                if (lONew.Result != leOld || lONew.Part != liOldPart || lONew.Rolled != liOldRolled
                    || lONew.Damage != liOldRolled || UWRandom.Next(1000) != liOldNext)
                    liPlayerDefenderDiffers++;

                // THE PLAYER'S MELEE ON A CREATURE (UWPlayerAttack.ResolveMelee): Check(score + flank,
                // defence); a bad miss against a creature that does not spare weapons wears his weapon
                // by 2d3 there, in frp_check (COMBAT.C:316); a hit's critical on the raw base, the
                // minimum, the dice, the charge, the flank, the armour. The wear between the check and
                // the dice follows the original since cleanup step 4.9b (2026-10-18; after the dice
                // until then).
                UWRandom.UseSeed(liSeed);

                leOld = UWSkillCheck.Check(Score + Flank, Defence);
                liOldRolled = 0;
                liOldDamage = 0;

                if (leOld == UWSkillCheck.ResultEnum.CriticalFailure)
                    UWRandom.RollDice(UWEquipmentWear.CriticalFailureDiceCount, UWEquipmentWear.CriticalFailureDiceRange);

                if (UWSkillCheck.IsSuccess(leOld))
                {
                    int liBase = Base;

                    if (leOld == UWSkillCheck.ResultEnum.CriticalSuccess)
                        liBase *= (48 + UWRandom.Next(0, 32)) >> 5;

                    if (liBase < UWCombat.MinimumBaseDamage)
                        liBase = UWCombat.MinimumBaseDamage;

                    liOldRolled = ((UWCombat.RollDamage(liBase) * Charge) >> 7) + Flank;
                    liOldDamage = liOldRolled <= Armour ? 0 : liOldRolled - Armour;
                }

                liOldNext = UWRandom.Next(1000);

                UWRandom.UseSeed(liSeed);

                lONew = UWAttackResolution.PlayerBlow(lOPlayer, new UWFixedDefender(UWDefenderKind.Creature, Defence, Armour), Part,
                    -1, true, fChainWear);

                if (lONew.Result != leOld || lONew.Rolled != liOldRolled || lONew.Damage != liOldDamage
                    || UWRandom.Next(1000) != liOldNext)
                    liMeleeDiffers++;

                if (leOld == UWSkillCheck.ResultEnum.CriticalFailure)
                    liBadMisses++;

                // THE PLAYER'S MELEE ON A DOOR: no check, no critical, no armour; the door's roll and,
                // on it, the wear by 2d4 in frp_check (COMBAT.C:292) before do_damage's dice (since
                // cleanup step 4.9b, 2026-10-18; after the dice until then).
                UWRandom.UseSeed(liSeed);

                bool lbWorn = UWEquipmentWear.WearsWeaponOnDoor(ChainDoorId, UWRandom.Next(0, 0x8000));

                if (lbWorn)
                    UWRandom.RollDice(UWEquipmentWear.DoorDiceCount, UWEquipmentWear.DoorDiceRange);

                liOldRolled = ((UWCombat.RollDamage(Base) * Charge) >> 7) + Flank;
                liOldNext = UWRandom.Next(1000);

                UWRandom.UseSeed(liSeed);

                lONew = UWAttackResolution.PlayerBlow(lOPlayer, UWAttackResolution.ObjectDefender, UWAttackResolution.NoPart,
                    ChainDoorId, false, fChainWear);

                if (lONew.Result != UWSkillCheck.ResultEnum.Success || lONew.Rolled != liOldRolled
                    || lONew.Damage != liOldRolled || UWRandom.Next(1000) != liOldNext)
                    liObjectDiffers++;

                if (lbWorn)
                    liDoorWears++;
            }

            fExpectInt("creature on creature: 400 seeds in the original's order (ninth, strong, pickloc, check, dice)",
                liCreatureDiffers, 0);
            fExpectInt("creature on the player: 400 seeds in the original's order, the armour left to the pipeline",
                liPlayerDefenderDiffers, 0);
            fExpectBool("creature's blow: the seeds strike more than one part", liOtherParts > 0, true);
            fExpectInt("player's melee on a creature: 400 seeds, a bad miss's wear between the check and the dice",
                liMeleeDiffers, 0);
            fExpectInt("player's melee on a door: 400 seeds, always hit, the door's wear before the dice, no armour",
                liObjectDiffers, 0);
            fExpectBool("the seeds cover bad misses and doors that wear the weapon, and doors that do not",
                liBadMisses > 0 && liDoorWears > 0 && liDoorWears < 400, true);
            fExpectBool("the seeds cover criticals and misses", liCriticals > 0 && liMisses > 0, true);

            // The base before the minimum: a critical doubles 1 to 2, not the raised 2 to 4 (do_damage
            // raises it only after the multiplier). Score 40 is a critical at any roll; roll 16 doubles.
            UWAttackerStats lOWeak = new UWAttackerStats(UWCritterBrain.PlayerIndex, 40, 1, UWCombat.NeutralCharge, 0, 0, false);
            RecordingRandom lORandom = new RecordingRandom(0, 16, 5);

            UWRandom.Use(lORandom);

            try
            {
                UWAttackOutcome lOWeakHit = UWAttackResolution.Resolve(lOWeak, new UWFixedDefender(UWDefenderKind.Creature), 0);

                fExpectBool("critical: base 1 doubled is 2, one die of 2 sides", lOWeakHit.Critical && lOWeakHit.Rolled == 2, true);
                Check.Str("critical: the draws - the check, the multiplier, one die of 2", string.Join(" ", lORandom.Ranges),
                    "31 32 1..3");
            }
            finally
            {
                UWRandom.Use(null);
            }
        }

        /// <summary>The attacker's z and height and the defender's z and top for the chains: the probe
        /// of the three ninths has its middle at 10, 20 and 30 against a defender of 0 to 24, so
        /// pickloc is drawn for two of them and certain (the head) for the third.</summary>
        private const int ChainOwnFeet = 0;

        private const int ChainOwnHeight = 30;

        private const int ChainDefenderFeet = 0;

        private const int ChainDefenderTop = 24;

        /// <summary>A door whose id &amp; 7 is 3: it wears the weapon when the roll * 12 / 0x8000 is
        /// below 6, half the time.</summary>
        private const int ChainDoorId = UWObjectIds.FirstDoor + 3;

        /// <summary>The player's weapon worn in the chains: the dice drawn, nothing worn.</summary>
        private static void fChainWear(int piDiceCount, int piDiceRange)
        {
            UWRandom.RollDice(piDiceCount, piDiceRange);
        }

        /// <summary>A creature row for the chains: attack 0 to-hit 5 and damage 4, the signed byte
        /// 0x11 -3 and strength 12 - a score of 3 and a damage of 6 before the strong bonus.</summary>
        private static UWObjectClassProperties.Critter fChainRow()
        {
            byte[] lyRow = new byte[48];

            lyRow[5] = 12;
            lyRow[0x11] = 0xFD;
            lyRow[0x13] = 5;
            lyRow[0x14] = 4;

            return new UWObjectClassProperties.Critter { Row = lyRow };
        }

        /// <summary>
        /// A creature's blow written out in the original's order, draw by draw (cleanup step 4.9b,
        /// 2026-10-18): the ninth, rand() % 9 (UnderworldExhumed critter/AI.C:1155), the probe from
        /// it; the strong creature's 7 + rand() % 6 to the score, then 4 + rand() % 12 to the damage
        /// (critter_attack, combat/COMBAT.C:802); pickloc of the defender against the probe
        /// (resolve_attack, COMBAT.C:260); frp_check's roll with the player's protection, its
        /// critical multiplier; do_damage's dice, the charge, the flank, a creature's armour.
        /// </summary>
        private static void fOriginalCreatureBlow(UWObjectClassProperties.Critter pORow, bool pbStrong, int piCharge, int piFlank,
            int piOwnFeet, int piOwnHeight, int piDefenderFeet, int piDefenderTop, int piProtection, int piDefence, int piArmour,
            out UWSkillCheck.ResultEnum peResult, out int piPart, out int piRolled, out int piDamage)
        {
            int liNinth = UWRandom.Next(0, UWCritterCombat.CritterBlowNinths);

            UWCritterCombat.GetBlowProbe(piOwnFeet, piOwnHeight, liNinth, UWCritterCombat.CritterBlowReach,
                out int liProbeBottom, out int liProbeTop);

            int liScore = UWCritterCombat.GetAttackScore(pORow, 0);
            int liBase = UWCritterCombat.GetAttackDamage(pORow, 0);

            if (pbStrong)
            {
                liScore += UWCritterCombat.GetStrongScoreBonus();
                liBase += UWCritterCombat.GetStrongDamageBonus();
            }

            piPart = UWArmourProtection.PickBodyPart(piDefenderFeet, piDefenderTop, liProbeBottom, liProbeTop);
            peResult = UWCritterCombat.ResolveHit(liScore, piFlank, piProtection, piDefence);
            piRolled = 0;
            piDamage = 0;

            if (!UWSkillCheck.IsSuccess(peResult))
                return;

            if (peResult == UWSkillCheck.ResultEnum.CriticalSuccess)
                liBase *= UWCritterCombat.GetCriticalMultiplier();

            piRolled = UWCritterCombat.ScaleByCharge(UWCritterCombat.RollDamage(liBase), piCharge, piFlank);
            piDamage = UWCritterCombat.SubtractArmour(piRolled, piArmour);
        }

        /// <summary>The draws and their order: the check, the critical, the player's wear, the dice.</summary>
        private static void fCheckAttackDraws()
        {
            UWAttackerStats lOAttacker = new UWAttackerStats(5, 30, 7, UWCombat.NeutralCharge, 0, 0, false);

            // A critical on the player: the check (31), the multiplier (32), the wear (asked between),
            // then the dice of 7: one six and one of 1.
            RecordingRandom lORandom = new RecordingRandom(0, 16, 0, 0);
            WearRecordingDefender lOPlayer = new WearRecordingDefender(lORandom);

            UWRandom.Use(lORandom);

            try
            {
                UWAttackOutcome lOOutcome = UWAttackResolution.Resolve(lOAttacker, lOPlayer, UWArmourProtection.PartHead);

                fExpectBool("player: a critical", lOOutcome.Critical, true);
                Check.Str("player: the draws - check, multiplier, dice of 14", string.Join(" ", lORandom.Ranges),
                    "31 32 1..7 1..7 1..3");
                fExpectInt("player: the piece wears once, after the check and the multiplier, before the dice",
                    lOPlayer.DrawsBeforeWear, 2);
                fExpectInt("player: ... the piece of the part struck", lOPlayer.WornPart, UWArmourProtection.PartHead);
            }
            finally
            {
                UWRandom.Use(null);
            }

            // The same critical on a creature: nothing wears.
            lORandom = new RecordingRandom(0, 16, 0, 0);

            UWFixedDefender lOCreature = new UWFixedDefender(UWDefenderKind.Creature);

            UWRandom.Use(lORandom);

            try
            {
                UWAttackResolution.Resolve(lOAttacker, lOCreature, UWArmourProtection.PartHead);
                fExpectInt("creature: a critical wears nothing", lOCreature.CriticalHits, 0);
            }
            finally
            {
                UWRandom.Use(null);
            }

            // A thing: the dice only - no check, no critical.
            lORandom = new RecordingRandom();
            UWRandom.Use(lORandom);

            try
            {
                UWAttackResolution.Resolve(lOAttacker, UWAttackResolution.ObjectDefender, UWAttackResolution.NoPart);
                Check.Str("thing: the dice alone", string.Join(" ", lORandom.Ranges), "1..7 1..2");
            }
            finally
            {
                UWRandom.Use(null);
            }

            // A miss draws the check alone.
            lORandom = new RecordingRandom(0);
            UWRandom.Use(lORandom);

            try
            {
                UWAttackOutcome lOMiss = UWAttackResolution.Resolve(new UWAttackerStats(5, 0, 7, 128, 0, 0, false),
                    new UWFixedDefender(UWDefenderKind.Creature, 20), 0);

                fExpectBool("miss: nothing rolled, nothing lands", !lOMiss.Hit && lOMiss.Rolled == 0 && lOMiss.Damage == 0, true);
                Check.Str("miss: the check alone", string.Join(" ", lORandom.Ranges), "31");
            }
            finally
            {
                UWRandom.Use(null);
            }

            // The protection: the player's only (cmbModTH). Score 16 + roll 0 against defence 0 is a
            // hit; protection 1 makes it a miss for the player, not for a creature.
            UWAttackerStats lOEdge = new UWAttackerStats(5, 16, 7, 128, 0, 0, false);

            UWRandom.Use(new RecordingRandom(0));

            try
            {
                fExpectBool("protection: comes off the score against the player",
                    UWAttackResolution.Check(lOEdge, new UWFixedDefender(UWDefenderKind.Player, 0, 0, 1), 0)
                        == UWSkillCheck.ResultEnum.Failure, true);
            }
            finally
            {
                UWRandom.Use(null);
            }

            UWRandom.Use(new RecordingRandom(0));

            try
            {
                fExpectBool("protection: a creature has none",
                    UWAttackResolution.Check(lOEdge, new UWFixedDefender(UWDefenderKind.Creature, 0, 0, 1), 0)
                        == UWSkillCheck.ResultEnum.Success, true);
            }
            finally
            {
                UWRandom.Use(null);
            }

            // A strong creature's blow in the original's order (cleanup step 4.9b, 2026-10-18): the
            // ninth 3 (the probe's middle at 20 against a body of 0 to 24 - pickloc draws a third),
            // the strong score's six, the strong damage's twelve, pickloc, then the check alone (a
            // miss against defence 30).
            lORandom = new RecordingRandom(3, 0, 0, 0, 0);
            UWRandom.Use(lORandom);

            try
            {
                UWAttackOutcome lOBlow = UWAttackResolution.CreatureBlow(fChainRow(), true, 0, true, 5, 128, 0, ChainOwnFeet,
                    ChainOwnHeight, new UWFixedDefender(UWDefenderKind.Player, 30), ChainDefenderFeet, ChainDefenderTop,
                    out UWAttackerStats lOStrongAttacker);

                Check.Str("creature's blow: the draws - ninth, strong score, strong damage, pickloc, check",
                    string.Join(" ", lORandom.Ranges), "0..9 6 12 0..3 31");
                fExpectBool("creature's blow: the head struck, the strong score and damage in the attacker",
                    lOBlow.Part == UWArmourProtection.PartHead && lOStrongAttacker.Score == 10 && lOStrongAttacker.BaseDamage == 10
                        && !lOBlow.Hit, true);
            }
            finally
            {
                UWRandom.Use(null);
            }

            // The player's weapon: a thing that is no door neither draws nor wears; a creature that
            // spares weapons does not wear it on a bad miss, nor does a plain miss.
            lORandom = new RecordingRandom();
            UWRandom.Use(lORandom);

            try
            {
                fExpectBool("player's weapon: a thing that is no door neither draws nor wears",
                    !UWAttackResolution.WearsPlayersWeapon(UWDefenderKind.Object, UWObjectIds.FirstDoor - 1,
                        UWSkillCheck.ResultEnum.Success, false, out int _, out int _) && lORandom.Ranges.Count == 0, true);
                fExpectBool("player's weapon: a creature that spares it, or a plain miss, does not wear it",
                    !UWAttackResolution.WearsPlayersWeapon(UWDefenderKind.Creature, -1, UWSkillCheck.ResultEnum.CriticalFailure,
                        false, out int _, out int _)
                    && !UWAttackResolution.WearsPlayersWeapon(UWDefenderKind.Creature, -1, UWSkillCheck.ResultEnum.Failure,
                        true, out int _, out int _), true);
            }
            finally
            {
                UWRandom.Use(null);
            }

            // Check, then Land, is Resolve.
            int liSplitDiffers = 0;

            for (int liSeed = 1; liSeed <= 100; liSeed++)
            {
                UWFixedDefender lODefender = new UWFixedDefender(UWDefenderKind.Creature, 10, 2);

                UWRandom.UseSeed(liSeed);

                UWAttackOutcome lOWhole = UWAttackResolution.Resolve(lOAttacker, lODefender, 1);

                UWRandom.UseSeed(liSeed);

                UWAttackOutcome lOSplit = UWAttackResolution.Land(lOAttacker, lODefender, 1,
                    UWAttackResolution.Check(lOAttacker, lODefender, UWAttackResolution.NoPart));

                if (lOWhole.Result != lOSplit.Result || lOWhole.Rolled != lOSplit.Rolled || lOWhole.Damage != lOSplit.Damage)
                    liSplitDiffers++;
            }

            fExpectInt("Check then Land: 100 seeds equal to Resolve", liSplitDiffers, 0);

            // The creature's own numbers: no row, 0 and 3 and no draw; a strong one draws 6, then 12.
            RecordingRandom lOStrong = new RecordingRandom(5, 11);

            UWRandom.Use(lOStrong);

            try
            {
                UWAttackerStats lONoRow = UWAttackerStats.Creature(default, false, 0, true, 7, 128, 0);

                fExpectBool("creature without a row: score 0, damage 3, no draw",
                    lONoRow.Score == 0 && lONoRow.BaseDamage == UWAttackerStats.NoRowDamage && lOStrong.Ranges.Count == 0, true);

                byte[] lyRow = new byte[48];

                lyRow[5] = 12;
                lyRow[0x11] = 0xFD;
                lyRow[0x13] = 5;
                lyRow[0x14] = 4;

                UWAttackerStats lORowed = UWAttackerStats.Creature(new UWObjectClassProperties.Critter { Row = lyRow }, true, 0, true,
                    7, 128, 0);

                fExpectBool("strong creature: score 3 + 12, damage 6 + 15",
                    lORowed.Score == 15 && lORowed.BaseDamage == 21, true);
                Check.Str("strong creature: the score's roll, then the damage's", string.Join(" ", lOStrong.Ranges), "6 12");
            }
            finally
            {
                UWRandom.Use(null);
            }
        }

        /// <summary>do_miss: 7 for a metal weapon on metal, else 8; the player's piece, a creature
        /// as target the attacker's own row (the original's slip).</summary>
        private static void fCheckAttackSounds()
        {
            UWAttackerStats lOMetal = new UWAttackerStats(5, 0, 7, 128, 0, UWCritterCombat.MetalKind, true);
            UWAttackerStats lOMetalLeather = new UWAttackerStats(5, 0, 7, 128, 0, UWCritterCombat.MetalKind, false);
            UWAttackerStats lOClaw = new UWAttackerStats(5, 0, 7, 128, 0, 0, true);

            fExpectInt("blocked: metal weapon on the player's metal piece is 7",
                UWAttackResolution.BlockedSound(lOMetal, new UWFixedDefender(UWDefenderKind.Player, 0, 0, 0, true), 0),
                UWCritterCombat.BlockedMetalSound);
            fExpectInt("blocked: ... on his leather or nothing is 8",
                UWAttackResolution.BlockedSound(lOMetal, new UWFixedDefender(UWDefenderKind.Player, 0, 0, 0, false), 0),
                UWCritterCombat.BlockedSound);
            fExpectInt("blocked: a creature as target takes the ATTACKER's armour kind (metal: 7)",
                UWAttackResolution.BlockedSound(lOMetal, new UWFixedDefender(UWDefenderKind.Creature, 0, 0, 0, false), 0),
                UWCritterCombat.BlockedMetalSound);
            fExpectInt("blocked: ... the attacker's leather gives 8 whatever the target wears",
                UWAttackResolution.BlockedSound(lOMetalLeather, new UWFixedDefender(UWDefenderKind.Creature, 0, 0, 0, true), 0),
                UWCritterCombat.BlockedSound);
            fExpectInt("blocked: a weapon that is no metal is 8",
                UWAttackResolution.BlockedSound(lOClaw, new UWFixedDefender(UWDefenderKind.Player, 0, 0, 0, true), 0),
                UWCritterCombat.BlockedSound);
            fExpectInt("blocked: a thing is never missed", UWAttackResolution.BlockedSound(lOMetal,
                UWAttackResolution.ObjectDefender, UWAttackResolution.NoPart), UWAttackResolution.NoSound);

            UWRandom.Use(new RecordingRandom(0));

            try
            {
                UWAttackOutcome lOMiss = UWAttackResolution.Resolve(lOMetal, new UWFixedDefender(UWDefenderKind.Player, 30, 0, 0, true), 0);

                fExpectInt("miss: the outcome carries the blocked sound", lOMiss.BlockedSound, UWCritterCombat.BlockedMetalSound);
            }
            finally
            {
                UWRandom.Use(null);
            }

            UWRandom.UseSeed(4);

            UWAttackOutcome lOHit = UWAttackResolution.Resolve(lOMetal, UWAttackResolution.ObjectDefender, UWAttackResolution.NoPart);

            fExpectBool("hit: no blocked sound, the volume four times the damage before the armour",
                lOHit.BlockedSound == UWAttackResolution.NoSound && lOHit.HitVolume == lOHit.Rolled << 2, true);

            fExpectBool("same side: a creature spares a creature of its side",
                UWAttackResolution.SparesSameSide(false, UWDefenderKind.Creature, true, true), true);
            fExpectBool("same side: ... and strikes the other side",
                UWAttackResolution.SparesSameSide(false, UWDefenderKind.Creature, false, true), false);
            fExpectBool("same side: the player spares nobody",
                UWAttackResolution.SparesSameSide(true, UWDefenderKind.Creature, true, true), false);
            fExpectBool("same side: nobody spares the player",
                UWAttackResolution.SparesSameSide(false, UWDefenderKind.Player, true, true), false);
        }

        /// <summary>How each outcome lands through IUWWorld.Damage.</summary>
        private static void fCheckAttackBlows()
        {
            UWAttackerStats lOCreature = new UWAttackerStats(9, 40, 7, 128, 0, 0, false);
            UWAttackerStats lOPlayer = new UWAttackerStats(UWCritterBrain.PlayerIndex, 40, 7, 128, 0, 0, false);

            UWRandom.UseSeed(11);

            UWAttackOutcome lOOnCreature = UWAttackResolution.Resolve(lOCreature, new UWFixedDefender(UWDefenderKind.Creature, 0, 2), 0);
            UWBlow lOBlow = lOOnCreature.Blow;

            fExpectBool("creature's blow: CreatureBlow with the damage after the armour, charged to it",
                lOBlow.Source == UWBlowSource.CreatureBlow && lOBlow.Amount == lOOnCreature.Damage && lOBlow.Attacker == 9
                    && lOOnCreature.Damage == UWCritterCombat.SubtractArmour(lOOnCreature.Rolled, 2), true);

            UWAttackOutcome lOOnThing = UWAttackResolution.Resolve(lOPlayer, UWAttackResolution.ObjectDefender, UWAttackResolution.NoPart);

            lOBlow = lOOnThing.Blow;
            fExpectBool("player's blow on a thing: PlayerBlow, the roll whole",
                lOBlow.Source == UWBlowSource.PlayerBlow && lOBlow.Amount == lOOnThing.Rolled
                    && lOBlow.Attacker == UWCritterBrain.PlayerIndex, true);

            UWRandom.Use(new RecordingRandom(0));

            try
            {
                UWAttackOutcome lOMiss = UWAttackResolution.Resolve(new UWAttackerStats(UWCritterBrain.PlayerIndex, 0, 7, 128, 0, 0,
                    false), new UWFixedDefender(UWDefenderKind.Creature, 30), 0);

                fExpectBool("player's miss: PlayerBlow with 0, so the creature records him",
                    lOMiss.Blow.Source == UWBlowSource.PlayerBlow && lOMiss.Blow.Amount == 0, true);
            }
            finally
            {
                UWRandom.Use(null);
            }

            // Critical on the player: score 40 at roll 0 is 40, a critical.
            UWRandom.Use(new RecordingRandom(0, 16, 0, 0));

            try
            {
                UWAttackOutcome lOOnPlayer = UWAttackResolution.Resolve(lOCreature,
                    new UWFixedDefender(UWDefenderKind.Player, 0, 5), UWArmourProtection.PartBody);
                UWDamageRequest lORequest = lOOnPlayer.Blow.PlayerRequest;

                fExpectBool("blow on the player: his request whole, the armour left to the pipeline",
                    lOOnPlayer.Blow.Source == UWBlowSource.Player && lORequest.Source == UWDamageSource.CreatureBlow
                        && lORequest.Rule == UWDamageRule.Hit && lORequest.Amount == lOOnPlayer.Rolled
                        && lOOnPlayer.Damage == lOOnPlayer.Rolled && lORequest.SubtractArmour && lORequest.HalveOnEasy
                        && lORequest.BodyPart == UWArmourProtection.PartBody, true);
                fExpectInt("blow on the player: a critical flashes red", lORequest.FlashColour, UWDamageRequest.CriticalFlashColour);
            }
            finally
            {
                UWRandom.Use(null);
            }

            UWRandom.Use(new RecordingRandom(0));

            try
            {
                UWAttackOutcome lOMissPlayer = UWAttackResolution.Resolve(new UWAttackerStats(9, 0, 7, 128, 0, 0, false),
                    new UWFixedDefender(UWDefenderKind.Player, 30), UWArmourProtection.PartBody);

                fExpectBool("miss on the player: the miss's request, which tells the music",
                    lOMissPlayer.Blow.PlayerRequest.Rule == UWDamageRule.Miss && lOMissPlayer.Blow.PlayerRequest.TellsMusic, true);
            }
            finally
            {
                UWRandom.Use(null);
            }
        }

        /// <summary>missile_thwack: no check; do_damage's dice over the base at the hit (since cleanup
        /// step 4.9b, 2026-10-18; at the launch until then); the player's request, a creature's
        /// armour, a thing's whole.</summary>
        private static void fCheckAttackMissile()
        {
            // Base 9 is 1d6 + 1d3: the values 5 and 2 roll 6 and 3, nine in all.
            RecordingRandom lORandom = new RecordingRandom(5, 2);

            UWRandom.Use(lORandom);

            try
            {
                UWAttackOutcome lOOnPlayer = UWAttackResolution.Missile(9, UWDamageTypes.Missile, 6,
                    new UWFixedDefender(UWDefenderKind.Player, 0, 2), UWArmourProtection.PartLegs, 6);
                UWDamageRequest lORequest = lOOnPlayer.Blow.PlayerRequest;

                Check.Str("missile: the dice of base 9 at the hit, nothing else", string.Join(" ", lORandom.Ranges),
                    "1..7 1..4");
                fExpectBool("missile on the player: the missile's request with the part, the volume from 9",
                    lOOnPlayer.Blow.Source == UWBlowSource.Player && lORequest.Source == UWDamageSource.Missile
                        && lORequest.Amount == 9 && lORequest.BodyPart == UWArmourProtection.PartLegs
                        && lORequest.Type == UWDamageTypes.Missile && lORequest.TellsMusic && lORequest.Shakes
                        && lOOnPlayer.HitVolume == 36, true);

                FakeVitalsHost lOArmoured = new FakeVitalsHost();

                lOArmoured.Armour[UWArmourProtection.PartLegs] = 2;

                UWDamageOutcome lOLanded = UWDamagePipeline.Apply(fPipelineVitals(lOArmoured, null), lORequest);

                fExpectInt("missile on the player: the pipeline takes the armour off", lOLanded.Applied, 7);

                UWRandom.Use(new RecordingRandom(3));

                UWAttackOutcome lOOwn = UWAttackResolution.Missile(4, UWDamageTypes.Missile, UWCritterBrain.PlayerIndex,
                    new UWFixedDefender(UWDefenderKind.Player), UWArmourProtection.PartBody, UWMissileHitRules.PlayerOwner);

                // damage_critter's who 1 (critter/AI.C:1502-1508): his own missile tells the music
                // as the player's (per user on the original, 2026-10-19; silent until then).
                fExpectBool("own missile: shakes, the music hears it as his own",
                    lOOwn.Blow.PlayerRequest.Shakes && lOOwn.Blow.PlayerRequest.Music == UWDamageMusic.Player, true);

                UWRandom.Use(new RecordingRandom(5, 2));

                UWAttackOutcome lOOnCreature = UWAttackResolution.Missile(9, UWDamageTypes.Missile, 6,
                    new UWFixedDefender(UWDefenderKind.Creature, 0, 4), UWArmourProtection.PartLegs, 6);

                fExpectBool("missile on a creature: less its armour, charged to the launcher",
                    lOOnCreature.Damage == 5 && lOOnCreature.Blow.Source == UWBlowSource.Missile
                        && lOOnCreature.Blow.Attacker == 6 && lOOnCreature.Blow.Amount == 5, true);

                UWRandom.Use(new RecordingRandom(5, 2));

                UWAttackOutcome lOOnThing = UWAttackResolution.Missile(9, UWDamageTypes.Missile, 6,
                    UWAttackResolution.ObjectDefender, UWAttackResolution.NoPart, 6);

                fExpectInt("missile on a thing: whole", lOOnThing.Damage, 9);

                // A flight without damage of its own (the boulder, a thing knocked loose, a throw of
                // what is no missile): no dice, 0 - as the 0 it carried before step 4.9b.
                lORandom = new RecordingRandom();
                UWRandom.Use(lORandom);

                UWAttackOutcome lONone = UWAttackResolution.Missile(UWAttackResolution.NoMissileDamage, UWDamageTypes.Missile, 6,
                    new UWFixedDefender(UWDefenderKind.Creature), UWArmourProtection.PartLegs, 6);

                fExpectBool("missile without damage: no dice, nothing rolled, nothing lands",
                    lORandom.Ranges.Count == 0 && lONone.Rolled == 0 && lONone.Damage == 0, true);
            }
            finally
            {
                UWRandom.Use(null);
            }

            // THE DICE MOVED, NOT CHANGED: seed by seed the hit's roll equals what the launches rolled
            // until cleanup step 4.9b - UWCombat.ComputeDamage with charge 128 for the player's shot,
            // throw and spell and a trap's bolt, UWCritterCombat.RollMissileDamage for a creature's -
            // over bases 0 (the minimum 2) to 39.
            int liDiffers = 0;

            for (int liSeed = 1; liSeed <= 400; liSeed++)
            {
                int liBase = liSeed % 40;

                UWRandom.UseSeed(liSeed);

                int liLaunch = UWCombat.ComputeDamage(liBase, false, 0, 0, UWCombat.NeutralCharge, 0);
                int liLaunchNext = UWRandom.Next(1000);

                UWRandom.UseSeed(liSeed);

                int liCreature = UWCritterCombat.RollMissileDamage(liBase);

                UWRandom.UseSeed(liSeed);

                UWAttackOutcome lOHit = UWAttackResolution.Missile(liBase, UWDamageTypes.Missile, 6,
                    UWAttackResolution.ObjectDefender, UWAttackResolution.NoPart, 6);

                if (lOHit.Rolled != liLaunch || lOHit.Rolled != liCreature || UWRandom.Next(1000) != liLaunchNext)
                    liDiffers++;
            }

            fExpectInt("missile: 400 seeds, the hit's dice as the launches rolled them", liDiffers, 0);
        }

        /// <summary>critter_attack's poison after a hit on the player.</summary>
        private static void fCheckAttackPoison()
        {
            UWAttackerStats lOPoisoner = new UWAttackerStats(5, 40, 7, 128, 0, 0, false, 0x14);
            UWAttackOutcome lOHit = new UWAttackOutcome(UWSkillCheck.ResultEnum.Success, UWDefenderKind.Player, 0, 3, 3,
                UWAttackResolution.NoSound, 5, false, UWDamageTypes.Physical, UWDamageRequest.CreatureBlow(3, 0, false));
            UWAttackOutcome lOSwallowed = new UWAttackOutcome(UWSkillCheck.ResultEnum.Success, UWDefenderKind.Player, 0, 0, 0,
                UWAttackResolution.NoSound, 5, false, UWDamageTypes.Physical, UWDamageRequest.CreatureBlow(0, 0, false));
            UWAttackOutcome lOMiss = new UWAttackOutcome(UWSkillCheck.ResultEnum.Failure, UWDefenderKind.Player, 0, 0, 0, 8, 5,
                false, UWDamageTypes.Physical, UWDamageRequest.CreatureMiss());
            UWAttackOutcome lOOnCreature = new UWAttackOutcome(UWSkillCheck.ResultEnum.Success, UWDefenderKind.Creature, 0, 3, 3,
                UWAttackResolution.NoSound, 5, false, UWDamageTypes.Physical, default);

            fExpectInt("poison: a hit poisons with byte 0x14 & 0xF", UWAttackResolution.PoisonNibble(lOPoisoner, lOHit, 0, 0), 4);
            fExpectInt("poison: ... even one the armour swallowed", UWAttackResolution.PoisonNibble(lOPoisoner, lOSwallowed, 0, 0), 4);
            fExpectInt("poison: a miss does not", UWAttackResolution.PoisonNibble(lOPoisoner, lOMiss, 0, 0), -1);
            fExpectInt("poison: not at or above the byte", UWAttackResolution.PoisonNibble(lOPoisoner, lOHit, 0x14, 0), -1);
            fExpectInt("poison: Poison Resistance rules it out",
                UWAttackResolution.PoisonNibble(lOPoisoner, lOHit, 0, UWDamageTypes.Poison), -1);
            fExpectInt("poison: a creature as target is never poisoned",
                UWAttackResolution.PoisonNibble(lOPoisoner, lOOnCreature, 0, 0), -1);
            fExpectInt("poison: an attacker without a byte does not",
                UWAttackResolution.PoisonNibble(new UWAttackerStats(5, 40, 7, 128, 0, 0, false), lOHit, 0, 0), -1);
        }
    }
}
