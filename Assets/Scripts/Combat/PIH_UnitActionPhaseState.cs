// PIH_UnitActionPhaseState.cs
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
            _inputHandler.SelectedAbility = null;
            _inputHandler.ClearAllHighlights();
            _inputHandler.AttemptToShowMoveRangeForSelectedUnit();
            if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || clickedTile == null || _pathfinder == null)
            {
                if(_pathfinder == null) DebugHelper.LogWarning("PIH_UnitActionPhaseState: Pathfinder is null, cannot process move.", _inputHandler);
                return;
            }

            if (clickedTile == _selectedUnit.CurrentTile)
            {
                DebugHelper.Log("PIH_UnitActionPhaseState: Clicked selected unit's tile. No action.", _inputHandler);
                return;
            }

            if (_inputHandler.HighlightedReachableTiles.Contains(clickedTile) && !clickedTile.IsOccupied)
            {
                if (!_selectedUnit.CanAffordAPForAction(PlayerInputHandler.MoveActionCost))
                {
                    // Corrected: _selectedUnit.CurrentActionPoints
                    DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford MOVE. Has {_selectedUnit.CurrentActionPoints} AP.", _inputHandler);
                    return;
                }
                // AP spending is now handled by UnitMovement.MoveOnPath
                // _selectedUnit.SpendAPForAction(PlayerInputHandler.MoveActionCost);

                List<Tile> path = _pathfinder.FindPath(_selectedUnit.CurrentTile.gridPosition, clickedTile.gridPosition, _selectedUnit);

                if (path != null && path.Count > 0)
                {
                    _inputHandler.ClearReachableHighlight(false);
                    _inputHandler.ShowPathHighlight(path);
                    _inputHandler.ChangeState(new PIH_UnitMovingState(path));
                }
                else
                {
                    // No AP was spent yet if MoveOnPath handles it, so no refund needed here.
                    // If MoveOnPath itself fails after spending AP, it should handle refund or log.
                    DebugHelper.LogError($"PIH: Pathing failed for {_selectedUnit.unitName} to {clickedTile.gridPosition}. No move initiated.", _inputHandler);
                    _inputHandler.ClearPathHighlight();
                    _inputHandler.AttemptToShowMoveRangeForSelectedUnit();
                }
            }
            else if (clickedTile.IsOccupied && clickedTile.occupyingUnit != _selectedUnit)
            { DebugHelper.Log($"PIH: Clicked occupied tile {clickedTile.gridPosition} ({clickedTile.occupyingUnit.unitName}). No move.", _inputHandler); }
            else { DebugHelper.Log($"PIH: Clicked tile {clickedTile.gridPosition} is not valid move target.", _inputHandler); }
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
                _inputHandler.ChangeState(new PIH_SelectingAttackTargetState());
            else
                // Corrected: _selectedUnit.CurrentActionPoints
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford ATTACK. Has {_selectedUnit.CurrentActionPoints} AP.", _inputHandler);
        }

        public override void OnSelectAbilityInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || _selectedUnit.Combat == null)
            {
                DebugHelper.LogWarning("PIH_UnitActionPhaseState: Cannot select ability. Selected unit, its Combat component, or IsAlive is invalid.", _inputHandler);
                return;
            }

            if (_selectedUnit.knownAbilities == null || _selectedUnit.knownAbilities.Count == 0)
            {
                DebugHelper.Log($"{_selectedUnit.unitName} has no abilities to select.", _inputHandler);
                return;
            }
            AbilitySO abilityToUse = _selectedUnit.knownAbilities[0];
            if (abilityToUse == null)
            {
                DebugHelper.LogWarning($"{_selectedUnit.unitName}'s first ability is null.", _inputHandler);
                return;
            }

            if (_selectedUnit.Combat.CanAffordAbility(abilityToUse, true))
            {
                _inputHandler.SelectedAbility = abilityToUse;
                DebugHelper.Log($"{_selectedUnit.unitName} selected ability: {abilityToUse.abilityName}. Transitioning.", _inputHandler);
                _inputHandler.ChangeState(new PIH_SelectingAbilityTargetState());
            }
            else
            {
                // CanAffordAbility already logs details.
                // DebugHelper.Log($"{_selectedUnit.unitName} could not select '{abilityToUse.abilityName}'. See previous logs for reason.", _inputHandler);
            }
        }

        public override void OnWaitInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.WaitActionCost))
            {
                _selectedUnit.SpendAPForAction(PlayerInputHandler.WaitActionCost);
                // Corrected: _selectedUnit.CurrentActionPoints
                DebugHelper.Log($"{_selectedUnit.unitName} performs WAIT. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler);
                _inputHandler.ClearAllHighlights();
                _inputHandler.CheckAndHandleEndOfTurnActionsPIH();
            }
            else
            {
                // Corrected: _selectedUnit.CurrentActionPoints
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford WAIT. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler);
            }
        }

        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
             if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            // Corrected: _selectedUnit.CurrentActionPoints
            DebugHelper.Log($"{_selectedUnit.unitName} explicitly ends turn. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler);
            _inputHandler.ClearAllHighlights();
            if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }

        public override void UpdateState()
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive ||
                (_inputHandler.CombatActive && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != _selectedUnit) )
            {
                _inputHandler.ChangeState(new PIH_WaitingForTurnState());
                return;
            }

            _inputHandler.ClearAllHighlights();
            _inputHandler.AttemptToShowMoveRangeForSelectedUnit();
            if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            _inputHandler.CheckAndHandleEndOfTurnActionsPIH();
        }

        public override void ExitState()
        {
            base.ExitState();
        }
    }
}