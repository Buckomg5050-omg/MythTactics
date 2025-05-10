// UnitCombat.cs
using UnityEngine;
using System.Collections;
using MythTactics.Combat; // For AbilitySO, enums, CombatCalculator, DamageCalculator

[RequireComponent(typeof(Unit))] // UnitCombat should always be on a GameObject with a Unit
public class UnitCombat : MonoBehaviour
{
    private Unit _unitMain; // Reference to the main Unit script
    private UnitStats _unitStats; // Quick access to UnitStats

    public void Initialize(Unit mainUnit)
    {
        _unitMain = mainUnit;
        if (_unitMain == null)
        {
            Debug.LogError("UnitCombat.Initialize: Main Unit reference is null!", this);
            enabled = false; // Disable if cannot initialize correctly
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

    // --- Combat Actions ---
    public IEnumerator PerformAttack(Unit targetUnit, PlayerInputHandler attackerPIHContext)
    {
        if (!_unitStats.IsAlive || targetUnit == null || !targetUnit.IsAlive || !_unitMain.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
        {
            yield break;
        }
        _unitMain.SpendAPForAction(PlayerInputHandler.AttackActionCost);
        DebugHelper.Log($"{_unitMain.unitName} attacks {targetUnit.unitName}. (AP: {_unitMain.currentActionPoints})", _unitMain);
        
        // Animation will be handled by UnitAnimation component later
        yield return _unitMain.StartCoroutine(_unitMain.PerformAttackAnimation()); // Still calling Unit's placeholder animation

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
            DebugHelper.LogWarning($"{_unitMain.unitName} somehow attempted PerformAbility for '{ability.abilityName}' without sufficient resources.", _unitMain);
            yield break;
        }

        // Target validation
        if (ability.targetType == AbilityTargetType.EnemyUnit || ability.targetType == AbilityTargetType.AllyUnit)
        {
            if (targetUnit == null || !targetUnit.IsAlive) { DebugHelper.LogWarning($"{_unitMain.unitName} cannot perform ability {ability.abilityName}: Target unit is invalid or not alive.", _unitMain); yield break; }
            if (_unitMain.CurrentTile == null || targetUnit.CurrentTile == null) { DebugHelper.LogWarning($"{_unitMain.unitName} or target {targetUnit.unitName} is not on a tile. Cannot perform ability {ability.abilityName}.", _unitMain); yield break; }
            if (GridManager.Instance.CalculateManhattanDistance(_unitMain.CurrentTile.gridPosition, targetUnit.CurrentTile.gridPosition) > ability.range) { DebugHelper.LogWarning($"{_unitMain.unitName} cannot perform ability {ability.abilityName} on {targetUnit.unitName}: Target out of range.", _unitMain); yield break; }
        }

        SpendResourcesForAbility(ability);
        DebugHelper.Log($"{_unitMain.unitName} uses {ability.abilityName}." + (targetUnit != null ? $" Targeting {targetUnit.unitName}." : "") + $" (AP: {_unitMain.currentActionPoints}, MP: {_unitStats.currentManaPoints}, SP: {_unitStats.currentStaminaPoints}, FP: {_unitStats.currentFocusPoints}, IP: {_unitStats.currentInfluencePoints})", _unitMain);

        // Animation will be handled by UnitAnimation component later
        yield return _unitMain.StartCoroutine(_unitMain.PerformAttackAnimation()); // Using placeholder attack animation

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
                    int totalDamage = DamageCalculator.CalculateMagicalAbilityDamage(ability, _unitMain, targetUnit);
                    if (targetUnit.gameObject.activeInHierarchy && targetUnit.Combat != null)
                    {
                        yield return targetUnit.StartCoroutine(targetUnit.Combat.TakeDamage(totalDamage));
                    }
                    else if (targetUnit.Combat == null)
                    {
                         DebugHelper.LogError($"{targetUnit.unitName} is missing UnitCombat component. Cannot apply ability damage.", targetUnit);
                    }
                }
                else if (ability.targetType != AbilityTargetType.Self && (targetUnit == null || !targetUnit.IsAlive))
                {
                    DebugHelper.LogWarning($"{ability.abilityName} by {_unitMain.unitName} was meant to damage, but target is invalid/null post-hit-check.", _unitMain);
                }
            }
            else if (ability.effectType == AbilityEffectType.Heal)
            {
                DebugHelper.Log($"{ability.abilityName} would heal (not implemented).", _unitMain);
            }
            // ... other AbilityEffectTypes
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

    // --- Resource Management for Abilities (called by this class) ---
    public bool CanAffordAbility(AbilitySO ability, bool logIfNotAffordable = false)
    {
        if (ability == null) { DebugHelper.LogError($"{_unitMain.unitName}: Attempted to check affordability for a NULL ability.", _unitMain); return false; }
        if (!_unitStats.IsAlive) return false;
        if (!_unitMain.CanAffordAPForAction(ability.apCost)) // Check AP via Unit.cs
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
        // Future: Add checks for FP and IP costs here from AbilitySO
        // if (_unitStats.currentFocusPoints < ability.fpCost) { ... return false; }
        // if (_unitStats.currentInfluencePoints < ability.ipCost) { ... return false; }
        return true;
    }

    public void SpendResourcesForAbility(AbilitySO ability)
    {
        if (ability == null || !_unitStats.IsAlive) return;
        _unitMain.SpendAPForAction(ability.apCost); // Spend AP via Unit.cs

        if (ability.mpCost > 0) _unitStats.SpendMana(ability.mpCost);
        if (ability.spCost > 0) _unitStats.SpendStamina(ability.spCost);
        // Future: Add spending for FP and IP costs here from AbilitySO
        // if (ability.fpCost > 0) _unitStats.SpendFocus(ability.fpCost);
        // if (ability.ipCost > 0) _unitStats.SpendInfluence(ability.ipCost);

        // Logging is now done in PerformAbility after this call, with all resource values.
    }

    // --- Damage & Death Handling ---
    public IEnumerator TakeDamage(int damageAmount)
    {
        if (!_unitStats.IsAlive) yield break;

        _unitStats.ModifyVitality(-damageAmount); // Negative for damage

        DebugHelper.Log($"{_unitMain.unitName} takes {damageAmount} damage, has {_unitStats.currentVitalityPoints}/{_unitStats.MaxVitalityPoints} VP remaining. (Alive: {_unitStats.IsAlive})", _unitMain);

        if (_unitStats.IsAlive)
        {
            // Animation will be handled by UnitAnimation component later
            yield return _unitMain.StartCoroutine(_unitMain.PerformHurtAnimation()); // Still calling Unit's placeholder animation
        }
        else
        {
            yield return _unitMain.StartCoroutine(Die()); // Call Die, now also in UnitCombat
        }
    }

    private IEnumerator Die()
    {
        // Ensure stats reflect death if not already handled by ModifyVitality completely setting IsAlive.
        // UnitStats.SetAliveStatus(false) is called by ModifyVitality if VP <=0.
        // This coroutine is more about the consequences of death.

        DebugHelper.Log($"!!!!!! {_unitMain.unitName} has been defeated! !!!!!!", _unitMain);
        
        // Stop movement if any (UnitMovement will handle this more cleanly later)
        if (_unitMain.IsMoving) _unitMain.StopMovementCoroutines(); // We'll need a public method on Unit to do this

        // Animation will be handled by UnitAnimation component later
        yield return _unitMain.StartCoroutine(_unitMain.PerformDeathAnimation()); // Still calling Unit's placeholder animation
        
        // Tile occupancy (UnitMovement will handle this)
        if (_unitMain.CurrentTile != null && _unitMain.CurrentTile.occupyingUnit == _unitMain)
        {
            _unitMain.CurrentTile.ClearOccupyingUnit();
        }
        _unitMain.ClearCurrentTileReference(); // We'll need a method on Unit to set its _currentTile to null

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.UnregisterUnit(_unitMain);
        }
        
        if (_unitMain.gameObject != null)
        {
            _unitMain.gameObject.SetActive(false); // Deactivate the entire unit GameObject
        }
    }
}