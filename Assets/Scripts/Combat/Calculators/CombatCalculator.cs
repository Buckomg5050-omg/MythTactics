// CombatCalculator.cs
using UnityEngine; 

// Enums are now defined within the namespace below
// No longer global

namespace MythTactics.Combat 
{ 
    // Enums defined within the MythTactics.Combat namespace
    public enum AbilityTargetType
    {
        Self,
        EnemyUnit,
        AllyUnit,
        Tile,
        AOE // Added AOE from a previous GDD version/your correct version
    }

    public enum AbilityEffectType
    {
        None,    
        Damage,
        Heal,
        Buff, 
        Debuff,
        Summon,   // Added Summon from a previous GDD version/your correct version
        Teleport, // Added Teleport from a previous GDD version/your correct version
        Special   // Added Special from a previous GDD version/your correct version
    }

    public enum DamageType 
    {
        Physical,
        Magical,
        True, 
        Fire,
        Cold,
        // Ice, // "Ice" is often synonymous with "Cold"; GDD lists "Cold/Ice". Let's stick to "Cold" for simplicity unless distinct mechanics are planned.
        Lightning,

        Poison      
        
    }

    public static class CombatCalculator
    {
        // Constants for Hit Chance Calculation
        // public const int UNARMED_BASE_ACCURACY = 65; // From your version, let's keep this for now
        public const int MIN_HIT_CHANCE = 5;
        public const int MAX_HIT_CHANCE = 95;

        // Constants for Critical Hit Calculation
        public const int BASE_CRIT_CHANCE = 5; 
        public const int MIN_CRIT_CHANCE = 0;  
        public const int MAX_CRIT_CHANCE = 100; 

        public static bool ResolveHit(Unit attacker, Unit defender)
        {
            // MODIFIED: Added null checks for Stats components
            if (attacker == null || defender == null || attacker.Stats == null || defender.Stats == null || !attacker.IsAlive)
            {
                DebugHelper.LogWarning("CombatCalculator.ResolveHit (Physical): Attacker, Target, their Stats are null, or attacker is not alive. Defaulting to miss.", attacker);
                return false;
            }
            // MODIFIED: Check currentAttributes on Stats component
            if (attacker.Stats.currentAttributes == null || defender.Stats.currentAttributes == null)
            {
                 DebugHelper.LogWarning($"CombatCalculator.ResolveHit (Physical): Attacker ({attacker.unitName}) or Defender ({defender.unitName}) currentAttributes on Stats component is null. Defaulting to miss.", attacker);
                 return false;
            }
            if (defender.CurrentTile == null) // This check was from your version
            {
                 DebugHelper.LogWarning($"CombatCalculator.ResolveHit (Physical): Target {defender.unitName} has no CurrentTile. Assuming no cover.", defender);
            }


            int attackerBaseAccuracy = (attacker.equippedWeapon != null) ? attacker.equippedWeapon.baseAccuracy : 60; // GDD weapon accuracy range 60-90. Unarmed can be 60.
            
            // MODIFIED: Access Echo via attacker.Stats.currentAttributes
            int attackerEchoBonus = Mathf.FloorToInt(attacker.Stats.currentAttributes.Echo / 2f);
            int totalOffensiveRating = attackerBaseAccuracy + attackerEchoBonus;

            // MODIFIED: Access baseEvasion from equippedBodyArmor
            int defenderBaseEvasion = (defender.equippedBodyArmor != null) ? defender.equippedBodyArmor.baseEvasion : 0; 
            // MODIFIED: Access Glimmer and Spark via defender.Stats.currentAttributes
            int defenderGlimmerBonus = Mathf.FloorToInt(defender.Stats.currentAttributes.Glimmer / 2f);
            int defenderSparkBonusForPhysical = Mathf.FloorToInt(defender.Stats.currentAttributes.Spark / 4f); 
            
            int defenderCoverBonus = 0;
            // MODIFIED: Access evasionBonus via tileTypeData
            if (defender.CurrentTile != null && defender.CurrentTile.tileTypeData != null)
            {
                defenderCoverBonus = defender.CurrentTile.tileTypeData.evasionBonus;
            }
            int totalDefensiveRating = defenderBaseEvasion + defenderGlimmerBonus + defenderSparkBonusForPhysical + defenderCoverBonus;

            int hitChance = totalOffensiveRating - totalDefensiveRating;
            hitChance = Mathf.Clamp(hitChance, MIN_HIT_CHANCE, MAX_HIT_CHANCE);

            int roll = Random.Range(1, 101);
            bool didHit = roll <= hitChance;

            DebugHelper.Log($"CombatCalculator.ResolveHit (Physical): {attacker.unitName} vs {defender.unitName}. " +
                            $"Offense (Acc:{attackerBaseAccuracy} + EchoBns:{attackerEchoBonus} = {totalOffensiveRating}). " +
                            $"Defense (ArmorEv:{defenderBaseEvasion} + GlimBns:{defenderGlimmerBonus} + SpkBnsPhys:{defenderSparkBonusForPhysical} + Cover:{defenderCoverBonus} = {totalDefensiveRating}). " +
                            $"Final Hit%: {hitChance}. Roll: {roll}. Result: {(didHit ? "HIT" : "MISS")}", attacker);
            return didHit;
        }
        
        public static bool ResolveAbilityHit(AbilitySO ability, Unit caster, Unit target)
        {
            // MODIFIED: Added null checks for Stats components
            if (ability == null || caster == null || target == null || caster.Stats == null || target.Stats == null || !caster.IsAlive)
            {
                DebugHelper.LogWarning("CombatCalculator.ResolveAbilityHit: Ability, Caster, Target, their Stats are null, or caster is not alive. Defaulting to miss.", caster);
                return false;
            }
            // MODIFIED: Check currentAttributes on Stats component
            if (caster.Stats.currentAttributes == null || target.Stats.currentAttributes == null)
            {
                 DebugHelper.LogWarning($"CombatCalculator.ResolveAbilityHit: Caster ({caster.unitName}) or Target ({target.unitName}) currentAttributes on Stats component is null. Defaulting to miss.", caster);
                 return false;
            }
            if (target.CurrentTile == null) // This check was from your version
            {
                 DebugHelper.LogWarning($"CombatCalculator.ResolveAbilityHit: Target {target.unitName} has no CurrentTile. Assuming no cover for ability hit calc.", target);
            }


            if (ability.baseAccuracy <= 0) // Auto-hit for abilities like self-buffs
            {
                DebugHelper.Log($"CombatCalculator.ResolveAbilityHit: {ability.abilityName} has baseAccuracy <= 0, auto-hitting valid target.", caster);
                return true;
            }

            int abilityBaseAccuracy = ability.baseAccuracy;
            // MODIFIED: Access Spark via caster.Stats.currentAttributes
            // Your GDD for ability hit was CasterSpark/4, my prev example used /2. Let's use /4 for GDD.
            int casterSparkBonus = Mathf.FloorToInt(caster.Stats.currentAttributes.Spark / 4f); 
            int totalOffensiveRating = abilityBaseAccuracy + casterSparkBonus;
            
            // MODIFIED: Access Glimmer and Spark via target.Stats.currentAttributes
            // GDD implies Glimmer for magical defense for abilities. Let's use /4 similar to Spark bonus for caster.
            int defenderGlimmerBonus = Mathf.FloorToInt(target.Stats.currentAttributes.Glimmer / 4f);
            // int defenderSparkBonus = Mathf.FloorToInt(target.Stats.currentAttributes.Spark / 4f); // Original version had this, GDD more implies Glimmer

            int defenderCoverBonus = 0;
            // MODIFIED: Access evasionBonus via tileTypeData
            if (target.CurrentTile != null && target.CurrentTile.tileTypeData != null)
            {
                defenderCoverBonus = target.CurrentTile.tileTypeData.evasionBonus;
            }
            // Using only Glimmer for magical defense stat as per GDD's physical hit formula implication
            int totalDefensiveRating = /*defenderBaseEvasion (typically not for magic) +*/ defenderGlimmerBonus + defenderCoverBonus;

            int hitChance = totalOffensiveRating - totalDefensiveRating;
            hitChance = Mathf.Clamp(hitChance, MIN_HIT_CHANCE, MAX_HIT_CHANCE);

            int roll = Random.Range(1, 101);
            bool didHit = roll <= hitChance;

            DebugHelper.Log($"CombatCalculator.ResolveAbilityHit ({ability.abilityName}): {caster.unitName} vs {target.unitName}. " +
                            $"Offense (AbilityAcc:{abilityBaseAccuracy} + SparkBns:{casterSparkBonus} = {totalOffensiveRating}). " +
                            $"Defense (GlimBns:{defenderGlimmerBonus} + Cover:{defenderCoverBonus} = {totalDefensiveRating}). " + // Removed SpkBns from log for clarity
                            $"Final Hit%: {hitChance}. Roll: {roll}. Result: {(didHit ? "HIT" : "MISS")}", caster);
            return didHit;
        }

        public static bool CheckCriticalHit(Unit attacker, Unit target) 
        {
            // MODIFIED: Added null checks for Stats component
            if (attacker == null || attacker.Stats == null || attacker.Stats.currentAttributes == null)
            {
                DebugHelper.LogWarning("CombatCalculator.CheckCriticalHit (Physical): Attacker, its Stats, or attributes are null. Defaulting to no crit.", attacker);
                return false;
            }
            int totalCritChance = BASE_CRIT_CHANCE;
            // MODIFIED: Access Core and Echo via attacker.Stats.currentAttributes
            totalCritChance += Mathf.FloorToInt(attacker.Stats.currentAttributes.Core / 4f);
            totalCritChance += Mathf.FloorToInt(attacker.Stats.currentAttributes.Echo / 4f);
            totalCritChance = Mathf.Clamp(totalCritChance, MIN_CRIT_CHANCE, MAX_CRIT_CHANCE);
            int roll = Random.Range(1, 101); 
            bool isCritical = roll <= totalCritChance;
            // MODIFIED: Access Core and Echo for logging via attacker.Stats.currentAttributes
            int coreCritBonus = Mathf.FloorToInt(attacker.Stats.currentAttributes.Core / 4f);
            int echoCritBonus = Mathf.FloorToInt(attacker.Stats.currentAttributes.Echo / 4f);
            DebugHelper.Log($"CombatCalculator.CheckCriticalHit (Physical): {attacker.unitName}. " +
                            $"BaseCrit:{BASE_CRIT_CHANCE} + CoreBns:{coreCritBonus} + EchoBns:{echoCritBonus} = TotalCrit%: {totalCritChance}. " +
                            $"Roll: {roll}. Result: {(isCritical ? "CRITICAL HIT!" : "Normal Hit")}", attacker);
            return isCritical;
        }
        
        public static bool CheckMagicalCriticalHit(Unit caster, Unit target)
        {
            // MODIFIED: Added null checks for Stats component
            if (caster == null || caster.Stats == null || caster.Stats.currentAttributes == null)
            {
                DebugHelper.LogWarning("CombatCalculator.CheckMagicalCriticalHit: Caster, its Stats, or attributes are null. Defaulting to no magical crit.", caster);
                return false;
            }
            int totalCritChance = BASE_CRIT_CHANCE;
            // MODIFIED: Access Spark and Glimmer via caster.Stats.currentAttributes
            totalCritChance += Mathf.FloorToInt(caster.Stats.currentAttributes.Spark / 4f);
            totalCritChance += Mathf.FloorToInt(caster.Stats.currentAttributes.Glimmer / 4f);
            totalCritChance = Mathf.Clamp(totalCritChance, MIN_CRIT_CHANCE, MAX_CRIT_CHANCE);
            int roll = Random.Range(1, 101); 
            bool isCritical = roll <= totalCritChance;
            // MODIFIED: Access Spark and Glimmer for logging via caster.Stats.currentAttributes
            int sparkCritBonus = Mathf.FloorToInt(caster.Stats.currentAttributes.Spark / 4f);
            int glimmerCritBonus = Mathf.FloorToInt(caster.Stats.currentAttributes.Glimmer / 4f);
            DebugHelper.Log($"CombatCalculator.CheckMagicalCriticalHit: {caster.unitName}. " +
                            $"BaseCrit:{BASE_CRIT_CHANCE} + SparkBns:{sparkCritBonus} + GlimmerBns:{glimmerCritBonus} = TotalCrit%: {totalCritChance}. " +
                            $"Roll: {roll}. Result: {(isCritical ? "MAGICAL CRITICAL HIT!" : "Normal Magical Hit")}", caster);
            return isCritical;
        }
    }
}