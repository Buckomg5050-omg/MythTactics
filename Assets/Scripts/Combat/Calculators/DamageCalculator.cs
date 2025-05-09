// DamageCalculator.cs
using UnityEngine;

namespace MythTactics.Combat
{
    public static class DamageCalculator
    {
        public const int UNARMED_BASE_DAMAGE = 1;
        public const float CRITICAL_HIT_MULTIPLIER = 1.5f;

        // NEW: Constant for PDR calculation (GDD 7.1.2)
        // This value balances how effective armor is. Higher K means armor contributes less to PDR for the same armor value.
        // Example values: 50 for faster "diminishing returns" on armor, 100 for slower. Let's start with 50.
        public const float K_ARMOR_CONSTANT = 50f;

        public static int CalculatePhysicalAttackDamage(int baseDamageFromWeaponOrUnarmed, Unit attacker, Unit defender, float criticalMultiplier = 1.0f)
        {
            if (attacker == null)
            {
                DebugHelper.LogWarning("DamageCalculator: Attacker is null. Returning 0 damage.", null);
                return 0;
            }
            if (defender == null)
            {
                DebugHelper.LogWarning("DamageCalculator: Defender is null. Cannot calculate PDR. Proceeding without armor reduction.", attacker);
                // Or return early if defender being null is critical error
            }

            // 1. Base Outgoing Damage (Physical: BaseWeaponDamage + Floor(Core / 4))
            int attackerCoreBonus = 0;
            if (attacker.currentAttributes != null)
            {
                attackerCoreBonus = Mathf.FloorToInt(attacker.currentAttributes.Core / 4f);
            }
            float rawDamage = baseDamageFromWeaponOrUnarmed + attackerCoreBonus;
            // DebugHelper.Log($"DamageCalculator: Initial raw damage (base + core): {rawDamage}", attacker);


            // 2. Apply Crit Multiplier
            if (criticalMultiplier > 1.0f)
            {
                rawDamage *= criticalMultiplier;
                // DebugHelper.Log($"DamageCalculator: After crit ({criticalMultiplier}x), raw damage: {rawDamage}", attacker);
            }

            // 3. Apply Attacker's general damage % modifiers (Future - GDD 7.1.2 step 3)
            // rawDamage *= (1 + attacker.GetGeneralDamageBonusPercent());

            // 4. Apply Defender's Mitigations (PDR for Physical) (GDD 7.1.2 step 4)
            float pdrPercentage = 0f;
            if (defender != null && defender.equippedBodyArmor != null)
            {
                int defenderArmorValue = defender.equippedBodyArmor.armorValue;
                if (defenderArmorValue > 0) // Avoid division by zero if K_ARMOR_CONSTANT is 0 and armor is 0, though K should not be 0.
                {
                    pdrPercentage = (float)defenderArmorValue / (defenderArmorValue + K_ARMOR_CONSTANT);
                }
                // DebugHelper.Log($"DamageCalculator: Defender {defender.unitName} has {defenderArmorValue} Armor. K_Constant: {K_ARMOR_CONSTANT}. PDR %: {pdrPercentage:P1}", defender);
            }
            else if (defender != null)
            {
                // DebugHelper.Log($"DamageCalculator: Defender {defender.unitName} has no armor equipped. PDR %: 0%", defender);
            }

            float damageAfterPDR = rawDamage * (1f - pdrPercentage);
            if (pdrPercentage > 0f)
            {
                // DebugHelper.Log($"DamageCalculator: After PDR ({pdrPercentage:P1}), damage is: {damageAfterPDR} (from {rawDamage})", attacker);
            }


            // 5. Apply +/- 10% Damage Variance (Future - GDD 7.1.2 step 5)
            // float variance = Random.Range(-0.10f, 0.10f);
            // damageAfterPDR *= (1 + variance);

            // 6. Ensure Minimum 1 Damage (GDD 7.1.2 step 6)
            int finalDamage = Mathf.Max(1, Mathf.FloorToInt(damageAfterPDR));

            // Consolidated log for the overall calculation result is in Unit.PerformAttack
            // This internal log can be useful for deep debugging PDR specifically:
            // DebugHelper.Log($"DamageCalculator Summary: {attacker.unitName} vs {defender?.unitName}. BaseInput: {baseDamageFromWeaponOrUnarmed}, CoreBns: {attackerCoreBonus}, CritMult: {criticalMultiplier}, PDR%: {pdrPercentage:P1}, FinalDmg: {finalDamage}", attacker);

            return finalDamage;
        }
    }
}