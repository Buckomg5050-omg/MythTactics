// UnitAI.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Unit))]
public class UnitAI : MonoBehaviour
{
    private Unit _unitMain;
    // MODIFIED: aiHandlerLogic will now be fetched in Awake
    private BasicAIHandler aiHandlerLogic; 
    private AIBehaviorProfile _currentBehaviorProfile = AIBehaviorProfile.None;

    // MODIFIED: Added Awake to fetch BasicAIHandler
    void Awake()
    {
        // Try to get the BasicAIHandler component on the same GameObject.
        aiHandlerLogic = GetComponent<BasicAIHandler>();
        if (aiHandlerLogic == null)
        {
            // This warning will now appear earlier if the handler is missing from the prefab.
            Debug.LogWarning($"UnitAI on {this.gameObject.name}: BasicAIHandler component not found in Awake. AI may be passive.", this);
        }
    }

    public void Initialize(Unit mainUnit)
    {
        _unitMain = mainUnit;
        if (_unitMain == null)
        {
            Debug.LogError("UnitAI.Initialize: Main Unit reference is null!", this);
            enabled = false; 
            return;
        }

        // MODIFIED: aiHandlerLogic is already fetched in Awake. We just check it here.
        if (aiHandlerLogic == null)
        {
            // This means it wasn't found in Awake either.
            Debug.LogWarning($"UnitAI on {_unitMain.unitName} still does not have AI Handler Logic after Awake. AI will be passive depending on profile: {_currentBehaviorProfile}.", this);
        }

        if (_unitMain.CurrentFaction == FactionType.Player)
        {
            // This component should have been disabled by Unit.PrimeDataFromTemplate if it's a player
            // but this log is a good sanity check if it's somehow enabled.
            if(this.enabled)
                Debug.LogWarning($"UnitAI initialized and enabled on a Player faction unit: {_unitMain.unitName}. It should typically be disabled.", this);
        }
    }

    public void SetBehaviorProfile(AIBehaviorProfile profile)
    {
        _currentBehaviorProfile = profile;
        // if (aiHandlerLogic != null && _unitMain != null) // Check if aiHandlerLogic is valid
        // {
        //     Debug.Log($"UnitAI on {_unitMain.unitName} profile set to: {profile}. AI Handler found: {aiHandlerLogic != null}", this);
        // }
    }

    public IEnumerator ProcessTurn()
    {
        if (!_unitMain.IsAlive || _unitMain.CurrentFaction == FactionType.Player)
        {
            // Simplified initial checks
            HandleTurnEndSafety();
            yield break;
        }

        if (aiHandlerLogic == null)
        {
            DebugHelper.Log($"{_unitMain.unitName} (UnitAI - Profile: {_currentBehaviorProfile}): No AI handler logic. Waiting out turn.", _unitMain);
            if (_unitMain.CanAffordAPForAction(PlayerInputHandler.WaitActionCost))
            {
                CombatLogger.LogEvent($"{_unitMain.unitName} (AI) waits.", Color.gray, LogMessageType.TurnFlow);
                _unitMain.SpendAPForAction(PlayerInputHandler.WaitActionCost);
            }
            HandleTurnEndSafety();
            yield break;
        }

        yield return StartCoroutine(aiHandlerLogic.ExecuteTurn(_unitMain, _currentBehaviorProfile));
        HandleTurnEndSafety();
    }

    public void HandleTurnEndSafety()
    {
        if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == _unitMain)
        {
            TurnManager.Instance.EndUnitTurn(_unitMain);
        }
    }
}