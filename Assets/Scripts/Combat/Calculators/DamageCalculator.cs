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
                DebugHelper.LogWarning("DamageCalculator: Attacker is null. Returning 0 damage.", null);
                return 0;
            }

            // Determine if this attack deals true damage
            bool isTrueDamage = false;
            if (attacker.equippedWeapon != null && attacker.equippedWeapon.dealsTrueDamage)
            {
                isTrueDamage = true;
                DebugHelper.Log($"DamageCalculator: {attacker.unitName}'s attack is True Damage (from weapon). PDR will be skipped.", attacker);
            }
            // Note: Unarmed attacks are not flagged as true damage by default via this weapon check.
            // If unarmed should be true damage, that would need separate logic or a flag on the unit/unarmed "profile".

            // 1. Base Outgoing Damage
            int attackerCoreBonus = 0;
            if (attacker.currentAttributes != null)
            {
                attackerCoreBonus = Mathf.FloorToInt(attacker.currentAttributes.Core / 4f);
            }
            float rawDamage = baseDamageFromWeaponOrUnarmed + attackerCoreBonus;

            // 2. Apply Crit Multiplier
            if (criticalMultiplier > 1.0f)
            {
                rawDamage *= criticalMultiplier;
            }

            // 3. Apply Attacker's general damage % modifiers (Future)

            // 4. Apply Defender's Mitigations (PDR for Physical) - SKIPPED IF TRUE DAMAGE
            float pdrPercentage = 0f;
            float damageAfterPDR = rawDamage; // Initialize with rawDamage

            if (!isTrueDamage) // MODIFIED: Only apply PDR if not true damage
            {
                if (defender != null && defender.equippedBodyArmor != null)
                {
                    int defenderArmorValue = defender.equippedBodyArmor.armorValue;
                    if (defenderArmorValue > 0)
                    {
                        pdrPercentage = (float)defenderArmorValue / (defenderArmorValue + K_ARMOR_CONSTANT);
                    }
                    // DebugHelper.Log($"DamageCalculator: Defender {defender.unitName} has {defenderArmorValue} Armor. K_Constant: {K_ARMOR_CONSTANT}. PDR %: {pdrPercentage:P1}", defender);
                }
                // else if (defender != null)
                // {
                //    DebugHelper.Log($"DamageCalculator: Defender {defender.unitName} has no armor equipped. PDR %: 0%", defender);
                // }

                damageAfterPDR = rawDamage * (1f - pdrPercentage);
                // if (pdrPercentage > 0f)
                // {
                //    DebugHelper.Log($"DamageCalculator: After PDR ({pdrPercentage:P1}), damage is: {damageAfterPDR} (from {rawDamage})", attacker);
                // }
            }
            else // If it is true damage, damageAfterPDR remains rawDamage (PDR is skipped)
            {
                 // Optionally log that PDR was skipped due to true damage, though the earlier log already covers it.
            }


            // 5. Apply +/- 10% Damage Variance (Future)

            // 6. Ensure Minimum 1 Damage
            int finalDamage = Mathf.Max(1, Mathf.FloorToInt(damageAfterPDR));

            return finalDamage;
        }
    }
}