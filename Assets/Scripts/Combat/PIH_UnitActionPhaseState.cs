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
            
            // ClearAllHighlights is called by PlayerInputHandler.ChangeState before this state's EnterState.
            // So, we just need to ensure the selected unit's tile is highlighted if the menu is shown.
            // The menu itself is shown by PlayerInputHandler.ChangeState if conditions are met.
            if (_inputHandler.actionMenuUI != null && _inputHandler.actionMenuUI.IsVisible())
            {
                if (_selectedUnit.CurrentTile != null) 
                    _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            }
            DebugHelper.Log($"PIH_UnitActionPhaseState: Entered for {_selectedUnit.unitName}. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler);
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || clickedTile == null )
            {
                return;
            }

            // If clicking on the currently selected unit's tile:
            if (clickedTile == _selectedUnit.CurrentTile)
            {
                // If the action menu is NOT visible, show it.
                if (_inputHandler.actionMenuUI != null && !_inputHandler.actionMenuUI.IsVisible())
                {
                    DebugHelper.Log("PIH_UnitActionPhaseState: Clicked selected unit's tile. Action menu was hidden. Showing menu.", _inputHandler);
                    // Call the public helper method in PlayerInputHandler to show the menu
                    _inputHandler.ShowActionMenuForSelectedUnitPublic(); // We will make this public
                }
                else
                {
                    // If menu is already visible, clicking the unit might do nothing or cycle info (future).
                    DebugHelper.Log("PIH_UnitActionPhaseState: Clicked selected unit's tile. Menu already visible or other condition. No specific action.", _inputHandler);
                }
                return; // Consumed the click
            }

            // If the action menu is visible, and click is NOT on a UI element,
            // a common behavior is to hide the menu.
            if (_inputHandler.actionMenuUI != null && _inputHandler.actionMenuUI.IsVisible() && 
                (UnityEngine.EventSystems.EventSystem.current != null && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
            {
                 DebugHelper.Log("PIH_UnitActionPhaseState: Clicked on map while action menu was visible. Hiding menu.", _inputHandler);
                 _inputHandler.actionMenuUI.HideMenu();
                 // Ensure unit remains selected visually
                 if (_selectedUnit.CurrentTile != null) 
                    _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
                 return; 
            }

            // If menu is NOT visible and clicked elsewhere, it's not a direct action from this state.
            // Other states (like PIH_SelectingMoveTargetState) would handle clicks on their respective highlighted tiles.
            // This state, when menu is hidden, primarily waits for an action menu trigger (space or click self).
            DebugHelper.Log($"PIH_UnitActionPhaseState: Clicked tile {clickedTile.gridPosition}. Menu is hidden. No direct action from this state for this tile.", _inputHandler);
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
            {
                // PlayerInputHandler.ChangeState will hide the action menu
                _inputHandler.ShowAttackRange(_selectedUnit); 
                _inputHandler.ChangeState(new PIH_SelectingAttackTargetState());
            }
            else
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford ATTACK. Has {_selectedUnit.CurrentActionPoints} AP.", _inputHandler);
        }

        public override void OnSelectAbilityInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || _selectedUnit.Combat == null) return;
            if (_selectedUnit.knownAbilities == null || _selectedUnit.knownAbilities.Count == 0)
            {
                DebugHelper.Log($"{_selectedUnit.unitName} has no abilities to select.", _inputHandler);
                return;
            }
            // This is just a placeholder for a proper ability selection mechanism (e.g. number keys)
            // For now, assume it tries to use the "Skills" button logic from Action Menu
            _inputHandler.HandleActionFromHotKey("Skills");

        }

        public override void OnWaitInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.WaitActionCost))
            {
                _inputHandler.HandleActionFromHotKey("Wait"); // Delegate to a common handler
            }
            else
            {
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford WAIT. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler);
            }
        }

        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            DebugHelper.Log($"{_selectedUnit.unitName} explicitly ends turn via hotkey. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler);
            // PlayerInputHandler.ChangeState will hide the action menu
            if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
            // The ChangeState to WaitingForTurn will happen via PlayerInputHandler.Update detecting ActiveUnit is no longer _selectedUnit
        }

        public override void UpdateState()
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive ||
                (_inputHandler.CombatActive && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != _selectedUnit) )
            {
                _inputHandler.ChangeState(new PIH_WaitingForTurnState());
                return;
            }
            // If menu is hidden AND no ranges are shown, ensure selected unit tile is highlighted.
            // This handles cases where menu was closed by spacebar or clicking away.
            if (_inputHandler.actionMenuUI != null && !_inputHandler.actionMenuUI.IsVisible() &&
                !_inputHandler.HighlightedReachableTiles.Any() &&
                !_inputHandler.HighlightedAttackRangeTiles.Any() &&
                !_inputHandler.HighlightedAbilityRangeTiles.Any())
            {
                if (_selectedUnit.CurrentTile != null && _selectedUnit.CurrentTile.CurrentHighlightState != TileHighlightState.SelectedUnit)
                {
                     _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
                }
            }
            _inputHandler.CheckAndHandleEndOfTurnActionsPIH(); 
        }

        public override void ExitState()
        {
            // Action menu hiding is handled by PlayerInputHandler.ChangeState generally
            // or by specific actions leading to other states.
            // No need to explicitly hide it here unless there's a specific case.
            base.ExitState();
        }
    }
}