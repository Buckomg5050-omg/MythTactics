// DamageCalculator.cs
// Located in: Assets/Scripts/Combat/
using UnityEngine;

namespace MythTactics.Combat // Assuming other combat scripts might also use this namespace
{
    public static class DamageCalculator
    {
        private const int UNARMED_BASE_DAMAGE = 1; // Base damage for an unarmed attack

        /// <summary>
        /// Calculates the damage for a basic unarmed physical attack.
        /// </summary>
        /// <param name="attacker">The unit performing the attack.</param>
        /// <param name="defender">The unit receiving the attack (currently unused for this basic calculation but included for future expansion).</param>
        /// <returns>The calculated damage amount.</returns>
        public static int CalculateBasicUnarmedAttackDamage(Unit attacker, Unit defender)
        {
            if (attacker == null)
            {
                DebugHelper.LogError("DamageCalculator: Attacker is null.", null);
                return 0; // Or some other default/error value
            }

            int coreBonus = 0;
            if (attacker.currentAttributes != null)
            {
                coreBonus = Mathf.FloorToInt(attacker.currentAttributes.Core / 4f);
            }
            else
            {
                DebugHelper.LogWarning($"DamageCalculator: Attacker '{attacker.unitName}' has no attributes to calculate Core damage bonus. Core bonus will be 0.", attacker);
            }

            int totalDamage = UNARMED_BASE_DAMAGE + coreBonus;
            
            // Ensure minimum 1 damage as per GDD 7.1.2 (applies to final damage, but good to enforce early too)
            return Mathf.Max(1, totalDamage); 
        }

        // Future methods could be added here for:
        // - Calculating damage with equipped weapons
        // - Calculating magical damage
        // - Applying critical hits
        // - Applying defender mitigations (armor, resistances)
        // - Applying damage variance
    }
}