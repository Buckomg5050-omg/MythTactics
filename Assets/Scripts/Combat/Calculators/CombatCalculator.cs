// CombatCalculator.cs
using UnityEngine;

namespace MythTactics.Combat
{
    public static class CombatCalculator
    {
        // Constants for Hit Chance Calculation (GDD 2.3)
        public const int UNARMED_BASE_ACCURACY = 65;
        public const int MIN_HIT_CHANCE = 5;
        public const int MAX_HIT_CHANCE = 95;

        // NEW: Constants for Critical Hit Calculation (GDD 3.2)
        public const int BASE_CRIT_CHANCE = 5; // Universal base crit chance
        public const int MIN_CRIT_CHANCE = 0;  // Minimum possible crit chance
        public const int MAX_CRIT_CHANCE = 100; // Maximum possible crit chance (can be adjusted for balance)


        public static bool ResolveHit(Unit attacker, Unit target)
        {
            if (attacker == null || target == null || !attacker.IsAlive)
            {
                DebugHelper.LogWarning("CombatCalculator.ResolveHit: Attacker or Target is null, or attacker is not alive. Defaulting to miss.", attacker);
                return false;
            }
            if (target.CurrentTile == null)
            {
                 DebugHelper.LogWarning($"CombatCalculator.ResolveHit: Target {target.unitName} has no CurrentTile. Assuming no cover.", target);
            }

            int attackerBaseAccuracy = UNARMED_BASE_ACCURACY;
            if (attacker.equippedWeapon != null)
            {
                attackerBaseAccuracy = attacker.equippedWeapon.baseAccuracy;
            }

            int attackerEchoBonus = 0;
            if (attacker.currentAttributes != null)
            {
                attackerEchoBonus = Mathf.FloorToInt(attacker.currentAttributes.Echo / 2f);
            }
            int totalOffensiveRating = attackerBaseAccuracy + attackerEchoBonus;

            int defenderBaseEvasion = 0;
            int defenderGlimmerBonus = 0;
            int defenderSparkBonus = 0;
            if (target.currentAttributes != null)
            {
                defenderGlimmerBonus = Mathf.FloorToInt(target.currentAttributes.Glimmer / 2f);
                defenderSparkBonus = Mathf.FloorToInt(target.currentAttributes.Spark / 4f);
            }

            int defenderCoverBonus = 0;
            if (target.CurrentTile != null && target.CurrentTile.tileTypeData != null)
            {
                defenderCoverBonus = target.CurrentTile.tileTypeData.evasionBonus;
            }
            int totalDefensiveRating = defenderBaseEvasion + defenderGlimmerBonus + defenderSparkBonus + defenderCoverBonus;

            int hitChance = totalOffensiveRating - totalDefensiveRating;
            hitChance = Mathf.Clamp(hitChance, MIN_HIT_CHANCE, MAX_HIT_CHANCE);

            int roll = Random.Range(1, 101);
            bool didHit = roll <= hitChance;

            DebugHelper.Log($"CombatCalculator.ResolveHit: {attacker.unitName} vs {target.unitName}. " +
                            $"Offense (Acc:{attackerBaseAccuracy} + EchoBns:{attackerEchoBonus} = {totalOffensiveRating}). " +
                            $"Defense (BaseEv:{defenderBaseEvasion} + GlimBns:{defenderGlimmerBonus} + SpkBns:{defenderSparkBonus} + Cover:{defenderCoverBonus} = {totalDefensiveRating}). " +
                            $"Final Hit%: {hitChance}. Roll: {roll}. Result: {(didHit ? "HIT" : "MISS")}", attacker);
            return didHit;
        }

        /// <summary>
        /// Determines if a successful hit is also a critical hit.
        /// Physical Crit Chance = BaseCritChance + Floor(AttackerCore / 4) + Floor(AttackerEcho / 4)
        /// </summary>
        /// <param name="attacker">The attacking unit.</param>
        /// <param name="target">The defending unit (currently unused for physical crit chance calc).</param>
        /// <returns>True if the hit is critical, false otherwise.</returns>
        public static bool CheckCriticalHit(Unit attacker, Unit target) // target param kept for future use (e.g. crit resistance)
        {
            if (attacker == null || attacker.currentAttributes == null)
            {
                DebugHelper.LogWarning("CombatCalculator.CheckCriticalHit: Attacker or attacker attributes are null. Defaulting to no crit.", attacker);
                return false;
            }

            int totalCritChance = BASE_CRIT_CHANCE;

            // Add bonuses for Physical Crit Chance (GDD 3.2)
            totalCritChance += Mathf.FloorToInt(attacker.currentAttributes.Core / 4f);
            totalCritChance += Mathf.FloorToInt(attacker.currentAttributes.Echo / 4f);
            // TODO: Add Equipment/AbilityCritBonuses when those systems exist

            totalCritChance = Mathf.Clamp(totalCritChance, MIN_CRIT_CHANCE, MAX_CRIT_CHANCE);

            int roll = Random.Range(1, 101); // d100 roll
            bool isCritical = roll <= totalCritChance;

            int coreCritBonus = Mathf.FloorToInt(attacker.currentAttributes.Core / 4f);
            int echoCritBonus = Mathf.FloorToInt(attacker.currentAttributes.Echo / 4f);

            DebugHelper.Log($"CombatCalculator.CheckCriticalHit: {attacker.unitName}. " +
                            $"BaseCrit:{BASE_CRIT_CHANCE} + CoreBns:{coreCritBonus} + EchoBns:{echoCritBonus} = TotalCrit%: {totalCritChance}. " +
                            $"Roll: {roll}. Result: {(isCritical ? "CRITICAL HIT!" : "Normal Hit")}", attacker);

            return isCritical;
        }
    }
}