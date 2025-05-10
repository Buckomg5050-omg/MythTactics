// DamageCalculator.cs
using UnityEngine;

namespace MythTactics.Combat
{
    public static class DamageCalculator
    {
        public const int UNARMED_BASE_DAMAGE = 1;
        public const float CRITICAL_HIT_MULTIPLIER = 1.5f; // Universal for now
        public const float K_ARMOR_CONSTANT = 50f;

        public static int CalculatePhysicalAttackDamage(int baseDamageFromWeaponOrUnarmed, Unit attacker, Unit defender, float criticalMultiplier = 1.0f)
        {
            // ... (existing CalculatePhysicalAttackDamage method remains unchanged) ...
            if (attacker == null)
            {
                DebugHelper.LogWarning("DamageCalculator: Attacker is null for physical attack. Returning 0 damage.", null);
                return 0;
            }

            bool isTrueDamage = false;
            if (attacker.equippedWeapon != null && attacker.equippedWeapon.dealsTrueDamage)
            {
                isTrueDamage = true;
                DebugHelper.Log($"DamageCalculator: {attacker.unitName}'s physical attack is True Damage (from weapon). PDR will be skipped.", attacker);
            }
            
            int attackerCoreBonus = (attacker.currentAttributes != null) ? Mathf.FloorToInt(attacker.currentAttributes.Core / 4f) : 0;
            float rawDamage = baseDamageFromWeaponOrUnarmed + attackerCoreBonus;

            if (criticalMultiplier > 1.0f) // This criticalMultiplier comes from CombatCalculator.CheckCriticalHit
            {
                rawDamage *= criticalMultiplier;
            }

            float pdrPercentage = 0f;
            float damageAfterPDR = rawDamage; 

            if (!isTrueDamage) 
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

            int casterSparkBonus = (caster.currentAttributes != null) ? Mathf.FloorToInt(caster.currentAttributes.Spark / 4f) : 0;
            float rawDamage = ability.basePower + casterSparkBonus;

            // MODIFIED: Check for magical critical hit
            bool isMagicalCrit = CombatCalculator.CheckMagicalCriticalHit(caster, target);
            string critMessage = ""; // For logging
            if (isMagicalCrit)
            {
                rawDamage *= CRITICAL_HIT_MULTIPLIER; // Using the universal crit multiplier for now
                critMessage = " (MAGICAL CRITICAL HIT!)";
                // The CombatCalculator.CheckMagicalCriticalHit already logs the crit occurrence.
                // We can add an additional log here if desired, or just let the final damage reflect it.
            }

            // ... (Future: general damage % modifiers, resistances, variance) ...

            int finalDamage = Mathf.Max(1, Mathf.FloorToInt(rawDamage));

            DebugHelper.Log($"DamageCalculator: Magical ability '{ability.abilityName}' from {caster.unitName} to {target?.unitName ?? "targetless effect"}{critMessage}. " +
                            $"BasePower: {ability.basePower}, SparkBonus: {casterSparkBonus}. Raw (post-crit if any): {rawDamage}. Final: {finalDamage}", caster);

            return finalDamage;
        }
    }
}