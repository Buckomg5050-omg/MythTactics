// PIH_SelectingMoveTargetState.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq; 

namespace MythTactics.Combat
{
    public class PIH_SelectingMoveTargetState : PlayerInputStateBase
    {
        public PIH_SelectingMoveTargetState() { }

        public override void EnterState(PlayerInputHandler inputHandler)
        {
            base.EnterState(inputHandler); 
            if (_selectedUnit == null || !_selectedUnit.IsAlive || _selectedUnit.Movement == null || _selectedUnit.Movement.CurrentTile == null)
            {
                DebugHelper.LogWarning("PIH_SelectingMoveTargetState: Entered with invalid unit or unit not on a tile. Reverting.", _inputHandler.gameObject);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }

            _inputHandler.actionMenuUI?.HideActionBar(); 

            _inputHandler.ShowReachableRange(
                _selectedUnit.Movement.CurrentTile.gridPosition, 
                _selectedUnit.Movement.CalculatedMoveRange, 
                _selectedUnit
            );

            DebugHelper.Log($"PIH_SelectingMoveTargetState: Entered for {_selectedUnit.unitName}. Move range shown. Waiting for move target selection.", _inputHandler.gameObject);
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            // CORRECTED: _owner replaced with _inputHandler
            Debug.Log($"PIH_STM: OnClickInput. Clicked Tile: {clickedTile?.gridPosition.ToString() ?? "NULL"}. HighlightedReachableTiles.Count: {_inputHandler.HighlightedReachableTiles.Count}", _inputHandler.gameObject);
            if (clickedTile != null)
            {
                Debug.Log($"PIH_STM: Clicked Tile ({clickedTile.gridPosition}) - IsOccupiedOrImpassable: {clickedTile.IsOccupiedOrImpassableFor(_selectedUnit)}, HighlightState: {clickedTile.CurrentHighlightState}, Is in HighlightedReachableTiles list? {_inputHandler.HighlightedReachableTiles.Contains(clickedTile)}", _inputHandler.gameObject);
            }

            if (_selectedUnit == null || !_selectedUnit.IsAlive || _selectedUnit.Movement == null || clickedTile == null)
            {
                Debug.LogWarning("PIH_STM: OnClickInput - Aborting due to invalid unit/tile.", _inputHandler.gameObject); // CORRECTED
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }

            bool isInReachableList = _inputHandler.HighlightedReachableTiles.Contains(clickedTile);
            bool isBlocked = clickedTile.IsOccupiedOrImpassableFor(_selectedUnit);

            if (isInReachableList && !isBlocked) 
            {
                List<Tile> path = _inputHandler.Pathfinder.FindPath(_selectedUnit.Movement.CurrentTile.gridPosition, clickedTile.gridPosition, _selectedUnit);

                if (path != null && path.Count > 0)
                {
                    if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.MoveActionCost))
                    {
                        Debug.Log($"PIH_STM: Path found and AP affordable. Initiating move to {clickedTile.gridPosition}", _inputHandler.gameObject); // CORRECTED
                        _selectedUnit.SpendAPForAction(PlayerInputHandler.MoveActionCost); 
                        _inputHandler.ChangeState(new PIH_UnitMovingState(path));
                    }
                    else
                    {
                        DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford Move. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler.gameObject);
                        _inputHandler.ChangeState(new PIH_UnitActionPhaseState()); 
                    }
                }
                else
                {
                    DebugHelper.LogWarning($"PIH_STM: No path found to {clickedTile.gridPosition} despite being in reachable list (or tile is invalid for pathing). Stay in selection.", _inputHandler.gameObject);
                    _inputHandler.ShowPathHighlight(null); 
                }
            }
            else
            {
                DebugHelper.Log($"PIH_STM: Clicked tile {clickedTile.gridPosition} is not a valid move target. isInReachableList: {isInReachableList}, isBlocked: {isBlocked}. Reverting to action phase.", _inputHandler.gameObject);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
            }
        }

        public override void UpdateState()
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || 
                (_inputHandler.CombatActive && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != _selectedUnit)) 
            {
                _inputHandler.ChangeState(new PIH_WaitingForTurnState());
                return;
            }
            if (!_selectedUnit.CanAffordAPForAction(PlayerInputHandler.MoveActionCost)) 
            {
                DebugHelper.LogWarning($"PIH_STM.UpdateState: Unit {_selectedUnit.unitName} can no longer afford Move. Reverting.", _inputHandler.gameObject);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }

            Vector2 screenPosition = _inputHandler.PlayerControls.Gameplay.Point.ReadValue<Vector2>();
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                _inputHandler.ClearPathHighlight(); 
                return;
            }

            Ray ray = _inputHandler.MainCamera.ScreenPointToRay(screenPosition);
            if (new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance))
            {
                Tile hoveredTile = GridManager.Instance.GetTile(GridManager.Instance.WorldToGrid(ray.GetPoint(distance)));
                if (hoveredTile != null && _selectedUnit != null && _selectedUnit.Movement != null && _selectedUnit.Movement.CurrentTile != null &&
                    _inputHandler.HighlightedReachableTiles.Contains(hoveredTile) && 
                    !hoveredTile.IsOccupiedOrImpassableFor(_selectedUnit)) 
                {
                    List<Tile> pathToHover = _inputHandler.Pathfinder.FindPath(_selectedUnit.Movement.CurrentTile.gridPosition, hoveredTile.gridPosition, _selectedUnit);
                    _inputHandler.ShowPathHighlight(pathToHover);
                }
                else
                {
                    _inputHandler.ClearPathHighlight();
                }
            }
            else
            {
                _inputHandler.ClearPathHighlight();
            }
        }
        
        public override void OnToggleActionMenuInput(InputAction.CallbackContext context)
        {
            DebugHelper.Log("PIH_STM: Action Menu Toggled (Cancel Move). Reverting to Action Phase.", _inputHandler.gameObject);
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            DebugHelper.Log("PIH_STM: ToggleAttackMode received (Cancel Move). Reverting to Action Phase.", _inputHandler.gameObject);
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }

        public override void OnSelectAbilityInput(InputAction.CallbackContext context)
        {
            DebugHelper.Log("PIH_STM: SelectAbility received (Cancel Move). Reverting to Action Phase.", _inputHandler.gameObject);
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }
        
        public override void OnWaitInput(InputAction.CallbackContext context)
        {
             DebugHelper.Log("PIH_STM: WaitInput received (Cancel Move). Reverting to Action Phase.", _inputHandler.gameObject);
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }

        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
            DebugHelper.Log("PIH_STM: EndTurnInput received (Cancel Move & End Turn).", _inputHandler.gameObject);
            if (_selectedUnit != null && TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }

        public override void ExitState()
        {
            _inputHandler.ClearReachableHighlight(true); 
            _inputHandler.ClearPathHighlight(); 
            base.ExitState();
        }
    }
}