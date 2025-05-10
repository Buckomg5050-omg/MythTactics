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
        if (aiUnit == null || !aiUnit.IsAlive) 
        {
            DebugHelper.LogWarning($"BasicAIHandler: ExecuteTurn called for null or dead unit ({aiUnit?.unitName}).", this);
            EndTurnSafety(aiUnit);
            yield break;
        }
        if (aiUnit.Combat == null || aiUnit.Movement == null) // MODIFIED: Check for Movement component too
        {
            DebugHelper.LogError($"BasicAIHandler: Unit {aiUnit.unitName} is missing UnitCombat or UnitMovement component! AI cannot function.", aiUnit);
            EndTurnSafety(aiUnit);
            yield break;
        }

        DebugHelper.Log($"--- {aiUnit.unitName} (AI) Turn Start. AP: {aiUnit.currentActionPoints} ---", aiUnit);

        Unit playerTarget = FindPlayerUnit();
        if (playerTarget == null || !playerTarget.IsAlive || playerTarget.Movement == null) 
        {
            DebugHelper.LogWarning($"{aiUnit.unitName} (AI): No alive player target found or target missing Movement component. Waiting.", aiUnit);
            yield return PerformWaitActionIfAble(aiUnit);
            EndTurnSafety(aiUnit);
            yield break;
        }
        // MODIFIED: Access CurrentTile via playerTarget.Movement.CurrentTile
        DebugHelper.Log($"{aiUnit.unitName} (AI): Target acquired - {playerTarget.unitName} at {playerTarget.Movement.CurrentTile?.gridPosition}", aiUnit);

        if (aiUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost) && IsTargetInAttackRange(aiUnit, playerTarget))
        {
            DebugHelper.Log($"{aiUnit.unitName} (AI): Target {playerTarget.unitName} is in attack range. Attacking.", aiUnit);
            yield return aiUnit.StartCoroutine(aiUnit.Combat.PerformAttack(playerTarget, null)); 
            yield return new WaitForSeconds(AI_ACTION_DELAY);

            if (!aiUnit.IsAlive) { EndTurnSafety(aiUnit); yield break; } 
            if (!playerTarget.IsAlive) { DebugHelper.Log($"{aiUnit.unitName} (AI): Target {playerTarget.unitName} defeated.", aiUnit); }
        }

        if (aiUnit.IsAlive && playerTarget.IsAlive &&
            aiUnit.CanAffordAPForAction(PlayerInputHandler.MoveActionCost) &&
            !IsTargetInAttackRange(aiUnit, playerTarget))
        {
            DebugHelper.Log($"{aiUnit.unitName} (AI): Target {playerTarget.unitName} not in attack range. Attempting to move.", aiUnit);
            
            if (GridManager.Instance.PathfinderInstance == null)
            {
                DebugHelper.LogError($"{aiUnit.unitName} (AI): PathfinderInstance is null! Cannot move.", aiUnit);
            }
            // MODIFIED: Access CurrentTile via aiUnit.Movement.CurrentTile and playerTarget.Movement.CurrentTile
            else if (aiUnit.Movement.CurrentTile == null || playerTarget.Movement.CurrentTile == null)
            {
                 DebugHelper.LogWarning($"{aiUnit.unitName} (AI): Own tile or target tile is null. Cannot path. Own: {aiUnit.Movement.CurrentTile?.gridPosition}, Target: {playerTarget.Movement.CurrentTile?.gridPosition}", aiUnit);
            }
            else
            {
                List<Tile> path = GridManager.Instance.PathfinderInstance.FindPath(
                    aiUnit.Movement.CurrentTile.gridPosition,
                    playerTarget.Movement.CurrentTile.gridPosition,
                    aiUnit,
                    true 
                );

                if (path != null && path.Count > 0)
                {
                    List<Tile> actualMovePath = GetActualPathWithinMovementRange(path, aiUnit);

                    if (actualMovePath.Count > 0)
                    {
                        Tile destinationTileThisMove = actualMovePath.Last();
                        // MODIFIED: Access CurrentTile via aiUnit.Movement.CurrentTile and playerTarget.Movement.CurrentTile
                        DebugHelper.Log($"{aiUnit.unitName} (AI): Moving from {aiUnit.Movement.CurrentTile.gridPosition} to {destinationTileThisMove.gridPosition} (Path segment length: {actualMovePath.Count}) towards {playerTarget.unitName} at {playerTarget.Movement.CurrentTile?.gridPosition}.", aiUnit);
                        
                        aiUnit.SpendAPForAction(PlayerInputHandler.MoveActionCost); 
                        // MODIFIED: Call MoveOnPath via aiUnit.Movement
                        yield return aiUnit.StartCoroutine(aiUnit.Movement.MoveOnPath(actualMovePath));
                        yield return new WaitForSeconds(AI_ACTION_DELAY);

                        if (!aiUnit.IsAlive) { EndTurnSafety(aiUnit); yield break; }

                        if (playerTarget.IsAlive && aiUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost) && IsTargetInAttackRange(aiUnit, playerTarget))
                        {
                            DebugHelper.Log($"{aiUnit.unitName} (AI): Target {playerTarget.unitName} is NOW in attack range after moving. Attacking.", aiUnit);
                            yield return aiUnit.StartCoroutine(aiUnit.Combat.PerformAttack(playerTarget, null));
                            yield return new WaitForSeconds(AI_ACTION_DELAY);

                            if (!aiUnit.IsAlive) { EndTurnSafety(aiUnit); yield break; }
                            if (!playerTarget.IsAlive) { DebugHelper.Log($"{aiUnit.unitName} (AI): Target {playerTarget.unitName} defeated after moving and attacking.", aiUnit); }
                        }
                        else if (playerTarget.IsAlive)
                        {
                             DebugHelper.Log($"{aiUnit.unitName} (AI): Moved, but target {playerTarget.unitName} still not in attack range or cannot afford attack. AP: {aiUnit.currentActionPoints}", aiUnit);
                        }
                    }
                    else { DebugHelper.Log($"{aiUnit.unitName} (AI): Path found but cannot move (within range or blocked).", aiUnit); }
                }
                else { DebugHelper.Log($"{aiUnit.unitName} (AI): No path found to {playerTarget.unitName}.", aiUnit); }
            }
        }
        else if (aiUnit.IsAlive && playerTarget.IsAlive && !IsTargetInAttackRange(aiUnit, playerTarget))
        { DebugHelper.Log($"{aiUnit.unitName} (AI): Target not in range, no move attempted (AP: {aiUnit.currentActionPoints}).", aiUnit); }

        if (aiUnit.IsAlive)
        {
            if (aiUnit.currentActionPoints > 0)
            {
                 DebugHelper.Log($"{aiUnit.unitName} (AI): Has {aiUnit.currentActionPoints} AP remaining. Attempting to wait.", aiUnit);
                 yield return PerformWaitActionIfAble(aiUnit); 
            }
            else { DebugHelper.Log($"{aiUnit.unitName} (AI): No AP remaining.", aiUnit); }
        }

        DebugHelper.Log($"--- {aiUnit.unitName} (AI) Turn End. Final AP: {aiUnit.currentActionPoints} ---", aiUnit);
        EndTurnSafety(aiUnit);
    }

    private List<Tile> GetActualPathWithinMovementRange(List<Tile> fullPath, Unit unit)
    {
        List<Tile> actualMovePath = new List<Tile>();
        if (fullPath == null || unit == null || unit.Movement == null) return actualMovePath; // MODIFIED: Check unit.Movement

        // MODIFIED: Access CalculatedMoveRange via unit.Movement.CalculatedMoveRange
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
        foreach (var unit in TurnManager.Instance.CombatUnits)
        {
            if (unit != null && unit.IsAlive && unit.CompareTag("Player")) { return unit; }
        }
        DebugHelper.LogWarning("BasicAIHandler: No alive unit with tag 'Player' found in CombatUnits.", this);
        return null;
    }

    private bool IsTargetInAttackRange(Unit attacker, Unit target)
    {
        // MODIFIED: Check attacker.Movement and target.Movement before accessing CurrentTile
        if (attacker == null || target == null || 
            attacker.Movement == null || target.Movement == null ||
            attacker.Movement.CurrentTile == null || target.Movement.CurrentTile == null || 
            !attacker.IsAlive || !target.IsAlive) 
        {
            return false;
        }

        if (GridManager.Instance == null || GridManager.Instance.PathfinderInstance == null)
        {
            DebugHelper.LogError("IsTargetInAttackRange: GridManager or PathfinderInstance is null!", attacker);
            return false;
        }
        
        // MODIFIED: Access CurrentTile via Movement component
        int distance = GridManager.Instance.CalculateManhattanDistance(attacker.Movement.CurrentTile.gridPosition, target.Movement.CurrentTile.gridPosition);
        
        int attackRange = attacker.CalculatedAttackRange; // CalculatedAttackRange is still on Unit
        return distance <= attackRange;
    }

    private IEnumerator PerformWaitActionIfAble(Unit aiUnit)
    {
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
        else { DebugHelper.LogError("BasicAIHandler.EndTurnSafety: TurnManager.Instance is null!", unitToEnd); }
    }
}