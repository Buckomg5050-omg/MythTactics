// PIH_WaitingForTurnState.cs
using UnityEngine.InputSystem;

namespace MythTactics.Combat
{
    public class PIH_WaitingForTurnState : PlayerInputStateBase
    {
        public override void EnterState(PlayerInputHandler inputHandler)
        {
            base.EnterState(inputHandler); // Calls the base EnterState
            // Ensure all highlights are cleared when entering this state,
            // as it implies no active player unit selection.
            _inputHandler.ClearAllHighlights();
        }

        public override void OnClickInput(InputAction.CallbackContext context, Tile clickedTile)
        {
            // No action on click when waiting for turn
            // DebugHelper.Log("PIH_WaitingForTurnState: Click ignored.", _inputHandler);
        }

        public override void OnToggleAttackModeInput(InputAction.CallbackContext context)
        {
            // No action on attack toggle when waiting for turn
            // DebugHelper.Log("PIH_WaitingForTurnState: Attack Toggle ignored.", _inputHandler);
        }

        public override void OnWaitInput(InputAction.CallbackContext context)
        {
            // No action on wait when waiting for turn
            // DebugHelper.Log("PIH_WaitingForTurnState: Wait ignored.", _inputHandler);
        }
        
        public override void OnEndTurnInput(InputAction.CallbackContext context)
        {
            // No action on end turn when waiting for turn (no active player unit to end turn for)
            // DebugHelper.Log("PIH_WaitingForTurnState: End Turn ignored.", _inputHandler);
        }

        // UpdateState and ExitState can be left to the base virtual methods if no specific logic is needed.
        // public override void UpdateState() { }
        // public override void ExitState() { base.ExitState(); }
    }
}