// PIH_ViewingUnitInfoState.cs
using UnityEngine;
using UnityEngine.InputSystem; 

namespace MythTactics.Combat
{
    public class PIH_ViewingUnitInfoState : PlayerInputStateBase
    {
        public override void EnterState(PlayerInputHandler playerInputHandler)
        {
            base.EnterState(playerInputHandler); 

            _inputHandler.PlayerControls.Gameplay.ToggleActionMenu.Enable();
            _inputHandler.PlayerControls.Gameplay.Click.Enable(); 

            if (_inputHandler.UnitInfoPanelInstance != null && _inputHandler.SelectedUnit != null)
            {
                // MODIFIED: Hide the persistent action bar when viewing unit info
                _inputHandler.actionMenuUI?.HideActionBar(); 
                _inputHandler.UnitInfoPanelInstance.ShowPanel(_inputHandler.SelectedUnit);
                DebugHelper.Log("PIH_ViewingUnitInfoState: Entered. Unit Info Panel shown.", _inputHandler);
            }
            else
            {
                DebugHelper.LogWarning("PIH_ViewingUnitInfoState: UnitInfoPanelInstance or SelectedUnit is null. Transitioning back to UnitActionPhaseState.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
            }
        }

        public override void ExitState()
        {
            if (_inputHandler != null && _inputHandler.UnitInfoPanelInstance != null)
            {
                _inputHandler.UnitInfoPanelInstance.HidePanel();
            }
            // MODIFIED: Action bar visibility will be handled by PIH.ChangeState based on the new state
            // No need to explicitly show/hide actionMenuUI here as PIH.ChangeState() will manage it.
            base.ExitState(); 
            DebugHelper.Log("PIH_ViewingUnitInfoState: Exited.", _inputHandler);
        }

        public override void UpdateState() { }
        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile) { }
        public override void OnToggleAttackModeInput(InputAction.CallbackContext context) { }
        public override void OnWaitInput(InputAction.CallbackContext context) { }
        public override void OnEndTurnInput(InputAction.CallbackContext context) { }
        public override void OnSelectAbilityInput(InputAction.CallbackContext context) { }

        public override void OnToggleActionMenuInput(InputAction.CallbackContext context) 
        {
            if (_inputHandler != null)
            {
                DebugHelper.Log("PIH_ViewingUnitInfoState: ToggleActionMenuInput. Closing panel.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
            }
        }
    }
}