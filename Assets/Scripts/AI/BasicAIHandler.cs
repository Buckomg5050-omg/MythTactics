// BasicAIHandler.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class BasicAIHandler : MonoBehaviour
{
    private Unit _unit; // This should be the AI unit executing the turn.

    private const float AI_ACTION_DELAY = 0.3f;

    void Awake()
    {
        _unit = GetComponent<Unit>();
        // It's generally better if this component doesn't assume it's on the unit and
        // relies on the 'aiUnit' passed to ExecuteTurn. However, this can be a fallback.
        if (_unit == null)
        {
            // This BasicAIHandler might be a component on a manager or a prefab not directly
            // instantiated as part of the Unit. In such cases, _unit will be null here,
            // which is fine as ExecuteTurn will receive the actual acting unit.
            // Debug.Log("BasicAIHandler.Awake: No Unit component found on this GameObject. Will rely on ExecuteTurn's parameter.", this);
        }
    }

    // MODIFIED: Added AIBehaviorProfile parameter
    public virtual IEnumerator ExecuteTurn(Unit aiUnit, AIBehaviorProfile profile)
    {
        if (aiUnit == null || !aiUnit.IsAlive)
        {
            DebugHelper.LogWarning($"BasicAIHandler: ExecuteTurn called for null or dead unit ({aiUnit?.unitName}). Profile: {profile}", this);
            EndTurnSafety(aiUnit);
            yield break;
        }
        _unit = aiUnit; // Prioritize the passed-in unit

        if (_unit.Combat == null || _unit.Movement == null || _unit.Stats == null)
        {
            DebugHelper.LogError($"BasicAIHandler: Unit {_unit.unitName} is missing UnitCombat, UnitMovement, or UnitStats component! AI cannot function. Profile: {profile}", _unit);
            EndTurnSafety(_unit);
            yield break;
        }

        DebugHelper.Log($"--- {_unit.unitName} (AI - Profile: {profile}) Turn Start. AP: {_unit.Stats.currentActionPoints}/{_unit.Stats.MaxActionPoints} ---", _unit);

        // --- Main AI Loop: Try to perform actions as long as AP allows ---
        bool actionTakenThisLoopIteration = true;
        int safetyBreak = 0;

        // Example of how you might use the profile:
        // float aggressionFactor = 1.0f; // Default
        // if (profile == AIBehaviorProfile.Aggressive) aggressionFactor = 1.5f;
        // else if (profile == AIBehaviorProfile.Defensive) aggressionFactor = 0.5f;
        // This factor could then influence target selection, willingness to take risks, etc.

        while (_unit.Stats.currentActionPoints > 0 && actionTakenThisLoopIteration && safetyBreak < 10)
        {
            safetyBreak++;
            actionTakenThisLoopIteration = false;

            if (!_unit.IsAlive) break;

            Unit playerTarget = FindPlayerUnit(); // You could enhance FindPlayerUnit to consider 'profile'
            if (playerTarget == null || !playerTarget.IsAlive || playerTarget.Movement == null)
            {
                DebugHelper.LogWarning($"{_unit.unitName} (AI - Profile: {profile}): No alive player target found or target missing Movement. Ending action loop.", _unit);
                break;
            }

            // --- AI Decision Making based on profile could go here ---
            // For now, the logic is simple: attack if possible, else move, else wait.
            // A more complex AI would have different priorities based on 'profile'.
            // e.g., A 'Support' profile might look for allies to heal/buff first.
            // A 'Defensive' profile might prioritize finding cover or using defensive abilities.

            // 1. TRY TO ATTACK IF POSSIBLE AND AFFORDABLE
            // (Could modify IsTargetInAttackRange or choice of target based on 'profile')
            if (_unit.Stats.currentActionPoints >= PlayerInputHandler.AttackActionCost && IsTargetInAttackRange(_unit, playerTarget))
            {
                DebugHelper.Log($"{_unit.unitName} (AI - Profile: {profile}): Target {playerTarget.unitName} is in attack range. Attacking.", _unit);
                yield return _unit.StartCoroutine(_unit.Combat.PerformAttack(playerTarget, null));
                actionTakenThisLoopIteration = true;
                yield return new WaitForSeconds(AI_ACTION_DELAY);
                if (!_unit.IsAlive || !playerTarget.IsAlive) break;
                continue;
            }

            // 2. TRY TO MOVE IF TARGET NOT IN RANGE AND CAN AFFORD MOVE
            // (Pathfinding goal or willingness to move could be influenced by 'profile')
            if (_unit.Stats.currentActionPoints >= PlayerInputHandler.MoveActionCost && !IsTargetInAttackRange(_unit, playerTarget))
            {
                // ... (movement logic mostly unchanged, but could be adapted by profile) ...
                 DebugHelper.Log($"{_unit.unitName} (AI - Profile: {profile}): Target {playerTarget.unitName} not in attack range. Attempting to move.", _unit);

                if (GridManager.Instance.PathfinderInstance == null)
                {
                    DebugHelper.LogError($"{_unit.unitName} (AI - Profile: {profile}): PathfinderInstance is null! Cannot move.", _unit);
                }
                else if (_unit.Movement.CurrentTile == null || playerTarget.Movement.CurrentTile == null)
                {
                    DebugHelper.LogWarning($"{_unit.unitName} (AI - Profile: {profile}): Own tile or target tile is null. Cannot path.", _unit);
                }
                else
                {
                    List<Tile> path = GridManager.Instance.PathfinderInstance.FindPath(
                        _unit.Movement.CurrentTile.gridPosition,
                        playerTarget.Movement.CurrentTile.gridPosition,
                        _unit,
                        true
                    );

                    if (path != null && path.Count > 0)
                    {
                        List<Tile> actualMovePath = GetActualPathWithinMovementRange(path, _unit);

                        if (actualMovePath.Count > 0)
                        {
                            DebugHelper.Log($"{_unit.unitName} (AI - Profile: {profile}): Moving towards {playerTarget.unitName}.", _unit);
                            yield return _unit.StartCoroutine(_unit.Movement.MoveOnPath(actualMovePath));
                            actionTakenThisLoopIteration = true;
                            yield return new WaitForSeconds(AI_ACTION_DELAY);

                            if (!_unit.IsAlive) break;

                            if (_unit.Stats.currentActionPoints >= PlayerInputHandler.AttackActionCost && IsTargetInAttackRange(_unit, playerTarget))
                            {
                                DebugHelper.Log($"{_unit.unitName} (AI - Profile: {profile}): Target {playerTarget.unitName} is NOW in attack range after moving. Attacking.", _unit);
                                yield return _unit.StartCoroutine(_unit.Combat.PerformAttack(playerTarget, null));
                                yield return new WaitForSeconds(AI_ACTION_DELAY);
                                if (!_unit.IsAlive || !playerTarget.IsAlive) break;
                            }
                            continue;
                        }
                        else { DebugHelper.Log($"{_unit.unitName} (AI - Profile: {profile}): Path found but cannot move (within range or blocked).", _unit); }
                    }
                    else { DebugHelper.Log($"{_unit.unitName} (AI - Profile: {profile}): No path found to {playerTarget.unitName}.", _unit); }
                }
            }
            if (!actionTakenThisLoopIteration) break;
        }

        if (safetyBreak >= 10)
        {
            DebugHelper.LogWarning($"{_unit.unitName} (AI - Profile: {profile}) : AI loop safety break triggered.", _unit);
        }

        if (_unit.IsAlive && _unit.Stats.currentActionPoints > 0)
        {
            DebugHelper.Log($"{_unit.unitName} (AI - Profile: {profile}): Has {_unit.Stats.currentActionPoints} AP remaining. Attempting to wait if possible.", _unit);
            yield return PerformWaitActionIfAble(_unit);
        }
        else if (_unit.IsAlive)
        {
             DebugHelper.Log($"{_unit.unitName} (AI - Profile: {profile}): No AP remaining or no further actions.", _unit);
        }

        DebugHelper.Log($"--- {_unit.unitName} (AI - Profile: {profile}) Turn End. Final AP: {_unit.Stats.currentActionPoints}/{_unit.Stats.MaxActionPoints} ---", _unit);
        EndTurnSafety(_unit);
    }

    private List<Tile> GetActualPathWithinMovementRange(List<Tile> fullPath, Unit unit)
    {
        List<Tile> actualMovePath = new List<Tile>();
        if (fullPath == null || unit == null || unit.Movement == null || unit.Stats == null) return actualMovePath;

        int movePointsAvailable = unit.Movement.CalculatedMoveRange;
        int currentPathCost = 0;

        foreach (Tile pathTile in fullPath)
        {
            if (pathTile == null) continue;
            int costToThisTile = pathTile.GetMovementCost(unit); // Assumes GetMovementCost exists on Tile and takes Unit
            if (currentPathCost + costToThisTile <= movePointsAvailable)
            {
                actualMovePath.Add(pathTile);
                currentPathCost += costToThisTile;
            }
            else { break; }
        }
        return actualMovePath;
    }

    private Unit FindPlayerUnit()
    {
        if (TurnManager.Instance == null) return null;
        Unit closestPlayerUnit = null;
        float minDistance = float.MaxValue;

        // Ensure _unit (the AI unit) is valid and on the grid before trying to use its position
        if (_unit == null || _unit.Movement == null || _unit.Movement.CurrentTile == null)
        {
            DebugHelper.LogWarning("BasicAIHandler.FindPlayerUnit: Current AI unit (_unit) is not properly initialized on the grid. Cannot find closest player.", _unit ?? (Component)this);
            // Fallback: return the first available player unit if AI's position is unknown
            return TurnManager.Instance.CombatUnits.FirstOrDefault(u => u != null && u.IsAlive && u.CurrentFaction == FactionType.Player && u.Movement != null && u.Movement.CurrentTile != null);
        }
        Vector2Int aiPos = _unit.Movement.CurrentTile.gridPosition;

        foreach (var unit in TurnManager.Instance.CombatUnits)
        {
            if (unit != null && unit.IsAlive && unit.CurrentFaction == FactionType.Player && unit.Movement != null && unit.Movement.CurrentTile != null)
            {
                float distance = Vector2Int.Distance(aiPos, unit.Movement.CurrentTile.gridPosition);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPlayerUnit = unit;
                }
            }
        }

        if (closestPlayerUnit == null)
        {
            DebugHelper.LogWarning("BasicAIHandler: No alive Player faction unit found in CombatUnits or they are not on tiles.", this);
        }
        return closestPlayerUnit;
    }

    private bool IsTargetInAttackRange(Unit attacker, Unit target)
    {
        if (attacker == null || target == null ||
            attacker.Movement == null || target.Movement == null ||
            attacker.Movement.CurrentTile == null || target.Movement.CurrentTile == null ||
            !attacker.IsAlive || !target.IsAlive)
        {
            return false;
        }

        if (GridManager.Instance == null)
        {
            DebugHelper.LogError("IsTargetInAttackRange: GridManager is null!", attacker);
            return false;
        }

        int distance = GridManager.Instance.CalculateManhattanDistance(attacker.Movement.CurrentTile.gridPosition, target.Movement.CurrentTile.gridPosition);
        int attackRange = attacker.CalculatedAttackRange;
        return distance <= attackRange;
    }

    private IEnumerator PerformWaitActionIfAble(Unit aiUnit)
    {
        if (aiUnit.CanAffordAPForAction(PlayerInputHandler.WaitActionCost))
        {
            aiUnit.SpendAPForAction(PlayerInputHandler.WaitActionCost);
            CombatLogger.LogEvent($"{aiUnit.unitName} (AI) performed 'Wait' action. AP: {aiUnit.Stats.currentActionPoints}/{aiUnit.Stats.MaxActionPoints}", Color.gray, LogMessageType.TurnFlow);
            yield return new WaitForSeconds(AI_ACTION_DELAY / 2f);
        }
    }

    private void EndTurnSafety(Unit aiUnitRef)
    {
        Unit unitToEnd = aiUnitRef ?? _unit; // Prioritize passed ref, fallback to internal _unit

        if (unitToEnd == null)
        {
            DebugHelper.LogWarning("BasicAIHandler.EndTurnSafety: Unit to end turn for is null.", this);
            return;
        }

        if (TurnManager.Instance != null)
        {
            if (TurnManager.Instance.ActiveUnit == unitToEnd)
            {
                 TurnManager.Instance.EndUnitTurn(unitToEnd);
            }
            else if (TurnManager.Instance.ActiveUnit != null)
            {
                 DebugHelper.LogWarning($"{unitToEnd.unitName} (AI) tried to EndTurnSafety, but ActiveUnit is {TurnManager.Instance.ActiveUnit.unitName}. Turn not ended by this AI call.", unitToEnd);
            }
            // If ActiveUnit is null, the turn might have already ended or combat concluded.
        }
        else { DebugHelper.LogError("BasicAIHandler.EndTurnSafety: TurnManager.Instance is null!", unitToEnd); }
    }
}