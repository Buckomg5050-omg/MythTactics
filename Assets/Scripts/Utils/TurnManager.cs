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
            unit.actionCounter = 0; 
            DebugHelper.Log($"TurnManager: Registered unit '{unit.unitName}'. Total units: {_combatUnits.Count}", this);
        }
        else
        {
            DebugHelper.LogWarning($"TurnManager: Unit '{unit.unitName}' is already registered.", this);
        }
        UpdateTurnDisplay();
    }

    public void UnregisterUnit(Unit unit)
    {
        if (unit == null)
        {
            DebugHelper.LogWarning("TurnManager: Attempted to unregister a null unit.", this);
            return;
        }

        bool removed = _combatUnits.Remove(unit);
        if (removed)
        {
            DebugHelper.Log($"TurnManager: Unregistered unit '{unit.unitName}'. Total units: {_combatUnits.Count}", this);
            if (ActiveUnit == unit)
            {
                DebugHelper.Log($"TurnManager: Active unit '{unit.unitName}' was unregistered. Clearing ActiveUnit and its tile highlight.", this);
                if (_previousActiveUnitTile != null)
                {
                    _previousActiveUnitTile.SetHighlight(TileHighlightState.None);
                    _previousActiveUnitTile = null;
                }
                ActiveUnit = null;
            }
        }
        else
        {
            DebugHelper.LogWarning($"TurnManager: Unit '{unit.unitName}' was not found in combat units to unregister.", this);
        }
        UpdateTurnDisplay();
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

        DebugHelper.Log($"TurnManager: Preparing for combat. Initializing {_combatUnits.Count} units.", this);
        foreach (Unit unit in _combatUnits)
        {
            if (unit != null && unit.IsAlive) 
            {
                unit.ResetActionPoints(); 
                unit.actionCounter = 0;   
            }
        }

        DebugHelper.Log("TurnManager: Combat started. Starting turn progression...", this);
        if (_advanceTurnsCoroutine != null)
        {
            StopCoroutine(_advanceTurnsCoroutine);
        }
        _advanceTurnsCoroutine = StartCoroutine(AdvanceTurnsCoroutine());
        UpdateTurnDisplay();
    }

    public void EndCombat()
    {
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
        DebugHelper.Log("TurnManager: Combat ended.", this);
        UpdateTurnDisplay();
    }

    private System.Collections.IEnumerator AdvanceTurnsCoroutine()
    {
        while (_isCombatActive)
        {
            if (ActiveUnit == null)
            {
                _combatUnits.RemoveAll(u => u == null || !u.IsAlive); 

                if (_combatUnits.Count == 0)
                {
                    DebugHelper.LogWarning("TurnManager: No (living) units left in combat. Stopping turn advancement.", this);
                    EndCombat();
                    yield break;
                }

                bool anyUnitReadyThisTick = false;
                int safetyCounter = 0;
                const int maxTicksWithoutReadyUnit = 10000; 

                while (!anyUnitReadyThisTick && _isCombatActive && safetyCounter < maxTicksWithoutReadyUnit)
                {
                    safetyCounter++;
                    List<Unit> unitsToProcess = new List<Unit>(_combatUnits); 
                    foreach (Unit unit in unitsToProcess) 
                    {
                        if (unit == null || !unit.IsAlive) continue;

                        if (unit.Stats != null) 
                        {
                            unit.actionCounter += unit.EffectiveSpeed;
                        }
                        else
                        {
                            DebugHelper.LogWarning($"TurnManager: Unit {unit.unitName} has null Stats component. Incrementing AC by 1.", unit);
                            unit.actionCounter += 1; 
                        }

                        if (unit.actionCounter >= ActionCounterThreshold)
                        {
                            anyUnitReadyThisTick = true;
                        }
                    }
                    if (!anyUnitReadyThisTick && _isCombatActive) 
                    {
                        UpdateTurnDisplay(); 
                        yield return null; 
                    }
                }

                if (safetyCounter >= maxTicksWithoutReadyUnit && !anyUnitReadyThisTick && _isCombatActive)
                {
                    DebugHelper.LogError("TurnManager: AdvanceTurnsCoroutine safety break! No unit became ready after many ticks.", this);
                    EndCombat();
                    yield break;
                }

                if (!_isCombatActive) yield break;

                List<Unit> readyUnits = _combatUnits
                    .Where(u => u != null && u.IsAlive && u.actionCounter >= ActionCounterThreshold && u.Stats != null && u.Stats.currentAttributes != null) 
                    .OrderByDescending(u => u.actionCounter)
                    .ThenByDescending(u => u.EffectiveSpeed)
                    .ThenByDescending(u => u.Stats.currentAttributes.Echo)
                    .ThenByDescending(u => u.Stats.currentAttributes.Glimmer)
                    .ToList();

                if (readyUnits.Count > 0)
                {
                    Unit nextUnit = readyUnits[0];
                    
                    if (_previousActiveUnitTile != null)
                    {
                        _previousActiveUnitTile.SetHighlight(TileHighlightState.None);
                    }

                    ActiveUnit = nextUnit; 
                    if (ActiveUnit == null || !ActiveUnit.IsAlive) 
                    {
                        ActiveUnit = null;
                        DebugHelper.LogWarning("TurnManager: Next chosen unit was null or not alive. Skipping its turn.", this);
                        UpdateTurnDisplay();
                        continue; 
                    }

                    ActiveUnit.ResetActionPoints(); 
                    
                    // NEW: Call resource regeneration
                    if (ActiveUnit.Stats != null)
                    {
                        ActiveUnit.Stats.RegenerateResourcesAtTurnStart();
                    }
                    else
                    {
                        DebugHelper.LogError($"TurnManager: ActiveUnit {ActiveUnit.unitName} is missing Stats component. Cannot regenerate resources.", ActiveUnit);
                    }


                    Tile activeUnitTile = (ActiveUnit.Movement != null) ? ActiveUnit.Movement.CurrentTile : null;
                    if (activeUnitTile != null) 
                    {
                        activeUnitTile.SetHighlight(TileHighlightState.ActiveTurnUnit);
                        _previousActiveUnitTile = activeUnitTile;
                    }
                    else
                    {
                        DebugHelper.LogWarning($"TurnManager: ActiveUnit {ActiveUnit.unitName} has no CurrentTile. Cannot set ActiveTurnUnit highlight.", ActiveUnit);
                        _previousActiveUnitTile = null;
                    }

                    DebugHelper.Log($"TurnManager: --- Unit {ActiveUnit.unitName}'s Turn Start --- AC: {ActiveUnit.actionCounter}, Speed: {ActiveUnit.EffectiveSpeed}, AP: {ActiveUnit.currentActionPoints}", ActiveUnit);
                    // Log for current MP/SP/FP/IP will come from UnitStats.RegenerateResourcesAtTurnStart()

                    UpdateTurnDisplay();

                    if (!ActiveUnit.CompareTag("Player"))
                    {
                        DebugHelper.Log($"TurnManager: Starting AI turn processing for {ActiveUnit.unitName}.", ActiveUnit);
                        if (ActiveUnit.AI != null)
                        {
                            StartCoroutine(ActiveUnit.AI.ProcessTurn());
                        }
                        else
                        {
                            DebugHelper.LogError($"{ActiveUnit.unitName} is AI but has no UnitAI component! Skipping turn.", ActiveUnit);
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
            DebugHelper.LogWarning($"TurnManager: EndUnitTurn called with a null unit.", this);
            UpdateTurnDisplay();
            return;
        }

        Tile unitCurrentTile = (unit.Movement != null) ? unit.Movement.CurrentTile : null;

        if (ActiveUnit != unit && unit.IsAlive) 
        {
            DebugHelper.LogWarning($"TurnManager: Unit '{unit.unitName}' tried to end turn, but is not the current ActiveUnit ('{ActiveUnit?.unitName ?? "None"}').", this);
            return;
        }

        if (ActiveUnit == unit || !unit.IsAlive) 
        {
            DebugHelper.Log($"TurnManager: --- Unit {unit.unitName}'s Turn End --- Resetting AC from {unit.actionCounter} to 0.", unit);
            if (_previousActiveUnitTile != null && (_previousActiveUnitTile == unitCurrentTile || !unit.IsAlive))
            {
                _previousActiveUnitTile.SetHighlight(TileHighlightState.None);
                _previousActiveUnitTile = null;
            }

            unit.actionCounter = 0;
            if (ActiveUnit == unit)
            {
                ActiveUnit = null;
            }
        }
        UpdateTurnDisplay();
    }

    private void UpdateTurnDisplay()
    {
        if (turnOrderUI != null)
        {
            if (_isCombatActive)
            {
                List<Unit> aliveCombatUnits = _combatUnits.Where(u => u != null && u.IsAlive).ToList(); 
                if (aliveCombatUnits.Count > 0)
                {
                    List<Unit> forecast = GetTurnOrderForecast(aliveCombatUnits);
                    turnOrderUI.UpdateTurnOrderDisplay(forecast, ActiveUnit);
                }
                else
                {
                    turnOrderUI.UpdateTurnOrderDisplay(new List<Unit>(), null);
                    if (_isCombatActive) 
                    {
                        DebugHelper.Log("TurnManager: All units defeated. Ending combat via UpdateTurnDisplay.", this);
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
        List<Unit> forecast = new List<Unit>();

        if (ActiveUnit != null && ActiveUnit.IsAlive) 
        {
            forecast.Add(ActiveUnit);
        }

        var sortedUpcoming = unitsToConsider
            .Where(u => u != ActiveUnit && u.IsAlive && u.Stats != null && u.Stats.currentAttributes != null) 
            .OrderByDescending(u => u.actionCounter >= ActionCounterThreshold)
            .ThenByDescending(u => u.actionCounter)
            .ThenByDescending(u => u.EffectiveSpeed) 
            .ThenByDescending(u => u.Stats.currentAttributes.Echo)
            .ThenByDescending(u => u.Stats.currentAttributes.Glimmer)
            .ToList();

        forecast.AddRange(sortedUpcoming);

        int maxSlots = (turnOrderUI != null) ? turnOrderUI.numberOfSlotsToDisplay : 5;
        return forecast.Take(maxSlots).ToList();
    }
}