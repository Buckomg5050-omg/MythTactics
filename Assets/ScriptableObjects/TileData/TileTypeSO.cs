// TileTypeSO.cs
using UnityEngine;

[CreateAssetMenu(fileName = "NewTileType", menuName = "MythTactics/Tile Type")]
public class TileTypeSO : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("The logical type of terrain this SO represents.")]
    public TerrainType type;

    [Tooltip("Display name for this terrain type (e.g., for UI tooltips).")]
    public string displayName = "Unnamed Terrain";

    [Header("Visuals")]
    [Tooltip("The sprite to be used for rendering this tile type on the grid.")]
    public Sprite tileSprite;

    [Header("Gameplay Properties")]
    [Tooltip("Movement points required to enter a tile of this type. GDD 1.2.")]
    public int movementCost = 1;

    [Tooltip("Evasion bonus granted to a unit occupying a tile of this type. GDD 2.3.")]
    public int evasionBonus = 0;
}