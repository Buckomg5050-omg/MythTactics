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
                DebugHelper.Log($"TurnManager: Active unit '{unit.unitName}' was unregistered. Clearing ActiveUnit.", this);
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

        DebugHelper.Log($"TurnManager: Preparing for combat. Initializing {_combatUnits.Count} units.", this);
        foreach (Unit unit in _combatUnits)
        {
            unit.ResetActionPoints(); 
            unit.actionCounter = 0;
            DebugHelper.Log($"TurnManager: Initialized {unit.unitName} - AP: {unit.currentActionPoints}, AC: {unit.actionCounter}, Speed: {unit.EffectiveSpeed}", this);
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
        DebugHelper.Log("TurnManager: AdvanceTurnsCoroutine started.", this);
        while (_isCombatActive)
        {
            if (ActiveUnit == null) 
            {
                if (_combatUnits.Count == 0)
                {
                    DebugHelper.LogWarning("TurnManager: No units in combat. Stopping turn advancement.", this);
                    _isCombatActive = false; 
                    break;
                }

                // Increment action counters 
                // To avoid issues if a unit's EffectiveSpeed is 0 and they never get a turn,
                // ensure there's a minimum increment or handle it in speed calculation.
                // For now, assuming EffectiveSpeed is always >= 1 from Unit.cs.
                bool anyUnitReady = false;
                foreach (Unit unit in _combatUnits)
                {
                    unit.actionCounter += unit.EffectiveSpeed;
                    if (unit.actionCounter >= ActionCounterThreshold)
                    {
                        anyUnitReady = true;
                    }
                }
                // DebugHelper.Log("TurnManager: Action counters advanced.", this); // Can be too verbose

                if (anyUnitReady)
                {
                    List<Unit> readyUnits = _combatUnits
                        .Where(u => u.actionCounter >= ActionCounterThreshold)
                        .OrderByDescending(u => u.actionCounter)
                        .ThenByDescending(u => u.EffectiveSpeed)
                        // .ThenByDescending(u => u.currentAttributes.Echo) // TODO
                        // .ThenByDescending(u => u.currentAttributes.Glimmer) // TODO
                        .ToList();

                    if (readyUnits.Count > 0)
                    {
                        ActiveUnit = readyUnits[0]; // Select the top unit after sorting
                        ActiveUnit.ResetActionPoints(); 
                        DebugHelper.Log($"TurnManager: --- Unit {ActiveUnit.unitName}'s Turn Start --- AC: {ActiveUnit.actionCounter}, AP: {ActiveUnit.currentActionPoints}, Speed: {ActiveUnit.EffectiveSpeed}", ActiveUnit);
                        
                        // HACK FOR TESTING: Auto-end turn for non-player units
                        if (!ActiveUnit.CompareTag("Player")) 
                        {
                            DebugHelper.Log($"TurnManager (Hack): Auto-ending turn for non-player unit {ActiveUnit.unitName} in 1s.", this);
                            yield return new WaitForSeconds(1f); // Simulate AI thinking
                            EndUnitTurn(ActiveUnit); 
                        }
                    }
                }
            }
            yield return null; 
        }
        DebugHelper.Log("TurnManager: AdvanceTurnsCoroutine ended because combat is no longer active or no units left.", this);
        _advanceTurnsCoroutine = null;
    }

    public void EndUnitTurn(Unit unit)
    {
        if (unit == null)
        {
            DebugHelper.LogWarning($"TurnManager: EndUnitTurn called with a null unit.", this);
            return;
        }
        // Check if the unit trying to end its turn is actually the active one.
        // It's okay if ActiveUnit is already null (e.g. unit died and was unregistered during its own turn actions).
        if (unit == ActiveUnit || ActiveUnit == null) 
        {
            if (ActiveUnit != null) // Only log and reset AC if there was an active unit
            {
                 DebugHelper.Log($"TurnManager: --- Unit {ActiveUnit.unitName}'s Turn End --- Resetting AC from {ActiveUnit.actionCounter} to 0.", ActiveUnit);
                ActiveUnit.actionCounter = 0; 
            } else if (_combatUnits.Contains(unit)) { // If active unit was already null, but this unit is ending its turn somehow
                DebugHelper.Log($"TurnManager: Unit {unit.unitName} ended turn (ActiveUnit was null). Resetting AC.", unit);
                unit.actionCounter = 0;
            }
            
            ActiveUnit = null; 
        }
        else
        {
            DebugHelper.LogWarning($"TurnManager: Unit '{unit.unitName}' tried to end turn, but is not the current ActiveUnit ('{ActiveUnit?.unitName ?? "None"}'). Turn not ended by this call.", this);
        }
    }
}