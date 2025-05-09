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
    }

    public void StartCombat()
    {
        if (_combatUnits.Count == 0)
        {
            DebugHelper.LogWarning("TurnManager: StartCombat called with no units registered. Combat not starting.", this);
            _isCombatActive = false;
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
            // This log is useful for setup verification, so keeping it for now.
            DebugHelper.Log($"TurnManager: Initialized {unit.unitName} - AP: {unit.currentActionPoints}, AC: {unit.actionCounter}, Speed: {unit.EffectiveSpeed}, Echo: {unit.currentAttributes.Echo}, Glimmer: {unit.currentAttributes.Glimmer}", this);
        }
        
        DebugHelper.Log("TurnManager: Combat started. Starting turn progression...", this);
        if (_advanceTurnsCoroutine != null)
        {
            StopCoroutine(_advanceTurnsCoroutine);
        }
        _advanceTurnsCoroutine = StartCoroutine(AdvanceTurnsCoroutine());
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
    }

    private System.Collections.IEnumerator AdvanceTurnsCoroutine()
    {
        // DebugHelper.Log("TurnManager: AdvanceTurnsCoroutine started.", this); // Can be commented out after initial verification
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
                while (!anyUnitReadyThisTick && _isCombatActive) 
                {
                    foreach (Unit unit in _combatUnits)
                    {
                        unit.actionCounter += unit.EffectiveSpeed;
                        // DebugHelper.Log($"TurnManager - AC Update: {unit.unitName} AC = {unit.actionCounter} (Added {unit.EffectiveSpeed})", unit); // VERBOSE
                        if (unit.actionCounter >= ActionCounterThreshold)
                        {
                            anyUnitReadyThisTick = true;
                        }
                    }
                    if (!anyUnitReadyThisTick) yield return null; 
                }
                if (!_isCombatActive) yield break; 

                List<Unit> readyUnits = _combatUnits
                    .Where(u => u.actionCounter >= ActionCounterThreshold)
                    .OrderByDescending(u => u.actionCounter)
                    .ThenByDescending(u => u.EffectiveSpeed)
                    .ThenByDescending(u => u.currentAttributes?.Echo ?? 0)    // Added Echo (null check for safety)
                    .ThenByDescending(u => u.currentAttributes?.Glimmer ?? 0) // Added Glimmer (null check for safety)
                    // TODO: Add random/fixed tie-breaker if still tied (e.g., .ThenBy(u => _combatUnits.IndexOf(u)) for fixed order)
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
                    
                    if (!ActiveUnit.CompareTag("Player")) 
                    {
                        DebugHelper.Log($"TurnManager (Hack): Auto-ending turn for non-player unit {ActiveUnit.unitName} in 1s.", this);
                        yield return new WaitForSeconds(1f); 
                        if (ActiveUnit == nextUnit && _isCombatActive) { // Ensure unit is still active and combat is ongoing
                            EndUnitTurn(ActiveUnit); 
                        }
                    }
                }
            }
            yield return null; 
        }
        // DebugHelper.Log("TurnManager: AdvanceTurnsCoroutine loop exited.", this); // Can be commented out
        _advanceTurnsCoroutine = null;
    }

    public void EndUnitTurn(Unit unit)
    {
        if (unit == null)
        {
            DebugHelper.LogWarning($"TurnManager: EndUnitTurn called with a null unit.", this);
            return;
        }
        
        if (unit == ActiveUnit) 
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
        // If ActiveUnit was already null (e.g. unit died during its turn and was unregistered)
        // but this unit somehow calls EndUnitTurn (e.g. a delayed call), ensure its AC is reset if it was the one acting.
        else if (ActiveUnit == null && _combatUnits.Contains(unit) && unit.actionCounter >= ActionCounterThreshold) 
        {
             DebugHelper.LogWarning($"TurnManager: {unit.unitName} called EndUnitTurn, but ActiveUnit was already null. Resetting its AC from {unit.actionCounter} to 0 just in case.", unit);
             unit.actionCounter = 0;
        }
        else
        {
            DebugHelper.LogWarning($"TurnManager: Unit '{unit.unitName}' tried to end turn, but is not the current ActiveUnit ('{ActiveUnit?.unitName ?? "None"}'). Turn not ended by this call.", this);
        }
    }
}