// PIH_UnitMovingState.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; 
using System.Collections; 

namespace MythTactics.Combat
{
    public class PIH_UnitMovingState : PlayerInputStateBase
    {
        private List<Tile> _path;
        private Coroutine _moveCoroutine;

        public PIH_UnitMovingState(List<Tile> path)
        {
            this._path = path;
        }

        public override void EnterState(PlayerInputHandler inputHandler)
        {
            base.EnterState(inputHandler);
            if (_selectedUnit == null || !_selectedUnit.IsAlive || 
                _selectedUnit.Movement == null || // MODIFIED: Add null check for Movement component
                _path == null || _path.Count == 0)
            {
                DebugHelper.LogWarning("PIH_UnitMovingState: Entered with invalid unit, path, or missing Movement component. Reverting.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }
            
            DebugHelper.Log($"PIH_UnitMovingState: Entered for {_selectedUnit.unitName}. Starting movement along path.", _inputHandler);
            _moveCoroutine = _inputHandler.StartCoroutine(MoveUnitCoroutine());
        }

        private IEnumerator MoveUnitCoroutine()
        {
            // MODIFIED: Call MoveOnPath via _selectedUnit.Movement
            // Assumes _selectedUnit.Movement was checked in EnterState
            yield return _inputHandler.StartCoroutine(_selectedUnit.Movement.MoveOnPath(_path)); 

            // Check IsAlive through the property on _selectedUnit which gets it from Stats
            if (_selectedUnit == null || !_selectedUnit.IsAlive) 
            {
                DebugHelper.Log($"{_selectedUnit?.unitName ?? "Unit"} died or became invalid during/after movement in PIH_UnitMovingState.", _inputHandler);
                _inputHandler.ClearAllHighlights(); 
                _inputHandler.ChangeState(new PIH_WaitingForTurnState()); 
                yield break;
            }
            
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            // Movement is in progress, ignore clicks
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            // Movement is in progress, ignore
        }

        public override void OnWaitInput(InputAction.CallbackContext context)
        {
            // Movement is in progress, ignore
        }

        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
            // Movement is in progress, ignore
        }
        
        public override void ExitState()
        {
            if (_moveCoroutine != null)
            {
                _inputHandler.StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
                // If movement was interrupted, the unit might not be on its final tile.
                // UnitMovement.StopMovementCoroutines() might be needed if it handles snapping to current tile.
                if (_selectedUnit != null && _selectedUnit.Movement != null && _selectedUnit.Movement.IsMoving)
                {
                    _selectedUnit.Movement.StopMovementCoroutines(); // Ensure the UnitMovement component knows it stopped.
                }
            }
            _inputHandler.ClearPathHighlight(); 
            base.ExitState();
        }
    }
}