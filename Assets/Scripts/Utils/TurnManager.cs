// TurnManager.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
// Make sure MythTactics.Combat is accessible if Unit, Tile, etc. are in there.
// If CombatLogger is in a different namespace, you might need a using directive for it too.

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("Turn Logic")]
    public const int ActionCounterThreshold = 1000;
    private int _currentRoundNumber = 0; // ADDED: Round counter

    [Header("Unit Management")]
    private List<Unit> _combatUnits = new List<Unit>();
    public IReadOnlyList<Unit> CombatUnits => _combatUnits.AsReadOnly();

    public Unit ActiveUnit { get; private set; }
    private Tile _previousActiveUnitTile = null;

    [Header("UI References")]
    [Tooltip("Assign the TurnOrderUI instance from your scene.")]
    public TurnOrderUI turnOrderUI;

    private bool _isCombatActive = false;
    public bool IsCombatActive => _isCombatActive;

    private Coroutine _advanceTurnsCoroutine = null;
    private HashSet<Unit> _unitsThatHaveTakenTurnThisRound = new HashSet<Unit>(); // ADDED: To track round progression

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning("Duplicate TurnManager instance found. Destroying this one.", this);
            Destroy(gameObject);
            return;
        }

        if (turnOrderUI == null)
        {
            DebugHelper.LogWarning("TurnManager: TurnOrderUI reference not set in Inspector. Attempting to find it.", this);
            turnOrderUI = FindFirstObjectByType<TurnOrderUI>();
            if (turnOrderUI == null)
            {
                DebugHelper.LogError("TurnManager: TurnOrderUI instance not found in scene! UI will not update.", this);
            }
        }
        if (EffectSystem.Instance == null)
        {
            DebugHelper.LogError("TurnManager: EffectSystem.Instance is null! Effects will not be processed. Ensure an EffectSystem object is in the scene.", this);
        }
    }

    public void RegisterUnit(Unit unit)
    {
        if (unit == null)
        {
            DebugHelper.LogWarning("TurnManager: Attempted to register a null unit.", this);
            return;
        }

        if (!_combatUnits.Contains(unit))
        {
            _combatUnits.Add(unit);
            unit.actionCounter = 0; // Initialize AC for newly registered unit
            // DebugHelper.Log($"TurnManager: Registered unit '{unit.unitName}'. Total units: {_combatUnits.Count}", this);
        }
        // else
        // {
            // DebugHelper.LogWarning($"TurnManager: Unit '{unit.unitName}' is already registered.", this);
        // }
        if (_isCombatActive) UpdateTurnDisplay(); 
    }

    public void UnregisterUnit(Unit unit)
    {
        if (unit == null)
        {
            DebugHelper.LogWarning("TurnManager: Attempted to unregister a null unit.", this);
            return;
        }

        bool removed = _combatUnits.Remove(unit);
        _unitsThatHaveTakenTurnThisRound.Remove(unit); // ADDED: Also remove from round tracking

        if (removed)
        {
            // DebugHelper.Log($"TurnManager: Unregistered unit '{unit.unitName}'. Total units: {_combatUnits.Count}", this);
            if (ActiveUnit == unit)
            {
                // DebugHelper.Log($"TurnManager: Active unit '{unit.unitName}' was unregistered. Clearing ActiveUnit and its tile highlight.", this);
                if (_previousActiveUnitTile != null)
                {
                    _previousActiveUnitTile.SetHighlight(TileHighlightState.None);
                    _previousActiveUnitTile = null;
                }
                ActiveUnit = null;
                if (_isCombatActive && _advanceTurnsCoroutine == null) 
                {
                    _advanceTurnsCoroutine = StartCoroutine(AdvanceTurnsCoroutine());
                }
            }
        }
        // else
        // {
            // DebugHelper.LogWarning($"TurnManager: Unit '{unit.unitName}' was not found in combat units to unregister.", this);
        // }
        if (_isCombatActive) UpdateTurnDisplay();
    }

    public void StartCombat()
    {
        if (_combatUnits.Count == 0)
        {
            DebugHelper.LogWarning("TurnManager: StartCombat called with no units registered. Combat not starting.", this);
            _isCombatActive = false;
            UpdateTurnDisplay(); 
            return;
        }

        _isCombatActive = true;
        ActiveUnit = null;
        if (_previousActiveUnitTile != null)
        {
            _previousActiveUnitTile.SetHighlight(TileHighlightState.None);
            _previousActiveUnitTile = null;
        }

        _currentRoundNumber = 1; // MODIFIED: Initialize round number
        _unitsThatHaveTakenTurnThisRound.Clear(); // MODIFIED: Clear round tracking
        CombatLogger.LogTurnEvent($"--- Round {_currentRoundNumber} Begins ---"); // MODIFIED: Log round start
        CombatLogger.LogTurnEvent("Combat Started!"); 

        // DebugHelper.Log($"TurnManager: Preparing for combat. Initializing {_combatUnits.Count} units. Round: {_currentRoundNumber}", this);
        foreach (Unit unit in _combatUnits)
        {
            if (unit != null && unit.IsAlive)
            {
                unit.ResetForCombatStart(); 
                unit.actionCounter = 0;
                if (unit.Stats != null) unit.Stats.ClearAllEffects(); 
            }
        }

        // DebugHelper.Log("TurnManager: Combat started. Starting turn progression...", this);
        if (_advanceTurnsCoroutine != null)
        {
            StopCoroutine(_advanceTurnsCoroutine);
        }
        _advanceTurnsCoroutine = StartCoroutine(AdvanceTurnsCoroutine());
        UpdateTurnDisplay(); 
    }

    public void EndCombat()
    {
        if (!_isCombatActive) return; 

        _isCombatActive = false;
        if (_previousActiveUnitTile != null)
        {
            _previousActiveUnitTile.SetHighlight(TileHighlightState.None);
            _previousActiveUnitTile = null;
        }
        ActiveUnit = null;
        if (_advanceTurnsCoroutine != null)
        {
            StopCoroutine(_advanceTurnsCoroutine);
            _advanceTurnsCoroutine = null;
        }
        
        foreach (Unit unit in _combatUnits)
        {
            if (unit != null && unit.Stats != null)
            {
                unit.Stats.ClearAllEffects();
            }
        }
        // DebugHelper.Log("TurnManager: Combat ended. All unit effects cleared.", this);
        CombatLogger.LogTurnEvent("Combat Ended!"); 
        UpdateTurnDisplay(); 
    }

    private System.Collections.IEnumerator AdvanceTurnsCoroutine()
    {
        // DebugHelper.Log("TurnManager: AdvanceTurnsCoroutine started.", this);
        yield return null; 

        while (_isCombatActive)
        {
            if (ActiveUnit == null) 
            {
                _combatUnits.RemoveAll(u => u == null || !u.IsAlive);

                if (_combatUnits.Count == 0)
                {
                    EndCombat(); 
                    yield break;
                }
                // Check for win/loss conditions more robustly
                int playerCount = _combatUnits.Count(u => u.CompareTag("Player") && u.IsAlive);
                int enemyCount = _combatUnits.Count(u => !u.CompareTag("Player") && u.IsAlive);

                if (playerCount == 0 && enemyCount > 0) {
                    DebugHelper.Log("TurnManager: All player units defeated. Enemy wins!", this);
                    EndCombat(); yield break;
                }
                if (enemyCount == 0 && playerCount > 0) {
                    DebugHelper.Log("TurnManager: All enemy units defeated. Player wins!", this);
                    EndCombat(); yield break;
                }


                // MODIFIED: Round progression logic
                if (_unitsThatHaveTakenTurnThisRound.Count >= _combatUnits.Count(u => u.IsAlive && _combatUnits.Contains(u))) // All alive units took a turn
                {
                    _currentRoundNumber++;
                    _unitsThatHaveTakenTurnThisRound.Clear();
                    CombatLogger.LogTurnEvent($"--- Round {_currentRoundNumber} Begins ---");
                    // DebugHelper.Log($"TurnManager: Advancing to Round {_currentRoundNumber}", this);
                }


                bool anyUnitReadyThisTick = false;
                int safetyCounter = 0;
                const int maxTicksWithoutReadyUnit = 10000; 

                while (!anyUnitReadyThisTick && _isCombatActive && safetyCounter < maxTicksWithoutReadyUnit)
                {
                    // ... (AC accumulation logic remains the same) ...
                    safetyCounter++;
                    List<Unit> unitsToProcessThisTick = new List<Unit>(_combatUnits); 
                    foreach (Unit unit in unitsToProcessThisTick)
                    {
                        if (unit == null || !unit.IsAlive) continue;
                        if (_unitsThatHaveTakenTurnThisRound.Contains(unit) && unit.actionCounter == 0) continue; // Skip if already took turn and AC reset

                        if (unit.Stats != null) { unit.actionCounter += unit.EffectiveSpeed; }
                        else { unit.actionCounter += 1; }

                        if (unit.actionCounter >= ActionCounterThreshold) { anyUnitReadyThisTick = true; }
                    }
                    if (!anyUnitReadyThisTick && _isCombatActive) { yield return null; }
                }

                if (safetyCounter >= maxTicksWithoutReadyUnit && !anyUnitReadyThisTick && _isCombatActive)
                {
                    DebugHelper.LogError("TurnManager: AdvanceTurnsCoroutine safety break! No unit became ready.", this);
                    EndCombat(); yield break;
                }
                if (!_isCombatActive) yield break; 

                List<Unit> readyUnits = _combatUnits
                    .Where(u => u != null && u.IsAlive && u.actionCounter >= ActionCounterThreshold && u.Stats != null && u.Stats.EffectiveAttributes != null
                                && !_unitsThatHaveTakenTurnThisRound.Contains(u)) // Only consider units that haven't acted this round
                    .OrderByDescending(u => u.actionCounter)
                    .ThenByDescending(u => u.EffectiveSpeed)
                    .ThenByDescending(u => u.Stats.EffectiveAttributes.Echo)
                    .ThenByDescending(u => u.Stats.EffectiveAttributes.Glimmer)
                    .ToList();

                if (readyUnits.Count > 0)
                {
                    Unit nextUnitToAct = readyUnits[0];

                    if (_previousActiveUnitTile != null)
                    {
                        _previousActiveUnitTile.SetHighlight(TileHighlightState.None);
                        _previousActiveUnitTile = null;
                    }

                    ActiveUnit = nextUnitToAct;
                    // ... (rest of setting up active unit, regenerating resources, logging turn start) ...
                    _unitsThatHaveTakenTurnThisRound.Add(ActiveUnit); // MODIFIED: Mark unit as having taken a turn this round

                    if (ActiveUnit.Stats != null)
                    {
                        ActiveUnit.Stats.RegenerateActionPointsAtTurnStart();
                        ActiveUnit.Stats.RegenerateResourcesAtTurnStart();
                    }
                    else { DebugHelper.LogError($"TurnManager: ActiveUnit {ActiveUnit.unitName} missing Stats. Cannot regen.", ActiveUnit); }

                    Tile activeUnitTile = ActiveUnit.CurrentTile;
                    if (activeUnitTile != null)
                    {
                        activeUnitTile.SetHighlight(TileHighlightState.ActiveTurnUnit);
                        _previousActiveUnitTile = activeUnitTile;
                    }
                    else { _previousActiveUnitTile = null; }

                    int currentAP = ActiveUnit.Stats != null ? ActiveUnit.Stats.currentActionPoints : -1;
                    int maxAP = ActiveUnit.Stats != null ? ActiveUnit.Stats.MaxActionPoints : -1;
                    // MODIFIED: Include round number in turn start log
                    string turnStartMessage = $"Round {_currentRoundNumber} - Unit {ActiveUnit.unitName}'s Turn Start (AP: {currentAP}/{maxAP})";
                    DebugHelper.Log($"TurnManager: --- {turnStartMessage} --- AC: {ActiveUnit.actionCounter}, Speed: {ActiveUnit.EffectiveSpeed}", ActiveUnit);
                    CombatLogger.LogTurnEvent(turnStartMessage); 

                    UpdateTurnDisplay(); 

                    if (!ActiveUnit.CompareTag("Player"))
                    {
                        // DebugHelper.Log($"TurnManager: Starting AI turn processing for {ActiveUnit.unitName}.", ActiveUnit);
                        if (ActiveUnit.AI != null) { StartCoroutine(ActiveUnit.AI.ProcessTurn()); }
                        else { EndUnitTurn(ActiveUnit); }
                    }
                }
                else if (_isCombatActive) 
                {
                    // If no units are ready but some haven't acted, clear the "acted this round" list to force a new round of AC accumulation.
                    // This handles cases where all remaining units have AC below threshold.
                    if (_unitsThatHaveTakenTurnThisRound.Count < _combatUnits.Count(u => u.IsAlive && _combatUnits.Contains(u)) &&
                        _combatUnits.Any(u => u.IsAlive && !_unitsThatHaveTakenTurnThisRound.Contains(u) && u.actionCounter < ActionCounterThreshold))
                    {
                        // Still units left to act this "logical" round that are just slow. Continue ticking.
                    }
                    else if (_combatUnits.Any(u => u.IsAlive)) // All alive units have had a chance or are below threshold, start new accumulation cycle.
                    {
                        // DebugHelper.LogWarning("TurnManager: All units have acted or are below threshold. Resetting for next tick cycle if needed.", this);
                        _unitsThatHaveTakenTurnThisRound.Clear(); // Allow everyone to accumulate AC again for the next round
                        // This implicitly starts a "new round" of AC accumulation if all units were processed.
                        // Round number increment is handled at the top of the ActiveUnit == null block.
                    }
                }
            }
            yield return null; 
        }
        _advanceTurnsCoroutine = null;
        // DebugHelper.Log("TurnManager: AdvanceTurnsCoroutine ended because combat is no longer active.", this);
    }


    public void EndUnitTurn(Unit unit)
    {
        if (unit == null)
        {
            if (ActiveUnit == null) ActiveUnit = null; 
            UpdateTurnDisplay(); 
            return;
        }

        Tile unitCurrentTile = unit.CurrentTile;

        if (ActiveUnit != unit && unit.IsAlive)
        {
            return;
        }
        
        // MODIFIED: Include round number in turn end log
        string turnEndMessage = $"Round {_currentRoundNumber} - Unit {unit.unitName}'s Turn End";
        DebugHelper.Log($"TurnManager: --- {turnEndMessage} ---", unit);
        CombatLogger.LogTurnEvent(turnEndMessage); 

        if (unit.IsAlive && unit.Stats != null && EffectSystem.Instance != null)
        {
            // DebugHelper.Log($"TurnManager: Ticking effects for {unit.unitName} at end of turn.", unit);
            EffectSystem.Instance.TickUnitEffects(unit);
        }
        // ... (rest of highlight clearing and AC reset) ...
        if (unitCurrentTile != null && unitCurrentTile == _previousActiveUnitTile) 
        {
            unitCurrentTile.SetHighlight(TileHighlightState.None); 
            _previousActiveUnitTile = null;
        }
        else if (_previousActiveUnitTile != null && ActiveUnit == unit) 
        {
             _previousActiveUnitTile.SetHighlight(TileHighlightState.None); 
            _previousActiveUnitTile = null;
        }
        unit.actionCounter = 0; 

        if (ActiveUnit == unit) 
        {
            ActiveUnit = null; 
        }
        
        _combatUnits.RemoveAll(u => u == null || !u.IsAlive); 
        int playerCount = _combatUnits.Count(u => u.CompareTag("Player") && u.IsAlive);
        int enemyCount = _combatUnits.Count(u => !u.CompareTag("Player") && u.IsAlive);

        if (playerCount == 0 && enemyCount > 0 && _isCombatActive)
        {
            DebugHelper.Log("TurnManager: All player units defeated. Ending combat (Player loss).", this);
            EndCombat();
        }
        else if (enemyCount == 0 && playerCount > 0 && _isCombatActive)
        {
            DebugHelper.Log("TurnManager: All enemy units defeated. Ending combat (Player win).", this);
            EndCombat();
        }
        
        UpdateTurnDisplay(); 
    }

    private void UpdateTurnDisplay()
    {
        // ... (existing code) ...
        if (turnOrderUI != null)
        {
            if (_isCombatActive)
            {
                List<Unit> aliveAndRegisteredUnits = _combatUnits.Where(u => u != null && u.IsAlive).ToList();
                
                if (aliveAndRegisteredUnits.Count > 0)
                {
                    List<Unit> forecast = GetTurnOrderForecast(aliveAndRegisteredUnits);
                    turnOrderUI.UpdateTurnOrderDisplay(forecast, ActiveUnit); 
                }
                else 
                {
                    turnOrderUI.UpdateTurnOrderDisplay(new List<Unit>(), null);
                    if (_isCombatActive) 
                    {
                         EndCombat();
                    }
                }
            }
            else 
            {
                turnOrderUI.UpdateTurnOrderDisplay(new List<Unit>(), null); 
            }
        }
    }

    private List<Unit> GetTurnOrderForecast(List<Unit> unitsToConsider)
    {
        // ... (existing forecast logic, no changes needed for round number here) ...
        List<Unit> forecast = new List<Unit>();
        List<(Unit unit, int ac)> tempUnitAC = unitsToConsider.Select(u => (u, u.actionCounter)).ToList();
        int simulationTicks = 0;
        const int maxSimulationTicks = 500; 
        bool activeUnitIsReady = (ActiveUnit != null && ActiveUnit.IsAlive && ActiveUnit.actionCounter >= ActionCounterThreshold);

        while (!tempUnitAC.Any(uac => uac.ac >= ActionCounterThreshold && uac.unit != ActiveUnit) && simulationTicks < maxSimulationTicks && unitsToConsider.Count > (activeUnitIsReady ? 1 : 0) )
        {
            bool changed = false;
            for (int i = 0; i < tempUnitAC.Count; i++)
            {
                if (tempUnitAC[i].unit.IsAlive) 
                {
                    var current = tempUnitAC[i];
                    tempUnitAC[i] = (current.unit, current.ac + current.unit.EffectiveSpeed);
                    changed = true;
                }
            }
            if (!changed) break; 
            simulationTicks++;
        }

        if (ActiveUnit != null && ActiveUnit.IsAlive && unitsToConsider.Contains(ActiveUnit))
        {
            forecast.Add(ActiveUnit);
        }

        var sortedUpcoming = tempUnitAC
            .Where(uac => uac.unit != ActiveUnit && uac.unit.IsAlive && uac.unit.Stats != null && uac.unit.Stats.EffectiveAttributes != null)
            .OrderByDescending(uac => uac.ac >= ActionCounterThreshold) 
            .ThenByDescending(uac => uac.ac)                          
            .ThenByDescending(uac => uac.unit.EffectiveSpeed)
            .ThenByDescending(uac => uac.unit.Stats.EffectiveAttributes.Echo)
            .ThenByDescending(uac => uac.unit.Stats.EffectiveAttributes.Glimmer)
            .Select(uac => uac.unit)
            .ToList();

        forecast.AddRange(sortedUpcoming);

        int maxSlots = (turnOrderUI != null) ? turnOrderUI.numberOfSlotsToDisplay : 5;
        return forecast.Take(maxSlots).ToList();
    }
}