// UnitAI.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Unit))]
public class UnitAI : MonoBehaviour
{
    private Unit _unitMain;
    [Tooltip("Assign the BasicAIHandler prefab or a scriptable object defining AI behavior if you change approach.")]
    [SerializeField] private BasicAIHandler aiHandlerLogic; // The actual AI logic implementation

    public void Initialize(Unit mainUnit)
    {
        _unitMain = mainUnit;
        if (_unitMain == null)
        {
            Debug.LogError("UnitAI.Initialize: Main Unit reference is null!", this);
            enabled = false; return;
        }

        // If aiHandlerLogic is a MonoBehaviour on a child GameObject or needs instantiation:
        // aiHandlerLogic = GetComponentInChildren<BasicAIHandler>(); // Or some other way to get/assign it
        // If BasicAIHandler is not a MonoBehaviour but a plain C# class, instantiate it here.
        // For now, assuming it's assigned in the Inspector on the same GameObject as UnitAI, or is a prefab instance.

        if (aiHandlerLogic == null)
        {
            // Attempt to get it from the same GameObject if not assigned
            aiHandlerLogic = GetComponent<BasicAIHandler>();
            if (aiHandlerLogic == null)
            {
                Debug.LogWarning($"UnitAI on {_unitMain.unitName} does not have AI Handler Logic assigned or found. AI will be passive.", this);
                // Optionally, could add a default BasicAIHandler if one should always exist
                // aiHandlerLogic = gameObject.AddComponent<BasicAIHandler>();
            }
        }
    }

    public IEnumerator ProcessTurn()
    {
        if (_unitMain == null || !_unitMain.IsAlive)
        {
            DebugHelper.LogWarning($"UnitAI.ProcessTurn: Unit is null or not alive.", _unitMain);
            HandleTurnEnd();
            yield break;
        }

        if (_unitMain.CompareTag("Player")) // Should not happen if TurnManager calls this correctly
        {
            DebugHelper.LogWarning($"UnitAI.ProcessTurn: Called on a Player unit {_unitMain.unitName}. AI should not control players.", _unitMain);
            HandleTurnEnd();
            yield break;
        }

        if (aiHandlerLogic == null)
        {
            DebugHelper.Log($"{_unitMain.unitName} (UnitAI): No AI handler logic. Waiting.", _unitMain);
            // AI performs a simple wait if no logic is present but it has AP
            if (_unitMain.CanAffordAPForAction(PlayerInputHandler.WaitActionCost))
            {
                _unitMain.SpendAPForAction(PlayerInputHandler.WaitActionCost);
            }
            HandleTurnEnd();
            yield break;
        }

        // Execute the assigned AI logic
        yield return StartCoroutine(aiHandlerLogic.ExecuteTurn(_unitMain));
        
        // The aiHandlerLogic.ExecuteTurn should call EndTurnSafety which calls TurnManager.EndUnitTurn.
        // However, as a failsafe, ensure the turn ends if it hasn't been by the AI logic itself.
        HandleTurnEnd();
    }

    private void HandleTurnEnd()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == _unitMain)
        {
            // This is a safety net. BasicAIHandler.EndTurnSafety should be the primary way an AI turn ends.
            DebugHelper.LogWarning($"UnitAI.HandleTurnEnd: Forcing EndUnitTurn for {_unitMain.unitName} as a safety measure.", _unitMain);
            TurnManager.Instance.EndUnitTurn(_unitMain);
        }
    }
}