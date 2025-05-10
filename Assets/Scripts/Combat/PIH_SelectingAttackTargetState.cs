// PIH_SelectingAttackTargetState.cs
using UnityEngine.InputSystem;
using System.Linq;
using System.Collections;
using UnityEngine;

namespace MythTactics.Combat
{
    public class PIH_SelectingAttackTargetState : PlayerInputStateBase
    {
        public override void EnterState(PlayerInputHandler inputHandler)
        {
            base.EnterState(inputHandler);
            // MODIFIED: Renamed method
            if (_selectedUnit == null || !_selectedUnit.IsAlive || !_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
            {
                DebugHelper.LogWarning("PIH_SelectingAttackTargetState: Entered with no/dead unit or insufficient AP for attack. Reverting to UnitActionPhase.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }

            _inputHandler.ClearReachableHighlight(true);
            _inputHandler.ClearPathHighlight();
            _inputHandler.ShowAttackRange(_selectedUnit);
            if (_selectedUnit.CurrentTile != null)
            {
                _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            }
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || clickedTile == null) return;

            // MODIFIED: Renamed method
            if (!_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
            {
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford ATTACK (cost: {PlayerInputHandler.AttackActionCost}) upon click. AP: {_selectedUnit.currentActionPoints}. Reverting.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }

            if (_inputHandler.HighlightedAttackRangeTiles.Contains(clickedTile))
            {
                Unit targetUnit = clickedTile.occupyingUnit;
                if (targetUnit != null && targetUnit != _selectedUnit && targetUnit.IsAlive)
                {
                    _inputHandler.StartCoroutine(_selectedUnit.PerformAttack(targetUnit, _inputHandler)); 
                    // Note: PerformAttack will call SpendAPForAction internally. No need to call it here.
                    // It will also call CheckAndHandleEndOfTurnActionsPIH.
                    _inputHandler.ChangeState(new PIH_WaitingForTurnState()); // Or PIH_UnitActionPhaseState if more actions possible
                }
                else if (targetUnit == _selectedUnit)
                {
                    DebugHelper.Log("PIH_SelectingAttackTargetState: Cannot target self with a basic attack.", _inputHandler);
                }
                else if (targetUnit != null && !targetUnit.IsAlive)
                {
                    DebugHelper.Log($"PIH_SelectingAttackTargetState: Target {targetUnit.unitName} on tile {clickedTile.gridPosition} is already defeated.", _inputHandler);
                }
                else
                {
                    DebugHelper.Log($"PIH_SelectingAttackTargetState: No valid (living) target unit on selected attack tile {clickedTile.gridPosition}.", _inputHandler);
                }
            }
            else
            {
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
            }
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }

        public override void OnWaitInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;

            // MODIFIED: Renamed methods
            if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.WaitActionCost))
            {
                _selectedUnit.SpendAPForAction(PlayerInputHandler.WaitActionCost);

                _inputHandler.ClearAllHighlights();
                if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
                _inputHandler.ChangeState(new PIH_WaitingForTurnState());
            }
            else
            {
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford WAIT action (Cost: {PlayerInputHandler.WaitActionCost}). AP: {_selectedUnit.currentActionPoints}", _inputHandler);
            }
        }

        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;

            _inputHandler.ClearAllHighlights();
            if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
            _inputHandler.ChangeState(new PIH_WaitingForTurnState());
        }

        public override void UpdateState()
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive ||
                // MODIFIED: Renamed method
                !_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost) ||
                (_inputHandler.CombatActive && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != _selectedUnit))
            {
                if (_inputHandler != null) 
                {
                    _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                }
                return;
            }
            if (_inputHandler != null && _selectedUnit != null)
            {
                _inputHandler.ShowAttackRange(_selectedUnit);
                 if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); // Re-ensure selected unit tile highlight
            }
        }

        public override void ExitState()
        {
            if (_inputHandler != null)
            {
                _inputHandler.ClearAttackRangeHighlight(true);
            }
            base.ExitState();
        }
    }
}