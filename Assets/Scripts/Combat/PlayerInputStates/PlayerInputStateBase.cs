// PlayerInputStateBase.cs
using UnityEngine;
using UnityEngine.InputSystem; // Required for InputAction.CallbackContext

namespace MythTactics.Combat 
{
    public abstract class PlayerInputStateBase
    {
        protected PlayerInputHandler _inputHandler; 

        // RESTORED: Convenient properties for derived classes
        protected Unit _selectedUnit => _inputHandler.SelectedUnit; 
        protected Pathfinder _pathfinder => _inputHandler.Pathfinder; 
        protected PlayerControls _playerControls => _inputHandler.PlayerControls; 
        protected Camera _mainCamera => _inputHandler.MainCamera;
        // You can add more for other frequently accessed PlayerInputHandler members if needed

        public virtual void EnterState(PlayerInputHandler inputHandler)
        {
            this._inputHandler = inputHandler;
        }

        public abstract void OnClickInput(InputAction.CallbackContext context, Tile clickedTile);
        public abstract void OnToggleAttackModeInput(InputAction.CallbackContext context);
        public virtual void OnWaitInput(InputAction.CallbackContext context) { } 
        public abstract void OnEndTurnInput(InputAction.CallbackContext context);
        public virtual void OnSelectAbilityInput(InputAction.CallbackContext context) { }
        public virtual void OnToggleActionMenuInput(InputAction.CallbackContext context) { }

        public virtual void UpdateState() { }
        
        public virtual void ExitState() { }
    }
}