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
            base.EnterState(inputHandler); // Sets _inputHandler
            if (_selectedUnit == null || !_selectedUnit.IsAlive || _selectedUnit.Movement == null || _selectedUnit.Movement.CurrentTile == null)
            {
                DebugHelper.LogWarning("PIH_SelectingMoveTargetState: Entered with invalid unit or unit not on a tile. Reverting.", _inputHandler.gameObject);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }

            _inputHandler.actionMenuUI?.HideMenu(); 

            _inputHandler.ShowReachableRange(
                _selectedUnit.Movement.CurrentTile.gridPosition, 
                _selectedUnit.Movement.CalculatedMoveRange, 
                _selectedUnit
            );

            DebugHelper.Log($"PIH_SelectingMoveTargetState: Entered for {_selectedUnit.unitName}. Move range shown. Waiting for move target selection.", _inputHandler.gameObject);
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || _selectedUnit.Movement == null || clickedTile == null)
            {
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }

            if (_inputHandler.HighlightedReachableTiles.Contains(clickedTile) && 
                !clickedTile.IsOccupiedOrImpassableFor(_selectedUnit)) 
            {
                List<Tile> path = _inputHandler.Pathfinder.FindPath(_selectedUnit.Movement.CurrentTile.gridPosition, clickedTile.gridPosition, _selectedUnit);

                if (path != null && path.Count > 0)
                {
                    // REVERTED: Directly execute move
                    if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.MoveActionCost))
                    {
                        _selectedUnit.SpendAPForAction(PlayerInputHandler.MoveActionCost); 
                        _inputHandler.ChangeState(new PIH_UnitMovingState(path));
                    }
                    else
                    {
                        DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford Move. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler.gameObject);
                        _inputHandler.ChangeState(new PIH_UnitActionPhaseState()); // Not enough AP, back to action phase
                    }
                }
                else
                {
                    DebugHelper.LogWarning($"PIH_SelectingMoveTargetState: No path found to {clickedTile.gridPosition} or tile is invalid. Stay in selection.", _inputHandler.gameObject);
                    _inputHandler.ShowPathHighlight(null); 
                }
            }
            else
            {
                DebugHelper.Log($"PIH_SelectingMoveTargetState: Clicked tile {clickedTile.gridPosition} is not a valid move target. Reverting to action phase.", _inputHandler.gameObject);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
            }
        }

        public override void UpdateState()
        {
            base.UpdateState();
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
            DebugHelper.Log("PIH_SelectingMoveTargetState: Action Menu Toggled. Reverting to Action Phase.", _inputHandler.gameObject);
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            DebugHelper.Log("PIH_SelectingMoveTargetState: ToggleAttackMode received. Reverting to Action Phase.", _inputHandler.gameObject);
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }

        public override void OnSelectAbilityInput(InputAction.CallbackContext context)
        {
            DebugHelper.Log("PIH_SelectingMoveTargetState: SelectAbility received. Reverting to Action Phase.", _inputHandler.gameObject);
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }
        
        public override void OnWaitInput(InputAction.CallbackContext context)
        {
             DebugHelper.Log("PIH_SelectingMoveTargetState: WaitInput received. Reverting to Action Phase.", _inputHandler.gameObject);
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }

        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
            DebugHelper.Log("PIH_SelectingMoveTargetState: EndTurnInput received.", _inputHandler.gameObject);
            if (_selectedUnit != null && TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
            _inputHandler.ChangeState(new PIH_WaitingForTurnState());
        }

        public override void ExitState()
        {
            _inputHandler.ClearReachableHighlight(true); 
            _inputHandler.ClearPathHighlight(); 
            base.ExitState();
        }
    }
}