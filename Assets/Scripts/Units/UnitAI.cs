// UnitAI.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Unit))]
public class UnitAI : MonoBehaviour
{
    private Unit _unitMain;
    [Tooltip("Assign the BasicAIHandler prefab or a scriptable object defining AI behavior if you change approach.")]
    [SerializeField] private BasicAIHandler aiHandlerLogic; // The actual AI logic implementation

    // MODIFIED: Added field to store the behavior profile
    private AIBehaviorProfile _currentBehaviorProfile = AIBehaviorProfile.None; // Default

    public void Initialize(Unit mainUnit)
    {
        _unitMain = mainUnit;
        if (_unitMain == null)
        {
            Debug.LogError("UnitAI.Initialize: Main Unit reference is null!", this);
            enabled = false; return;
        }

        if (aiHandlerLogic == null)
        {
            aiHandlerLogic = GetComponent<BasicAIHandler>();
            if (aiHandlerLogic == null)
            {
                Debug.LogWarning($"UnitAI on {_unitMain.unitName} does not have AI Handler Logic assigned or found. AI may be passive depending on profile: {_currentBehaviorProfile}.", this);
            }
        }

        // If this unit is a player unit, the AI component should typically be disabled by the factory or spawner.
        // However, if it's active, we log it.
        if (_unitMain.CurrentFaction == FactionType.Player)
        {
            Debug.LogWarning($"UnitAI initialized on a Player faction unit: {_unitMain.unitName}. This component should likely be disabled for players.", this);
            // this.enabled = false; // Consider disabling it here if it wasn't already by the factory
        }
    }

    // MODIFIED: New method to set the behavior profile, called by UnitFactory or Unit.PrimeDataFromTemplate
    public void SetBehaviorProfile(AIBehaviorProfile profile)
    {
        _currentBehaviorProfile = profile;
        // Debug.Log($"UnitAI on {(_unitMain != null ? _unitMain.unitName : this.gameObject.name)} profile set to: {profile}", this);

        // Future: Based on the profile, you might:
        // - Load different parameters for BasicAIHandler
        // - Swap out the aiHandlerLogic with a different implementation
        // - Enable/disable certain sub-behaviors within BasicAIHandler
        if (aiHandlerLogic != null)
        {
            // Example: aiHandlerLogic.UpdateBehaviorParameters(_currentBehaviorProfile);
        }
    }

    public IEnumerator ProcessTurn()
    {
        if (_unitMain == null || !_unitMain.IsAlive)
        {
            DebugHelper.LogWarning($"UnitAI.ProcessTurn: Unit '{(_unitMain != null ? _unitMain.unitName : "Unknown")}' is null or not alive.", _unitMain);
            HandleTurnEndSafety(); // MODIFIED: Changed to Safety call
            yield break;
        }

        // This check should ideally be handled by TurnManager ensuring AI only processes turns for non-player AI factions
        if (_unitMain.CurrentFaction == FactionType.Player)
        {
            DebugHelper.LogWarning($"UnitAI.ProcessTurn: Called on a Player unit {_unitMain.unitName}. AI should not control players.", _unitMain);
            HandleTurnEndSafety(); // MODIFIED: Changed to Safety call
            yield break;
        }

        if (aiHandlerLogic == null)
        {
            DebugHelper.Log($"{_unitMain.unitName} (UnitAI - Profile: {_currentBehaviorProfile}): No AI handler logic. Waiting out turn.", _unitMain);
            // AI performs a simple wait if no logic is present but it has AP
            if (_unitMain.CanAffordAPForAction(PlayerInputHandler.WaitActionCost)) // Assuming WaitActionCost is accessible or define a const
            {
                CombatLogger.LogEvent($"{_unitMain.unitName} (AI) waits.", Color.gray, LogMessageType.TurnFlow);
                _unitMain.SpendAPForAction(PlayerInputHandler.WaitActionCost);
            }
            HandleTurnEndSafety(); // MODIFIED: Changed to Safety call
            yield break;
        }

        // Execute the assigned AI logic
        // Pass the profile to the handler if it needs to adapt its behavior
        yield return StartCoroutine(aiHandlerLogic.ExecuteTurn(_unitMain, _currentBehaviorProfile)); // MODIFIED: Pass profile

        // The aiHandlerLogic.ExecuteTurn should call HandleTurnEndSafety itself.
        // This is an additional failsafe.
        HandleTurnEndSafety();
    }

    // Renamed for clarity and to emphasize it's the primary way AI should signal turn end.
    public void HandleTurnEndSafety()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == _unitMain)
        {
            // This ensures the turn manager correctly processes the end of the AI's turn.
            // DebugHelper.Log($"UnitAI.HandleTurnEndSafety: Requesting EndUnitTurn for {_unitMain.unitName}.", _unitMain);
            TurnManager.Instance.EndUnitTurn(_unitMain);
        }
        // else if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit != _unitMain)
        // {
            // DebugHelper.LogWarning($"UnitAI.HandleTurnEndSafety: Called for {_unitMain.unitName}, but active unit is {TurnManager.Instance.ActiveUnit?.unitName}. Turn already ended or different active unit.", _unitMain);
        // }
    }
}