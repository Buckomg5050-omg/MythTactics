// UnitCombat.cs
using UnityEngine;
using System.Collections;
using MythTactics.Combat;
using System.Collections.Generic;
using System.Text;

[RequireComponent(typeof(Unit))]
public class UnitCombat : MonoBehaviour
{
    private Unit _unitMain;
    private UnitStats _unitStats;

    public void Initialize(Unit mainUnit)
    {
        _unitMain = mainUnit;
        if (_unitMain == null)
        {
            Debug.LogError("UnitCombat.Initialize: Main Unit reference is null!", this);
            enabled = false;
            return;
        }
        _unitStats = _unitMain.Stats;
        if (_unitStats == null)
        {
            Debug.LogError($"UnitCombat.Initialize: UnitStats component not found on {_unitMain.unitName}!", this);
            enabled = false;
            return;
        }
    }

    // MODIFIED: Signature changed to accept an Action callback
    public IEnumerator PerformAttack(Unit targetUnit, System.Action onAttackComplete)
    {
        if (!_unitStats.IsAlive || targetUnit == null || !targetUnit.IsAlive)
        {
            DebugHelper.LogWarning($"{_unitMain.unitName} PerformAttack: Attacker/Target not alive or target is null.", _unitMain);
            onAttackComplete?.Invoke(); // Invoke callback even if attack doesn't proceed fully
            yield break;
        }

        if(!_unitMain.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
        {
            DebugHelper.LogWarning($"{_unitMain.unitName} cannot afford attack in PerformAttack. Needs {PlayerInputHandler.AttackActionCost} AP, Has {_unitStats.currentActionPoints}", _unitMain);
            onAttackComplete?.Invoke(); // Invoke callback
            yield break;
        }

        _unitMain.SpendAPForAction(PlayerInputHandler.AttackActionCost);
        DebugHelper.Log($"{_unitMain.unitName} attacks {targetUnit.unitName}. (AP: {_unitStats.currentActionPoints}/{_unitStats.MaxActionPoints})", _unitMain);

        if (_unitMain.Animation != null) 
        {
            // Optional: Face target before attacking
            // Vector3 directionToTarget = (targetUnit.transform.position - _unitMain.transform.position).normalized;
            // _unitMain.Animation.FaceDirection(directionToTarget); // Assuming Animation component has this
            yield return _unitMain.Animation.PlayAttackAnimation();
        }
        else 
        {
            yield return new WaitForSeconds(0.5f); // Fallback delay if no animation component
        }

        if (CombatCalculator.ResolveHit(_unitMain, targetUnit))
        {
            DebugHelper.Log($"{_unitMain.unitName}'s attack HITS {targetUnit.unitName}!", _unitMain);
            bool isCritical = CombatCalculator.CheckCriticalHit(_unitMain, targetUnit);
            float criticalDamageMultiplier = 1.0f;
            string critMessage = "";
            if (isCritical)
            {
                criticalDamageMultiplier = DamageCalculator.CRITICAL_HIT_MULTIPLIER;
                critMessage = " (CRITICAL HIT!)";
                DebugHelper.Log($"CRITICAL HIT by {_unitMain.unitName} on {targetUnit.unitName}!", _unitMain);
            }

            // Ensure target is still valid before calculating/applying damage
            if (targetUnit != null && targetUnit.IsAlive) 
            {
                int currentAttackBaseDamage = (_unitMain.equippedWeapon != null) ? _unitMain.equippedWeapon.baseDamage : DamageCalculator.UNARMED_BASE_DAMAGE;
                int totalDamage = DamageCalculator.CalculatePhysicalAttackDamage(currentAttackBaseDamage, _unitMain, targetUnit, criticalDamageMultiplier);
                DebugHelper.Log($"{_unitMain.unitName} dealing {totalDamage} damage to {targetUnit.unitName}{critMessage}.", _unitMain);
                
                if (targetUnit.gameObject.activeInHierarchy && targetUnit.Combat != null)
                {
                    // We wait for TakeDamage to complete if it's a coroutine
                    yield return targetUnit.StartCoroutine(targetUnit.Combat.TakeDamage(totalDamage)); 
                }
                else if (targetUnit.Combat == null)
                {
                    DebugHelper.LogError($"{targetUnit.unitName} is missing UnitCombat component. Cannot apply damage.", targetUnit);
                }
            }
            else
            {
                 DebugHelper.Log($"{_unitMain.unitName}'s attack hit, but target {targetUnit?.unitName ?? "Unknown"} is no longer valid (null or not alive) before damage application.", _unitMain);
            }
        }
        else
        {
            DebugHelper.Log($"{_unitMain.unitName}'s attack MISSES {targetUnit.unitName}!", _unitMain);
        }

        // MODIFIED: Call the onAttackComplete callback instead of directly interacting with PIH
        onAttackComplete?.Invoke();
    }

    // MODIFIED: PerformAbility also now takes an Action callback
    public IEnumerator PerformAbility(AbilitySO ability, Unit targetUnit, System.Action onAbilityComplete)
    {
        if (!_unitStats.IsAlive || ability == null)
        {
            DebugHelper.LogWarning($"{_unitMain.unitName} PerformAbility called with null ability or dead unit.", _unitMain);
            onAbilityComplete?.Invoke();
            yield break;
        }
        if (!CanAffordAbility(ability, true)) // logIfNotAffordable is true by default
        {
            onAbilityComplete?.Invoke();
            yield break;
        }

        Unit abilityTarget = null; 

        switch (ability.targetType)
        {
            case AbilityTargetType.Self:
                abilityTarget = _unitMain;
                break;
            case AbilityTargetType.EnemyUnit:
            case AbilityTargetType.AllyUnit: 
                if (targetUnit == null || !targetUnit.IsAlive)
                {
                    DebugHelper.LogWarning($"{_unitMain.unitName} cannot perform ability {ability.abilityName}: Target unit is invalid or not alive.", _unitMain);
                    onAbilityComplete?.Invoke();
                    yield break;
                }
                if (_unitMain.Movement == null || _unitMain.Movement.CurrentTile == null || targetUnit.Movement == null || targetUnit.Movement.CurrentTile == null)
                {
                    DebugHelper.LogWarning($"{_unitMain.unitName} or target {targetUnit.unitName} is not on a tile. Cannot perform ability {ability.abilityName}.", _unitMain);
                    onAbilityComplete?.Invoke();
                    yield break;
                }
                if (GridManager.Instance.CalculateManhattanDistance(_unitMain.Movement.CurrentTile.gridPosition, targetUnit.Movement.CurrentTile.gridPosition) > ability.range)
                {
                    DebugHelper.LogWarning($"{_unitMain.unitName} cannot perform ability {ability.abilityName} on {targetUnit.unitName}: Target out of range.", _unitMain);
                    onAbilityComplete?.Invoke();
                    yield break;
                }
                abilityTarget = targetUnit;
                break;
            case AbilityTargetType.Tile:
                 DebugHelper.LogWarning($"AbilityTargetType.Tile not fully handled yet for {ability.abilityName}.", _unitMain);
                 // For now, assume it completes even if not fully handled
                 onAbilityComplete?.Invoke(); 
                 yield break; 
            default:
                DebugHelper.LogError($"Unsupported AbilityTargetType: {ability.targetType} for {ability.abilityName}", _unitMain);
                onAbilityComplete?.Invoke();
                yield break;
        }

        SpendResourcesForAbility(ability);

        StringBuilder logMessageBuilder = new StringBuilder();
        logMessageBuilder.Append(_unitMain.unitName).Append(" uses ").Append(ability.abilityName).Append(".");
        if (abilityTarget != null && abilityTarget != _unitMain) { logMessageBuilder.Append(" Targeting ").Append(abilityTarget.unitName).Append("."); }
        else if (ability.targetType == AbilityTargetType.Self) { logMessageBuilder.Append(" On SELF."); }
        logMessageBuilder.Append(" (AP: ").Append(_unitStats.currentActionPoints).Append("/").Append(_unitStats.MaxActionPoints).Append(",");
        logMessageBuilder.Append(" MP: ").Append(_unitStats.currentManaPoints).Append("/").Append(_unitStats.MaxManaPoints).Append(" ...)"); // Truncated for brevity
        DebugHelper.Log(logMessageBuilder.ToString(), _unitMain);

        if (_unitMain.Animation != null) yield return _unitMain.Animation.PlayAttackAnimation(); // Or a generic ability animation
        else yield return new WaitForSeconds(0.5f);

        bool abilityEffectConnects = true; 
        bool needsHitRoll = (ability.targetType == AbilityTargetType.EnemyUnit ||
                             (ability.targetType == AbilityTargetType.AllyUnit && 
                              (ability.effectType == AbilityEffectType.Damage || ability.effectType == AbilityEffectType.Debuff))) &&
                             ability.baseAccuracy > 0; 

        if (ability.targetType == AbilityTargetType.Self || 
            (ability.targetType == AbilityTargetType.AllyUnit && ability.effectType == AbilityEffectType.Buff) ||
            (ability.effectType == AbilityEffectType.None)) 
        {
            needsHitRoll = false;
        }

        if (needsHitRoll)
        {
            if (abilityTarget == null)
            {
                DebugHelper.LogError($"PerformAbility: abilityTarget is null for a unit-targeting ability '{ability.abilityName}' that needs a hit roll.", _unitMain);
                abilityEffectConnects = false;
            }
            else { abilityEffectConnects = CombatCalculator.ResolveAbilityHit(ability, _unitMain, abilityTarget); }
        }

        if (abilityEffectConnects)
        {
            if (ability.effectType == AbilityEffectType.Damage && ability.basePower > 0)
            {
                if (abilityTarget != null && abilityTarget.IsAlive && abilityTarget.Combat != null)
                {
                    int totalDamage = DamageCalculator.CalculateMagicalAbilityDamage(ability, _unitMain, abilityTarget); 
                    DebugHelper.Log($"{_unitMain.unitName}'s ability '{ability.abilityName}' deals {totalDamage} initial damage to {abilityTarget.unitName}.", _unitMain);
                    if (abilityTarget.gameObject.activeInHierarchy)
                    {
                        yield return abilityTarget.StartCoroutine(abilityTarget.Combat.TakeDamage(totalDamage));
                    }
                }
                else if (abilityTarget != null && abilityTarget.Combat == null) { DebugHelper.LogError($"{abilityTarget.unitName} is missing UnitCombat. Cannot apply ability damage.", abilityTarget); }
            }
            else if (ability.effectType == AbilityEffectType.Heal && ability.basePower > 0)
            {
                if (abilityTarget != null && abilityTarget.IsAlive && abilityTarget.Stats != null)
                {
                    int baseHealAmount = ability.basePower;
                    int magicalPotencyBonus = (_unitMain.Stats != null) ? Mathf.FloorToInt(_unitMain.Stats.EffectiveAttributes.Spark / 4f) : 0;
                    int totalHeal = baseHealAmount + magicalPotencyBonus;
                    totalHeal = Mathf.Max(0, totalHeal);
                    DebugHelper.Log($"{_unitMain.unitName}'s ability '{ability.abilityName}' initially heals {abilityTarget.unitName} for {totalHeal} VP.", _unitMain);
                    abilityTarget.Stats.ModifyVitality(totalHeal);
                }
            }

            if (EffectSystem.Instance != null && ability.effectsToApplyOnHit != null && ability.effectsToApplyOnHit.Count > 0)
            {
                Unit effectRecipient = (ability.targetType == AbilityTargetType.Self) ? _unitMain : abilityTarget;
                if (effectRecipient != null && effectRecipient.IsAlive)
                {
                    foreach (EffectSO effectSO in ability.effectsToApplyOnHit)
                    {
                        if (effectSO != null)
                        {
                            DebugHelper.Log($"{_unitMain.unitName}'s ability '{ability.abilityName}' attempts to apply effect '{effectSO.effectName}' to {effectRecipient.unitName}.", _unitMain);
                            EffectSystem.Instance.ApplyEffect(effectRecipient, effectSO, _unitMain);
                        }
                    }
                }
                else if (effectRecipient != null && !effectRecipient.IsAlive) { DebugHelper.Log($"Effect recipient {effectRecipient.unitName} for ability '{ability.abilityName}' is not alive. Effects not applied.", _unitMain); }
            }
            else if (EffectSystem.Instance == null && ability.effectsToApplyOnHit != null && ability.effectsToApplyOnHit.Count > 0)
            { DebugHelper.LogError("EffectSystem.Instance is null! Cannot apply status effects.", _unitMain); }
        }
        else { DebugHelper.Log($"{ability.abilityName} from {_unitMain.unitName} MISSES {abilityTarget?.unitName ?? "intended target"}!", _unitMain); }

        onAbilityComplete?.Invoke();
    }

    public bool CanAffordAbility(AbilitySO ability, bool logIfNotAffordable = false)
    {
        if (ability == null) { DebugHelper.LogError(_unitMain.unitName + ": Attempted to check affordability for a NULL ability.", _unitMain); return false; }
        if (!_unitStats.IsAlive) return false;

        if (!_unitMain.CanAffordAPForAction(ability.apCost))
        {
            if (logIfNotAffordable) DebugHelper.LogWarning($"{_unitMain.unitName} cannot afford '{ability.abilityName}'. Insufficient AP. Needs: {ability.apCost}, Has: {_unitStats.currentActionPoints}.", _unitMain);
            return false;
        }
        if (_unitStats.currentManaPoints < ability.mpCost)
        {
            if (logIfNotAffordable) DebugHelper.LogWarning($"{_unitMain.unitName} cannot afford '{ability.abilityName}'. Insufficient MP. Needs: {ability.mpCost}, Has: {_unitStats.currentManaPoints}.", _unitMain);
            return false;
        }
        if (_unitStats.currentStaminaPoints < ability.spCost)
        {
            if (logIfNotAffordable) DebugHelper.LogWarning($"{_unitMain.unitName} cannot afford '{ability.abilityName}'. Insufficient SP. Needs: {ability.spCost}, Has: {_unitStats.currentStaminaPoints}.", _unitMain);
            return false;
        }
        if (_unitStats.currentFocusPoints < ability.fpCost)
        {
            if (logIfNotAffordable) DebugHelper.LogWarning($"{_unitMain.unitName} cannot afford '{ability.abilityName}'. Insufficient FP. Needs: {ability.fpCost}, Has: {_unitStats.currentFocusPoints}.", _unitMain);
            return false;
        }
        if (_unitStats.currentInfluencePoints < ability.ipCost)
        {
            if (logIfNotAffordable) DebugHelper.LogWarning($"{_unitMain.unitName} cannot afford '{ability.abilityName}'. Insufficient IP. Needs: {ability.ipCost}, Has: {_unitStats.currentInfluencePoints}.", _unitMain);
            return false;
        }
        return true;
    }

    public void SpendResourcesForAbility(AbilitySO ability)
    {
        if (ability == null || !_unitStats.IsAlive) return;
        _unitMain.SpendAPForAction(ability.apCost);
        if (ability.mpCost > 0) _unitStats.SpendMana(ability.mpCost);
        if (ability.spCost > 0) _unitStats.SpendStamina(ability.spCost);
        if (ability.fpCost > 0) _unitStats.SpendFocus(ability.fpCost);
        if (ability.ipCost > 0) _unitStats.SpendInfluence(ability.ipCost);
    }

    public IEnumerator TakeDamage(int damageAmount) // Removed caster param as it's not used here
    {
        if (!_unitStats.IsAlive) yield break;
        _unitStats.ModifyVitality(-damageAmount);
        DebugHelper.Log($"{_unitMain.unitName} takes {damageAmount} damage, has {_unitStats.currentVitalityPoints}/{_unitStats.MaxVitalityPoints} VP remaining. (Alive: {_unitStats.IsAlive})", _unitMain);

        if (_unitStats.IsAlive)
        {
            if (_unitMain.Animation != null) yield return _unitMain.Animation.PlayHurtAnimation();
            else yield return new WaitForSeconds(0.3f);
        }
        else
        {
            // Ensure Die() is also a coroutine if it has yields
            yield return _unitMain.StartCoroutine(Die()); 
        }
    }

    private IEnumerator Die()
    {
        DebugHelper.Log($"!!!!!! {_unitMain.unitName} has been defeated! !!!!!!", _unitMain);
        if (_unitMain.Movement != null && _unitMain.Movement.IsMoving) _unitMain.Movement.StopMovementCoroutines();
        if (_unitMain.Animation != null) yield return _unitMain.Animation.PlayDeathAnimation();
        else yield return new WaitForSeconds(1.0f);
        if (_unitMain.Movement != null) _unitMain.Movement.ClearCurrentTileReferenceForDeath();
        if (TurnManager.Instance != null) TurnManager.Instance.UnregisterUnit(_unitMain);
        _unitStats?.ClearAllEffects();
        if (_unitMain.gameObject != null) _unitMain.gameObject.SetActive(false);
    }
}