// PIH_SelectingAttackTargetState.cs
using UnityEngine.InputSystem;
using System.Linq; 
using System.Collections; // NEW: For StartCoroutine

namespace MythTactics.Combat
{
    public class PIH_SelectingAttackTargetState : PlayerInputStateBase
    {
        public override void EnterState(PlayerInputHandler inputHandler)
        {
            base.EnterState(inputHandler);
            if (_selectedUnit == null || !_selectedUnit.IsAlive || !_selectedUnit.CanAffordAction(PlayerInputHandler.AttackActionCost))
            {
                DebugHelper.LogWarning("PIH_SelectingAttackTargetState: Entered with no/dead unit or insufficient AP for attack. Reverting to UnitActionPhase.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }

            // DebugHelper.Log($"PIH_SelectingAttackTargetState: Entered for {_selectedUnit.unitName}. Showing attack range.", _inputHandler);
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

            if (!_selectedUnit.CanAffordAction(PlayerInputHandler.AttackActionCost))
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
                    // Attack initiated, but actual AP cost, animation, damage, and state change
                    // will be handled within the Unit's PerformAttack coroutine.
                    // We transition to a "waiting for action to complete" state or simply
                    // start the coroutine and let PlayerInputHandler's Update loop manage PIH state
                    // based on ActiveUnit from TurnManager.
                    // For now, let PIH_UnitActionPhaseState handle re-evaluation after attack.
                    
                    // We start the PerformAttack coroutine on the _selectedUnit (the attacker)
                    // Pass the PlayerInputHandler context so PerformAttack can call CheckAndHandleEndOfTurnActionsPIH
                    _inputHandler.StartCoroutine(_selectedUnit.PerformAttack(targetUnit, _inputHandler));

                    // After initiating attack, clear attack range and go to a neutral state (WaitingForTurn)
                    // The Unit.PerformAttack will call PIH.CheckAndHandleEndOfTurnActionsPIH, which may put it back to UnitActionPhase
                    _inputHandler.ClearAttackRangeHighlight(true);
                    _inputHandler.ChangeState(new PIH_WaitingForTurnState()); // Wait for attack to resolve
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
                DebugHelper.Log($"PIH_SelectingAttackTargetState: Clicked tile {clickedTile.gridPosition} is outside current attack range. Cancelling attack selection.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
            }
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            DebugHelper.Log("PIH_SelectingAttackTargetState: Attack Toggle pressed. Reverting to UnitActionPhase.", _inputHandler);
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }

        public override void OnWaitInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;

            if (_selectedUnit.CanAffordAction(PlayerInputHandler.WaitActionCost))
            {
                DebugHelper.Log("PIH_SelectingAttackTargetState: Wait action selected. Cancelling attack and performing Wait.", _inputHandler);
                _selectedUnit.SpendActionPoints(PlayerInputHandler.WaitActionCost);
                
                _inputHandler.ClearAllHighlights();
                if(TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
                 _inputHandler.ChangeState(new PIH_WaitingForTurnState()); // Explicitly go to waiting
            }
            else
            {
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford WAIT action (Cost: {PlayerInputHandler.WaitActionCost}). AP: {_selectedUnit.currentActionPoints}", _inputHandler);
            }
        }

        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
             if (_selectedUnit == null || !_selectedUnit.IsAlive) return;

            DebugHelper.Log("PIH_SelectingAttackTargetState: End Turn selected. Cancelling attack and ending turn.", _inputHandler);
            _inputHandler.ClearAllHighlights();
            if(TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
            _inputHandler.ChangeState(new PIH_WaitingForTurnState()); // Explicitly go to waiting
        }
        
        public override void UpdateState()
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || 
                !_selectedUnit.CanAffordAction(PlayerInputHandler.AttackActionCost) || 
                (_inputHandler.CombatActive && TurnManager.Instance.ActiveUnit != _selectedUnit))
            {
                // DebugHelper.Log("PIH_SelectingAttackTargetState: Conditions met to transition to UnitActionPhase/Waiting.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }
        }

        public override void ExitState()
        {
            _inputHandler.ClearAttackRangeHighlight(true); 
            base.ExitState();
        }
    }
}