// DamageCalculator.cs
// Located in: Assets/Scripts/Combat/
using UnityEngine;

namespace MythTactics.Combat 
{
    public static class DamageCalculator
    {
        // This constant could also live in a GameBalanceSO or similar if many systems need it.
        // For now, DamageCalculator is a reasonable place if it's primarily for default damage values.
        public const int UNARMED_BASE_DAMAGE = 1; 

        /// <summary>
        /// Calculates the damage for a physical attack.
        /// </summary>
        /// <param name="baseDamage">The base damage of the attack (e.g., from weapon or unarmed).</param>
        /// <param name="attacker">The unit performing the attack.</param>
        /// <param name="defender">The unit receiving the attack.</param>
        /// <returns>The calculated damage amount.</returns>
        public static int CalculatePhysicalAttackDamage(int baseDamage, Unit attacker, Unit defender) // Renamed and takes baseDamage
        {
            if (attacker == null)
            {
                DebugHelper.LogError("DamageCalculator: Attacker is null.", null);
                return 0; 
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

            int totalDamage = baseDamage + coreBonus;
            
            return Mathf.Max(1, totalDamage); 
        }
    }
}