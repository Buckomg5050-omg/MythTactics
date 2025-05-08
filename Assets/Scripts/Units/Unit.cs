// Unit.cs
using UnityEngine;

/// <summary>
/// A very basic placeholder for a unit on the grid.
/// Initially, its main purpose is to mark tile occupancy.
/// This will be expanded significantly later as per GDD Section 6.
/// </summary>
public class Unit : MonoBehaviour
{
    // We will add many properties later: stats, currentVP, currentAP, faction, etc.

    [Header("Basic Info")]
    public string unitName = "Unit";

    // Reference to the tile this unit is currently on.
    // This might be managed by a UnitManager or the Unit itself.
    // For now, it's useful for quick reference.
    private Tile _currentTile;
    public Tile CurrentTile => _currentTile;

    /// <summary>
    /// Sets the unit's current tile.
    /// Placeholder: More robust logic will handle unoccupying old tile / occupying new tile.
    /// </summary>
    public void SetCurrentTile(Tile tile)
    {
        // In a full system, we'd also tell the old tile it's no longer occupied
        // and the new tile it is now occupied.
        _currentTile = tile;
        if (tile != null)
        {
            // For visual debugging, place the unit's transform at the tile's world position
            // Assuming the unit's visual pivot is at its base.
            transform.position = tile.transform.position;
            // Could also update a logical gridPosition on the unit itself.
        }
        // DebugHelper.Log($"{unitName} moved to tile {tile?.gridPosition}", this);
    }


    // Example of how it might be created later (not used yet)
    // public void Initialize(string name /*, UnitTemplateSO template, int level etc. */)
    // {
    //     this.unitName = name;
    //     // ... more initialization
    // }
}