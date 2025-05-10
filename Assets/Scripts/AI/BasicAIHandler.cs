// BasicAIHandler.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class BasicAIHandler : MonoBehaviour
{
    private Unit _unit; // This should be the AI unit executing the turn.

    private const float AI_ACTION_DELAY = 0.3f;

    // It's better if BasicAIHandler doesn't directly rely on GetComponent<Unit>() in Awake.
    // Instead, the unit it's controlling should be passed to it, e.g., in ExecuteTurn.
    // For now, we'll keep Awake but ensure ExecuteTurn uses the passed 'aiUnit'.

    void Awake()
    {
        // This _unit might be for if the AIHandler is a component directly on the Unit prefab itself.
        // However, ExecuteTurn takes 'aiUnit', which should be prioritized.
        _unit = GetComponent<Unit>();
        if (_unit == null)
        {
            // This error is valid if BasicAIHandler is always expected to be on a Unit.
            // DebugHelper.LogError("BasicAIHandler is attached to a GameObject without a Unit component!", this);
            // enabled = false;
        }
    }

    public IEnumerator ExecuteTurn(Unit aiUnit)
    {
        if (aiUnit == null || !aiUnit.IsAlive)
        {
            DebugHelper.LogWarning($"BasicAIHandler: ExecuteTurn called for null or dead unit ({aiUnit?.unitName}).", this);
            EndTurnSafety(aiUnit);
            yield break;
        }
        // Ensure the _unit reference is the one currently taking the turn.
        // This is important if the BasicAIHandler instance is shared or a prefab not directly on the unit.
        _unit = aiUnit;


        if (_unit.Combat == null || _unit.Movement == null || _unit.Stats == null)
        {
            DebugHelper.LogError($"BasicAIHandler: Unit {_unit.unitName} is missing UnitCombat, UnitMovement, or UnitStats component! AI cannot function.", _unit);
            EndTurnSafety(_unit);
            yield break;
        }

        // AP is now accessed via _unit.Stats
        DebugHelper.Log($"--- {_unit.unitName} (AI) Turn Start. AP: {_unit.Stats.currentActionPoints}/{_unit.Stats.MaxActionPoints} ---", _unit);

        // --- Main AI Loop: Try to perform actions as long as AP allows ---
        bool actionTakenThisLoopIteration = true; // Start true to enter the loop
        int safetyBreak = 0; // Prevents infinite loop if AI logic gets stuck

        while (_unit.Stats.currentActionPoints > 0 && actionTakenThisLoopIteration && safetyBreak < 10)
        {
            safetyBreak++;
            actionTakenThisLoopIteration = false; // Reset for this iteration

            if (!_unit.IsAlive) break; // Unit might die mid-turn

            Unit playerTarget = FindPlayerUnit();
            if (playerTarget == null || !playerTarget.IsAlive || playerTarget.Movement == null)
            {
                DebugHelper.LogWarning($"{_unit.unitName} (AI): No alive player target found or target missing Movement. Ending action loop.", _unit);
                break; // No target, can't do much else for this basic AI
            }

            // 1. TRY TO ATTACK IF POSSIBLE AND AFFORDABLE
            if (_unit.Stats.currentActionPoints >= PlayerInputHandler.AttackActionCost && IsTargetInAttackRange(_unit, playerTarget))
            {
                DebugHelper.Log($"{_unit.unitName} (AI): Target {playerTarget.unitName} is in attack range. Attacking.", _unit);
                // PerformAttack already handles AP spending via _unit.SpendAPForAction
                yield return _unit.StartCoroutine(_unit.Combat.PerformAttack(playerTarget, null));
                actionTakenThisLoopIteration = true;
                yield return new WaitForSeconds(AI_ACTION_DELAY);

                if (!_unit.IsAlive || !playerTarget.IsAlive) break; // Break loop if self or target dies
                continue; // Try another action if AP remains
            }

            // 2. TRY TO MOVE IF TARGET NOT IN RANGE AND CAN AFFORD MOVE
            if (_unit.Stats.currentActionPoints >= PlayerInputHandler.MoveActionCost && !IsTargetInAttackRange(_unit, playerTarget))
            {
                DebugHelper.Log($"{_unit.unitName} (AI): Target {playerTarget.unitName} not in attack range. Attempting to move.", _unit);

                if (GridManager.Instance.PathfinderInstance == null)
                {
                    DebugHelper.LogError($"{_unit.unitName} (AI): PathfinderInstance is null! Cannot move.", _unit);
                }
                else if (_unit.Movement.CurrentTile == null || playerTarget.Movement.CurrentTile == null)
                {
                    DebugHelper.LogWarning($"{_unit.unitName} (AI): Own tile or target tile is null. Cannot path.", _unit);
                }
                else
                {
                    List<Tile> path = GridManager.Instance.PathfinderInstance.FindPath(
                        _unit.Movement.CurrentTile.gridPosition,
                        playerTarget.Movement.CurrentTile.gridPosition,
                        _unit,
                        true // avoidAlliesAndEnemies true to path around others
                    );

                    if (path != null && path.Count > 0)
                    {
                        List<Tile> actualMovePath = GetActualPathWithinMovementRange(path, _unit);

                        if (actualMovePath.Count > 0)
                        {
                            // MoveOnPath in UnitMovement now handles AP checking and spending.
                            // So, the AI just needs to initiate it.
                            // The AP check above (currentActionPoints >= MoveActionCost) is a pre-check.
                            DebugHelper.Log($"{_unit.unitName} (AI): Moving towards {playerTarget.unitName}.", _unit);
                            yield return _unit.StartCoroutine(_unit.Movement.MoveOnPath(actualMovePath));
                            actionTakenThisLoopIteration = true;
                            yield return new WaitForSeconds(AI_ACTION_DELAY);

                            if (!_unit.IsAlive) break; // Break loop if self dies

                            // After moving, check again if can attack (and afford it)
                            if (_unit.Stats.currentActionPoints >= PlayerInputHandler.AttackActionCost && IsTargetInAttackRange(_unit, playerTarget))
                            {
                                DebugHelper.Log($"{_unit.unitName} (AI): Target {playerTarget.unitName} is NOW in attack range after moving. Attacking.", _unit);
                                yield return _unit.StartCoroutine(_unit.Combat.PerformAttack(playerTarget, null));
                                // actionTakenThisLoopIteration is already true from move
                                yield return new WaitForSeconds(AI_ACTION_DELAY);
                                if (!_unit.IsAlive || !playerTarget.IsAlive) break;
                            }
                            continue; // Try another action if AP remains
                        }
                        else { DebugHelper.Log($"{_unit.unitName} (AI): Path found but cannot move (within range or blocked).", _unit); }
                    }
                    else { DebugHelper.Log($"{_unit.unitName} (AI): No path found to {playerTarget.unitName}.", _unit); }
                }
            }
            // If no action was taken (attack or move), break the loop to prevent busy waiting if AP > 0 but no valid action found.
            if (!actionTakenThisLoopIteration) break;
        } // End of while AP > 0 and actionTakenThisLoopIteration

        if (safetyBreak >= 10)
        {
            DebugHelper.LogWarning($"{_unit.unitName} (AI) : AI loop safety break triggered.", _unit);
        }

        // 3. IF STILL ALIVE AND HAS AP, PERFORM WAIT ACTION
        if (_unit.IsAlive && _unit.Stats.currentActionPoints > 0)
        {
            // PerformWaitActionIfAble now correctly uses Unit.CanAffordAPForAction and Unit.SpendAPForAction
            // which delegate to UnitStats.
            DebugHelper.Log($"{_unit.unitName} (AI): Has {_unit.Stats.currentActionPoints} AP remaining. Attempting to wait if possible.", _unit);
            yield return PerformWaitActionIfAble(_unit); // This will consume remaining AP if possible.
        }
        else if (_unit.IsAlive)
        {
             DebugHelper.Log($"{_unit.unitName} (AI): No AP remaining or no further actions.", _unit);
        }

        DebugHelper.Log($"--- {_unit.unitName} (AI) Turn End. Final AP: {_unit.Stats.currentActionPoints}/{_unit.Stats.MaxActionPoints} ---", _unit);
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
            int costToThisTile = pathTile.GetMovementCost(unit);
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
        // Find the closest player unit instead of just the first one.
        Unit closestPlayerUnit = null;
        float minDistance = float.MaxValue;

        if (_unit == null || _unit.Movement == null || _unit.Movement.CurrentTile == null) // AI unit must be on a tile
        {
            DebugHelper.LogWarning("BasicAIHandler.FindPlayerUnit: AI unit is not properly initialized on the grid.", _unit);
            // Fallback to first player unit if AI unit's position is unknown.
            return TurnManager.Instance.CombatUnits.FirstOrDefault(u => u != null && u.IsAlive && u.CompareTag("Player") && u.Movement != null && u.Movement.CurrentTile != null);
        }

        Vector2Int aiPos = _unit.Movement.CurrentTile.gridPosition;

        foreach (var unit in TurnManager.Instance.CombatUnits)
        {
            if (unit != null && unit.IsAlive && unit.CompareTag("Player") && unit.Movement != null && unit.Movement.CurrentTile != null)
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
            DebugHelper.LogWarning("BasicAIHandler: No alive unit with tag 'Player' found in CombatUnits or they are not on tiles.", this);
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

        if (GridManager.Instance == null) // Removed PathfinderInstance check as it's not used directly here
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
        // Unit.CanAffordAPForAction now delegates to UnitStats
        if (aiUnit.CanAffordAPForAction(PlayerInputHandler.WaitActionCost))
        {
            // Unit.SpendAPForAction now delegates to UnitStats
            aiUnit.SpendAPForAction(PlayerInputHandler.WaitActionCost);
            DebugHelper.Log($"{aiUnit.unitName} (AI) performed 'Wait' action. AP: {aiUnit.Stats.currentActionPoints}/{aiUnit.Stats.MaxActionPoints}", aiUnit);
            yield return new WaitForSeconds(AI_ACTION_DELAY / 2f);
        }
        // No "else" log here, as the main loop already states if no AP is left.
    }

    private void EndTurnSafety(Unit aiUnitRef)
    {
        Unit unitToEnd = aiUnitRef; // aiUnitRef should always be valid if ExecuteTurn started
        if (unitToEnd == null)
        {
            // Try to use the _unit field if aiUnitRef was somehow null, though this shouldn't happen.
            unitToEnd = _unit;
            if (unitToEnd == null)
            {
                DebugHelper.LogWarning("BasicAIHandler.EndTurnSafety: Unit to end is null (both passed ref and internal _unit).", this);
                return;
            }
            DebugHelper.LogWarning("BasicAIHandler.EndTurnSafety: Passed aiUnitRef was null, using internal _unit.", this);
        }


        if (TurnManager.Instance != null)
        {
            // Only end the turn if this AI unit is indeed the active unit.
            if (TurnManager.Instance.ActiveUnit == unitToEnd)
            {
                if (unitToEnd.IsAlive)
                {
                    TurnManager.Instance.EndUnitTurn(unitToEnd);
                }
                else
                {
                    // If the unit died during its own turn, EndUnitTurn still needs to be called
                    // to clean up ActiveUnit in TurnManager.
                    DebugHelper.Log($"{unitToEnd.unitName} (AI) died during its turn. Calling EndUnitTurn.", unitToEnd);
                    TurnManager.Instance.EndUnitTurn(unitToEnd);
                }
            }
            // If ActiveUnit is already null or a different unit, this AI's turn was likely ended by an external factor
            // (e.g. PlayerInputHandler force ending turn if AI takes too long, or unit dying and being unregistered)
            // or the AI logic is misbehaving by trying to end turn when it's not active.
            else if (TurnManager.Instance.ActiveUnit != null)
            {
                 DebugHelper.LogWarning($"{unitToEnd.unitName} (AI) tried to EndTurnSafety, but ActiveUnit is {TurnManager.Instance.ActiveUnit.unitName}. Turn not ended by AI.", unitToEnd);
            }
        }
        else { DebugHelper.LogError("BasicAIHandler.EndTurnSafety: TurnManager.Instance is null!", unitToEnd); }
    }
}