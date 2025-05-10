// BasicAIHandler.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class BasicAIHandler : MonoBehaviour
{
    private Unit _unit; 

    private const float AI_ACTION_DELAY = 0.3f; 

    void Awake()
    {
        _unit = GetComponent<Unit>();
        if (_unit == null)
        {
            DebugHelper.LogError("BasicAIHandler is attached to a GameObject without a Unit component!", this);
            enabled = false; 
        }
    }

    public IEnumerator ExecuteTurn(Unit aiUnit) 
    {
        if (aiUnit == null || !aiUnit.IsAlive) // IsAlive now checks aiUnit.Stats.IsAlive
        {
            DebugHelper.LogWarning($"BasicAIHandler: ExecuteTurn called for null or dead unit ({aiUnit?.unitName}).", this);
            EndTurnSafety(aiUnit);
            yield break;
        }
        // Ensure Combat component is available
        if (aiUnit.Combat == null)
        {
            DebugHelper.LogError($"BasicAIHandler: Unit {aiUnit.unitName} is missing UnitCombat component! AI cannot function.", aiUnit);
            EndTurnSafety(aiUnit);
            yield break;
        }


        DebugHelper.Log($"--- {aiUnit.unitName} (AI) Turn Start. AP: {aiUnit.currentActionPoints} ---", aiUnit);

        Unit playerTarget = FindPlayerUnit();
        if (playerTarget == null || !playerTarget.IsAlive) // IsAlive checks playerTarget.Stats.IsAlive
        {
            DebugHelper.LogWarning($"{aiUnit.unitName} (AI): No alive player target found. Waiting and ending turn.", aiUnit);
            yield return PerformWaitActionIfAble(aiUnit);
            EndTurnSafety(aiUnit);
            yield break;
        }
        DebugHelper.Log($"{aiUnit.unitName} (AI): Target acquired - {playerTarget.unitName} at {playerTarget.CurrentTile?.gridPosition}", aiUnit);

        // Attack if in range and can afford
        if (aiUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost) && IsTargetInAttackRange(aiUnit, playerTarget))
        {
            DebugHelper.Log($"{aiUnit.unitName} (AI): Target {playerTarget.unitName} is in attack range. Attacking.", aiUnit);
            // MODIFIED: Call PerformAttack via aiUnit.Combat
            yield return aiUnit.StartCoroutine(aiUnit.Combat.PerformAttack(playerTarget, null)); 
            yield return new WaitForSeconds(AI_ACTION_DELAY);

            if (!aiUnit.IsAlive) { EndTurnSafety(aiUnit); yield break; } // Check own survival
            if (!playerTarget.IsAlive) // Check target survival
            {
                DebugHelper.Log($"{aiUnit.unitName} (AI): Target {playerTarget.unitName} defeated.", aiUnit);
            }
        }

        // Move if not in range (and alive, target alive, can afford move)
        if (aiUnit.IsAlive && playerTarget.IsAlive &&
            aiUnit.CanAffordAPForAction(PlayerInputHandler.MoveActionCost) &&
            !IsTargetInAttackRange(aiUnit, playerTarget))
        {
            DebugHelper.Log($"{aiUnit.unitName} (AI): Target {playerTarget.unitName} not in attack range. Attempting to move.", aiUnit);
            
            if (GridManager.Instance.PathfinderInstance == null)
            {
                DebugHelper.LogError($"{aiUnit.unitName} (AI): PathfinderInstance is null! Cannot move.", aiUnit);
            }
            else if (aiUnit.CurrentTile == null || playerTarget.CurrentTile == null)
            {
                 DebugHelper.LogWarning($"{aiUnit.unitName} (AI): Own tile or target tile is null. Cannot path. Own: {aiUnit.CurrentTile?.gridPosition}, Target: {playerTarget.CurrentTile?.gridPosition}", aiUnit);
            }
            else
            {
                List<Tile> path = GridManager.Instance.PathfinderInstance.FindPath(
                    aiUnit.CurrentTile.gridPosition,
                    playerTarget.CurrentTile.gridPosition,
                    aiUnit,
                    true // findAdjacentToTargetInstead
                );

                if (path != null && path.Count > 0)
                {
                    // GetActualPathWithinMovementRange still uses aiUnit.CalculatedMoveRange which is fine
                    List<Tile> actualMovePath = GetActualPathWithinMovementRange(path, aiUnit);

                    if (actualMovePath.Count > 0)
                    {
                        Tile destinationTileThisMove = actualMovePath.Last();
                        DebugHelper.Log($"{aiUnit.unitName} (AI): Moving from {aiUnit.CurrentTile.gridPosition} to {destinationTileThisMove.gridPosition} (Path segment length: {actualMovePath.Count}) towards {playerTarget.unitName} at {playerTarget.CurrentTile?.gridPosition}.", aiUnit);
                        
                        aiUnit.SpendAPForAction(PlayerInputHandler.MoveActionCost); // AP management still on Unit
                        // MoveOnPath is still on Unit for now
                        yield return aiUnit.StartCoroutine(aiUnit.MoveOnPath(actualMovePath));
                        yield return new WaitForSeconds(AI_ACTION_DELAY);

                        if (!aiUnit.IsAlive) { EndTurnSafety(aiUnit); yield break; }

                        // Attack after moving if possible
                        if (playerTarget.IsAlive && aiUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost) && IsTargetInAttackRange(aiUnit, playerTarget))
                        {
                            DebugHelper.Log($"{aiUnit.unitName} (AI): Target {playerTarget.unitName} is NOW in attack range after moving. Attacking.", aiUnit);
                            // MODIFIED: Call PerformAttack via aiUnit.Combat
                            yield return aiUnit.StartCoroutine(aiUnit.Combat.PerformAttack(playerTarget, null));
                            yield return new WaitForSeconds(AI_ACTION_DELAY);

                            if (!aiUnit.IsAlive) { EndTurnSafety(aiUnit); yield break; }
                            if (!playerTarget.IsAlive)
                            {
                                DebugHelper.Log($"{aiUnit.unitName} (AI): Target {playerTarget.unitName} defeated after moving and attacking.", aiUnit);
                            }
                        }
                        else if (playerTarget.IsAlive)
                        {
                             DebugHelper.Log($"{aiUnit.unitName} (AI): Moved, but target {playerTarget.unitName} still not in attack range or cannot afford attack. AP: {aiUnit.currentActionPoints}", aiUnit);
                        }
                    }
                    else
                    {
                        DebugHelper.Log($"{aiUnit.unitName} (AI): Path found but cannot afford to move to the first tile or path is effectively empty (within move range).", aiUnit);
                    }
                }
                else
                {
                    DebugHelper.Log($"{aiUnit.unitName} (AI): No path found to {playerTarget.unitName}.", aiUnit);
                }
            }
        }
        else if (aiUnit.IsAlive && playerTarget.IsAlive && !IsTargetInAttackRange(aiUnit, playerTarget))
        {
             DebugHelper.Log($"{aiUnit.unitName} (AI): Target not in range, but did not attempt move (e.g. no AP for move). AP: {aiUnit.currentActionPoints}", aiUnit);
        }

        // Wait if AP remaining
        if (aiUnit.IsAlive)
        {
            if (aiUnit.currentActionPoints > 0)
            {
                 DebugHelper.Log($"{aiUnit.unitName} (AI): Has {aiUnit.currentActionPoints} AP remaining. Attempting to wait.", aiUnit);
                 yield return PerformWaitActionIfAble(aiUnit); // This internally calls Unit's AP methods
            }
            else
            {
                 DebugHelper.Log($"{aiUnit.unitName} (AI): No AP remaining.", aiUnit);
            }
        }

        DebugHelper.Log($"--- {aiUnit.unitName} (AI) Turn End. Final AP: {aiUnit.currentActionPoints} ---", aiUnit);
        EndTurnSafety(aiUnit);
    }

    private List<Tile> GetActualPathWithinMovementRange(List<Tile> fullPath, Unit unit)
    {
        List<Tile> actualMovePath = new List<Tile>();
        if (fullPath == null || unit == null) return actualMovePath;

        // CalculatedMoveRange is still on Unit for now
        int movePointsAvailable = unit.CalculatedMoveRange; 
        int currentPathCost = 0;

        foreach (Tile pathTile in fullPath)
        {
            if (pathTile == null) continue;
            int costToThisTile = pathTile.GetMovementCost(unit); // GetMovementCost is on Tile
            if (currentPathCost + costToThisTile <= movePointsAvailable)
            {
                actualMovePath.Add(pathTile);
                currentPathCost += costToThisTile;
            }
            else
            {
                break; 
            }
        }
        return actualMovePath;
    }

    private Unit FindPlayerUnit()
    {
        if (TurnManager.Instance == null) return null;
        foreach (var unit in TurnManager.Instance.CombatUnits)
        {
            // IsAlive now checks unit.Stats.IsAlive
            if (unit != null && unit.IsAlive && unit.CompareTag("Player")) 
            {
                return unit;
            }
        }
        DebugHelper.LogWarning("BasicAIHandler: No alive unit with tag 'Player' found in CombatUnits.", this);
        return null;
    }

    private bool IsTargetInAttackRange(Unit attacker, Unit target)
    {
        // IsAlive checks attacker.Stats.IsAlive / target.Stats.IsAlive
        if (attacker == null || target == null || attacker.CurrentTile == null || target.CurrentTile == null || !attacker.IsAlive || !target.IsAlive) 
        {
            return false;
        }

        if (GridManager.Instance == null || GridManager.Instance.PathfinderInstance == null)
        {
            DebugHelper.LogError("IsTargetInAttackRange: GridManager or PathfinderInstance is null!", attacker);
            return false;
        }
        
        int distance = GridManager.Instance.CalculateManhattanDistance(attacker.CurrentTile.gridPosition, target.CurrentTile.gridPosition);
        
        // CalculatedAttackRange is still on Unit
        int attackRange = attacker.CalculatedAttackRange; 
        return distance <= attackRange;
    }

    private IEnumerator PerformWaitActionIfAble(Unit aiUnit)
    {
        // CanAffordAPForAction and SpendAPForAction are still on Unit
        if (aiUnit.CanAffordAPForAction(PlayerInputHandler.WaitActionCost)) 
        {
            aiUnit.SpendAPForAction(PlayerInputHandler.WaitActionCost);
            DebugHelper.Log($"{aiUnit.unitName} (AI) performed 'Wait' action. AP: {aiUnit.currentActionPoints}", aiUnit);
            yield return new WaitForSeconds(AI_ACTION_DELAY / 2f);
        }
    }

    private void EndTurnSafety(Unit aiUnitRef)
    {
        Unit unitToEnd = aiUnitRef ?? _unit;
        if (unitToEnd == null)
        {
            DebugHelper.LogWarning("BasicAIHandler.EndTurnSafety: Unit to end is null.", this);
            return;
        }

        if (TurnManager.Instance != null)
        {
            // IsAlive checks unitToEnd.Stats.IsAlive
            if (TurnManager.Instance.ActiveUnit == unitToEnd && unitToEnd.IsAlive) 
            {
                TurnManager.Instance.EndUnitTurn(unitToEnd);
            }
            else if (TurnManager.Instance.ActiveUnit == unitToEnd && !unitToEnd.IsAlive)
            {
                 DebugHelper.Log($"{unitToEnd.unitName} (AI) is dead but was still ActiveUnit. Forcing EndUnitTurn.", unitToEnd);
                 TurnManager.Instance.EndUnitTurn(unitToEnd);
            }
        }
        else
        {
            DebugHelper.LogError("BasicAIHandler.EndTurnSafety: TurnManager.Instance is null!", unitToEnd);
        }
    }
}