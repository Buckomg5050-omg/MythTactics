// PIH_SelectingAttackTargetState.cs
using UnityEngine.InputSystem;
using System.Linq;
using UnityEngine;

namespace MythTactics.Combat
{
    public class PIH_SelectingAttackTargetState : PlayerInputStateBase
    {
        private bool _actionIsResolving = false; 

        public override void EnterState(PlayerInputHandler inputHandler)
        {
            base.EnterState(inputHandler);
            _actionIsResolving = false; 

            if (_selectedUnit == null || !_selectedUnit.IsAlive)
            {
                DebugHelper.LogWarning("PIH_SelectingAttackTargetState: Entered with no/dead unit. Reverting.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }
            
            if (!_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
            {
                 DebugHelper.LogWarning($"PIH_SelectingAttackTargetState: Unit {_selectedUnit.unitName} cannot afford attack upon entering (Needs {PlayerInputHandler.AttackActionCost} AP, Has {_selectedUnit.CurrentActionPoints}). Reverting.", _inputHandler);
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
            DebugHelper.Log("PIH_SelectingAttackTargetState: Entered. Attack range shown.", _inputHandler);
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            if (_actionIsResolving)
            {
                DebugHelper.Log("PIH_SelectingAttackTargetState: Click ignored, action already resolving.", _inputHandler);
                return; 
            }

            if (_selectedUnit == null || !_selectedUnit.IsAlive || clickedTile == null || _selectedUnit.Combat == null)
            {
                DebugHelper.Log("PIH_SelectingAttackTargetState: Click ignored due to null unit, tile, or missing Combat component.", _inputHandler);
                return;
            }

            if (_inputHandler.HighlightedAttackRangeTiles.Contains(clickedTile))
            {
                Unit targetUnit = clickedTile.occupyingUnit;
                if (targetUnit != null && targetUnit != _selectedUnit && targetUnit.IsAlive)
                {
                    if (!_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
                    {
                        DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford ATTACK at time of click. AP: {_selectedUnit.CurrentActionPoints}. Reverting.", _inputHandler);
                        _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                        return;
                    }
                    
                    _actionIsResolving = true; 
                    DebugHelper.Log($"PIH_SelectingAttackTargetState: Initiating attack from {_selectedUnit.unitName} on {targetUnit.unitName}.", _inputHandler);
                    
                    _inputHandler.StartCoroutine(_selectedUnit.Combat.PerformAttack(targetUnit, 
                        () => { 
                            DebugHelper.Log($"PIH_SelectingAttackTargetState: PerformAttack callback received for {_selectedUnit.unitName}.", _inputHandler);
                            _actionIsResolving = false; 
                            _inputHandler.ChangeState(new PIH_UnitActionPhaseState()); 
                        }
                    ));
                }
                else if (targetUnit == _selectedUnit) { DebugHelper.Log("PIH: Cannot target self with basic attack.", _inputHandler); }
                else if (targetUnit != null && !targetUnit.IsAlive) { DebugHelper.Log($"PIH: Target {targetUnit.unitName} is defeated.", _inputHandler); }
                else { DebugHelper.Log($"PIH: No valid unit target on tile {clickedTile.gridPosition}.", _inputHandler); }
            }
            else 
            {
                DebugHelper.Log("PIH_SelectingAttackTargetState: Clicked outside attack range. Reverting to Action Phase.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
            }
        }

        // This method is called if the player presses the "ToggleAttackMode" key again (e.g. 'T')
        // It effectively acts as a way to cancel out of attack targeting.
        public override void OnToggleAttackModeInput(InputAction.CallbackContext context) 
        {
            if (_actionIsResolving) return;
            DebugHelper.Log("PIH_SelectingAttackTargetState: ToggleAttackModeInput (cancelling). Reverting to Action Phase.", _inputHandler);
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }

        public override void OnWaitInput(InputAction.CallbackContext context)
        {
            if (_actionIsResolving) return;
            DebugHelper.Log("PIH_SelectingAttackTargetState: 'Wait' input received. Invalid in this state. Cancelling attack targeting.", _inputHandler);
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }

        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
            if (_actionIsResolving) return;
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            DebugHelper.Log("PIH_SelectingAttackTargetState: 'End Turn' input received. Ending turn.", _inputHandler);
            _inputHandler.ClearAllHighlights();
            if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }

        public override void UpdateState()
        {
            if (_actionIsResolving) return; 

            if (_selectedUnit == null || !_selectedUnit.IsAlive ||
                (_inputHandler.CombatActive && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != _selectedUnit))
            {
                if (_inputHandler != null)
                {
                     DebugHelper.LogWarning("PIH_SelectingAttackTargetState.UpdateState: Unit/Turn invalid. Reverting.", _inputHandler);
                    _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                }
                return;
            }
            
            if (!_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
            {
                DebugHelper.LogWarning($"PIH_SelectingAttackTargetState.UpdateState: Unit {_selectedUnit.unitName} can no longer afford attack mode (Needs {PlayerInputHandler.AttackActionCost} AP, Has {_selectedUnit.CurrentActionPoints}). Reverting.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
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
            _actionIsResolving = false; 
            base.ExitState();
        }
    }
}