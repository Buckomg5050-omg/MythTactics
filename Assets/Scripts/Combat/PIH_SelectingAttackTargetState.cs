// PIH_SelectingAttackTargetState.cs
using UnityEngine.InputSystem;
using System.Linq;
using UnityEngine;

namespace MythTactics.Combat
{
    public class PIH_SelectingAttackTargetState : PlayerInputStateBase
    {
        public override void EnterState(PlayerInputHandler inputHandler)
        {
            base.EnterState(inputHandler);
            if (_selectedUnit == null || !_selectedUnit.IsAlive ||
                !_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
            {
                DebugHelper.LogWarning("PIH_SelectingAttackTargetState: Entered with no/dead unit or insufficient AP. Reverting.", _inputHandler);
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
            if (_selectedUnit == null || !_selectedUnit.IsAlive || clickedTile == null || _selectedUnit.Combat == null)
            {
                DebugHelper.Log("PIH_SelectingAttackTargetState: Click ignored due to null unit, tile, or missing Combat component.", _inputHandler);
                return;
            }

            if (!_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
            {
                // Corrected: _selectedUnit.CurrentActionPoints
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford ATTACK. AP: {_selectedUnit.CurrentActionPoints}. Reverting.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }

            if (_inputHandler.HighlightedAttackRangeTiles.Contains(clickedTile))
            {
                Unit targetUnit = clickedTile.occupyingUnit;
                if (targetUnit != null && targetUnit != _selectedUnit && targetUnit.IsAlive)
                {
                    _inputHandler.StartCoroutine(_selectedUnit.Combat.PerformAttack(targetUnit, _inputHandler));
                }
                else if (targetUnit == _selectedUnit) { DebugHelper.Log("PIH: Cannot target self with basic attack.", _inputHandler); }
                else if (targetUnit != null && !targetUnit.IsAlive) { DebugHelper.Log($"PIH: Target {targetUnit.unitName} is defeated.", _inputHandler); }
                else { DebugHelper.Log($"PIH: No valid target on tile {clickedTile.gridPosition}.", _inputHandler); }
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
            if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.WaitActionCost))
            {
                _selectedUnit.SpendAPForAction(PlayerInputHandler.WaitActionCost);
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
            _inputHandler.ClearAllHighlights();
            if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }

        public override void UpdateState()
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive ||
                !_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost) ||
                (_inputHandler.CombatActive && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != _selectedUnit))
            {
                if (_inputHandler != null)
                {
                     DebugHelper.LogWarning("PIH_SelectingAttackTargetState.UpdateState: Conditions no longer valid. Reverting.", _inputHandler);
                    _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                }
                return;
            }
            if (_inputHandler != null && _selectedUnit != null)
            {
                _inputHandler.ShowAttackRange(_selectedUnit);
                 if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            }
        }

        public override void ExitState()
        {
            if (_inputHandler != null) _inputHandler.ClearAttackRangeHighlight(true);
            base.ExitState();
        }
    }
}