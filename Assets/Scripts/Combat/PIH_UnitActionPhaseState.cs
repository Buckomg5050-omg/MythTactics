// PIH_UnitActionPhaseState.cs
using UnityEngine.InputSystem;
using System.Collections.Generic; 
using System.Linq; // NEW: Added for .Contains() on IReadOnlyList

namespace MythTactics.Combat
{
    public class PIH_UnitActionPhaseState : PlayerInputStateBase
    {
        public override void EnterState(PlayerInputHandler inputHandler)
        {
            base.EnterState(inputHandler);
            if (_selectedUnit == null || !_selectedUnit.IsAlive)
            {
                DebugHelper.LogWarning("PIH_UnitActionPhaseState: Entered with no selected or living unit. Transitioning to WaitingForTurn.", _inputHandler);
                _inputHandler.ChangeState(new PIH_WaitingForTurnState());
                return;
            }

            // DebugHelper.Log($"PIH_UnitActionPhaseState: Entered for {_selectedUnit.unitName}. AP: {_selectedUnit.currentActionPoints}", _inputHandler);
            _inputHandler.ClearAttackRangeHighlight(true); 
            _inputHandler.ClearPathHighlight();          
            _inputHandler.AttemptToShowMoveRangeForSelectedUnit();
            if (_selectedUnit.CurrentTile != null)
            {
                _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            }
            _inputHandler.CheckAndHandleEndOfTurnActionsPIH(); 
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || clickedTile == null) return;

            if (clickedTile == _selectedUnit.CurrentTile)
            {
                DebugHelper.Log("PIH_UnitActionPhaseState: Clicked selected unit's tile.", _inputHandler);
                return;
            }
            
            // Now we use the public accessor which returns IReadOnlyList
            if (_inputHandler.HighlightedReachableTiles.Contains(clickedTile) && !clickedTile.IsOccupied)
            {
                if (!_selectedUnit.CanAffordAction(PlayerInputHandler.MoveActionCost))
                {
                    DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford MOVE AP cost ({PlayerInputHandler.MoveActionCost}). Has {_selectedUnit.currentActionPoints}. Move blocked.", _inputHandler);
                    return;
                }
                _selectedUnit.SpendActionPoints(PlayerInputHandler.MoveActionCost); 

                List<Tile> path = _pathfinder.FindPath(_selectedUnit.CurrentTile.gridPosition, clickedTile.gridPosition, _selectedUnit);

                if (path != null && path.Count > 0)
                {
                    _inputHandler.ClearReachableHighlight(false); 
                    _inputHandler.ShowPathHighlight(path);
                    _inputHandler.ChangeState(new PIH_UnitMovingState(path)); 
                }
                else
                {
                    DebugHelper.LogError($"PIH_UnitActionPhaseState: Pathing failed for {_selectedUnit.unitName} to {clickedTile.gridPosition} AFTER AP was spent! Refunding AP.", _inputHandler);
                    _selectedUnit.currentActionPoints += PlayerInputHandler.MoveActionCost; 
                    _inputHandler.ClearPathHighlight();
                    _inputHandler.AttemptToShowMoveRangeForSelectedUnit(); 
                }
            }
            else if (clickedTile.IsOccupied && clickedTile.occupyingUnit != _selectedUnit)
            {
                 DebugHelper.Log($"PIH_UnitActionPhaseState: Clicked on occupied tile {clickedTile.gridPosition} ({clickedTile.occupyingUnit.unitName}). No move action.", _inputHandler);
            }
            else
            {
                DebugHelper.Log($"PIH_UnitActionPhaseState: Clicked tile {clickedTile.gridPosition} is not a valid move target.", _inputHandler);
            }
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;

            if (_selectedUnit.CanAffordAction(PlayerInputHandler.AttackActionCost))
            {
                _inputHandler.ChangeState(new PIH_SelectingAttackTargetState());
            }
            else
            {
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford ATTACK (cost: {PlayerInputHandler.AttackActionCost}). Has {_selectedUnit.currentActionPoints} AP. Cannot enter attack mode.", _inputHandler);
            }
        }

        public override void OnWaitInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;

            if (_selectedUnit.CanAffordAction(PlayerInputHandler.WaitActionCost))
            {
                _selectedUnit.SpendActionPoints(PlayerInputHandler.WaitActionCost);
                DebugHelper.Log($"{_selectedUnit.unitName} performs WAIT action. AP remaining: {_selectedUnit.currentActionPoints}", _inputHandler);
                
                _inputHandler.ClearAllHighlights();
                if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
            }
            else
            {
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford WAIT action (Cost: {PlayerInputHandler.WaitActionCost}). AP: {_selectedUnit.currentActionPoints}", _inputHandler);
            }
        }

        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
             if (_selectedUnit == null || !_selectedUnit.IsAlive) return;

            DebugHelper.Log($"{_selectedUnit.unitName} explicitly ends turn (from UnitActionPhase). AP remaining: {_selectedUnit.currentActionPoints}", _inputHandler);
            _inputHandler.ClearAllHighlights();
            if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }
        
        public override void UpdateState()
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || 
                (_inputHandler.CombatActive && TurnManager.Instance.ActiveUnit != _selectedUnit) )
            {
                // DebugHelper.Log("PIH_UnitActionPhaseState: Conditions met to transition to WaitingForTurn.", _inputHandler);
                _inputHandler.ChangeState(new PIH_WaitingForTurnState());
                return;
            }
            _inputHandler.AttemptToShowMoveRangeForSelectedUnit(); 
            _inputHandler.CheckAndHandleEndOfTurnActionsPIH(); 
        }

        public override void ExitState()
        {
            // Clearing reachable highlights here ensures that if we transition to, say, attack targeting,
            // the move highlights don't persist.
            _inputHandler.ClearReachableHighlight(true); 
            base.ExitState();
        }
    }
}