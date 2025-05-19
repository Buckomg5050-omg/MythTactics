// PIH_UnitActionPhaseState.cs
using UnityEngine.InputSystem;
using System.Collections.Generic; // Not strictly needed by this version, but often useful
using System.Linq; // For .Any() if used later
using UnityEngine;

namespace MythTactics.Combat // Ensure this namespace matches your project structure
{
    public class PIH_UnitActionPhaseState : PlayerInputStateBase
    {
        public override void EnterState(PlayerInputHandler inputHandler)
        {
            base.EnterState(inputHandler); // Sets _inputHandler and _selectedUnit

            if (_selectedUnit == null || !_selectedUnit.IsAlive)
            {
                // DebugHelper.LogWarning("PIH_UnitActionPhaseState: Entered with no selected or living unit. Transitioning to WaitingForTurn.", _inputHandler);
                _inputHandler.ChangeState(new PIH_WaitingForTurnState());
                return;
            }

            _inputHandler.SelectedAbility = null; // Clear any previously selected ability

            // Ensure selected unit's tile is highlighted if it's their turn and action menu is up or should be shown
            if (_selectedUnit.CurrentTile != null && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == _selectedUnit)
            {
                _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            }

            // DebugHelper.Log($"PIH_UnitActionPhaseState: Entered for {_selectedUnit.unitName}. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler);
            
            // This log was to check if PIH.ChangeState correctly showed the menu before this state's Enter.
            // if (_inputHandler.actionMenuUI != null)
            // {
            //     DebugHelper.Log($"PIH_UnitActionPhaseState.EnterState END: ActionMenu IsVisible? {_inputHandler.actionMenuUI.IsVisible()}", _inputHandler);
            // }
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            // PlayerInputHandler.GetTileUnderMouse(true) already uses the cached _isPointerOverUIThisFrame.
            // If clickedTile is null here, it means the click was on UI or off-grid and blocked by GetTileUnderMouse.
            if (clickedTile == null)
            {
                // This means the click was likely on a UI element (like the ActionMenu itself, or another panel)
                // or off the grid entirely. No action specific to clicking a *tile* in this state.
                // DebugHelper.Log("PIH_UnitActionPhaseState: OnClickInput received null tile (likely UI click or off-grid). No map-click action taken.", _inputHandler);
                return;
            }

            // If we reach here, clickedTile is a valid grid tile.
            if (_selectedUnit == null || !_selectedUnit.IsAlive) 
            {
                // Should not happen if EnterState handled it, but as a safeguard.
                // DebugHelper.LogWarning("PIH_UnitActionPhaseState: OnClickInput with no selected/living unit, despite receiving a tile click. Ignoring.", _inputHandler);
                return; 
            }

            // Case 1: Clicked on the selected unit's own tile
            if (clickedTile == _selectedUnit.CurrentTile)
            {
                // If action menu is hidden, show it (toggle behavior for self-click)
                if (_inputHandler.actionMenuUI != null && !_inputHandler.actionMenuUI.IsVisible() &&
                    TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == _selectedUnit) 
                {
                    // DebugHelper.Log("PIH_UnitActionPhaseState: Clicked selected unit's tile; action menu was hidden. Showing menu.", _inputHandler);
                    _inputHandler.ShowActionMenuForSelectedUnitPublic();
                }
                // else 
                // { 
                //     DebugHelper.Log("PIH_UnitActionPhaseState: Clicked selected unit's tile. Menu already visible or not unit's turn. No specific action.", _inputHandler); 
                // }
                return; 
            }

            // Case 2: Clicked on a different tile on the map while the action menu was visible
            // This implies the player wants to dismiss the action menu by clicking away.
            if (_inputHandler.actionMenuUI != null && _inputHandler.actionMenuUI.IsVisible())
            {
                 // DebugHelper.Log("PIH_UnitActionPhaseState: Clicked on map (valid tile) while action menu was visible. Hiding menu.", _inputHandler);
                 _inputHandler.actionMenuUI.HideMenu();
                 // Re-highlight the selected unit's tile as the menu is now gone.
                 if (_selectedUnit.CurrentTile != null) 
                 {
                    _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
                 }
                 return; 
            }

            // Case 3: Clicked on a different tile on the map and action menu was NOT visible.
            // No specific action in PIH_UnitActionPhaseState for this, unless you want to implement unit selection/deselection here.
            // For now, it's unhandled in this specific state if the menu is already hidden.
            // DebugHelper.Log($"PIH_UnitActionPhaseState: Clicked tile {clickedTile.gridPosition}. Menu was hidden. No specific action for this click in this state.", _inputHandler);
        }

        public override void OnToggleActionMenuInput(InputAction.CallbackContext context)
        {
            // This method is called by PlayerInputHandler's general toggle logic.
            // If this state is active, it usually means the menu should either show
            // or PlayerInputHandler itself will handle hiding if it's already visible.
            // The core logic for showing/hiding is in PlayerInputHandler.OnToggleActionMenuPerformedHandler
            // This state doesn't need to do much extra here, as PIH.ChangeState to this
            // often triggers ShowActionMenuForSelectedUnitPublic.
            if (_inputHandler.actionMenuUI != null)
            {
                if (_inputHandler.actionMenuUI.IsVisible())
                {
                    _inputHandler.actionMenuUI.HideMenu();
                     if (_selectedUnit != null && _selectedUnit.CurrentTile != null) 
                     {
                        _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
                     }
                }
                else if (_selectedUnit != null && _selectedUnit.IsAlive && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == _selectedUnit)
                {
                    _inputHandler.ShowActionMenuForSelectedUnitPublic();
                }
            }
        }


        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
            { 
                _inputHandler.HandleActionFromHotKey("Attack"); 
            }
            // else { DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford ATTACK. Has {_selectedUnit.CurrentActionPoints} AP.", _inputHandler); }
        }

        public override void OnSelectAbilityInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || _selectedUnit.Combat == null) return;
            if (_selectedUnit.knownAbilities == null || _selectedUnit.knownAbilities.Count == 0) 
            { 
                // DebugHelper.Log($"{_selectedUnit.unitName} has no abilities to select with hotkey.", _inputHandler); 
                return; 
            }
             _inputHandler.HandleActionFromHotKey("Skills"); 
        }

        public override void OnWaitInput(InputAction.CallbackContext context) // Assuming you have a Wait input action
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.WaitActionCost))
            { 
                _inputHandler.HandleActionFromHotKey("Wait"); 
            }
            // else { DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford WAIT. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler); }
        }

        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            // DebugHelper.Log($"{_selectedUnit.unitName} explicitly ends turn via hotkey. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler);
            _inputHandler.actionMenuUI?.HideMenu(); 
            if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }

        public override void UpdateState()
        {
            // If unit becomes null, not alive, or no longer active unit, go back to waiting.
            if (_selectedUnit == null || !_selectedUnit.IsAlive ||
                (_inputHandler.CombatActive && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != _selectedUnit) )
            { 
                _inputHandler.ChangeState(new PIH_WaitingForTurnState()); 
                return; 
            }

            // Ensure selected unit's tile remains highlighted if no other UI is showing range.
            if (_inputHandler.actionMenuUI != null && !_inputHandler.actionMenuUI.IsVisible() &&
                _inputHandler.HighlightedReachableTiles != null && !_inputHandler.HighlightedReachableTiles.Any() &&
                _inputHandler.HighlightedAttackRangeTiles != null && !_inputHandler.HighlightedAttackRangeTiles.Any() &&
                _inputHandler.HighlightedAbilityRangeTiles != null && !_inputHandler.HighlightedAbilityRangeTiles.Any())
            {
                if (_selectedUnit.CurrentTile != null && _selectedUnit.CurrentTile.CurrentHighlightState != TileHighlightState.SelectedUnit &&
                    TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == _selectedUnit) 
                { 
                    _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); 
                }
            }
            _inputHandler.CheckAndHandleEndOfTurnActionsPIH(); 
        }

        public override void ExitState()
        {
            // Clean up specific to this state if needed, e.g., ensuring action menu is hidden
            // if (_inputHandler.actionMenuUI != null)
            // {
            //     _inputHandler.actionMenuUI.HideMenu();
            // }
            base.ExitState(); // Calls base class ExitState if any
        }
    }
}