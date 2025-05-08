// TileTypeSO.cs
using UnityEngine;

/// <summary>
/// ScriptableObject defining the properties for a specific type of terrain tile.
/// As per GDD 5.2.1.
/// </summary>
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
    public Sprite tileSprite; // We'll need to assign actual sprites later

    [Header("Gameplay Properties")]
    [Tooltip("Movement points required to enter a tile of this type. GDD 1.2.")]
    public int movementCost = 1;

    [Tooltip("Evasion bonus granted to a unit occupying a tile of this type. GDD 2.3.")]
    public int evasionBonus = 0;

    // Potential future properties from GDD 5.2.1:
    // public bool isFlammable;
    // public bool isDestructible;
    // public TileTypeSO transformToOnBurn;
    // public EffectSO onTurnStartEffect; // EffectSO would be another ScriptableObject
    // public EffectSO onOccupyEffect;   // EffectSO would be another ScriptableObject
}