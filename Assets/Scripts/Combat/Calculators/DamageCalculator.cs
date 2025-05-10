// DamageCalculator.cs
using UnityEngine;

namespace MythTactics.Combat
{
    public static class DamageCalculator
    {
        public const int UNARMED_BASE_DAMAGE = 1;
        public const float CRITICAL_HIT_MULTIPLIER = 1.5f; 
        public const float K_ARMOR_CONSTANT = 50f;

        public static int CalculatePhysicalAttackDamage(int baseDamageFromWeaponOrUnarmed, Unit attacker, Unit defender, float criticalMultiplier = 1.0f)
        {
            if (attacker == null)
            {
                DebugHelper.LogWarning("DamageCalculator: Attacker is null for physical attack. Returning 0 damage.", null);
                return 0;
            }

            bool isTrueDamageFromWeapon = false; // Specifically for weapon-based true damage
            if (attacker.equippedWeapon != null && attacker.equippedWeapon.dealsTrueDamage)
            {
                isTrueDamageFromWeapon = true;
                DebugHelper.Log($"DamageCalculator: {attacker.unitName}'s physical attack is True Damage (from weapon). PDR will be skipped.", attacker);
            }
            
            int attackerCoreBonus = (attacker.currentAttributes != null) ? Mathf.FloorToInt(attacker.currentAttributes.Core / 4f) : 0;
            float rawDamage = baseDamageFromWeaponOrUnarmed + attackerCoreBonus;

            if (criticalMultiplier > 1.0f) 
            {
                rawDamage *= criticalMultiplier;
            }

            float pdrPercentage = 0f;
            float damageAfterPDR = rawDamage; 

            if (!isTrueDamageFromWeapon) 
            {
                if (defender != null && defender.equippedBodyArmor != null)
                {
                    int defenderArmorValue = defender.equippedBodyArmor.armorValue;
                    if (defenderArmorValue > 0)
                    {
                        pdrPercentage = (float)defenderArmorValue / (defenderArmorValue + K_ARMOR_CONSTANT);
                    }
                }
                damageAfterPDR = rawDamage * (1f - pdrPercentage);
            }
            
            int finalDamage = Mathf.Max(1, Mathf.FloorToInt(damageAfterPDR));
            return finalDamage;
        }

        public static int CalculateMagicalAbilityDamage(AbilitySO ability, Unit caster, Unit target)
        {
            if (ability == null)
            {
                DebugHelper.LogError("DamageCalculator: AbilitySO is null for magical damage calculation. Returning 0 damage.", caster);
                return 0;
            }
            if (caster == null)
            {
                DebugHelper.LogWarning("DamageCalculator: Caster is null for magical ability damage. Returning 0 damage.", null);
                return 0;
            }

            // MODIFIED: Check if the ability itself deals true damage
            bool isAbilityTrueDamage = ability.dealsTrueDamage;
            if (isAbilityTrueDamage)
            {
                DebugHelper.Log($"DamageCalculator: Ability '{ability.abilityName}' is True Damage. Mitigations will be skipped.", caster);
            }

            int casterSparkBonus = (caster.currentAttributes != null) ? Mathf.FloorToInt(caster.currentAttributes.Spark / 4f) : 0;
            float rawDamage = ability.basePower + casterSparkBonus;

            bool isMagicalCrit = CombatCalculator.CheckMagicalCriticalHit(caster, target);
            string critMessage = ""; 
            if (isMagicalCrit)
            {
                rawDamage *= CRITICAL_HIT_MULTIPLIER; 
                critMessage = " (MAGICAL CRITICAL HIT!)";
            }

            // TODO: Apply Caster's general damage % modifiers (Future)

            // TODO: Apply Target's Mitigations (Magical Resistances/Vulnerabilities) (Future)
            // If isAbilityTrueDamage is true, this section would be skipped.
            // For now, no magical mitigations exist, so the effect of true damage is mainly for logging and future-proofing.
            // Example structure for when mitigations exist:
            // if (!isAbilityTrueDamage)
            // {
            //     // Apply magical resistances/vulnerabilities to rawDamage
            // }


            // TODO: Apply +/- Damage Variance (Future)

            int finalDamage = Mathf.Max(1, Mathf.FloorToInt(rawDamage));

            DebugHelper.Log($"DamageCalculator: Magical ability '{ability.abilityName}' ({ (isAbilityTrueDamage ? "True Damage, " : "") }Type: {ability.damageType}) " +
                            $"from {caster.unitName} to {target?.unitName ?? "targetless effect"}{critMessage}. " +
                            $"BasePower: {ability.basePower}, SparkBonus: {casterSparkBonus}. Raw (post-crit, pre-mitigation): {rawDamage}. Final: {finalDamage}", caster);

            return finalDamage;
        }
    }
}