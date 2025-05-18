// TurnManager.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("Turn Logic")]
    public const int ActionCounterThreshold = 1000;
    private int _currentRoundNumber = 0;

    [Header("Unit Management")]
    private List<Unit> _combatUnits = new List<Unit>();
    public IReadOnlyList<Unit> CombatUnits => _combatUnits.AsReadOnly();

    public Unit ActiveUnit { get; private set; }
    private Tile _previousActiveUnitTile = null;

    [Header("UI References")]
    public TurnOrderUI turnOrderUI;

    private bool _isCombatActive = false;
    public bool IsCombatActive => _isCombatActive;

    private Coroutine _advanceTurnsCoroutine = null;
    private HashSet<Unit> _unitsThatHaveTakenTurnThisRound = new HashSet<Unit>();

    [Header("XP Management")]
    public int TotalXPEarnedThisBattle { get; private set; } = 0;

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
    }

    public void ClearCombatUnits()
    {
        Debug.Log("[TurnManager] Clearing all combat units and resetting combat state.");
        _combatUnits.Clear();
        _unitsThatHaveTakenTurnThisRound.Clear();
        ActiveUnit = null;
        _isCombatActive = false;
        _currentRoundNumber = 0;
        TotalXPEarnedThisBattle = 0;

        if (_previousActiveUnitTile != null)
        {
            _previousActiveUnitTile.SetHighlight(TileHighlightState.None);
            _previousActiveUnitTile = null;
        }

        if (_advanceTurnsCoroutine != null)
        {
            StopCoroutine(_advanceTurnsCoroutine);
            _advanceTurnsCoroutine = null;
        }
        UpdateTurnDisplay();
    }

    public void RegisterUnit(Unit unit)
    {
        if (unit == null)
        {
            Debug.LogWarning("[TurnManager] Attempted to register a null unit.", this);
            return;
        }
        if (!_combatUnits.Contains(unit))
        {
            _combatUnits.Add(unit);
            unit.actionCounter = 0;
        }
        if (_isCombatActive) UpdateTurnDisplay();
    }

    public void UnregisterUnit(Unit unit)
    {
        if (unit == null) return;
        bool removed = _combatUnits.Remove(unit);
        _unitsThatHaveTakenTurnThisRound.Remove(unit);

        if (removed)
        {
            if (ActiveUnit == unit)
            {
                if (_previousActiveUnitTile != null)
                {
                    _previousActiveUnitTile.SetHighlight(TileHighlightState.None);
                    _previousActiveUnitTile = null;
                }
                ActiveUnit = null;

                if (_isCombatActive)
                {
                     _combatUnits.RemoveAll(u => u == null || !u.IsAlive);
                    int playerCount = _combatUnits.Count(u => u.CurrentFaction == FactionType.Player && u.IsAlive);
                    int enemyCount = _combatUnits.Count(u => u.CurrentFaction == FactionType.Enemy && u.IsAlive);

                    if (playerCount == 0 || enemyCount == 0)
                    {
                        EndCombat();
                    }
                    else if (_advanceTurnsCoroutine == null && _combatUnits.Any(u => u.IsAlive))
                    {
                        _advanceTurnsCoroutine = StartCoroutine(AdvanceTurnsCoroutine());
                    }
                }
            }
        }
        if (_isCombatActive) UpdateTurnDisplay();
    }

    public void StartCombat()
    {
        _combatUnits.RemoveAll(u => u == null);
        if (_combatUnits.Count(u => u.IsAlive) < 1) // Modified to allow solo testing, but ideally 2
        {
            Debug.LogWarning($"[TurnManager] Not enough alive units ({_combatUnits.Count(u => u.IsAlive)}) to start combat. At least 1 required.", this);
            _isCombatActive = false;
            UpdateTurnDisplay();
            // return; // Allow to proceed for testing single unit scenarios if needed
        }

        bool hasPlayers = _combatUnits.Any(u => u.IsAlive && u.CurrentFaction == FactionType.Player);
        bool hasEnemies = _combatUnits.Any(u => u.IsAlive && u.CurrentFaction == FactionType.Enemy);

        if (!hasPlayers && hasEnemies) Debug.LogWarning("[TurnManager] Starting combat with no Player units.", this);
        if (hasPlayers && !hasEnemies) Debug.LogWarning("[TurnManager] Starting combat with no Enemy units.", this);
        if (!hasPlayers && !hasEnemies && _combatUnits.Any(u=>u.IsAlive)) Debug.LogWarning("[TurnManager] Starting combat with no Player or Enemy units (e.g. all Neutral).", this);


        _isCombatActive = true;
        ActiveUnit = null;
        if (_previousActiveUnitTile != null)
        {
            _previousActiveUnitTile.SetHighlight(TileHighlightState.None);
            _previousActiveUnitTile = null;
        }
        _currentRoundNumber = 1;
        _unitsThatHaveTakenTurnThisRound.Clear();
        ResetBattleXP();

        CombatLogger.LogTurnEvent($"--- Round {_currentRoundNumber} Begins ---");
        CombatLogger.LogTurnEvent("Combat Started!");

        foreach (Unit unit in _combatUnits)
        {
            if (unit != null && unit.IsAlive)
            {
                unit.ResetForCombatStart();
                unit.actionCounter = 0;
            }
        }

        if (_advanceTurnsCoroutine != null) StopCoroutine(_advanceTurnsCoroutine);
        _advanceTurnsCoroutine = StartCoroutine(AdvanceTurnsCoroutine());
        UpdateTurnDisplay();
    }

    public void EndCombat()
    {
        if (!_isCombatActive && ActiveUnit == null)
        {
            return;
        }

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

        CombatLogger.LogEvent($"--- Combat Results ---", Color.gray, LogMessageType.System);

        if (TotalXPEarnedThisBattle > 0)
        {
            CombatLogger.LogEvent($"Total XP Earned This Battle: {TotalXPEarnedThisBattle}", Color.yellow, LogMessageType.System);
            List<Unit> survivingPlayerUnits = _combatUnits.Where(u => u != null && u.IsAlive && u.CurrentFaction == FactionType.Player && u.Stats != null).ToList();
            if (survivingPlayerUnits.Count > 0)
            {
                int xpPerUnit = Mathf.FloorToInt((float)TotalXPEarnedThisBattle / survivingPlayerUnits.Count);
                if (xpPerUnit > 0)
                {
                    CombatLogger.LogEvent($"Distributing {xpPerUnit} XP to {survivingPlayerUnits.Count} surviving player unit(s).", Color.magenta, LogMessageType.System);
                    foreach (Unit playerUnit in survivingPlayerUnits)
                    {
                        playerUnit.Stats.AddXP(xpPerUnit);
                    }
                }
                else { CombatLogger.LogEvent("Not enough total XP to distribute meaningful amounts.", Color.yellow, LogMessageType.System); }
            }
            else { CombatLogger.LogEvent("No surviving player units to distribute XP to.", Color.yellow, LogMessageType.System); }
        }
        else { CombatLogger.LogEvent("No XP earned this battle.", Color.yellow, LogMessageType.System); }

        foreach (Unit unit in _combatUnits)
        {
            if(unit != null) unit.actionCounter = 0;
        }
        CombatLogger.LogTurnEvent("Combat Ended!");
        UpdateTurnDisplay();
    }

    public void ResetBattleXP()
    {
        TotalXPEarnedThisBattle = 0;
    }

    public void AddBattleXP(int amount)
    {
        if (amount > 0) TotalXPEarnedThisBattle += amount;
    }

    private IEnumerator AdvanceTurnsCoroutine()
    {
        yield return null; 

        while (_isCombatActive)
        {
            if (ActiveUnit == null)
            {
                _combatUnits.RemoveAll(u => u == null || !u.IsAlive);
                if (!_combatUnits.Any(u => u.IsAlive))
                {
                    DebugHelper.Log("TurnManager: No units alive. Ending combat.", this);
                    EndCombat();
                    yield break;
                }

                int playerCount = _combatUnits.Count(u => u.CurrentFaction == FactionType.Player && u.IsAlive);
                int enemyCount = _combatUnits.Count(u => u.CurrentFaction == FactionType.Enemy && u.IsAlive);

                if (playerCount == 0 && enemyCount > 0 && _isCombatActive) { DebugHelper.Log("TurnManager: All player units defeated. Ending combat (Player loss).", this); EndCombat(); yield break; }
                if (enemyCount == 0 && playerCount > 0 && _isCombatActive) { DebugHelper.Log("TurnManager: All enemy units defeated. Ending combat (Player win).", this); EndCombat(); yield break; }
                if ((playerCount == 0 || enemyCount == 0) && _combatUnits.Any(u => u.IsAlive) && _isCombatActive) { DebugHelper.Log("TurnManager: One side has no units remaining. Ending combat.", this); EndCombat(); yield break; }


                if (_unitsThatHaveTakenTurnThisRound.Count >= _combatUnits.Count(u => u.IsAlive))
                {
                    _currentRoundNumber++;
                    _unitsThatHaveTakenTurnThisRound.Clear();
                    CombatLogger.LogTurnEvent($"--- Round {_currentRoundNumber} Begins ---");
                }

                bool anyUnitReadyThisTick = false;
                int safetyCounter = 0;
                const int maxTicksWithoutReadyUnit = 20000;

                while (!anyUnitReadyThisTick && _isCombatActive && safetyCounter < maxTicksWithoutReadyUnit)
                {
                    safetyCounter++;
                    List<Unit> unitsToProcessThisTick = new List<Unit>(_combatUnits);
                    foreach (Unit unit in unitsToProcessThisTick)
                    {
                        if (unit == null || !unit.IsAlive) continue;
                        if (!_unitsThatHaveTakenTurnThisRound.Contains(unit) || unit.actionCounter == 0)
                        {
                            unit.actionCounter += unit.EffectiveSpeed;
                        }
                        if (unit.actionCounter >= ActionCounterThreshold && !_unitsThatHaveTakenTurnThisRound.Contains(unit)) {
                            anyUnitReadyThisTick = true;
                        }
                    }
                    if (!anyUnitReadyThisTick && _isCombatActive) { yield return null; }
                }

                if (safetyCounter >= maxTicksWithoutReadyUnit && !anyUnitReadyThisTick && _isCombatActive)
                { DebugHelper.LogError("TurnManager: AdvanceTurnsCoroutine safety break! No unit became ready after max ticks.", this); EndCombat(); yield break; }
                if (!_isCombatActive) yield break;

                List<Unit> readyUnits = _combatUnits
                    .Where(u => u != null && u.IsAlive && u.actionCounter >= ActionCounterThreshold
                                && !_unitsThatHaveTakenTurnThisRound.Contains(u) && u.Stats != null) // Added u.Stats != null
                    .OrderByDescending(u => u.actionCounter)
                    .ThenByDescending(u => u.EffectiveSpeed)
                    .ThenByDescending(u => (u.Stats.EffectiveAttributes != null) ? u.Stats.EffectiveAttributes.Echo : 0)
                    .ThenByDescending(u => (u.Stats.EffectiveAttributes != null) ? u.Stats.EffectiveAttributes.Glimmer : 0)
                    .ThenBy(u => u.GetInstanceID())
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

                    if (ActiveUnit.Stats != null)
                    {
                        ActiveUnit.Stats.RegenerateActionPointsAtTurnStart();
                        ActiveUnit.Stats.RegenerateResourcesAtTurnStart();
                        // MODIFIED: Commented out the problematic line
                        // if(EffectSystem.Instance != null) EffectSystem.Instance.TickTurnStartEffects(ActiveUnit); 
                    }
                    else { DebugHelper.LogError($"TurnManager: ActiveUnit {ActiveUnit.unitName} missing Stats. Cannot regen/tick.", ActiveUnit); }

                    Tile activeUnitTile = ActiveUnit.CurrentTile;
                    if (activeUnitTile != null)
                    {
                        activeUnitTile.SetHighlight(TileHighlightState.ActiveTurnUnit);
                        _previousActiveUnitTile = activeUnitTile;
                    } else { _previousActiveUnitTile = null; }

                    int currentAP = ActiveUnit.Stats != null ? ActiveUnit.Stats.currentActionPoints : -1;
                    int maxAP = ActiveUnit.Stats != null ? ActiveUnit.Stats.MaxActionPoints : -1;
                    string turnStartMessage = $"Round {_currentRoundNumber} - Unit {ActiveUnit.unitName}'s Turn Start (AP: {currentAP}/{maxAP})";
                    DebugHelper.Log($"TurnManager: --- {turnStartMessage} --- AC: {ActiveUnit.actionCounter}, Speed: {ActiveUnit.EffectiveSpeed}", ActiveUnit);
                    CombatLogger.LogTurnEvent(turnStartMessage);
                    UpdateTurnDisplay();

                    if (ActiveUnit.CurrentFaction != FactionType.Player)
                    {
                        if (ActiveUnit.AI != null) { StartCoroutine(ActiveUnit.AI.ProcessTurn()); }
                        else
                        {
                            Debug.LogWarning($"TurnManager: AI Unit {ActiveUnit.unitName} has no AI component. Ending its turn.", ActiveUnit);
                            EndUnitTurn(ActiveUnit);
                        }
                    }
                }
            }
            yield return null;
        }
        _advanceTurnsCoroutine = null;
    }

    public void EndUnitTurn(Unit unit)
    {
        if (unit == null)
        {
            Debug.LogWarning("[TurnManager] EndUnitTurn called with a null unit.", this);
            if (ActiveUnit == null && _isCombatActive && _advanceTurnsCoroutine == null)
            {
                _advanceTurnsCoroutine = StartCoroutine(AdvanceTurnsCoroutine());
            }
            UpdateTurnDisplay();
            return;
        }

        Tile unitCurrentTile = unit.CurrentTile;

        if (ActiveUnit != unit && unit.IsAlive) {
            return;
        }
        
        string turnEndMessage = $"Round {_currentRoundNumber} - Unit {unit.unitName}'s Turn End";
        DebugHelper.Log($"TurnManager: --- {turnEndMessage} ---", unit);
        CombatLogger.LogTurnEvent(turnEndMessage);

        if (unit.IsAlive && unit.Stats != null && EffectSystem.Instance != null) // Check EffectSystem.Instance here
        {
            EffectSystem.Instance.TickUnitEffects(unit);
        }

        if (unitCurrentTile != null && unitCurrentTile == _previousActiveUnitTile)
        {
            unitCurrentTile.SetHighlight(TileHighlightState.None);
            _previousActiveUnitTile = null;
        } else if (_previousActiveUnitTile != null && ActiveUnit == unit) {
             _previousActiveUnitTile.SetHighlight(TileHighlightState.None);
            _previousActiveUnitTile = null;
        }

        unit.actionCounter = 0;
        _unitsThatHaveTakenTurnThisRound.Add(unit);

        if (ActiveUnit == unit) ActiveUnit = null;

        _combatUnits.RemoveAll(u => u == null || !u.IsAlive);
        int playerCount = _combatUnits.Count(u => u.CurrentFaction == FactionType.Player && u.IsAlive);
        int enemyCount = _combatUnits.Count(u => u.CurrentFaction == FactionType.Enemy && u.IsAlive);

        bool combatShouldEnd = false;
        if (playerCount == 0 && enemyCount > 0 && _isCombatActive)
        { DebugHelper.Log("TurnManager: All player units defeated. Ending combat (Player loss).", this); combatShouldEnd = true; }
        else if (enemyCount == 0 && playerCount > 0 && _isCombatActive)
        { DebugHelper.Log("TurnManager: All enemy units defeated. Ending combat (Player win).", this); combatShouldEnd = true; }
        else if ((playerCount == 0 || enemyCount == 0) && _isCombatActive && _combatUnits.Any(u => u.IsAlive))
        { DebugHelper.Log("TurnManager: One faction has no units remaining. Ending combat.", this); combatShouldEnd = true; }

        UpdateTurnDisplay();

        if (combatShouldEnd)
        {
            EndCombat();
        }
        else if (_isCombatActive && ActiveUnit == null && _advanceTurnsCoroutine == null)
        {
            _advanceTurnsCoroutine = StartCoroutine(AdvanceTurnsCoroutine());
        }
    }

    private void UpdateTurnDisplay()
    {
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
        if (unitsToConsider == null || !unitsToConsider.Any()) return new List<Unit>();

        List<Unit> forecast = new List<Unit>();
        List<(Unit unit, int currentAC, int effectiveSpeed)> tempUnitData = unitsToConsider
            .Where(u => u != null && u.IsAlive)
            .Select(u => (u, u.actionCounter, u.EffectiveSpeed))
            .ToList();

        if (ActiveUnit != null && ActiveUnit.IsAlive && unitsToConsider.Contains(ActiveUnit))
        {
            forecast.Add(ActiveUnit);
        }
        
        int maxForecastSlots = (turnOrderUI != null) ? turnOrderUI.numberOfSlotsToDisplay : 5;

        // Temporary list for simulation that we can modify
        List<(Unit unit, int simAC, int effSpeed)> simulationList = tempUnitData
            .Where(ud => !forecast.Contains(ud.unit)) // Exclude already active unit if it's in forecast
            .Select(ud => (ud.unit, ud.currentAC, ud.effectiveSpeed))
            .ToList();

        // Simulate turns to fill the forecast
        while(forecast.Count < maxForecastSlots && simulationList.Any())
        {
            // Find the unit(s) that will reach threshold with the minimum number of "global ticks"
            int minTicksToReady = int.MaxValue;
            Unit nextUnitInSim = null;

            if (!simulationList.Any(ud => ud.simAC >= ActionCounterThreshold)) // If no one is currently ready
            {
                foreach (var entry in simulationList)
                {
                    if (entry.effSpeed <= 0) continue; // Avoid division by zero or infinite loop for units with no speed
                    int ticksNeeded = Mathf.Max(0, (ActionCounterThreshold - entry.simAC + entry.effSpeed - 1) / entry.effSpeed); // Ceiling division
                    if (ticksNeeded < minTicksToReady)
                    {
                        minTicksToReady = ticksNeeded;
                    }
                }

                if (minTicksToReady == int.MaxValue) break; // No one can become ready

                // Advance all units by minTicksToReady
                for (int i = 0; i < simulationList.Count; i++)
                {
                    var current = simulationList[i];
                    simulationList[i] = (current.unit, current.simAC + (minTicksToReady * current.effSpeed), current.effSpeed);
                }
            }
            
            // Select the best unit among those now ready or already over threshold
            var bestNext = simulationList
                .Where(ud => ud.simAC >= ActionCounterThreshold)
                .OrderByDescending(ud => ud.simAC)
                .ThenByDescending(ud => ud.effSpeed)
                .ThenByDescending(ud => (ud.unit.Stats?.EffectiveAttributes != null) ? ud.unit.Stats.EffectiveAttributes.Echo : 0)
                .ThenByDescending(ud => (ud.unit.Stats?.EffectiveAttributes != null) ? ud.unit.Stats.EffectiveAttributes.Glimmer : 0)
                .ThenBy(ud => ud.unit.GetInstanceID())
                .FirstOrDefault();

            if (bestNext.unit != null)
            {
                nextUnitInSim = bestNext.unit;
                if (!forecast.Contains(nextUnitInSim)) // Should always be true due to earlier filter
                {
                    forecast.Add(nextUnitInSim);
                }
                // Remove from simulation list or mark as "acted" in this forecast
                simulationList.RemoveAll(ud => ud.unit == nextUnitInSim);
            }
            else // No one reached threshold even after advancing by minTicksToReady (shouldn't happen if minTicksToReady was calculated correctly unless all speeds are 0)
            {
                break; 
            }
        }
        
        // If forecast is still not full, add remaining units by their current (original) AC / speed as a fallback
        if (forecast.Count < maxForecastSlots)
        {
            var remainingUnits = tempUnitData // Use original tempUnitData for this fallback
                .Where(ud => ud.unit.IsAlive && !forecast.Contains(ud.unit))
                .OrderByDescending(ud => ud.currentAC) // Order by original AC state
                .ThenByDescending(ud => ud.effectiveSpeed)
                .ThenByDescending(ud => (ud.unit.Stats?.EffectiveAttributes != null) ? ud.unit.Stats.EffectiveAttributes.Echo : 0)
                .ThenByDescending(ud => (ud.unit.Stats?.EffectiveAttributes != null) ? ud.unit.Stats.EffectiveAttributes.Glimmer : 0)
                .ThenBy(ud => ud.unit.GetInstanceID())
                .Select(ud => ud.unit);
            forecast.AddRange(remainingUnits);
        }

        return forecast.Distinct().Take(maxForecastSlots).ToList();
    }
}