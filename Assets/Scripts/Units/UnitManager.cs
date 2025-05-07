using System.Collections.Generic;
using UnityEngine;

public class UnitManager : MonoBehaviour
{
    private List<Unit> _units = new List<Unit>();
    private Unit _selectedUnit;

    public Unit SelectedUnit => _selectedUnit;

    public void RegisterUnit(Unit unit)
    {
        if (unit != null && !_units.Contains(unit))
        {
            _units.Add(unit);
            Debug.Log($"Registered unit at {unit.CurrentTile.GridPosition}");
        }
    }

    public void UnregisterUnit(Unit unit)
    {
        if (unit != null)
        {
            _units.Remove(unit);
            if (_selectedUnit == unit)
                _selectedUnit = null;
        }
    }

    public Unit GetUnitAt(Vector2Int gridPos)
    {
        return _units.Find(u => u.CurrentTile != null && u.CurrentTile.GridPosition == gridPos);
    }

    public void SelectUnit(Unit unit)
    {
        if (_selectedUnit != null)
            _selectedUnit.CurrentTile.SetHighlight(TileHighlight.None);
        _selectedUnit = unit;
        if (_selectedUnit != null)
        {
            _selectedUnit.CurrentTile.SetHighlight(TileHighlight.Selected);
            Debug.Log($"Selected unit at {_selectedUnit.CurrentTile.GridPosition}");
        }
    }

    public void DeselectUnit()
    {
        if (_selectedUnit != null)
        {
            _selectedUnit.CurrentTile.SetHighlight(TileHighlight.None);
            Debug.Log($"Deselected unit at {_selectedUnit.CurrentTile.GridPosition}");
            _selectedUnit = null;
        }
    }
}