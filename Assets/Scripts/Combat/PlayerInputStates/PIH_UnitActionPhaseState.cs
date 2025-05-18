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
            
            if (_selectedUnit.CurrentTile != null && TurnManager.Instance.ActiveUnit == _selectedUnit)
            {
                _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            }
            // DebugHelper.Log($"PIH_UnitActionPhaseState: Entered for {_selectedUnit.unitName}. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler); // Original log
            
            if (_inputHandler.actionMenuUI != null)
            {
                // DebugHelper.Log($"PIH_UnitActionPhaseState.EnterState END: ActionMenu IsVisible? {_inputHandler.actionMenuUI.IsVisible()}", _inputHandler); // Original log
            }
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            // If clickedTile is null, PlayerInputHandler.GetTileUnderMouse already determined the click was on UI
            // and should not be processed as a world click by this state.
            if (clickedTile == null)
            {
                // DebugHelper.Log("PIH_UnitActionPhaseState: ClickInput received null tile (likely UI click, or off-grid). No action.", _inputHandler);
                return;
            }

            // At this point, clickedTile is NOT null, meaning a valid grid tile was clicked.

            if (_selectedUnit == null || !_selectedUnit.IsAlive) 
            {
                // DebugHelper.LogWarning("PIH_UnitActionPhaseState: OnClickInput with no selected/living unit, but got a tile click. Ignoring.", _inputHandler);
                return; 
            }

            if (clickedTile == _selectedUnit.CurrentTile)
            {
                if (_inputHandler.actionMenuUI != null && !_inputHandler.actionMenuUI.IsVisible() &&
                    TurnManager.Instance.ActiveUnit == _selectedUnit) 
                {
                    // DebugHelper.Log("PIH_UnitActionPhaseState: Clicked selected unit's tile, action menu was hidden. Showing menu.", _inputHandler);
                    _inputHandler.ShowActionMenuForSelectedUnitPublic();
                }
                // else { DebugHelper.Log("PIH_UnitActionPhaseState: Clicked selected unit's tile. Menu already visible or not unit's turn. No specific action.", _inputHandler); }
                return; 
            }

            // If a valid grid tile (not the selected unit's tile) was clicked AND the action menu is visible,
            // it means the player clicked off the menu onto the map. Hide the menu.
            if (_inputHandler.actionMenuUI != null && _inputHandler.actionMenuUI.IsVisible())
            {
                 // DebugHelper.Log("PIH_UnitActionPhaseState: Clicked on map (valid tile) while action menu was visible. Hiding menu.", _inputHandler);
                 _inputHandler.actionMenuUI.HideMenu();
                 if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
                 // No return here, allow fall-through if other logic needs to run for map clicks when menu is hidden
            }
            // else // Action menu wasn't visible, or click was on unit's own tile (handled above)
            // {
            //    DebugHelper.Log($"PIH_UnitActionPhaseState: Clicked tile {clickedTile.gridPosition}. Action menu was hidden or click was on own unit. No further direct action from this state for this tile click.", _inputHandler);
            // }
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
            { _inputHandler.HandleActionFromHotKey("Attack"); }
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

        public override void OnWaitInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.WaitActionCost))
            { _inputHandler.HandleActionFromHotKey("Wait"); }
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
            if (_selectedUnit == null || !_selectedUnit.IsAlive ||
                (_inputHandler.CombatActive && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != _selectedUnit) )
            { _inputHandler.ChangeState(new PIH_WaitingForTurnState()); return; }

            if (_inputHandler.actionMenuUI != null && !_inputHandler.actionMenuUI.IsVisible() &&
                !_inputHandler.HighlightedReachableTiles.Any() &&
                !_inputHandler.HighlightedAttackRangeTiles.Any() &&
                !_inputHandler.HighlightedAbilityRangeTiles.Any())
            {
                if (_selectedUnit.CurrentTile != null && _selectedUnit.CurrentTile.CurrentHighlightState != TileHighlightState.SelectedUnit &&
                    TurnManager.Instance.ActiveUnit == _selectedUnit) 
                { _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit); }
            }
            _inputHandler.CheckAndHandleEndOfTurnActionsPIH(); 
        }

        public override void ExitState()
        {
            base.ExitState();
        }
    }
}