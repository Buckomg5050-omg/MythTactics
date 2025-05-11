// PIH_SelectingAbilityTargetState.cs
using UnityEngine.InputSystem;
using System.Linq;
using UnityEngine;

namespace MythTactics.Combat
{
    public class PIH_SelectingAbilityTargetState : PlayerInputStateBase
    {
        private AbilitySO _currentAbility;
        private bool _actionIsResolving = false; // Flag to prevent input during ability execution

        public override void EnterState(PlayerInputHandler inputHandler)
        {
            base.EnterState(inputHandler);
            _actionIsResolving = false; // Reset flag
            _currentAbility = _inputHandler.SelectedAbility;

            if (_selectedUnit == null || !_selectedUnit.IsAlive || _currentAbility == null ||
                _selectedUnit.Combat == null || !_selectedUnit.Combat.CanAffordAbility(_currentAbility, true)) // Log if cannot afford
            {
                DebugHelper.LogWarning("PIH_SelectingAbilityTargetState: Entered with invalid unit, no ability, missing Combat, or cannot afford. Reverting.", _inputHandler);
                _inputHandler.SelectedAbility = null;
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }
            DebugHelper.Log($"PIH_SelectingAbilityTargetState: Targeting for ability '{_currentAbility.abilityName}' by {_selectedUnit.unitName}.", _inputHandler);
            _inputHandler.ClearAllHighlights();
            _inputHandler.ShowAbilityRange(_selectedUnit, _currentAbility);
            if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            if (_actionIsResolving)
            {
                DebugHelper.Log("PIH_SelectingAbilityTargetState: Click ignored, ability already resolving.", _inputHandler);
                return;
            }

            if (_selectedUnit == null || !_selectedUnit.IsAlive || clickedTile == null || _currentAbility == null || _selectedUnit.Combat == null)
            {
                DebugHelper.Log("PIH_SelectingAbilityTargetState: Click ignored due to null unit, tile, ability, or missing Combat component.", _inputHandler);
                return;
            }

            // Check affordability again right before execution
            if (!_selectedUnit.Combat.CanAffordAbility(_currentAbility, true))
            {
                // CanAffordAbility logs details.
                _inputHandler.SelectedAbility = null;
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }

            if (_inputHandler.HighlightedAbilityRangeTiles.Contains(clickedTile))
            {
                Unit targetOnTile = clickedTile.occupyingUnit;
                Unit finalTargetUnit = null;
                bool isValidTargetType = true;

                switch (_currentAbility.targetType)
                {
                    case AbilityTargetType.EnemyUnit:
                        if (targetOnTile == null) { DebugHelper.Log($"PIH: {_currentAbility.abilityName} needs enemy, tile empty.", _inputHandler); isValidTargetType = false; }
                        else if (!targetOnTile.IsAlive) { DebugHelper.Log($"PIH: {_currentAbility.abilityName} cannot target dead unit.", _inputHandler); isValidTargetType = false; }
                        else if (targetOnTile == _selectedUnit) { DebugHelper.Log($"PIH: {_currentAbility.abilityName} cannot target self (enemy only).", _inputHandler); isValidTargetType = false; }
                        // TODO: Add faction check if distinguishing between enemy/neutral/ally is important
                        else { finalTargetUnit = targetOnTile; }
                        break;
                    case AbilityTargetType.AllyUnit:
                        if (targetOnTile == null) { DebugHelper.Log($"PIH: {_currentAbility.abilityName} needs ally, tile empty.", _inputHandler); isValidTargetType = false; }
                        else if (!targetOnTile.IsAlive) { DebugHelper.Log($"PIH: {_currentAbility.abilityName} cannot target dead unit.", _inputHandler); isValidTargetType = false; }
                        // TODO: Add faction check
                        else { finalTargetUnit = targetOnTile; }
                        break;
                    case AbilityTargetType.Self:
                        finalTargetUnit = _selectedUnit;
                        if (clickedTile != _selectedUnit.CurrentTile) { DebugHelper.Log($"PIH: Self-cast {_currentAbility.abilityName} clicked elsewhere; targeting self.", _inputHandler); }
                        break;
                    case AbilityTargetType.Tile:
                        DebugHelper.Log($"PIH: Tile target for {_currentAbility.abilityName} selected. Needs specific handling if it's not just about applying to a unit on the tile.", _inputHandler);
                        // For now, if a tile target ability can affect units, use targetOnTile
                        // If it's purely a tile effect (e.g. create obstacle), finalTargetUnit might remain null
                        finalTargetUnit = targetOnTile; // Example: assume it targets unit on tile if present
                        break;
                    default:
                        DebugHelper.LogError($"PIH: Unknown AbilityTargetType: {_currentAbility.targetType} for '{_currentAbility.abilityName}'.", _inputHandler);
                        isValidTargetType = false;
                        break;
                }

                if (!isValidTargetType) return; // Exit if target type validation failed

                // Proceed if target is valid for the ability type or if it's a self-cast
                if (finalTargetUnit != null || _currentAbility.targetType == AbilityTargetType.Self || _currentAbility.targetType == AbilityTargetType.Tile)
                {
                    _actionIsResolving = true;
                    DebugHelper.Log($"PIH: Executing ability '{_currentAbility.abilityName}' on {finalTargetUnit?.unitName ?? "TILE"}.", _inputHandler);
                    
                    // CORRECTED: Pass a callback instead of _inputHandler
                    _inputHandler.StartCoroutine(_selectedUnit.Combat.PerformAbility(_currentAbility, finalTargetUnit, 
                        () => { // This is the onAbilityComplete callback
                            DebugHelper.Log($"PIH_SelectingAbilityTargetState: PerformAbility callback for '{_currentAbility?.abilityName}'.", _inputHandler);
                            _actionIsResolving = false;
                            _inputHandler.SelectedAbility = null; // Clear selected ability after use
                            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                        }
                    ));
                }
                // No explicit else here, as isValidTargetType check above should catch issues.
            }
            else // Clicked outside highlighted ability range
            {
                DebugHelper.Log("PIH_SelectingAbilityTargetState: Clicked outside ability range. Cancelling.", _inputHandler);
                _inputHandler.SelectedAbility = null;
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
            }
        }
        
        // Generic cancel / "back" functionality
        private void CancelAbilityTargeting()
        {
            if (_actionIsResolving) return;
            DebugHelper.Log("PIH_SelectingAbilityTargetState: Cancelling ability targeting. Reverting to Action Phase.", _inputHandler);
            _inputHandler.SelectedAbility = null;
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context) // e.g. 'T' key
        {
            CancelAbilityTargeting();
        }

        public override void OnSelectAbilityInput(InputAction.CallbackContext context) // e.g. 'Q' key
        {
            // If pressing the same ability key or a general "select ability" key again, cancel current targeting.
            CancelAbilityTargeting();
        }

        // If you add a dedicated "Cancel" input action (e.g. Escape, Right Mouse Button)
        // public override void OnCancelInput(InputAction.CallbackContext context)
        // {
        //     CancelAbilityTargeting();
        // }


        public override void OnWaitInput(InputAction.CallbackContext context)
        {
            if (_actionIsResolving) return;
            DebugHelper.Log("PIH_SelectingAbilityTargetState: 'Wait' input received. Invalid in this state. Cancelling ability targeting.", _inputHandler);
            CancelAbilityTargeting();
        }

        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
            if (_actionIsResolving) return;
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            DebugHelper.Log("PIH_SelectingAbilityTargetState: 'End Turn' input received. Ending turn.", _inputHandler);
            _inputHandler.SelectedAbility = null;
            _inputHandler.ClearAllHighlights();
            if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }

        public override void UpdateState()
        {
            if (_actionIsResolving) return;

            if (_selectedUnit == null || !_selectedUnit.IsAlive || _currentAbility == null || _selectedUnit.Combat == null ||
                (_inputHandler.CombatActive && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != _selectedUnit))
            {
                if (_inputHandler != null)
                {
                    DebugHelper.LogWarning("PIH_SelectingAbilityTargetState.UpdateState: Unit/Ability/Turn invalid. Reverting.", _inputHandler);
                    _inputHandler.SelectedAbility = null;
                    _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                }
                return;
            }

            // If AP/resources change externally and unit can no longer afford the selected ability
            if (!_selectedUnit.Combat.CanAffordAbility(_currentAbility, false)) // Don't log verbosely every frame
            {
                DebugHelper.LogWarning($"PIH_SelectingAbilityTargetState.UpdateState: Unit {_selectedUnit.unitName} can no longer afford '{_currentAbility.abilityName}'. Reverting.", _inputHandler);
                _inputHandler.SelectedAbility = null;
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }

            // Keep ability range visible
            if (_inputHandler != null && _selectedUnit != null && _currentAbility != null)
            {
                _inputHandler.ShowAbilityRange(_selectedUnit, _currentAbility);
                if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            }
        }

        public override void ExitState()
        {
            if (_inputHandler != null)
            {
                _inputHandler.ClearAbilityRangeHighlight(true);
                // _inputHandler.SelectedAbility = null; // Clear here or let PIH handle it based on next state
            }
            _actionIsResolving = false;
            base.ExitState();
        }
    }
}