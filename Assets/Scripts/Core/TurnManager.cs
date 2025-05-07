using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    private List<Unit> _units = new List<Unit>();
    private int _currentTurnIndex = -1;
    private Unit _activeUnit;

    public event Action<Unit> OnTurnStart;
    public event Action<Unit> OnTurnEnd;
    public Unit ActiveUnit => _activeUnit;

    private void Awake()
    {
        UnitManager unitManager = FindFirstObjectByType<UnitManager>();
        unitManager.OnUnitRegistered += RegisterUnit;
        unitManager.OnUnitUnregistered += UnregisterUnit;
    }

    private void Start()
    {
        CalculateTurnOrder();
        StartNextTurn();
    }

    private void RegisterUnit(Unit unit)
    {
        if (!_units.Contains(unit))
        {
            _units.Add(unit);
            Debug.Log($"TurnManager: Registered unit {unit.name} for turn order.");
            CalculateTurnOrder();
        }
    }

    private void UnregisterUnit(Unit unit)
    {
        if (_units.Contains(unit))
        {
            _units.Remove(unit);
            Debug.Log($"TurnManager: Unregistered unit {unit.name} from turn order.");
            CalculateTurnOrder();
        }
    }

    public void CalculateTurnOrder()
    {
        _units = _units.OrderByDescending(u => u.GetInitiative()).ToList();
        Debug.Log("Turn order recalculated: " + string.Join(", ", _units.Select(u => $"{u.name} (Init: {u.GetInitiative()})")));
    }

    public void EndTurn()
    {
        if (_activeUnit != null)
        {
            OnTurnEnd?.Invoke(_activeUnit);
            Debug.Log($"Turn ended for {_activeUnit.name}.");
            _activeUnit = null;
        }
        StartNextTurn();
    }

    private void StartNextTurn()
    {
        _currentTurnIndex = (_currentTurnIndex + 1) % _units.Count;
        if (_units.Count == 0)
        {
            Debug.LogWarning("No units available for turn order.");
            return;
        }

        _activeUnit = _units[_currentTurnIndex];
        OnTurnStart?.Invoke(_activeUnit);
        Debug.Log($"Turn started for {_activeUnit.name} (Initiative: {_activeUnit.GetInitiative()}).");
    }
}