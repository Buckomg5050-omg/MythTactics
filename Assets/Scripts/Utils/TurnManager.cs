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
        if (EffectSystem.Instance == null)
        {
            DebugHelper.LogError("TurnManager: EffectSystem.Instance is null! Effects will not be processed.", this);
        }
    }

    public void RegisterUnit(Unit unit)
    {
        if (unit == null) return;
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
        if (removed && ActiveUnit == unit)
        {
            if (_previousActiveUnitTile != null)
            {
                _previousActiveUnitTile.SetHighlight(TileHighlightState.None);
                _previousActiveUnitTile = null;
            }
            ActiveUnit = null;
            if (_isCombatActive && _advanceTurnsCoroutine == null && _combatUnits.Count > 0) 
            {
                _advanceTurnsCoroutine = StartCoroutine(AdvanceTurnsCoroutine());
            }
        }
        if (_isCombatActive) UpdateTurnDisplay();
    }

    public void StartCombat()
    {
        if (_combatUnits.Count == 0)
        {
            _isCombatActive = false; UpdateTurnDisplay(); return;
        }
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
                if (unit.Stats != null) unit.Stats.ClearAllEffects(); 
            }
        }
        if (_advanceTurnsCoroutine != null) StopCoroutine(_advanceTurnsCoroutine);
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
        
        // MODIFIED: XP Distribution Logic
        if (TotalXPEarnedThisBattle > 0)
        {
            CombatLogger.LogEvent($"Total XP Earned This Battle: {TotalXPEarnedThisBattle}", Color.yellow, LogMessageType.System);

            List<Unit> survivingPlayerUnits = _combatUnits.Where(u => u != null && u.IsAlive && u.CompareTag("Player") && u.Stats != null).ToList();
            if (survivingPlayerUnits.Count > 0)
            {
                int xpPerUnit = Mathf.FloorToInt((float)TotalXPEarnedThisBattle / survivingPlayerUnits.Count);
                if (xpPerUnit > 0)
                {
                    CombatLogger.LogEvent($"Distributing {xpPerUnit} XP to {survivingPlayerUnits.Count} surviving player unit(s).", Color.magenta, LogMessageType.System);
                    foreach (Unit playerUnit in survivingPlayerUnits)
                    {
                        playerUnit.Stats.AddXP(xpPerUnit); // AddXP method in UnitStats will log individual gains
                    }
                }
                else
                {
                    CombatLogger.LogEvent("Not enough total XP to distribute meaningful amounts.", Color.yellow, LogMessageType.System);
                }
            }
            else
            {
                CombatLogger.LogEvent("No surviving player units to distribute XP to.", Color.yellow, LogMessageType.System);
            }
        }
        else
        {
            CombatLogger.LogEvent("No XP earned this battle.", Color.yellow, LogMessageType.System);
        }
        // End of MODIFICATION
        
        // Clear effects after XP distribution, in case XP gain triggers level up effects (future)
        foreach (Unit unit in _combatUnits) 
        {
            if (unit != null && unit.Stats != null) unit.Stats.ClearAllEffects();
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

    private System.Collections.IEnumerator AdvanceTurnsCoroutine()
    {
        yield return null; 
        while (_isCombatActive)
        {
            if (ActiveUnit == null) 
            {
                _combatUnits.RemoveAll(u => u == null || !u.IsAlive);
                if (_combatUnits.Count == 0) { EndCombat(); yield break; }
                
                int playerCount = _combatUnits.Count(u => u.CompareTag("Player") && u.IsAlive);
                int enemyCount = _combatUnits.Count(u => !u.CompareTag("Player") && u.IsAlive);

                if (playerCount == 0 && enemyCount > 0) { DebugHelper.Log("TurnManager: All player units defeated. Enemy wins!", this); EndCombat(); yield break; }
                if (enemyCount == 0 && playerCount > 0) { DebugHelper.Log("TurnManager: All enemy units defeated. Player wins!", this); EndCombat(); yield break; }

                if (_unitsThatHaveTakenTurnThisRound.Count >= _combatUnits.Count(u => u.IsAlive && _combatUnits.Contains(u)))
                {
                    _currentRoundNumber++;
                    _unitsThatHaveTakenTurnThisRound.Clear();
                    CombatLogger.LogTurnEvent($"--- Round {_currentRoundNumber} Begins ---");
                }

                bool anyUnitReadyThisTick = false;
                int safetyCounter = 0;
                const int maxTicksWithoutReadyUnit = 10000; 

                while (!anyUnitReadyThisTick && _isCombatActive && safetyCounter < maxTicksWithoutReadyUnit)
                {
                    safetyCounter++;
                    List<Unit> unitsToProcessThisTick = new List<Unit>(_combatUnits); 
                    foreach (Unit unit in unitsToProcessThisTick)
                    {
                        if (unit == null || !unit.IsAlive) continue;
                        if (_unitsThatHaveTakenTurnThisRound.Contains(unit) && unit.actionCounter == 0) continue; 
                        if (unit.Stats != null) { unit.actionCounter += unit.EffectiveSpeed; }
                        else { unit.actionCounter += 1; }
                        if (unit.actionCounter >= ActionCounterThreshold) { anyUnitReadyThisTick = true; }
                    }
                    if (!anyUnitReadyThisTick && _isCombatActive) { yield return null; }
                }

                if (safetyCounter >= maxTicksWithoutReadyUnit && !anyUnitReadyThisTick && _isCombatActive)
                { DebugHelper.LogError("TurnManager: AdvanceTurnsCoroutine safety break! No unit became ready.", this); EndCombat(); yield break; }
                if (!_isCombatActive) yield break; 

                List<Unit> readyUnits = _combatUnits
                    .Where(u => u != null && u.IsAlive && u.actionCounter >= ActionCounterThreshold && u.Stats != null && u.Stats.EffectiveAttributes != null
                                && !_unitsThatHaveTakenTurnThisRound.Contains(u)) 
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
                    _unitsThatHaveTakenTurnThisRound.Add(ActiveUnit);

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
                    string turnStartMessage = $"Round {_currentRoundNumber} - Unit {ActiveUnit.unitName}'s Turn Start (AP: {currentAP}/{maxAP})";
                    DebugHelper.Log($"TurnManager: --- {turnStartMessage} --- AC: {ActiveUnit.actionCounter}, Speed: {ActiveUnit.EffectiveSpeed}", ActiveUnit);
                    CombatLogger.LogTurnEvent(turnStartMessage); 
                    UpdateTurnDisplay(); 

                    if (!ActiveUnit.CompareTag("Player"))
                    {
                        if (ActiveUnit.AI != null) { StartCoroutine(ActiveUnit.AI.ProcessTurn()); }
                        else { EndUnitTurn(ActiveUnit); }
                    }
                }
                else if (_isCombatActive) 
                {
                    if (_unitsThatHaveTakenTurnThisRound.Count < _combatUnits.Count(u => u.IsAlive && _combatUnits.Contains(u)) &&
                        _combatUnits.Any(u => u.IsAlive && !_unitsThatHaveTakenTurnThisRound.Contains(u) && u.actionCounter < ActionCounterThreshold))
                    {
                    }
                    else if (_combatUnits.Any(u => u.IsAlive)) 
                    {
                        _unitsThatHaveTakenTurnThisRound.Clear(); 
                    }
                }
            }
            yield return null; 
        }
        _advanceTurnsCoroutine = null;
    }

    public void EndUnitTurn(Unit unit)
    {
        if (unit == null) { UpdateTurnDisplay(); return; }
        Tile unitCurrentTile = unit.CurrentTile;
        if (ActiveUnit != unit && unit.IsAlive) return;
        
        string turnEndMessage = $"Round {_currentRoundNumber} - Unit {unit.unitName}'s Turn End";
        DebugHelper.Log($"TurnManager: --- {turnEndMessage} ---", unit);
        CombatLogger.LogTurnEvent(turnEndMessage); 

        if (unit.IsAlive && unit.Stats != null && EffectSystem.Instance != null)
        {
            EffectSystem.Instance.TickUnitEffects(unit);
        }
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

        if (ActiveUnit == unit) ActiveUnit = null; 
        
        _combatUnits.RemoveAll(u => u == null || !u.IsAlive); 
        int playerCount = _combatUnits.Count(u => u.CompareTag("Player") && u.IsAlive);
        int enemyCount = _combatUnits.Count(u => !u.CompareTag("Player") && u.IsAlive);

        if (playerCount == 0 && enemyCount > 0 && _isCombatActive)
        { DebugHelper.Log("TurnManager: All player units defeated. Ending combat (Player loss).", this); EndCombat(); }
        else if (enemyCount == 0 && playerCount > 0 && _isCombatActive)
        { DebugHelper.Log("TurnManager: All enemy units defeated. Ending combat (Player win).", this); EndCombat(); }
        
        UpdateTurnDisplay(); 
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
                    if (_isCombatActive) EndCombat();
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