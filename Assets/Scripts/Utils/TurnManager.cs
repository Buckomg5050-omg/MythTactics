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
            turnOrderUI = FindObjectOfType<TurnOrderUI>(); 
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
        UpdateTurnDisplay(); // Update display when units change
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
        UpdateTurnDisplay(); // Update display when units change
    }

    public void StartCombat()
    {
        if (_combatUnits.Count == 0)
        {
            DebugHelper.LogWarning("TurnManager: StartCombat called with no units registered. Combat not starting.", this);
            _isCombatActive = false;
            UpdateTurnDisplay(); // Clear UI if combat doesn't start
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
            unit.ResetActionPoints(); 
            unit.actionCounter = 0;
            DebugHelper.Log($"TurnManager: Initialized {unit.unitName} - AP: {unit.currentActionPoints}, AC: {unit.actionCounter}, Speed: {unit.EffectiveSpeed}, Echo: {unit.currentAttributes?.Echo}, Glimmer: {unit.currentAttributes?.Glimmer}", this);
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
        // DebugHelper.Log("TurnManager: AdvanceTurnsCoroutine started.", this);
        while (_isCombatActive)
        {
            if (ActiveUnit == null) 
            {
                if (_combatUnits.Count == 0)
                {
                    DebugHelper.LogWarning("TurnManager: No units left in combat. Stopping turn advancement.", this);
                    EndCombat(); 
                    yield break; 
                }
                
                bool anyUnitReadyThisTick = false;
                int safetyCounter = 0; // To prevent infinite loop if all speeds are 0
                const int maxTicksWithoutReadyUnit = 5000; // Arbitrary large number

                while (!anyUnitReadyThisTick && _isCombatActive && safetyCounter < maxTicksWithoutReadyUnit) 
                {
                    safetyCounter++;
                    foreach (Unit unit in _combatUnits)
                    {
                        // Ensure unit has attributes to prevent null ref on EffectiveSpeed if it was somehow added without them
                        if(unit.currentAttributes != null)
                            unit.actionCounter += unit.EffectiveSpeed; 
                        else
                            unit.actionCounter += 1; // Default to minimal increment if attributes are missing

                        if (unit.actionCounter >= ActionCounterThreshold)
                        {
                            anyUnitReadyThisTick = true;
                        }
                    }
                    if (!anyUnitReadyThisTick) yield return null; 
                }

                if (safetyCounter >= maxTicksWithoutReadyUnit && !anyUnitReadyThisTick)
                {
                    DebugHelper.LogError("TurnManager: AdvanceTurnsCoroutine safety break! No unit became ready after many ticks. Check unit speeds or combat end conditions.", this);
                    EndCombat();
                    yield break;
                }

                if (!_isCombatActive) yield break; 

                List<Unit> readyUnits = _combatUnits
                    .Where(u => u.actionCounter >= ActionCounterThreshold)
                    .OrderByDescending(u => u.actionCounter)
                    .ThenByDescending(u => u.EffectiveSpeed)
                    .ThenByDescending(u => u.currentAttributes?.Echo ?? 0)    
                    .ThenByDescending(u => u.currentAttributes?.Glimmer ?? 0) 
                    .ToList();

                if (readyUnits.Count > 0)
                {
                    Unit nextUnit = readyUnits[0]; 

                    if (_previousActiveUnitTile != null)
                    {
                        _previousActiveUnitTile.SetHighlight(TileHighlightState.None);
                    }

                    ActiveUnit = nextUnit;
                    ActiveUnit.ResetActionPoints(); 
                    
                    if (ActiveUnit.CurrentTile != null)
                    {
                        ActiveUnit.CurrentTile.SetHighlight(TileHighlightState.ActiveTurnUnit);
                        _previousActiveUnitTile = ActiveUnit.CurrentTile;
                    } else {
                        DebugHelper.LogWarning($"TurnManager: ActiveUnit {ActiveUnit.unitName} has no CurrentTile. Cannot set ActiveTurnUnit highlight.", ActiveUnit);
                        _previousActiveUnitTile = null;
                    }

                    DebugHelper.Log($"TurnManager: --- Unit {ActiveUnit.unitName}'s Turn Start --- AC: {ActiveUnit.actionCounter}, Speed: {ActiveUnit.EffectiveSpeed}, Echo: {ActiveUnit.currentAttributes?.Echo}, Glimmer: {ActiveUnit.currentAttributes?.Glimmer}, AP: {ActiveUnit.currentActionPoints}", ActiveUnit);
                    UpdateTurnDisplay(); 
                        
                    if (!ActiveUnit.CompareTag("Player")) 
                    {
                        DebugHelper.Log($"TurnManager (Hack): Auto-ending turn for non-player unit {ActiveUnit.unitName} in 1s.", this);
                        yield return new WaitForSeconds(1f); 
                        if (ActiveUnit == nextUnit && _isCombatActive) { 
                            EndUnitTurn(ActiveUnit); 
                        }
                    }
                }
            }
            yield return null; 
        }
        // DebugHelper.Log("TurnManager: AdvanceTurnsCoroutine loop exited.", this);
        _advanceTurnsCoroutine = null;
    }

    public void EndUnitTurn(Unit unit)
    {
        if (unit == null)
        {
            DebugHelper.LogWarning($"TurnManager: EndUnitTurn called with a null unit.", this);
            UpdateTurnDisplay(); // Update UI in case this affects turn order
            return;
        }
        
        bool wasActiveUnit = (unit == ActiveUnit);

        if (wasActiveUnit) 
        {
            DebugHelper.Log($"TurnManager: --- Unit {ActiveUnit.unitName}'s Turn End --- Resetting AC from {ActiveUnit.actionCounter} to 0.", ActiveUnit);
            if (_previousActiveUnitTile != null && _previousActiveUnitTile == ActiveUnit.CurrentTile)
            {
                _previousActiveUnitTile.SetHighlight(TileHighlightState.None); 
                _previousActiveUnitTile = null;
            }
            ActiveUnit.actionCounter = 0; 
            ActiveUnit = null; 
        }
        else if (ActiveUnit == null && _combatUnits.Contains(unit) && unit.actionCounter >= ActionCounterThreshold) 
        {
             DebugHelper.LogWarning($"TurnManager: {unit.unitName} called EndUnitTurn, but ActiveUnit was already null (perhaps it died and was unregistered). Resetting its AC from {unit.actionCounter} to 0 just in case it was the one that should have ended.", unit);
             unit.actionCounter = 0; // Prevent immediate re-turn if it was about to go
        }
        else
        {
            DebugHelper.LogWarning($"TurnManager: Unit '{unit.unitName}' tried to end turn, but is not the current ActiveUnit ('{ActiveUnit?.unitName ?? "None"}'). Turn not ended by this call.", this);
        }

        if(wasActiveUnit || ActiveUnit == null) // If turn truly ended or active unit is now null
        {
            UpdateTurnDisplay(); 
        }
    }

    private void UpdateTurnDisplay()
    {
        if (turnOrderUI != null) // Check if UI is assigned
        {
            if (_isCombatActive && _combatUnits.Count > 0)
            {
                List<Unit> forecast = GetTurnOrderForecast();
                turnOrderUI.UpdateTurnOrderDisplay(forecast, ActiveUnit);
            }
            else // Combat not active or no units, clear the display
            {
                 turnOrderUI.UpdateTurnOrderDisplay(new List<Unit>(), null);
            }
        }
    }
    
    private List<Unit> GetTurnOrderForecast()
    {
        List<Unit> forecast = new List<Unit>();

        // If a unit is currently active, it's the first in the forecast.
        if (ActiveUnit != null)
        {
            forecast.Add(ActiveUnit);
        }

        // For the rest of the slots, predict based on current AC and speed.
        // This is a simplified forecast; a more accurate one would simulate AC accumulation.
        var sortedUpcoming = _combatUnits
            .Where(u => u != ActiveUnit) // Exclude the already active unit
            // Primary sort: units already over threshold, then by how much over
            // Secondary sort: units not yet over threshold, by how "close" they are (higher AC is closer)
            // Then by speed and other tie-breakers
            .OrderByDescending(u => u.actionCounter >= ActionCounterThreshold) // True (1) before False (0)
            .ThenByDescending(u => u.actionCounter) 
            .ThenByDescending(u => u.EffectiveSpeed)
            .ThenByDescending(u => u.currentAttributes?.Echo ?? 0)
            .ThenByDescending(u => u.currentAttributes?.Glimmer ?? 0)
            .ToList();
        
        forecast.AddRange(sortedUpcoming);

        // Ensure we don't try to display more units than we have UI slots for.
        int maxSlots = (turnOrderUI != null) ? turnOrderUI.numberOfSlotsToDisplay : 5;
        if (forecast.Count > maxSlots)
        {
            return forecast.GetRange(0, maxSlots);
        }
        return forecast;
    }
}