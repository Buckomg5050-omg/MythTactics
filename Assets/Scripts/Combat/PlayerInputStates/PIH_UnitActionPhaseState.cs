// PIH_UnitActionPhaseState.cs
using UnityEngine.InputSystem;
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
                _inputHandler.ChangeState(new PIH_WaitingForTurnState());
                return;
            }

            _inputHandler.SelectedAbility = null; 

            if (_selectedUnit.CurrentTile != null && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == _selectedUnit)
            {
                _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            }
            
            // MODIFIED: Action bar is now refreshed/shown by PlayerInputHandler.Update() or PlayerInputHandler.ChangeState()
            // when transitioning into this state. No explicit call to show it here is needed.
            // DebugHelper.Log($"PIH_UnitActionPhaseState: Entered for {_selectedUnit.unitName}. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler);
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            if (clickedTile == null)
            {
                // UI click or off-grid, potentially close sub-menus on the action bar
                _inputHandler.actionMenuUI?.HideSubMenu();
                return;
            }

            if (_selectedUnit == null || !_selectedUnit.IsAlive) 
            {
                return; 
            }

            // If player clicks on the map and a sub-menu of the action bar is open, close the sub-menu.
            if (_inputHandler.actionMenuUI != null && _inputHandler.actionMenuUI.IsSubMenuOpen()) // Requires IsSubMenuOpen() on ActionMenuUI
            {
                 _inputHandler.actionMenuUI.HideSubMenu();
                 return; 
            }
            // If no sub-menu was open, clicking a tile in this state doesn't do anything else by default.
            // One might implement unit re-selection here if desired.
        }

        public override void OnToggleActionMenuInput(InputAction.CallbackContext context)
        {
            // PlayerInputHandler.OnToggleActionMenuPerformedHandler now handles closing sub-panels.
            // If this state is active and no sub-panels are open, the PIH handler might not do much more.
            // This method might become less relevant for a persistent bar.
            // For now, ensure any action bar sub-menu is closed if this is triggered.
            _inputHandler.actionMenuUI?.HideSubMenu();
        }


        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            // Attempt to activate "Basic Attack" from the sub-menu
            if (_inputHandler.actionMenuUI != null)
            {
                // Check if unit can afford the basic attack first for a better UX, though ActionMenuUI will also check.
                if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
                {
                    _inputHandler.actionMenuUI.SimulateSubMenuActionClick(ActionMenuUI.BASIC_ATTACK_ACTION_NAME);
                }
            }
        }

        public override void OnSelectAbilityInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || _selectedUnit.Combat == null) return;
            if (_selectedUnit.knownAbilities == null || !_selectedUnit.knownAbilities.Any()) return; 
            
            // This should now ideally trigger the "Skills" button on the action bar.
            if (_inputHandler.actionMenuUI != null)
            {
                 _inputHandler.actionMenuUI.SimulateMainActionClick("Skills");
            }
        }

        public override void OnWaitInput(InputAction.CallbackContext context) 
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            if (_inputHandler.actionMenuUI != null)
            {
                _inputHandler.actionMenuUI.SimulateMainActionClick("Wait");
            }
        }

        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            // This hotkey directly ends the turn, bypassing the "Wait" sub-menu.
            DebugHelper.Log($"{_selectedUnit.unitName} explicitly ends turn via hotkey.", _inputHandler);
            _inputHandler.actionMenuUI?.HideSubMenu(); // Close any open sub-menu
            // _inputHandler.actionMenuUI?.HideActionBar(); // PIH.ChangeState or Update will hide it
            if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }

        public override void UpdateState()
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive ||
                (_inputHandler.CombatActive && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != _selectedUnit) )
            { 
                _inputHandler.ChangeState(new PIH_WaitingForTurnState()); 
                return; 
            }
            
            // The action bar is refreshed in PlayerInputHandler.Update() when in this state.
            // This state's Update primarily checks for conditions to exit.
            _inputHandler.CheckAndHandleEndOfTurnActionsPIH(); 
        }

        public override void ExitState()
        {
            // Action bar visibility is handled by PlayerInputHandler.ChangeState()
            base.ExitState();
        }
    }
}