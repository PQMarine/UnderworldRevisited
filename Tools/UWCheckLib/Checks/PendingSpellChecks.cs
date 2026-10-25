using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.14 (2026-10-22): the spell waiting for its target as UWPendingSpellState -
    /// a projectile for the direction, a target spell for the object, the mana paid only on the
    /// release, Escape letting both go - and B17's one rule with its per-scheme policy (decided per
    /// user the same day): a click into the classic inventory consumes a waiting target spell even
    /// on no item (the original), the modern bags resolve it only on an item. Data-free.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckPendingSpell()
        {
            Check.Group("Pending spell (UWPendingSpellState - cleanup step 5.14, B17)");

            UWPendingSpellState lOState = new UWPendingSpellState();

            Check.Bool("pending spell: nothing waits at first", lOState.IsTargeting, false);
            Check.Bool("pending spell: no target spell at first", lOState.IsTargetSpellPending, false);

            // A projectile: waits for the direction, pays its mana only when taken on the release.
            lOState.BeginProjectile(0x13, 28, 5, 6);
            Check.Bool("projectile: waits", lOState.IsTargeting, true);
            Check.Bool("projectile: is no target spell", lOState.IsTargetSpellPending, false);
            Check.Int("projectile: its object", lOState.ProjectileId, 0x13);
            Check.Int("projectile: its base damage", lOState.ProjectileBaseDamage, 28);
            Check.Int("projectile: its speed byte", lOState.ProjectileSpeedByte, 5);
            Check.Int("projectile: the mana to pay", lOState.ManaCost, 6);
            Check.Int("projectile: the release pays the mana once", lOState.TakeManaCost(), 6);
            Check.Int("projectile: and then nothing", lOState.TakeManaCost(), 0);
            lOState.ForgetProjectile();
            Check.Bool("projectile: gone after the launch", lOState.IsTargeting, false);

            // A projectile that found no room keeps its unpaid mana until the next cast replaces it.
            lOState.BeginProjectile(0x13, 28, 5, 6);
            lOState.ForgetProjectile();
            Check.Int("projectile: no room, nothing paid", lOState.ManaCost, 6);

            // A projectile does not count in the inventory.
            Check.Int("projectile: an inventory click leaves the inventory alone",
                (int)UWPendingSpellState.DecideInventoryClick(UWPendingSpellState.InventoryPolicy.Classic, false, true),
                (int)UWPendingSpellState.InventoryClickOutcome.NotWaiting);

            // Escape lets everything go.
            lOState.BeginProjectile(0x13, 28, 5, 6);
            lOState.BeginTarget(11, 3);
            lOState.Cancel();
            Check.Bool("Escape: nothing waits", lOState.IsTargeting, false);
            Check.Int("Escape: no mana left to pay", lOState.ManaCost, 0);
            Check.Int("Escape: no target class", lOState.TargetMajor, UWPendingSpellState.None);

            // B17: the rule.
            Check.Int("B17: classic, item clicked: cast on it",
                (int)UWPendingSpellState.DecideInventoryClick(UWPendingSpellState.InventoryPolicy.Classic, true, true),
                (int)UWPendingSpellState.InventoryClickOutcome.CastOnItem);
            Check.Int("B17: classic, no item: the spell is lost",
                (int)UWPendingSpellState.DecideInventoryClick(UWPendingSpellState.InventoryPolicy.Classic, true, false),
                (int)UWPendingSpellState.InventoryClickOutcome.Lost);
            Check.Int("B17: modern, item clicked: cast on it",
                (int)UWPendingSpellState.DecideInventoryClick(UWPendingSpellState.InventoryPolicy.Modern, true, true),
                (int)UWPendingSpellState.InventoryClickOutcome.CastOnItem);
            Check.Int("B17: modern, no item: it keeps waiting",
                (int)UWPendingSpellState.DecideInventoryClick(UWPendingSpellState.InventoryPolicy.Modern, true, false),
                (int)UWPendingSpellState.InventoryClickOutcome.KeepsWaiting);

            // B17 applied to the state: Name Enchantment (class 11, minor 3 here) on a pack item.
            lOState.BeginTarget(11, 3);
            Check.Int("B17 classic: empty click consumes",
                (int)lOState.ResolveInventoryClick(UWPendingSpellState.InventoryPolicy.Classic, false, out int liMinor, out int liMajor),
                (int)UWPendingSpellState.InventoryClickOutcome.Lost);
            Check.Bool("B17 classic: nothing waits afterwards", lOState.IsTargetSpellPending, false);
            Check.Int("B17 classic: the lost spell's class comes out", liMajor, 11);

            lOState.BeginTarget(11, 3);
            Check.Int("B17 modern: empty click waits",
                (int)lOState.ResolveInventoryClick(UWPendingSpellState.InventoryPolicy.Modern, false, out liMinor, out liMajor),
                (int)UWPendingSpellState.InventoryClickOutcome.KeepsWaiting);
            Check.Bool("B17 modern: still waiting", lOState.IsTargetSpellPending, true);
            Check.Int("B17 modern: no class comes out", liMajor, UWPendingSpellState.None);
            Check.Int("B17 modern: item click casts",
                (int)lOState.ResolveInventoryClick(UWPendingSpellState.InventoryPolicy.Modern, true, out liMinor, out liMajor),
                (int)UWPendingSpellState.InventoryClickOutcome.CastOnItem);
            Check.Int("B17 modern: the spell's minor class", liMinor, 3);
            Check.Int("B17 modern: the spell's major class", liMajor, 11);
            Check.Bool("B17 modern: spent", lOState.IsTargetSpellPending, false);

            // A target spell taken in the world (in reach) is spent the same way.
            lOState.BeginTarget(7, 1);
            Check.Bool("target spell: waits for an object", lOState.IsTargetSpellPending, true);
            Check.Bool("target spell: taken", lOState.TakeTarget(out liMinor, out liMajor), true);
            Check.Int("target spell: its major class", liMajor, 7);
            Check.Bool("target spell: none left to take", lOState.TakeTarget(out liMinor, out liMajor), false);
        }
    }
}
