// DamageCalculator.cs
using UnityEngine;
using MythTactics.Combat; // For DamageType enum etc.

public static class DamageCalculator
{
    public const float CRITICAL_HIT_MULTIPLIER = 1.5f;
    public const int UNARMED_BASE_DAMAGE = 1;
    public const float ARMOR_K_CONSTANT = 50f; // Example value for PDR calculation

    // GDD 2.3: Physical Attack Damage Bonus: Floor(Core / 4)
    // GDD 2.3: True Damage skips mitigation
    public static int CalculatePhysicalAttackDamage(int baseDamage, Unit attacker, Unit defender, float criticalMultiplier = 1.0f)
    {
        if (attacker == null || defender == null || attacker.Stats == null || defender.Stats == null)
        {
            DebugHelper.LogError("CalculatePhysicalAttackDamage: Attacker or Defender or their Stats are null.");
            return 0;
        }

        // 1. Base Outgoing Damage (BaseWeaponDamage + Floor(Core / 4))
        // MODIFIED: Access Core via attacker.Stats.currentAttributes
        int attackerCoreBonus = Mathf.FloorToInt((attacker.Stats.currentAttributes != null ? attacker.Stats.currentAttributes.Core : 0) / 4f);
        int outgoingDamage = baseDamage + attackerCoreBonus;

        // 2. Apply Crit Multiplier
        outgoingDamage = Mathf.RoundToInt(outgoingDamage * criticalMultiplier);

        // 3. Apply Attacker's general damage % modifiers (Future placeholder)
        // outgoingDamage = Mathf.RoundToInt(outgoingDamage * GetAttackerDamageModifiers(attacker));

        int finalDamage = outgoingDamage;

        // 4. Apply Defender's Mitigations (unless true damage)
        bool isTrueDamage = attacker.equippedWeapon != null && attacker.equippedWeapon.dealsTrueDamage;

        if (!isTrueDamage)
        {
            // Physical: PDR% = ArmorValue / (ArmorValue + K_ArmorConstant). Apply Armor Penetration.
            int armorValue = (defender.equippedBodyArmor != null) ? defender.equippedBodyArmor.armorValue : 0;
            // TODO: Add Armor Penetration if/when implemented
            float pdr = armorValue / (armorValue + ARMOR_K_CONSTANT);
            finalDamage = Mathf.RoundToInt(outgoingDamage * (1f - pdr));
            DebugHelper.Log($"DamageCalc (Phys): BaseDmg:{baseDamage}, CoreBns:{attackerCoreBonus}, CritX:{criticalMultiplier}, Outgoing:{outgoingDamage}, ArmorVal:{armorValue}, PDR:{pdr:P1}, FinalPreVar:{finalDamage}", attacker);
        }
        else
        {
            DebugHelper.Log($"DamageCalc (Phys TRUE): BaseDmg:{baseDamage}, CoreBns:{attackerCoreBonus}, CritX:{criticalMultiplier}, Outgoing:{outgoingDamage}. True damage, PDR skipped. FinalPreVar:{finalDamage}", attacker);
        }

        // 5. Apply +/- 10% Damage Variance (Future placeholder, GDD 7.1.2)
        // float variance = Random.Range(-0.10f, 0.10f);
        // finalDamage = Mathf.RoundToInt(finalDamage * (1.0f + variance));
        
        // 6. Ensure Minimum 1 Damage (if any damage was to be dealt)
        if (finalDamage <= 0 && outgoingDamage > 0) // If it was meant to do damage but got reduced to 0 or less
        {
            finalDamage = 1;
        }
        if (finalDamage < 0 && outgoingDamage <=0) // If it started negative (e.g. healing from damage calc) make it 0
        {
            finalDamage = 0;
        }


        return finalDamage;
    }

    // GDD 2.3: Magical Potency Bonus (Damage/Healing): Floor(Spark / 4)
    // GDD 7.1.2: True Damage skips mitigation (though magical resistance is usually a multiplier, not PDR)
    public static int CalculateMagicalAbilityDamage(AbilitySO ability, Unit caster, Unit target)
    {
        if (ability == null || caster == null || target == null || caster.Stats == null || target.Stats == null)
        {
            DebugHelper.LogError("CalculateMagicalAbilityDamage: Ability, Caster, Target, or their Stats are null.");
            return 0;
        }

        // 1. Base Outgoing Damage (BaseSpellPower + Floor(Spark / 4))
        // MODIFIED: Access Spark via caster.Stats.currentAttributes
        int casterSparkBonus = Mathf.FloorToInt((caster.Stats.currentAttributes != null ? caster.Stats.currentAttributes.Spark : 0) / 4f);
        int outgoingDamage = ability.basePower + casterSparkBonus;

        // 2. Apply Crit Multiplier if critical (passed from CombatCalculator)
        //    For now, assume criticals are handled before this function for abilities, or ability.dealsCriticalDamage flag.
        //    GDD 2.3 implies magical criticals are 1.5x, CombatCalculator.CheckMagicalCriticalHit exists.
        //    Let's assume CombatManager/Unit.PerformAbility will pass a criticalMultiplier if applicable.
        //    For simplicity here, if we need it:
        //    bool isMagicalCrit = CombatCalculator.CheckMagicalCriticalHit(caster, target); // Or get from ability effect
        //    if (isMagicalCrit) outgoingDamage = Mathf.RoundToInt(outgoingDamage * CRITICAL_HIT_MULTIPLIER);
        //    This is simplified; PerformAbility already handles crit multiplier via CombatCalculator.ResolveAbilityHit indirectly through effects.
        //    The damage calculation itself here should just take the base power and apply Spark.
        //    If ability specifies a critical, the DamageCalculator itself should not re-check it but apply it.
        //    This specific CalculateMagicalAbilityDamage currently doesn't take a crit multiplier, so we'll assume
        //    the `ability.basePower` already incorporates any pre-calculation or it's applied after this.
        //    For true GDD alignment, PerformAbility should get the crit status, then pass the multiplier here if any.
        //    Revisiting the `Unit.PerformAbility` logic: it calls ResolveAbilityHit, then this. Crit is NOT yet integrated into this specific func.
        //    DamageCalculator.CalculateMagicalAbilityDamage is called in PerformAbility *after* hit resolution.
        //    The damage from this func is then used in TakeDamage.
        //    Let's add a crit check and application here for now, aligning with GDD.
        //    Though it might be better for PerformAbility to determine crit and pass the multiplier.
        //    For now:
        bool isMagicalCritical = CombatCalculator.CheckMagicalCriticalHit(caster, target); // Check for magical crit
        float criticalDamageMultiplier = 1.0f;
        if (isMagicalCritical)
        {
            criticalDamageMultiplier = CRITICAL_HIT_MULTIPLIER;
            // DebugHelper.Log($"Magical CRITICAL HIT by {caster.unitName} on {target.unitName} with {ability.abilityName}!", caster); // Logged in CombatCalculator
        }
        outgoingDamage = Mathf.RoundToInt(outgoingDamage * criticalDamageMultiplier);


        // 3. Apply Caster's general damage % modifiers (Future placeholder)
        // outgoingDamage = Mathf.RoundToInt(outgoingDamage * GetCasterDamageModifiers(caster));

        int finalDamage = outgoingDamage;

        // 4. Apply Defender's Mitigations (unless true damage)
        if (!ability.dealsTrueDamage)
        {
            // Magical/Elemental: Net Damage Multiplier from additive Resistances/Vulnerabilities. (Future placeholder)
            // float netResistanceMultiplier = GetNetResistanceMultiplier(target, ability.damageType);
            // finalDamage = Mathf.RoundToInt(outgoingDamage * netResistanceMultiplier);
            DebugHelper.Log($"DamageCalc (Magic): Ability:{ability.abilityName}, BasePow:{ability.basePower}, SparkBns:{casterSparkBonus}, CritX:{criticalDamageMultiplier}, Outgoing:{outgoingDamage}. No resistances yet. FinalPreVar:{finalDamage}", caster);

        }
        else
        {
            DebugHelper.Log($"DamageCalc (Magic TRUE): Ability:{ability.abilityName}, BasePow:{ability.basePower}, SparkBns:{casterSparkBonus}, CritX:{criticalDamageMultiplier}, Outgoing:{outgoingDamage}. True damage, mitigations skipped. FinalPreVar:{finalDamage}", caster);
        }

        // 5. Apply +/- 10% Damage Variance (Future placeholder, GDD 7.1.2)
        // float variance = Random.Range(-0.10f, 0.10f);
        // finalDamage = Mathf.RoundToInt(finalDamage * (1.0f + variance));

        // 6. Ensure Minimum 1 Damage (if any damage was to be dealt and it's not 0-damage ability)
        if (finalDamage <= 0 && outgoingDamage > 0 && ability.basePower > 0)
        {
            finalDamage = 1;
        }
         if (finalDamage < 0 && outgoingDamage <=0)
        {
            finalDamage = 0;
        }

        return finalDamage;
    }
}