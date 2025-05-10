// UnitCombat.cs
using UnityEngine;
using System.Collections;
using MythTactics.Combat; 

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

    public IEnumerator PerformAttack(Unit targetUnit, PlayerInputHandler attackerPIHContext)
    {
        if (!_unitStats.IsAlive || targetUnit == null || !targetUnit.IsAlive || !_unitMain.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
            yield break;
        
        _unitMain.SpendAPForAction(PlayerInputHandler.AttackActionCost);
        DebugHelper.Log($"{_unitMain.unitName} attacks {targetUnit.unitName}. (AP: {_unitMain.currentActionPoints})", _unitMain);
        
        if (_unitMain.Animation != null) yield return _unitMain.Animation.PlayAttackAnimation();
        else yield return new WaitForSeconds(0.5f); 

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
            if (targetUnit != null && targetUnit.IsAlive)
            {
                int currentAttackBaseDamage = (_unitMain.equippedWeapon != null) ? _unitMain.equippedWeapon.baseDamage : DamageCalculator.UNARMED_BASE_DAMAGE;
                int totalDamage = DamageCalculator.CalculatePhysicalAttackDamage(currentAttackBaseDamage, _unitMain, targetUnit, criticalDamageMultiplier);
                DebugHelper.Log($"{_unitMain.unitName} dealing {totalDamage} damage to {targetUnit.unitName}{critMessage}.", _unitMain);
                if (targetUnit != null && targetUnit.gameObject.activeInHierarchy && targetUnit.Combat != null)
                {
                    yield return targetUnit.StartCoroutine(targetUnit.Combat.TakeDamage(totalDamage));
                }
                else if (targetUnit.Combat == null)
                {
                    DebugHelper.LogError($"{targetUnit.unitName} is missing UnitCombat component. Cannot apply damage.", targetUnit);
                }
            }
        }
        else
        {
            DebugHelper.Log($"{_unitMain.unitName}'s attack MISSES {targetUnit.unitName}!", _unitMain);
        }

        if (attackerPIHContext != null && attackerPIHContext.SelectedUnit == _unitMain)
        {
            attackerPIHContext.CheckAndHandleEndOfTurnActionsPIH();
        }
    }

    public IEnumerator PerformAbility(AbilitySO ability, Unit targetUnit, PlayerInputHandler attackerPIHContext)
    {
        if (!_unitStats.IsAlive || ability == null)
        {
            DebugHelper.LogWarning($"{_unitMain.unitName} PerformAbility called with null ability or dead unit.", _unitMain);
            yield break;
        }
        if (!CanAffordAbility(ability, true)) 
        {
            // CanAffordAbility now logs the specific reason
            yield break;
        }

        if (ability.targetType == AbilityTargetType.EnemyUnit || ability.targetType == AbilityTargetType.AllyUnit)
        {
            if (targetUnit == null || !targetUnit.IsAlive) { DebugHelper.LogWarning($"{_unitMain.unitName} cannot perform ability {ability.abilityName}: Target unit is invalid or not alive.", _unitMain); yield break; }
            if (_unitMain.Movement == null || _unitMain.Movement.CurrentTile == null || targetUnit.Movement == null || targetUnit.Movement.CurrentTile == null) 
            { DebugHelper.LogWarning($"{_unitMain.unitName} or target {targetUnit.unitName} is not on a tile (or missing Movement comp). Cannot perform ability {ability.abilityName}.", _unitMain); yield break; }
            if (GridManager.Instance.CalculateManhattanDistance(_unitMain.Movement.CurrentTile.gridPosition, targetUnit.Movement.CurrentTile.gridPosition) > ability.range) 
            { DebugHelper.LogWarning($"{_unitMain.unitName} cannot perform ability {ability.abilityName} on {targetUnit.unitName}: Target out of range.", _unitMain); yield break; }
        }

        SpendResourcesForAbility(ability); 

        // MODIFIED: Log includes all current resources AFTER spending
        DebugHelper.Log($"{_unitMain.unitName} uses {ability.abilityName}." + (targetUnit != null ? $" Targeting {targetUnit.unitName}." : "") + 
                        $" (AP: {_unitMain.currentActionPoints}, MP: {_unitStats.currentManaPoints}, SP: {_unitStats.currentStaminaPoints}, FP: {_unitStats.currentFocusPoints}, IP: {_unitStats.currentInfluencePoints})", _unitMain);

        if (_unitMain.Animation != null) yield return _unitMain.Animation.PlayAttackAnimation(); 
        else yield return new WaitForSeconds(0.5f); 

        bool abilityHits = true;
        if (ability.targetType == AbilityTargetType.EnemyUnit || ability.targetType == AbilityTargetType.AllyUnit)
        {
            if (targetUnit == null)
            {
                DebugHelper.LogError($"PerformAbility: TargetUnit is null for a unit-targeting ability '{ability.abilityName}'.", _unitMain);
                abilityHits = false;
            }
            else
            {
                abilityHits = CombatCalculator.ResolveAbilityHit(ability, _unitMain, targetUnit);
            }
        }

        if (abilityHits)
        {
            if (ability.effectType == AbilityEffectType.Damage)
            {
                if (targetUnit != null && targetUnit.IsAlive) 
                {
                    if (targetUnit.Combat == null)
                    {
                        DebugHelper.LogError($"{targetUnit.unitName} is missing UnitCombat component. Cannot apply ability damage.", targetUnit);
                    }
                    else
                    {
                        int totalDamage = DamageCalculator.CalculateMagicalAbilityDamage(ability, _unitMain, targetUnit);
                        if (targetUnit.gameObject.activeInHierarchy) 
                        {
                            yield return targetUnit.StartCoroutine(targetUnit.Combat.TakeDamage(totalDamage));
                        }
                    }
                }
                else if (ability.targetType != AbilityTargetType.Self && (targetUnit == null || !targetUnit.IsAlive) )
                { 
                    DebugHelper.LogWarning($"{ability.abilityName} by {_unitMain.unitName} was meant to damage, but target is invalid/null post-hit-check.", _unitMain); 
                }
            }
            else if (ability.effectType == AbilityEffectType.Heal) 
            { 
                DebugHelper.Log($"{ability.abilityName} would heal (not implemented).", _unitMain); 
            }
        }
        else 
        {
             DebugHelper.Log($"{ability.abilityName} from {_unitMain.unitName} MISSES {targetUnit?.unitName ?? "intended target"}!", _unitMain);
        }
        
        if (attackerPIHContext != null && attackerPIHContext.SelectedUnit == _unitMain) 
        {
            attackerPIHContext.CheckAndHandleEndOfTurnActionsPIH();
        }
    }

    public bool CanAffordAbility(AbilitySO ability, bool logIfNotAffordable = false)
    {
        if (ability == null) { DebugHelper.LogError($"{_unitMain.unitName}: Attempted to check affordability for a NULL ability.", _unitMain); return false; }
        if (!_unitStats.IsAlive) return false;
        if (!_unitMain.CanAffordAPForAction(ability.apCost)) 
        {
            if (logIfNotAffordable) DebugHelper.LogWarning($"{_unitMain.unitName} cannot afford '{ability.abilityName}'. Insufficient AP. Needs: {ability.apCost}, Has: {_unitMain.currentActionPoints}.", _unitMain);
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
        // TODO: Add checks for FP and IP costs from AbilitySO
        return true;
    }

    public void SpendResourcesForAbility(AbilitySO ability)
    {
        if (ability == null || !_unitStats.IsAlive) return;
        _unitMain.SpendAPForAction(ability.apCost); 

        if (ability.mpCost > 0) _unitStats.SpendMana(ability.mpCost);
        if (ability.spCost > 0) _unitStats.SpendStamina(ability.spCost);
        // TODO: Add spending for FP and IP costs from AbilitySO
    }

    public IEnumerator TakeDamage(int damageAmount)
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
        
        if (_unitMain.gameObject != null) _unitMain.gameObject.SetActive(false); 
    }
}