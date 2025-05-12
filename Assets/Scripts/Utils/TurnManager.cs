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
            unit.actionCounter = 0;
            DebugHelper.Log($"TurnManager: Registered unit '{unit.unitName}'. Total units: {_combatUnits.Count}", this);
        }
        else
        {
            DebugHelper.LogWarning($"TurnManager: Unit '{unit.unitName}' is already registered.", this);
        }
        if (_isCombatActive) UpdateTurnDisplay(); // Only update display if combat is active
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
                // If combat is active and active unit is removed, we might need to advance turn or check for combat end
                if (_isCombatActive && _advanceTurnsCoroutine == null) // If turn advancement isn't already running
                {
                    _advanceTurnsCoroutine = StartCoroutine(AdvanceTurnsCoroutine());
                }
            }
        }
        else
        {
            DebugHelper.LogWarning($"TurnManager: Unit '{unit.unitName}' was not found in combat units to unregister.", this);
        }
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

        DebugHelper.Log($"TurnManager: Preparing for combat. Initializing {_combatUnits.Count} units.", this);
        CombatLogger.LogTurnEvent("Combat Started!"); // ADDED Combat Log Message
        foreach (Unit unit in _combatUnits)
        {
            if (unit != null && unit.IsAlive)
            {
                unit.ResetForCombatStart(); 
                unit.actionCounter = 0;
                if (unit.Stats != null) unit.Stats.ClearAllEffects(); 
            }
        }

        DebugHelper.Log("TurnManager: Combat started. Starting turn progression...", this);
        if (_advanceTurnsCoroutine != null)
        {
            StopCoroutine(_advanceTurnsCoroutine);
        }
        _advanceTurnsCoroutine = StartCoroutine(AdvanceTurnsCoroutine());
        UpdateTurnDisplay(); // Initial display after setup
    }

    public void EndCombat()
    {
        if (!_isCombatActive) return; // Prevent multiple calls

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
        DebugHelper.Log("TurnManager: Combat ended. All unit effects cleared.", this);
        CombatLogger.LogTurnEvent("Combat Ended!"); // ADDED Combat Log Message
        UpdateTurnDisplay(); // Clear the display
    }

    private System.Collections.IEnumerator AdvanceTurnsCoroutine()
    {
        DebugHelper.Log("TurnManager: AdvanceTurnsCoroutine started.", this);
        yield return null; // Wait a frame to ensure all units are registered from StartCombat

        while (_isCombatActive)
        {
            if (ActiveUnit == null) // If no unit is currently active, find the next one
            {
                _combatUnits.RemoveAll(u => u == null || !u.IsAlive);

                if (_combatUnits.Count == 0)
                {
                    DebugHelper.LogWarning("TurnManager: No (living) units left in combat. Ending combat.", this);
                    EndCombat(); // This will set _isCombatActive to false, breaking the loop
                    yield break;
                }
                if (_combatUnits.All(u => !u.CompareTag("Player")) && _combatUnits.Count > 0) // Example: Only AI left
                {
                    DebugHelper.Log("TurnManager: Only AI units remain. Combat might end or continue based on game rules.", this);
                    // Potentially end combat here if that's a win/loss condition
                }
                 if (_combatUnits.All(u => u.CompareTag("Player")) && _combatUnits.Count > 0 && _combatUnits.Count < FindObjectsByType<Unit>(FindObjectsSortMode.None).Length) // Only Player units left, and some enemies were defeated
                {
                    DebugHelper.Log("TurnManager: Only Player units remain. Combat victory!", this);
                    EndCombat();
                    yield break;
                }


                bool anyUnitReadyThisTick = false;
                int safetyCounter = 0;
                const int maxTicksWithoutReadyUnit = 10000; // Increased for safety

                while (!anyUnitReadyThisTick && _isCombatActive && safetyCounter < maxTicksWithoutReadyUnit)
                {
                    safetyCounter++;
                    List<Unit> unitsToProcessThisTick = new List<Unit>(_combatUnits); // Copy for safe iteration if list changes
                    foreach (Unit unit in unitsToProcessThisTick)
                    {
                        if (unit == null || !unit.IsAlive) continue;

                        if (unit.Stats != null)
                        {
                            unit.actionCounter += unit.EffectiveSpeed;
                        }
                        else
                        {
                            DebugHelper.LogWarning($"TurnManager: Unit {unit.unitName} has null Stats. Incrementing AC by 1.", unit);
                            unit.actionCounter += 1;
                        }

                        if (unit.actionCounter >= ActionCounterThreshold)
                        {
                            anyUnitReadyThisTick = true;
                        }
                    }

                    if (!anyUnitReadyThisTick && _isCombatActive)
                    {
                        // UpdateTurnDisplay(); // Update display every tick if desired, can be spammy
                        yield return null; // Wait for next frame if no one is ready
                    }
                }

                if (safetyCounter >= maxTicksWithoutReadyUnit && !anyUnitReadyThisTick && _isCombatActive)
                {
                    DebugHelper.LogError("TurnManager: AdvanceTurnsCoroutine safety break! No unit became ready.", this);
                    EndCombat();
                    yield break;
                }

                if (!_isCombatActive) yield break; // Check again if EndCombat was called

                List<Unit> readyUnits = _combatUnits
                    .Where(u => u != null && u.IsAlive && u.actionCounter >= ActionCounterThreshold && u.Stats != null && u.Stats.EffectiveAttributes != null)
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
                    if (ActiveUnit == null || !ActiveUnit.IsAlive) // Should not happen due to Where clause
                    {
                        ActiveUnit = null;
                        DebugHelper.LogWarning("TurnManager: Next chosen unit was null or not alive after sort. Skipping turn.", this);
                        UpdateTurnDisplay(); // Refresh display as active unit is invalid
                        continue; // Skip to next iteration of while(ActiveUnit == null)
                    }

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
                    string turnStartMessage = $"Unit {ActiveUnit.unitName}'s Turn Start (AP: {currentAP}/{maxAP})";
                    DebugHelper.Log($"TurnManager: --- {turnStartMessage} --- AC: {ActiveUnit.actionCounter}, Speed: {ActiveUnit.EffectiveSpeed}", ActiveUnit);
                    CombatLogger.LogTurnEvent(turnStartMessage); // ADDED Combat Log Message

                    UpdateTurnDisplay(); // Update UI now that active unit is set

                    if (!ActiveUnit.CompareTag("Player"))
                    {
                        DebugHelper.Log($"TurnManager: Starting AI turn processing for {ActiveUnit.unitName}.", ActiveUnit);
                        if (ActiveUnit.AI != null)
                        {
                            StartCoroutine(ActiveUnit.AI.ProcessTurn()); // AI will call EndUnitTurn
                        }
                        else
                        {
                            DebugHelper.LogError($"{ActiveUnit.unitName} is AI but has no UnitAI component! Ending turn.", ActiveUnit);
                            EndUnitTurn(ActiveUnit); // End turn immediately if AI is broken
                        }
                    }
                    // For player units, PlayerInputHandler takes over. Combat continues when player ends turn.
                }
                else if (_isCombatActive) // No units ready, but combat is active (e.g. all units have low AC)
                {
                    // This case should be handled by the inner while loop's yield return null;
                    // If we reach here, it means the inner loop exited without anyUnitReadyThisTick but combat is still active.
                    // This could happen if all units die simultaneously or some other edge case.
                    DebugHelper.LogWarning("TurnManager: No units ready to act, but combat is active. Ticking again.", this);
                }
            }
            yield return null; // Wait for the active unit to finish its turn (Player or AI will call EndUnitTurn)
        }
        _advanceTurnsCoroutine = null;
        DebugHelper.Log("TurnManager: AdvanceTurnsCoroutine ended because combat is no longer active.", this);
    }


    public void EndUnitTurn(Unit unit)
    {
        if (unit == null)
        {
            DebugHelper.LogWarning($"TurnManager: EndUnitTurn called with a null unit.", this);
            // It's possible ActiveUnit was this null unit if something went wrong.
            if (ActiveUnit == null) ActiveUnit = null; // Redundant but safe.
            UpdateTurnDisplay(); // Update display even if unit is null to reflect changes.
            return;
        }

        Tile unitCurrentTile = unit.CurrentTile;

        if (ActiveUnit != unit && unit.IsAlive)
        {
            DebugHelper.LogWarning($"TurnManager: Unit '{unit.unitName}' (Alive: {unit.IsAlive}) tried to end turn, but is not the current ActiveUnit ('{ActiveUnit?.unitName ?? "None"}'). Ignoring.", this);
            return;
        }
        
        // Process end of turn for the unit that is ending its turn
        DebugHelper.Log($"TurnManager: --- Unit {unit.unitName}'s Turn End ---", unit);
        CombatLogger.LogTurnEvent($"Unit {unit.unitName}'s Turn End"); // ADDED Combat Log Message

        if (unit.IsAlive && unit.Stats != null && EffectSystem.Instance != null)
        {
            DebugHelper.Log($"TurnManager: Ticking effects for {unit.unitName} at end of turn.", unit);
            EffectSystem.Instance.TickUnitEffects(unit);
        }
        else if (unit.IsAlive && EffectSystem.Instance == null)
        {
             DebugHelper.LogError($"TurnManager: EffectSystem.Instance is null. Cannot tick effects for {unit.unitName}.", unit);
        }

        if (unitCurrentTile != null && unitCurrentTile == _previousActiveUnitTile) // If the unit is still on the tile it started on (or was last highlighted as active)
        {
            unitCurrentTile.SetHighlight(TileHighlightState.None); // Clear highlight
            _previousActiveUnitTile = null;
        }
        else if (_previousActiveUnitTile != null && ActiveUnit == unit) // If it's the active unit and it moved
        {
             _previousActiveUnitTile.SetHighlight(TileHighlightState.None); // Clear old tile
            _previousActiveUnitTile = null;
        }


        unit.actionCounter = 0; 

        if (ActiveUnit == unit) // If the unit ending its turn was indeed the active one
        {
            ActiveUnit = null; // Make way for the next unit selection
        }
        
        // Check for combat end conditions after a turn ends
        _combatUnits.RemoveAll(u => u == null || !u.IsAlive); // Clean up dead units
        if (_combatUnits.Count(u => u.CompareTag("Player") && u.IsAlive) == 0 && _isCombatActive)
        {
            DebugHelper.Log("TurnManager: All player units defeated. Ending combat (Player loss).", this);
            EndCombat();
        }
        else if (_combatUnits.Count(u => !u.CompareTag("Player") && u.IsAlive) == 0 && _isCombatActive)
        {
            DebugHelper.Log("TurnManager: All enemy units defeated. Ending combat (Player win).", this);
            EndCombat();
        }
        
        UpdateTurnDisplay(); // Update UI to reflect new turn order / potential combat end
    }

    private void UpdateTurnDisplay()
    {
        if (turnOrderUI != null)
        {
            if (_isCombatActive)
            {
                // Ensure we only consider units that are actually part of the current combat
                List<Unit> aliveAndRegisteredUnits = _combatUnits.Where(u => u != null && u.IsAlive).ToList();
                
                if (aliveAndRegisteredUnits.Count > 0)
                {
                    List<Unit> forecast = GetTurnOrderForecast(aliveAndRegisteredUnits);
                    turnOrderUI.UpdateTurnOrderDisplay(forecast, ActiveUnit); // Pass current ActiveUnit
                }
                else // No living units left
                {
                    turnOrderUI.UpdateTurnOrderDisplay(new List<Unit>(), null);
                    // EndCombat might have already been called if this was the last unit
                    if (_isCombatActive) // Check again, as EndCombat sets it to false
                    {
                         DebugHelper.Log("TurnManager: All units defeated (checked in UpdateTurnDisplay). Ending combat.", this);
                         EndCombat();
                    }
                }
            }
            else // Combat is not active
            {
                turnOrderUI.UpdateTurnOrderDisplay(new List<Unit>(), null); // Clear the display
            }
        }
    }

    private List<Unit> GetTurnOrderForecast(List<Unit> unitsToConsider)
    {
        List<Unit> forecast = new List<Unit>();

        // Create a temporary list of units with their *current* action counters for this forecast
        List<(Unit unit, int ac)> tempUnitAC = unitsToConsider.Select(u => (u, u.actionCounter)).ToList();
        
        // Simulate ticks if no one is at threshold yet, to get a more accurate short-term forecast
        // This is a simplified simulation; a more complex one might run more ticks.
        int simulationTicks = 0;
        const int maxSimulationTicks = 500; // Prevent infinite loop if speeds are zero

        // Check if the current ActiveUnit (if any) is already at threshold or beyond
        bool activeUnitIsReady = (ActiveUnit != null && ActiveUnit.IsAlive && ActiveUnit.actionCounter >= ActionCounterThreshold);

        // If no one is ready (or only the active unit is, but we want to see who's next after them), simulate some ticks
        while (!tempUnitAC.Any(uac => uac.ac >= ActionCounterThreshold && uac.unit != ActiveUnit) && simulationTicks < maxSimulationTicks && unitsToConsider.Count > (activeUnitIsReady ? 1 : 0) )
        {
            bool changed = false;
            for (int i = 0; i < tempUnitAC.Count; i++)
            {
                if (tempUnitAC[i].unit.IsAlive) // Only advance AC for living units
                {
                    var current = tempUnitAC[i];
                    tempUnitAC[i] = (current.unit, current.ac + current.unit.EffectiveSpeed);
                    changed = true;
                }
            }
            if (!changed) break; // All units might have 0 speed
            simulationTicks++;
        }


        // Add current active unit first if it exists and is alive
        if (ActiveUnit != null && ActiveUnit.IsAlive && unitsToConsider.Contains(ActiveUnit))
        {
            forecast.Add(ActiveUnit);
        }

        // Sort other units based on their (potentially simulated) action counters
        var sortedUpcoming = tempUnitAC
            .Where(uac => uac.unit != ActiveUnit && uac.unit.IsAlive && uac.unit.Stats != null && uac.unit.Stats.EffectiveAttributes != null)
            .OrderByDescending(uac => uac.ac >= ActionCounterThreshold) // Prioritize those at/over threshold
            .ThenByDescending(uac => uac.ac)                          // Then by highest AC
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