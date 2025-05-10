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
            
            if (_selectedUnit == null || !_selectedUnit.IsAlive || _currentAbility == null || !_selectedUnit.CanAffordAbility(_currentAbility, true))
            {
                DebugHelper.LogWarning("PIH_SelectingAbilityTargetState: Entered with invalid unit, no ability, or cannot afford (reason logged by CanAffordAbility). Reverting.", _inputHandler);
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
            if (_selectedUnit == null || !_selectedUnit.IsAlive || clickedTile == null || _currentAbility == null) 
            { 
                DebugHelper.Log("PIH_SelectingAbilityTargetState: Click ignored due to null unit, tile, or ability.", _inputHandler); 
                return; 
            }
            
            if (!_selectedUnit.CanAffordAbility(_currentAbility, true))
            {
                DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford ABILITY '{_currentAbility.abilityName}' upon click (reason logged by CanAffordAbility). Reverting.", _inputHandler);
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
                        if (targetOnTile == null)
                        {
                            DebugHelper.Log($"PIH_SelectingAbilityTargetState: {_currentAbility.abilityName} requires an enemy target, but tile {clickedTile.gridPosition} is empty.", _inputHandler);
                            return; // Stay in targeting mode, allow re-selection
                        }
                        else if (!targetOnTile.IsAlive)
                        {
                            DebugHelper.Log($"PIH_SelectingAbilityTargetState: {_currentAbility.abilityName} cannot target defeated unit {targetOnTile.unitName} on tile {clickedTile.gridPosition}.", _inputHandler);
                            return;
                        }
                        else if (targetOnTile == _selectedUnit)
                        {
                            DebugHelper.Log($"PIH_SelectingAbilityTargetState: {_currentAbility.abilityName} cannot target self; requires an enemy.", _inputHandler);
                            return;
                        }
                        // TODO: Add actual faction check: else if (!IsEnemy(targetOnTile)) { ... log ... return; }
                        else // Assumed valid enemy if not self and alive for now
                        {
                            finalTargetUnit = targetOnTile;
                        }
                        break;

                    case AbilityTargetType.AllyUnit:
                        if (targetOnTile == null)
                        {
                            DebugHelper.Log($"PIH_SelectingAbilityTargetState: {_currentAbility.abilityName} requires an ally target, but tile {clickedTile.gridPosition} is empty.", _inputHandler);
                            return; 
                        }
                        else if (!targetOnTile.IsAlive)
                        {
                            DebugHelper.Log($"PIH_SelectingAbilityTargetState: {_currentAbility.abilityName} cannot target defeated unit {targetOnTile.unitName} on tile {clickedTile.gridPosition}.", _inputHandler);
                            return;
                        }
                        // TODO: Add actual faction check: else if (!IsAlly(targetOnTile)) { ... log ... return; }
                        else // Assumed valid ally if alive for now
                        {
                            finalTargetUnit = targetOnTile;
                        }
                        break;

                    case AbilityTargetType.Self:
                        finalTargetUnit = _selectedUnit; 
                        if (clickedTile != _selectedUnit.CurrentTile)
                        {
                            DebugHelper.Log($"PIH_SelectingAbilityTargetState: Self-cast ability '{_currentAbility.abilityName}' clicked on tile {clickedTile.gridPosition} instead of self. Using self as target.", _inputHandler);
                        }
                        break;

                    case AbilityTargetType.Tile: 
                        // For tile-targeted abilities, finalTargetUnit remains null. We'd pass clickedTile to PerformAbility.
                        // This requires PerformAbility to be overloaded or to take a generic target object.
                        // For now, we'll just log and not execute for this type.
                        DebugHelper.Log($"PIH_SelectingAbilityTargetState: {_currentAbility.abilityName} targets tile {clickedTile.gridPosition}. Execution for tile-target abilities not fully implemented.", _inputHandler);
                        return; 

                    default:
                        DebugHelper.LogError($"PIH_SelectingAbilityTargetState: Unknown AbilityTargetType: {_currentAbility.targetType} for '{_currentAbility.abilityName}'.", _inputHandler);
                        return;
                }

                // Proceed to execute if a valid finalTargetUnit is set (for unit-targeting abilities)
                // or if it's a self-cast (where finalTargetUnit is the caster)
                if (finalTargetUnit != null || _currentAbility.targetType == AbilityTargetType.Self) // Self is covered by finalTargetUnit now
                {
                    // For Self, finalTargetUnit is _selectedUnit.
                    // For EnemyUnit/AllyUnit, finalTargetUnit is the validated unit on tile.
                    DebugHelper.Log($"PIH_SelectingAbilityTargetState: Executing ability '{_currentAbility.abilityName}' on {(finalTargetUnit != null ? finalTargetUnit.unitName : "ERROR: No target for unit-targeting ability")}.", _inputHandler);
                    _inputHandler.StartCoroutine(_selectedUnit.PerformAbility(_currentAbility, finalTargetUnit, _inputHandler));
                    
                    _inputHandler.SelectedAbility = null; 
                    _inputHandler.ChangeState(new PIH_UnitActionPhaseState()); 
                }
                // No else needed here, as invalid target cases within the switch should 'return'
            }
            else 
            {
                DebugHelper.Log("PIH_SelectingAbilityTargetState: Clicked outside ability range. Cancelling ability targeting.", _inputHandler);
                _inputHandler.SelectedAbility = null;
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
            }
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            DebugHelper.Log("PIH_SelectingAbilityTargetState: ToggleAttackMode pressed. Cancelling ability targeting.", _inputHandler);
            _inputHandler.SelectedAbility = null;
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
        }
        
        public override void OnSelectAbilityInput(InputAction.CallbackContext context)
        {
            DebugHelper.Log("PIH_SelectingAbilityTargetState: SelectAbility (Q) pressed again. Cancelling ability targeting.", _inputHandler);
            _inputHandler.SelectedAbility = null; 
            _inputHandler.ChangeState(new PIH_UnitActionPhaseState()); 
        }
        
        public override void OnWaitInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            if (_selectedUnit.CanAffordAPForAction(PlayerInputHandler.WaitActionCost))
            {
                _selectedUnit.SpendAPForAction(PlayerInputHandler.WaitActionCost);
                _inputHandler.SelectedAbility = null;
                _inputHandler.ClearAllHighlights();
                _inputHandler.CheckAndHandleEndOfTurnActionsPIH(); 
            }
            else { DebugHelper.LogWarning($"{_selectedUnit.unitName} cannot afford WAIT action. AP: {_selectedUnit.currentActionPoints}", _inputHandler); }
        }
        
        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive) return;
            _inputHandler.SelectedAbility = null;
            _inputHandler.ClearAllHighlights();
            if (TurnManager.Instance != null) TurnManager.Instance.EndUnitTurn(_selectedUnit);
        }

        public override void UpdateState()
        {
            if (_selectedUnit == null || !_selectedUnit.IsAlive || _currentAbility == null ||
                !_selectedUnit.CanAffordAbility(_currentAbility, true) ||
                (_inputHandler.CombatActive && TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != _selectedUnit))
            {
                if (_inputHandler != null) 
                {
                    _inputHandler.SelectedAbility = null;
                    _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
                }
                return;
            }
            if (_inputHandler != null && _selectedUnit != null && _currentAbility != null)
            {
                _inputHandler.ShowAbilityRange(_selectedUnit, _currentAbility);
                if (_selectedUnit.CurrentTile != null) _selectedUnit.CurrentTile.SetHighlight(TileHighlightState.SelectedUnit);
            }
        }

        public override void ExitState()
        {
            if (_inputHandler != null) _inputHandler.ClearAbilityRangeHighlight(true);
            base.ExitState();
        }
    }
}