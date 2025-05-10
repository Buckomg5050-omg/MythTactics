// CombatCalculator.cs
// Located in: Assets/Scripts/Combat/Calculators/CombatCalculator.cs

using UnityEngine; // ENSURE THIS (and any other usings) IS AT THE TOP

namespace MythTactics.Combat
{
    // Enums shared within the Combat namespace
    public enum AbilityTargetType
    {
        Self,
        EnemyUnit,
        AllyUnit,
        Tile 
    }

    public enum AbilityEffectType
    {
        Damage,
        Heal,
        Buff, 
        Debuff 
    }

    public enum DamageType 
    {
        Physical,
        Magical,
        True, 
        Fire,
        Cold,
        Ice,    
        Lightning
    }

    public static class CombatCalculator
    {
        // Constants for Hit Chance Calculation (GDD 2.3)
        public const int UNARMED_BASE_ACCURACY = 65;
        public const int MIN_HIT_CHANCE = 5;
        public const int MAX_HIT_CHANCE = 95;

        // Constants for Critical Hit Calculation (GDD 3.2)
        public const int BASE_CRIT_CHANCE = 5; 
        public const int MIN_CRIT_CHANCE = 0;  
        public const int MAX_CRIT_CHANCE = 100; 


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

            int defenderBaseEvasion = 0; // Assuming this comes from armor/shields, which aren't fully implemented for evasion yet
            int defenderGlimmerBonus = 0;
            int defenderSparkBonus = 0;
            if (target.currentAttributes != null)
            {
                defenderGlimmerBonus = Mathf.FloorToInt(target.currentAttributes.Glimmer / 2f);
                defenderSparkBonus = Mathf.FloorToInt(target.currentAttributes.Spark / 4f); // As per GDD 2.3
            }

            int defenderCoverBonus = 0;
            if (target.CurrentTile != null && target.CurrentTile.tileTypeData != null)
            {
                defenderCoverBonus = target.CurrentTile.tileTypeData.evasionBonus;
            }
            // GDD 2.3: Hit% = (AttackerWeaponBaseAccuracy + Floor(AttackerEcho / 2)) - (DefenderArmorBaseEvasion + Floor(DefenderGlimmer / 2) + Floor(DefenderSpark / 4) + DefenderCoverBonusFromTile)
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
        
        public static bool CheckCriticalHit(Unit attacker, Unit target) 
        {
            if (attacker == null || attacker.currentAttributes == null)
            {
                DebugHelper.LogWarning("CombatCalculator.CheckCriticalHit (Physical): Attacker or attacker attributes are null. Defaulting to no crit.", attacker);
                return false;
            }

            int totalCritChance = BASE_CRIT_CHANCE;
            totalCritChance += Mathf.FloorToInt(attacker.currentAttributes.Core / 4f);
            totalCritChance += Mathf.FloorToInt(attacker.currentAttributes.Echo / 4f);
            
            totalCritChance = Mathf.Clamp(totalCritChance, MIN_CRIT_CHANCE, MAX_CRIT_CHANCE);
            int roll = Random.Range(1, 101); 
            bool isCritical = roll <= totalCritChance;

            int coreCritBonus = Mathf.FloorToInt(attacker.currentAttributes.Core / 4f);
            int echoCritBonus = Mathf.FloorToInt(attacker.currentAttributes.Echo / 4f);

            DebugHelper.Log($"CombatCalculator.CheckCriticalHit (Physical): {attacker.unitName}. " +
                            $"BaseCrit:{BASE_CRIT_CHANCE} + CoreBns:{coreCritBonus} + EchoBns:{echoCritBonus} = TotalCrit%: {totalCritChance}. " +
                            $"Roll: {roll}. Result: {(isCritical ? "CRITICAL HIT!" : "Normal Hit")}", attacker);
            return isCritical;
        }
        
        public static bool CheckMagicalCriticalHit(Unit caster, Unit target)
        {
            if (caster == null || caster.currentAttributes == null)
            {
                DebugHelper.LogWarning("CombatCalculator.CheckMagicalCriticalHit: Caster or caster attributes are null. Defaulting to no magical crit.", caster);
                return false;
            }

            int totalCritChance = BASE_CRIT_CHANCE;
            totalCritChance += Mathf.FloorToInt(caster.currentAttributes.Spark / 4f);
            totalCritChance += Mathf.FloorToInt(caster.currentAttributes.Glimmer / 4f);
            
            totalCritChance = Mathf.Clamp(totalCritChance, MIN_CRIT_CHANCE, MAX_CRIT_CHANCE);
            int roll = Random.Range(1, 101); 
            bool isCritical = roll <= totalCritChance;

            int sparkCritBonus = Mathf.FloorToInt(caster.currentAttributes.Spark / 4f);
            int glimmerCritBonus = Mathf.FloorToInt(caster.currentAttributes.Glimmer / 4f);

            DebugHelper.Log($"CombatCalculator.CheckMagicalCriticalHit: {caster.unitName}. " +
                            $"BaseCrit:{BASE_CRIT_CHANCE} + SparkBns:{sparkCritBonus} + GlimmerBns:{glimmerCritBonus} = TotalCrit%: {totalCritChance}. " +
                            $"Roll: {roll}. Result: {(isCritical ? "MAGICAL CRITICAL HIT!" : "Normal Magical Hit")}", caster);
            return isCritical;
        }
    }
}