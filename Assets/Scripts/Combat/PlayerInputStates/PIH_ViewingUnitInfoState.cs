using UnityEngine;
using UnityEngine.InputSystem; // Required for InputAction.CallbackContext

namespace MythTactics.Combat
{
    public class PIH_ViewingUnitInfoState : PlayerInputStateBase
    {
        // PlayerInputHandler reference is inherited as 'protected PlayerInputHandler _inputHandler;'
        // from PlayerInputStateBase after the next correction to PlayerInputStateBase.
        // For now, let's assume _inputHandler is correctly set by the base EnterState.

        public override void EnterState(PlayerInputHandler playerInputHandler)
        {
            base.EnterState(playerInputHandler); // This sets the inherited _inputHandler

            _inputHandler.PlayerControls.Gameplay.ToggleActionMenu.Enable();
            _inputHandler.PlayerControls.Gameplay.Click.Enable(); // Or other relevant actions

            if (_inputHandler.UnitInfoPanelInstance != null && _inputHandler.SelectedUnit != null)
            {
                _inputHandler.actionMenuUI?.HideMenu(); // CORRECTED: Lowercase 'a' for actionMenuUI
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
            base.ExitState(); // Call base ExitState
            DebugHelper.Log("PIH_ViewingUnitInfoState: Exited.", _inputHandler);
        }

        public override void UpdateState()
        {
            // No continuous updates needed
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            // Does nothing for now, panel close is handled by its button or ToggleActionMenuInput
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context) { /* Do nothing */ }
        public override void OnWaitInput(InputAction.CallbackContext context) { /* Do nothing */ }
        public override void OnEndTurnInput(InputAction.CallbackContext context) { /* Do nothing */ }
        public override void OnSelectAbilityInput(InputAction.CallbackContext context) { /* Do nothing */ }

        public override void OnToggleActionMenuInput(InputAction.CallbackContext context) // CORRECTED: Added 'override'
        {
            if (_inputHandler != null)
            {
                DebugHelper.Log("PIH_ViewingUnitInfoState: ToggleActionMenuInput. Closing panel.", _inputHandler);
                _inputHandler.ChangeState(new PIH_UnitActionPhaseState());
            }
        }
    }
}