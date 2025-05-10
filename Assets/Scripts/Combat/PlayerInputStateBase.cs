// PlayerInputStateBase.cs
using UnityEngine;
using UnityEngine.InputSystem; // Required for InputAction.CallbackContext

namespace MythTactics.Combat 
{
    public abstract class PlayerInputStateBase
    {
        protected PlayerInputHandler _inputHandler; 
        protected Unit _selectedUnit => _inputHandler.SelectedUnit; 
        protected Pathfinder _pathfinder => _inputHandler.Pathfinder; 
        protected PlayerControls _playerControls => _inputHandler.PlayerControls; 
        protected Camera _mainCamera => _inputHandler.MainCamera; 


        public virtual void EnterState(PlayerInputHandler inputHandler)
        {
            this._inputHandler = inputHandler;
        }

        public abstract void OnClickInput(InputAction.CallbackContext context, Tile clickedTile);
        public abstract void OnToggleAttackModeInput(InputAction.CallbackContext context);
        public abstract void OnWaitInput(InputAction.CallbackContext context);
        public abstract void OnEndTurnInput(InputAction.CallbackContext context);

        // NEW: Called for select ability input
        public virtual void OnSelectAbilityInput(InputAction.CallbackContext context) { }

        public virtual void UpdateState() { }
        
        public virtual void ExitState()
        {
        }
    }
}