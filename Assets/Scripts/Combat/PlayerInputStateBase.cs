// PlayerInputStateBase.cs
using UnityEngine;
using UnityEngine.InputSystem; // Required for InputAction.CallbackContext

namespace MythTactics.Combat // Using a namespace can be good for organization
{
    public abstract class PlayerInputStateBase
    {
        protected PlayerInputHandler _inputHandler; // Reference to the context (PlayerInputHandler)
        protected Unit _selectedUnit => _inputHandler.SelectedUnit; // Convenience accessor
        protected Pathfinder _pathfinder => _inputHandler.Pathfinder; // Convenience accessor
        protected PlayerControls _playerControls => _inputHandler.PlayerControls; // Convenience accessor
        protected Camera _mainCamera => _inputHandler.MainCamera; // Convenience accessor


        // Called when entering this state
        public virtual void EnterState(PlayerInputHandler inputHandler)
        {
            this._inputHandler = inputHandler;
            // DebugHelper.Log($"{this.GetType().Name} entered.", _inputHandler);
        }

        // Called for click input
        public abstract void OnClickInput(InputAction.CallbackContext context, Tile clickedTile);

        // Called for attack mode toggle input
        public abstract void OnToggleAttackModeInput(InputAction.CallbackContext context);

        // Called for wait input
        public abstract void OnWaitInput(InputAction.CallbackContext context);

        // Called for end turn input
        public abstract void OnEndTurnInput(InputAction.CallbackContext context);

        // Called every frame by PlayerInputHandler's Update
        public virtual void UpdateState() { }

        // Called when exiting this state
        public virtual void ExitState()
        {
            // DebugHelper.Log($"{this.GetType().Name} exited.", _inputHandler);
        }
    }
}