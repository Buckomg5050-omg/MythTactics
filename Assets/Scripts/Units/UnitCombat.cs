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

    public IEnumerator PerformAttack(Unit targetUnit, System.Action onAttackComplete)
    {
        if (!_unitStats.IsAlive || targetUnit == null || !targetUnit.IsAlive)
        {
            onAttackComplete?.Invoke(); 
            yield break;
        }

        if(!_unitMain.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
        {
            onAttackComplete?.Invoke(); 
            yield break;
        }

        _unitMain.SpendAPForAction(PlayerInputHandler.AttackActionCost);

        if (_unitMain.Animation != null) 
        {
            yield return _unitMain.Animation.PlayAttackAnimation();
        }
        else 
        {
            yield return new WaitForSeconds(0.5f); 
        }

        if (CombatCalculator.ResolveHit(_unitMain, targetUnit))
        {
            bool isCritical = CombatCalculator.CheckCriticalHit(_unitMain, targetUnit);
            float criticalDamageMultiplier = isCritical ? DamageCalculator.CRITICAL_HIT_MULTIPLIER : 1.0f;

            if (targetUnit != null && targetUnit.IsAlive) 
            {
                int currentAttackBaseDamage = (_unitMain.equippedWeapon != null) ? _unitMain.equippedWeapon.baseDamage : DamageCalculator.UNARMED_BASE_DAMAGE;
                int totalDamage = DamageCalculator.CalculatePhysicalAttackDamage(currentAttackBaseDamage, _unitMain, targetUnit, criticalDamageMultiplier);
                
                DamageType attackDamageType = DamageType.Physical; 
                CombatLogger.LogDamage(_unitMain, targetUnit, totalDamage, attackDamageType, isCritical);

                if (targetUnit.gameObject.activeInHierarchy && targetUnit.Combat != null)
                {
                    yield return targetUnit.StartCoroutine(targetUnit.Combat.TakeDamage(totalDamage, _unitMain)); 
                }
                else if (targetUnit.Combat == null)
                {
                    DebugHelper.LogError($"{targetUnit.unitName} is missing UnitCombat component. Cannot apply damage.", targetUnit);
                }
            }
        }
        else
        {
            CombatLogger.LogEvent($"{_unitMain.unitName}'s attack MISSES {targetUnit.unitName}.", Color.yellow); 
        }

        onAttackComplete?.Invoke();
    }

    public IEnumerator PerformAbility(AbilitySO ability, Unit targetUnit, System.Action onAbilityComplete)
    {
        if (!_unitStats.IsAlive || ability == null)
        {
            onAbilityComplete?.Invoke();
            yield break;
        }
        if (!CanAffordAbility(ability, true)) 
        {
            onAbilityComplete?.Invoke();
            yield break;
        }

        Unit abilityTarget = null; 
        switch (ability.targetType)
        {
            case AbilityTargetType.Self: abilityTarget = _unitMain; break;
            case AbilityTargetType.EnemyUnit: case AbilityTargetType.AllyUnit: 
                if (targetUnit == null || !targetUnit.IsAlive) { onAbilityComplete?.Invoke(); yield break; }
                if (_unitMain.Movement == null || targetUnit.Movement == null || _unitMain.CurrentTile == null || targetUnit.CurrentTile == null) { onAbilityComplete?.Invoke(); yield break; }
                if (GridManager.Instance.CalculateManhattanDistance(_unitMain.CurrentTile.gridPosition, targetUnit.CurrentTile.gridPosition) > ability.range) { onAbilityComplete?.Invoke(); yield break; }
                abilityTarget = targetUnit; break;
            case AbilityTargetType.Tile: onAbilityComplete?.Invoke(); yield break; 
            default: onAbilityComplete?.Invoke(); yield break;
        }

        SpendResourcesForAbility(ability);

        StringBuilder logMessageBuilder = new StringBuilder();
        logMessageBuilder.Append(_unitMain.unitName).Append(" uses ").Append(ability.abilityName);
        if (abilityTarget != null && abilityTarget != _unitMain) { logMessageBuilder.Append(" targeting ").Append(abilityTarget.unitName); }
        else if (ability.targetType == AbilityTargetType.Self) { logMessageBuilder.Append(" on SELF"); }
        CombatLogger.LogEvent(logMessageBuilder.ToString() + ".", Color.white, LogMessageType.CombatAction);

        if (_unitMain.Animation != null) yield return _unitMain.Animation.PlayAttackAnimation(); 
        else yield return new WaitForSeconds(0.5f);

        bool abilityEffectConnects = true; 
        bool needsHitRoll = (ability.targetType == AbilityTargetType.EnemyUnit ||
                             (ability.targetType == AbilityTargetType.AllyUnit && 
                              (ability.effectType == AbilityEffectType.Damage || ability.effectType == AbilityEffectType.Debuff))) &&
                             ability.baseAccuracy > 0 && ability.baseAccuracy < 100; 

        if (ability.targetType == AbilityTargetType.Self || 
            (ability.targetType == AbilityTargetType.AllyUnit && (ability.effectType == AbilityEffectType.Buff || ability.effectType == AbilityEffectType.Heal)) ||
            (ability.effectType == AbilityEffectType.None)) 
        {
            needsHitRoll = false;
        }

        if (needsHitRoll)
        {
            if (abilityTarget == null) { abilityEffectConnects = false; }
            else { abilityEffectConnects = CombatCalculator.ResolveAbilityHit(ability, _unitMain, abilityTarget); }
        }

        if (abilityEffectConnects)
        {
            if (ability.effectType == AbilityEffectType.Damage && ability.basePower > 0)
            {
                if (abilityTarget != null && abilityTarget.IsAlive && abilityTarget.Combat != null)
                {
                    bool isMagicalCritical = (ability.damageType != DamageType.Physical && CombatCalculator.CheckMagicalCriticalHit(_unitMain, abilityTarget));
                    bool isPhysicalCritical = (ability.damageType == DamageType.Physical && CombatCalculator.CheckCriticalHit(_unitMain, abilityTarget));
                    bool isCritical = isMagicalCritical || isPhysicalCritical;

                    int totalDamage = DamageCalculator.CalculateMagicalAbilityDamage(ability, _unitMain, abilityTarget); 
                    CombatLogger.LogDamage(_unitMain, abilityTarget, totalDamage, ability.damageType, isCritical); 

                    if (abilityTarget.gameObject.activeInHierarchy)
                    {
                        yield return abilityTarget.StartCoroutine(abilityTarget.Combat.TakeDamage(totalDamage, _unitMain)); 
                    }
                }
            }
            else if (ability.effectType == AbilityEffectType.Heal && ability.basePower > 0)
            {
                if (abilityTarget != null && abilityTarget.IsAlive && abilityTarget.Stats != null)
                {
                    int baseHealAmount = ability.basePower;
                    int magicalPotencyBonus = (_unitMain.Stats != null) ? Mathf.FloorToInt(_unitMain.Stats.EffectiveAttributes.Spark / 4f) : 0;
                    int totalHeal = baseHealAmount + magicalPotencyBonus;
                    totalHeal = Mathf.Max(0, totalHeal);
                    CombatLogger.LogHeal(_unitMain, abilityTarget, totalHeal); 
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
                            EffectSystem.Instance.ApplyEffect(effectRecipient, effectSO, _unitMain);
                        }
                    }
                }
            }
        }
        else { 
            CombatLogger.LogEvent($"{_unitMain.unitName}'s ability {ability.abilityName} MISSES {abilityTarget?.unitName ?? "target"}.", Color.yellow, LogMessageType.CombatAction);
        }

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

    public IEnumerator TakeDamage(int damageAmount, Unit attacker = null) 
    {
        if (!_unitStats.IsAlive) yield break;
        _unitStats.ModifyVitality(-damageAmount);
        
        string attackerName = attacker != null ? attacker.unitName : "Effects"; 
        Color logColor = Color.red; 
        if (attacker != null && _unitMain.CompareTag("Player") && !attacker.CompareTag("Player")) logColor = new Color(1f, 0.6f, 0.6f); 
        else if (attacker != null && !_unitMain.CompareTag("Player") && attacker.CompareTag("Player")) logColor = new Color(1f, 0.8f, 0.3f); 

        CombatLogger.LogEvent($"{_unitMain.unitName} takes {damageAmount} damage from {attackerName}. ({_unitStats.currentVitalityPoints}/{_unitStats.MaxVitalityPoints} VP)", logColor, LogMessageType.System);

        if (_unitStats.IsAlive)
        {
            if (_unitMain.Animation != null) yield return _unitMain.Animation.PlayHurtAnimation();
            else yield return new WaitForSeconds(0.3f);
        }
        else
        {
            CombatLogger.LogEvent($"{_unitMain.unitName} has been defeated by {attackerName}!", Color.magenta, LogMessageType.CombatAction); 
            yield return _unitMain.StartCoroutine(Die()); 
        }
    }

    private IEnumerator Die()
    {
        if (_unitMain != null && !_unitMain.CompareTag("Player") && _unitMain.xpValue > 0)
        {
            // MODIFIED: Add XP to TurnManager's accumulator
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.AddBattleXP(_unitMain.xpValue);
            }
            // End of MODIFICATION
            CombatLogger.LogEvent($"{_unitMain.unitName} defeated! (Grants {_unitMain.xpValue} XP)", Color.cyan, LogMessageType.System);
        }

        if (_unitMain.Movement != null && _unitMain.Movement.IsMoving) _unitMain.Movement.StopMovementCoroutines();
        if (_unitMain.Animation != null) yield return _unitMain.Animation.PlayDeathAnimation();
        else yield return new WaitForSeconds(1.0f);
        if (_unitMain.Movement != null) _unitMain.Movement.ClearCurrentTileReferenceForDeath();
        if (TurnManager.Instance != null) TurnManager.Instance.UnregisterUnit(_unitMain);
        _unitStats?.ClearAllEffects(); 
        if (_unitMain.gameObject != null) _unitMain.gameObject.SetActive(false);
    }
}