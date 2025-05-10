// PIH_SelectingAttackTargetState.cs
using UnityEngine.InputSystem;
using System.Linq;
// using System.Collections; // Not used in this version
using UnityEngine;

namespace MythTactics.Combat
{
    public class PIH_SelectingAttackTargetState : PlayerInputStateBase
    {
        public override void EnterState(PlayerInputHandler inputHandler)
        {
            base.EnterState(inputHandler);
            if (_selectedUnit == null || !_selectedUnit.IsAlive || 
                !_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost)) // CanAffordAPForAction is on Unit
            {
                DebugHelper.LogWarning("PIH_SelectingAttackTargetState: Entered with no/dead unit or insufficient AP. Reverting.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }

            _inputHandler.ClearReachableHighlight(true);
            _inputHandler.ClearPathHighlight();
            _inputHandler.ShowAttackRange(_selectedUnit); // ShowAttackRange checks AP via Unit
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

            if (!_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost)) // CanAffordAPForAction is on Unit
            {
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford ATTACK. AP: {_selectedUnit.currentActionPoints}. Reverting.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }

            if (_inputHandler.HighlightedAttackRangeTiles.Contains(clickedTile))
            {
                Unit targetUnit = clickedTile.occupyingUnit;
                if (targetUnit != null && targetUnit != _selectedUnit && targetUnit.IsAlive)
                {
                    // MODIFIED: Call PerformAttack via _selectedUnit.Combat
                    _inputHandler.StartCoroutine(_selectedUnit.Combat.PerformAttack(targetUnit, _inputHandler)); 
                    
                    // PerformAttack in UnitCombat should handle calling CheckAndHandleEndOfTurnActionsPIH or PIH changes state.
                    // For now, assuming the attack coroutine leads to the next state.
                    // _inputHandler.ChangeState(new PIH_WaitingForTurnState()); 
                }
                else if (targetUnit == _selectedUnit) { DebugHelper.Log("PIH: Cannot target self with basic attack.", _inputHandler); }
                else if (targetUnit != null && !targetUnit.IsAlive) { DebugHelper.Log($"PIH: Target {targetUnit.unitName} is defeated.", _inputHandler); }
                else { DebugHelper.Log($"PIH: No valid target on tile {clickedTile.gridPosition}.", _inputHandler); }
            }
            else
            {
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState()); // Clicked outside attack range
            }
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState()); // Toggle back to action phase
        }

        public override void OnWaitInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.WaitActionCost)) // CanAffordAPForAction is on Unit
            {
                _selectedUnit.SpendAPForAction(PlayerInputHandler.WaitActionCost); // SpendAPForAction is on Unit
                _inputHandler.ClearAllHighlights();
                // CheckAndHandleEndOfTurnActionsPIH will be called, which might end turn or allow more actions
                _inputHandler.CheckAndHandleEndOfTurnActionsPIH(); 
                // Or directly end turn if Wait always ends turn
                // if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
                // _inputHandler.ChangeState(new PIH_WaitingForTurnState());
            }
            else { DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford WAIT. AP: {_selectedUnit.currentActionPoints}", _inputHandler); }
        }

        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            _inputHandler.ClearAllHighlights();
            if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
            // ChangeState to WaitingForTurn is handled by PIH main Update loop
        }

        public override void UpdateState()
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive ||
                !_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost) || // CanAffordAPForAction is on Unit
                (_inputHandler.CombatActive && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != _selectedUnit))
            {
                if (_inputHandler != null) 
                {
                     DebugHelper.LogWarning("PIH_SelectingAttackTargetState.UpdateState: Conditions no longer valid. Reverting.", _inputHandler);
                    _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                }
                return;
            }
            // Re-show range in case of dynamic changes
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