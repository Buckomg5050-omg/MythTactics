// TileTypeSO.cs
using UnityEngine;

// REMOVED TerrainType enum from here - it should be in its own file (e.g., Assets/Scripts/Grid/TerrainType.cs)

[CreateAssetMenu(fileName = "NewTileType", menuName = "MythTactics/Grid/Tile Type")]
public class TileTypeSO : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("The logical type of terrain this SO represents. Determines behavior and interaction rules.")]
    public TerrainType type = TerrainType.Plains; // This will use the globally accessible TerrainType enum

    [Tooltip("Display name for this terrain type (e.g., for UI tooltips or debug information).")]
    public string displayName = "Unnamed Terrain";

    [Header("Visuals")]
    [Tooltip("The sprite to be used for rendering this tile type on the grid.")]
    public Sprite tileSprite;

    [Header("Gameplay Properties")]
    // MODIFIED: Simplified tooltip string for CS0182 error
    [Tooltip("Movement points required to enter a tile of this type. Use 255 for impassable terrain.")]
    public int movementCost = 1;

    [Tooltip("Evasion bonus granted to a unit occupying a tile of this type (e.g., for cover). GDD 2.3.")]
    public int evasionBonus = 0;

    // [Header("Advanced Properties (Future Implementation)")]
    // ... (other commented out fields)
}