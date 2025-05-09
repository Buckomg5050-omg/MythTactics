// PIH_SelectingAbilityTargetState.cs
using UnityEngine.InputSystem;
using System.Linq; 
using UnityEngine; 

namespace MythTactics.Combat 
{
    public class PIH_SelectingAbilityTargetState : PlayerInputStateBase
    {
        private AbilitySO _currentAbility;

        public override void EnterState(PlayerInputHandler inputHandler)
        {
            base.EnterState(inputHandler);
            _currentAbility = _inputHandler.SelectedAbility; 
            
            // MODIFIED: Call CanAffordAbility via _selectedUnit.Combat
            if (_selectedUnit == null || !_selectedUnit.IsAlive || _currentAbility == null || 
                _selectedUnit.Combat == null || !_selectedUnit.Combat.CanAffordAbility(_currentAbility, true))
            {
                DebugHelper.LogWarning("PIH_SelectingAbilityTargetState: Entered with invalid unit, no ability, missing Combat component, or cannot afford. Reverting.", _inputHandler);
                _inputHandler.SelectedAbility = null; 
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }
            DebugHelper.Log($"PIH_SelectingAbilityTargetState: Targeting for ability '{_currentAbility.abilityName}' by {_selectedUnit.unitName}.", _inputHandler);
            _inputHandler.ClearAllHighlights(); 
            _inputHandler.ShowAbilityRange(_selectedUnit, _currentAbility); // ShowAbilityRange already checks CanAffordAbility via Combat
            if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || clickedTile == null || _currentAbility == null || _selectedUnit.Combat == null) 
            { 
                DebugHelper.Log("PIH_SelectingAbilityTargetState: Click ignored due to null unit, tile, ability, or missing Combat component.", _inputHandler); 
                return; 
            }
            
            // MODIFIED: Call CanAffordAbility via _selectedUnit.Combat
            if (!_selectedUnit.Combat.CanAffordAbility(_currentAbility, true))
            {
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford ABILITY '{_currentAbility.abilityName}' upon click. Reverting.", _inputHandler);
                _inputHandler.SelectedAbility = null;
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                return;
            }

            if (_inputHandler.HighlightedAbilityRangeTiles.Contains(clickedTile))
            {
                Unit targetOnTile = clickedTile.occupyingUnit;
                Unit finalTargetUnit = null; 

                switch (_currentAbility.targetType)
                {
                    case AbilityTargetType.EnemyUnit:
                        if (targetOnTile == null) { DebugHelper.Log($"PIH: {_currentAbility.abilityName} needs enemy, tile empty.", _inputHandler); return; }
                        else if (!targetOnTile.IsAlive) { DebugHelper.Log($"PIH: {_currentAbility.abilityName} cannot target dead unit.", _inputHandler); return; }
                        else if (targetOnTile == _selectedUnit) { DebugHelper.Log($"PIH: {_currentAbility.abilityName} cannot target self (enemy only).", _inputHandler); return; }
                        // TODO: Faction check
                        else { finalTargetUnit = targetOnTile; }
                        break;
                    case AbilityTargetType.AllyUnit:
                        if (targetOnTile == null) { DebugHelper.Log($"PIH: {_currentAbility.abilityName} needs ally, tile empty.", _inputHandler); return; }
                        else if (!targetOnTile.IsAlive) { DebugHelper.Log($"PIH: {_currentAbility.abilityName} cannot target dead unit.", _inputHandler); return; }
                        // TODO: Faction check
                        else { finalTargetUnit = targetOnTile; }
                        break;
                    case AbilityTargetType.Self:
                        finalTargetUnit = _selectedUnit; 
                        if (clickedTile != _selectedUnit.CurrentTile) { DebugHelper.Log($"PIH: Self-cast {_currentAbility.abilityName} clicked elsewhere; targeting self.", _inputHandler); }
                        break;
                    case AbilityTargetType.Tile: 
                        DebugHelper.Log($"PIH: Tile target for {_currentAbility.abilityName} not fully implemented.", _inputHandler); return; 
                    default:
                        DebugHelper.LogError($"PIH: Unknown AbilityTargetType: {_currentAbility.targetType} for '{_currentAbility.abilityName}'.", _inputHandler); return;
                }

                if (finalTargetUnit != null || _currentAbility.targetType == AbilityTargetType.Self)
                {
                    DebugHelper.Log($"PIH: Executing ability '{_currentAbility.abilityName}' on {finalTargetUnit?.unitName ?? "TILE (not implemented)"}.", _inputHandler);
                    // MODIFIED: Call PerformAbility via _selectedUnit.Combat
                    _inputHandler.StartCoroutine(_selectedUnit.Combat.PerformAbility(_currentAbility, finalTargetUnit, _inputHandler));
                    
                    _inputHandler.SelectedAbility = null; 
                    // PerformAbility in UnitCombat should handle calling CheckAndHandleEndOfTurnActionsPIH or PIH changes state.
                    // For now, let's assume the ability coroutine leads to the next state.
                    // If ability execution is quick and doesn't change state, we might go to UnitActionPhase or WaitingForTurn.
                    // This depends on whether the ability ends the turn.
                    // _inputHandler.ChangeState(new PIH_UnitActionPhaseState()); // Or PIH_WaitingForTurnState
                }
            }
            else 
            {
                DebugHelper.Log("PIH_SelectingAbilityTargetState: Clicked outside ability range. Cancelling.", _inputHandler);
                _inputHandler.SelectedAbility = null;
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
            }
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            DebugHelper.Log("PIH_SelectingAbilityTargetState: Attack toggle pressed. Cancelling ability.", _inputHandler);
            _inputHandler.SelectedAbility = null;
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }
        
        public override void OnSelectAbilityInput(InputAction.CallbackContext context)
        {
            DebugHelper.Log("PIH_SelectingAbilityTargetState: Ability (Q) pressed again. Cancelling.", _inputHandler);
            _inputHandler.SelectedAbility = null; 
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState()); 
        }
        
        public override void OnWaitInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.WaitActionCost)) // CanAffordAPForAction is on Unit
            {
                _selectedUnit.SpendAPForAction(PlayerInputHandler.WaitActionCost); // SpendAPForAction is on Unit
                _inputHandler.SelectedAbility = null;
                _inputHandler.ClearAllHighlights();
                _inputHandler.CheckAndHandleEndOfTurnActionsPIH(); 
            }
            else { DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford WAIT. AP: {_selectedUnit.currentActionPoints}", _inputHandler); }
        }
        
        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            _inputHandler.SelectedAbility = null;
            _inputHandler.ClearAllHighlights();
            if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
            // ChangeState to WaitingForTurn is handled by PIH main Update loop
        }

        public override void UpdateState()
        {
            // MODIFIED: Call CanAffordAbility via _selectedUnit.Combat
            if (_selectedUnit == null || !_selectedUnit.IsAlive || _currentAbility == null ||
                _selectedUnit.Combat == null || !_selectedUnit.Combat.CanAffordAbility(_currentAbility, false) || // Use false to prevent spamming affordability logs
                (_inputHandler.CombatActive && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != _selectedUnit))
            {
                if (_inputHandler != null) 
                {
                    DebugHelper.LogWarning("PIH_SelectingAbilityTargetState.UpdateState: Conditions no longer valid. Reverting.", _inputHandler);
                    _inputHandler.SelectedAbility = null;
                    _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                }
                return;
            }
            // Re-show range in case of dynamic changes or re-entering state (though EnterState handles initial show)
            if (_inputHandler != null && _selectedUnit != null && _currentAbility != null)
            {
                _inputHandler.ShowAbilityRange(_selectedUnit, _currentAbility);
                if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            }
        }

        public override void ExitState()
        {
            if (_inputHandler != null) _inputHandler.ClearAbilityRangeHighlight(true);
            // _inputHandler.SelectedAbility = null; // Often good to clear here, but PIH Update might also handle it
            base.ExitState();
        }
    }
}