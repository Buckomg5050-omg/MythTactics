// CombatCalculator.cs
using UnityEngine;
using System.Linq; // Required for FirstOrDefault and Sum

namespace MythTactics.Combat
{
    public enum AbilityTargetType { /* ... same as before ... */ Self, EnemyUnit, AllyUnit, Tile, AOE }
    public enum AbilityEffectType { /* ... same as before ... */ None, Damage, Heal, Buff, Debuff, Summon, Teleport, Special }
    public enum DamageType { /* ... same as before ... */ Physical, Magical, True, Fire, Cold, Lightning, Poison }

    public static class CombatCalculator
    {
        public const int MIN_HIT_CHANCE = 5;
        public const int MAX_HIT_CHANCE = 95;
        public const int BASE_CRIT_CHANCE = 5;
        public const int MIN_CRIT_CHANCE = 0;
        public const int MAX_CRIT_CHANCE = 100;

        public static bool ResolveHit(Unit attacker, Unit defender)
        {
            if (attacker == null || defender == null || attacker.Stats == null || defender.Stats == null || !attacker.IsAlive)
            {
                DebugHelper.LogWarning("CombatCalculator.ResolveHit (Physical): Attacker, Target, their Stats are null, or attacker is not alive. Defaulting to miss.", attacker);
                return false;
            }
            if (attacker.Stats.currentAttributes == null || defender.Stats.currentAttributes == null)
            {
                DebugHelper.LogWarning($"CombatCalculator.ResolveHit (Physical): Attacker ({attacker.unitName}) or Defender ({defender.unitName}) currentAttributes on Stats component is null. Defaulting to miss.", attacker);
                return false;
            }
            // CurrentTile null check is fine as defenderCoverBonus defaults to 0

            int attackerBaseAccuracy = (attacker.equippedWeapon != null) ? attacker.equippedWeapon.baseAccuracy : 60;
            int attackerEchoBonus = Mathf.FloorToInt(attacker.Stats.EffectiveAttributes.Echo / 2f); // Use EffectiveAttributes
            int totalOffensiveRating = attackerBaseAccuracy + attackerEchoBonus;

            int defenderBaseEvasionFromArmor = (defender.equippedBodyArmor != null) ? defender.equippedBodyArmor.baseEvasion : 0;
            int defenderGlimmerBonus = Mathf.FloorToInt(defender.Stats.EffectiveAttributes.Glimmer / 2f); // Use EffectiveAttributes
            int defenderSparkBonusForPhysical = Mathf.FloorToInt(defender.Stats.EffectiveAttributes.Spark / 4f); // Use EffectiveAttributes
            
            int defenderCoverBonus = 0;
            if (defender.CurrentTile != null && defender.CurrentTile.tileTypeData != null)
            {
                defenderCoverBonus = defender.CurrentTile.tileTypeData.evasionBonus;
            }

            // MODIFIED: Calculate Temporary Evasion Bonus from defender's active effects
            int defenderTempEvasionBonusFromEffects = 0;
            if (defender.Stats.ActiveEffects != null)
            {
                foreach (ActiveStatusEffect activeEffect in defender.Stats.ActiveEffects)
                {
                    if (activeEffect.BaseEffect != null && activeEffect.BaseEffect.statModifiers != null)
                    {
                        foreach (StatModifier mod in activeEffect.BaseEffect.statModifiers)
                        {
                            if (mod.stat == StatType.TemporaryEvasionBonus && mod.type == ModifierType.Flat)
                            {
                                defenderTempEvasionBonusFromEffects += Mathf.RoundToInt(mod.value * activeEffect.CurrentStacks);
                            }
                        }
                    }
                }
            }
            // End of MODIFICATION

            int totalDefensiveRating = defenderBaseEvasionFromArmor + 
                                       defenderGlimmerBonus + 
                                       defenderSparkBonusForPhysical + 
                                       defenderCoverBonus + 
                                       defenderTempEvasionBonusFromEffects; // MODIFIED: Added temp bonus

            int hitChance = totalOffensiveRating - totalDefensiveRating;
            hitChance = Mathf.Clamp(hitChance, MIN_HIT_CHANCE, MAX_HIT_CHANCE);

            int roll = Random.Range(1, 101);
            bool didHit = roll <= hitChance;

            // MODIFIED: Updated Debug Log to include TempEvasionBonus
            DebugHelper.Log($"CombatCalculator.ResolveHit (Physical): {attacker.unitName} vs {defender.unitName}. " +
                            $"Offense (Acc:{attackerBaseAccuracy} + EchoBns:{attackerEchoBonus} = {totalOffensiveRating}). " +
                            $"Defense (ArmorEv:{defenderBaseEvasionFromArmor} + GlimBns:{defenderGlimmerBonus} + SpkBnsPhys:{defenderSparkBonusForPhysical} + Cover:{defenderCoverBonus} + EffectEv:{defenderTempEvasionBonusFromEffects} = {totalDefensiveRating}). " +
                            $"Final Hit%: {hitChance}. Roll: {roll}. Result: {(didHit ? "HIT" : "MISS")}", attacker);
            return didHit;
        }
        
        // ResolveAbilityHit, CheckCriticalHit, CheckMagicalCriticalHit remain the same as your last provided version
        // ... (unless you want to add TemporaryEvasionBonus to ResolveAbilityHit as well, which might make sense for some abilities)
        public static bool ResolveAbilityHit(AbilitySO ability, Unit caster, Unit target)
        {
            if (ability == null || caster == null || target == null || caster.Stats == null || target.Stats == null || !caster.IsAlive)
            {
                DebugHelper.LogWarning("CombatCalculator.ResolveAbilityHit: Ability, Caster, Target, their Stats are null, or caster is not alive. Defaulting to miss.", caster);
                return false;
            }
            if (caster.Stats.currentAttributes == null || target.Stats.currentAttributes == null)
            {
                 DebugHelper.LogWarning($"CombatCalculator.ResolveAbilityHit: Caster ({caster.unitName}) or Target ({target.unitName}) currentAttributes on Stats component is null. Defaulting to miss.", caster);
                 return false;
            }

            if (ability.baseAccuracy <= 0) 
            {
                DebugHelper.Log($"CombatCalculator.ResolveAbilityHit: {ability.abilityName} has baseAccuracy <= 0, auto-hitting valid target.", caster);
                return true;
            }

            int abilityBaseAccuracy = ability.baseAccuracy;
            int casterSparkBonus = Mathf.FloorToInt(caster.Stats.EffectiveAttributes.Spark / 4f); // Use EffectiveAttributes
            int totalOffensiveRating = abilityBaseAccuracy + casterSparkBonus;
            
            int defenderGlimmerBonus = Mathf.FloorToInt(target.Stats.EffectiveAttributes.Glimmer / 4f); // Use EffectiveAttributes

            int defenderCoverBonus = 0;
            if (target.CurrentTile != null && target.CurrentTile.tileTypeData != null)
            {
                defenderCoverBonus = target.CurrentTile.tileTypeData.evasionBonus;
            }

            // MODIFICATION POINT: Consider if TemporaryEvasionBonus should apply against abilities
            int defenderTempEvasionBonusFromEffects = 0;
            // if (target.Stats.ActiveEffects != null) // Example: Add this if desired for abilities too
            // {
            //     foreach (ActiveStatusEffect activeEffect in target.Stats.ActiveEffects)
            //     {
            //         if (activeEffect.BaseEffect != null && activeEffect.BaseEffect.statModifiers != null)
            //         {
            //             foreach (StatModifier mod in activeEffect.BaseEffect.statModifiers)
            //             {
            //                 if (mod.stat == StatType.TemporaryEvasionBonus && mod.type == ModifierType.Flat)
            //                 {
            //                     defenderTempEvasionBonusFromEffects += Mathf.RoundToInt(mod.value * activeEffect.CurrentStacks);
            //                 }
            //             }
            //         }
            //     }
            // }
            
            int totalDefensiveRating = defenderGlimmerBonus + defenderCoverBonus + defenderTempEvasionBonusFromEffects; // Add if implemented above

            int hitChance = totalOffensiveRating - totalDefensiveRating;
            hitChance = Mathf.Clamp(hitChance, MIN_HIT_CHANCE, MAX_HIT_CHANCE);

            int roll = Random.Range(1, 101);
            bool didHit = roll <= hitChance;

            DebugHelper.Log($"CombatCalculator.ResolveAbilityHit ({ability.abilityName}): {caster.unitName} vs {target.unitName}. " +
                            $"Offense (AbilityAcc:{abilityBaseAccuracy} + SparkBns:{casterSparkBonus} = {totalOffensiveRating}). " +
                            $"Defense (GlimBns:{defenderGlimmerBonus} + Cover:{defenderCoverBonus} + EffectEv:{defenderTempEvasionBonusFromEffects} = {totalDefensiveRating}). " + 
                            $"Final Hit%: {hitChance}. Roll: {roll}. Result: {(didHit ? "HIT" : "MISS")}", caster);
            return didHit;
        }

        public static bool CheckCriticalHit(Unit attacker, Unit target) 
        {
            if (attacker == null || attacker.Stats == null || attacker.Stats.currentAttributes == null)
            {
                DebugHelper.LogWarning("CombatCalculator.CheckCriticalHit (Physical): Attacker, its Stats, or attributes are null. Defaulting to no crit.", attacker);
                return false;
            }
            int totalCritChance = BASE_CRIT_CHANCE;
            totalCritChance += Mathf.FloorToInt(attacker.Stats.EffectiveAttributes.Core / 4f); // Use EffectiveAttributes
            totalCritChance += Mathf.FloorToInt(attacker.Stats.EffectiveAttributes.Echo / 4f); // Use EffectiveAttributes
            totalCritChance = Mathf.Clamp(totalCritChance, MIN_CRIT_CHANCE, MAX_CRIT_CHANCE);
            int roll = Random.Range(1, 101); 
            bool isCritical = roll <= totalCritChance;
            int coreCritBonus = Mathf.FloorToInt(attacker.Stats.EffectiveAttributes.Core / 4f); // Use EffectiveAttributes
            int echoCritBonus = Mathf.FloorToInt(attacker.Stats.EffectiveAttributes.Echo / 4f); // Use EffectiveAttributes
            DebugHelper.Log($"CombatCalculator.CheckCriticalHit (Physical): {attacker.unitName}. " +
                            $"BaseCrit:{BASE_CRIT_CHANCE} + CoreBns:{coreCritBonus} + EchoBns:{echoCritBonus} = TotalCrit%: {totalCritChance}. " +
                            $"Roll: {roll}. Result: {(isCritical ? "CRITICAL HIT!" : "Normal Hit")}", attacker);
            return isCritical;
        }
        
        public static bool CheckMagicalCriticalHit(Unit caster, Unit target)
        {
            if (caster == null || caster.Stats == null || caster.Stats.currentAttributes == null)
            {
                DebugHelper.LogWarning("CombatCalculator.CheckMagicalCriticalHit: Caster, its Stats, or attributes are null. Defaulting to no magical crit.", caster);
                return false;
            }
            int totalCritChance = BASE_CRIT_CHANCE;
            totalCritChance += Mathf.FloorToInt(caster.Stats.EffectiveAttributes.Spark / 4f); // Use EffectiveAttributes
            totalCritChance += Mathf.FloorToInt(caster.Stats.EffectiveAttributes.Glimmer / 4f); // Use EffectiveAttributes
            totalCritChance = Mathf.Clamp(totalCritChance, MIN_CRIT_CHANCE, MAX_CRIT_CHANCE);
            int roll = Random.Range(1, 101); 
            bool isCritical = roll <= totalCritChance;
            int sparkCritBonus = Mathf.FloorToInt(caster.Stats.EffectiveAttributes.Spark / 4f); // Use EffectiveAttributes
            int glimmerCritBonus = Mathf.FloorToInt(caster.Stats.EffectiveAttributes.Glimmer / 4f); // Use EffectiveAttributes
            DebugHelper.Log($"CombatCalculator.CheckMagicalCriticalHit: {caster.unitName}. " +
                            $"BaseCrit:{BASE_CRIT_CHANCE} + SparkBns:{sparkCritBonus} + GlimmerBns:{glimmerCritBonus} = TotalCrit%: {totalCritChance}. " +
                            $"Roll: {roll}. Result: {(isCritical ? "MAGICAL CRITICAL HIT!" : "Normal Magical Hit")}", caster);
            return isCritical;
        }
    }
}