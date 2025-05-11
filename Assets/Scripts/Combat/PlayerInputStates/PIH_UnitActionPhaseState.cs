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
            
            // Logic to ensure selected unit tile is highlighted correctly,
            // considering the action menu might have just been shown by PlayerInputHandler.ChangeState
            if (_selectedUnit.CurrentTile != null && TurnManager.Instance.ActiveUnit == _selectedUnit)
            {
                // If action menu is supposed to be visible (because PIH.ChangeState decided so),
                // ensure unit is highlighted. If menu is not visible, still highlight if it's their turn.
                _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            }
            DebugHelper.Log($"PIH_UnitActionPhaseState: Entered for {_selectedUnit.unitName}. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler);
            
            // ***** NEW LOG AT END OF ENTERSTATE *****
            if (_inputHandler.actionMenuUI != null)
            {
                DebugHelper.Log($"PIH_UnitActionPhaseState.EnterState END: ActionMenu IsVisible? {_inputHandler.actionMenuUI.IsVisible()}", _inputHandler);
            }
            // *****************************************
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || clickedTile == null ) { return; }

            if (clickedTile == _selectedUnit.CurrentTile)
            {
                if (_inputHandler.actionMenuUI != null && !_inputHandler.actionMenuUI.IsVisible() &&
                    TurnManager.Instance.ActiveUnit == _selectedUnit) 
                {
                    DebugHelper.Log("PIH_UnitActionPhaseState: Clicked selected unit's tile, action menu was hidden. Showing menu.", _inputHandler);
                    _inputHandler.ShowActionMenuForSelectedUnitPublic();
                }
                else { DebugHelper.Log("PIH_UnitActionPhaseState: Clicked selected unit's tile. Menu already visible or not unit's turn. No specific action.", _inputHandler); }
                return; 
            }

            if (_inputHandler.actionMenuUI != null && _inputHandler.actionMenuUI.IsVisible() && 
                (UnityEngine.EventSystems.EventSystem.current != null && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
            {
                 DebugHelper.Log("PIH_UnitActionPhaseState: Clicked on map while action menu was visible. Hiding menu.", _inputHandler);
                 _inputHandler.actionMenuUI.HideMenu();
                 if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
                 return; 
            }
            DebugHelper.Log($"PIH_UnitActionPhaseState: Clicked tile {clickedTile.gridPosition}. Menu is hidden or click on UI. No direct action from this state for this tile.", _inputHandler);
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
            { _inputHandler.HandleActionFromHotKey("Attack"); }
            else { DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford ATTACK. Has {_selectedUnit.CurrentActionPoints} AP.", _inputHandler); }
        }

        public override void OnSelectAbilityInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || _selectedUnit.Combat == null) return;
            if (_selectedUnit.knownAbilities == null || _selectedUnit.knownAbilities.Count == 0) 
            { DebugHelper.Log($"{_selectedUnit.unitName} has no abilities to select with hotkey.", _inputHandler); return; }
             _inputHandler.HandleActionFromHotKey("Skills"); 
        }

        public override void OnWaitInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.WaitActionCost))
            { _inputHandler.HandleActionFromHotKey("Wait"); }
            else { DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford WAIT. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler); }
        }

        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            DebugHelper.Log($"{_selectedUnit.unitName} explicitly ends turn via hotkey. AP: {_selectedUnit.CurrentActionPoints}", _inputHandler);
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