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
            if (_selectedUnit == null || !_selectedUnit.IsAlive || _path == null || _path.Count == 0)
            {
                DebugHelper.LogWarning("PIH_UnitMovingState: Entered with invalid unit or path. Reverting to UnitActionPhase.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }

            // AP for movement was already spent in PIH_UnitActionPhaseState before transitioning here.
            // So, the CanAffordAction check here is removed.
            
            DebugHelper.Log($"PIH_UnitMovingState: Entered for {_selectedUnit.unitName}. Starting movement along path.", _inputHandler);
            _moveCoroutine = _inputHandler.StartCoroutine(MoveUnitCoroutine());
        }

        private IEnumerator MoveUnitCoroutine()
        {
            yield return _inputHandler.StartCoroutine(_selectedUnit.MoveOnPath(_path)); 

            if (!_inputHandler.SelectedUnit.IsAlive) // Check selected unit from handler, as ours might be stale if unit died.
            {
                DebugHelper.Log($"{_selectedUnit.unitName} died or became invalid during/after movement in PIH_UnitMovingState.", _inputHandler);
                _inputHandler.ClearAllHighlights(); 
                // PIH Update loop will handle transitioning to WaitingForTurn if active unit is null or not player.
                // If the unit that died was the selected unit, PIH Update will clear _selectedUnit.
                _inputHandler.ChangeState(new PIH_WaitingForTurnState()); // Go to waiting, let PIH Update sort out next state if needed
                yield break;
            }
            
            // DebugHelper.Log($"PIH_UnitMovingState: {_selectedUnit.unitName} finished movement. AP: {_selectedUnit.currentActionPoints}/{_selectedUnit.maxActionPoints}", _inputHandler);
            
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            // DebugHelper.Log("PIH_UnitMovingState: Click ignored, unit is moving.", _inputHandler);
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            // DebugHelper.Log("PIH_UnitMovingState: Attack Toggle ignored, unit is moving.", _inputHandler);
        }

        public override void OnWaitInput(InputAction.CallbackContext context)
        {
            // DebugHelper.Log("PIH_UnitMovingState: Wait ignored, unit is moving.", _inputHandler);
        }

        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
            // DebugHelper.Log("PIH_UnitMovingState: End Turn ignored, unit is moving.", _inputHandler);
        }
        
        public override void ExitState()
        {
            if (_moveCoroutine != null)
            {
                _inputHandler.StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
                // DebugHelper.LogWarning("PIH_UnitMovingState: Exited while move coroutine was active. Stopped coroutine.", _inputHandler);
            }
            _inputHandler.ClearPathHighlight(); 
            base.ExitState();
        }
    }
}