// MapDataSO.cs
using UnityEngine;
using System.Collections.Generic;

// This struct should be defined within or accessible to MapDataSO
[System.Serializable]
public struct MapTileData
{
    [Tooltip("Playable grid coordinates (e.g., 0,0 is bottom-left of playable area).")]
    public Vector2Int position;
    [Tooltip("The TileTypeSO defining this specific tile's properties.")]
    public TileTypeSO tileType;
    [Tooltip("The height level of this tile, if your game uses height mechanics.")]
    public int heightLevel; // Default to 0 if not using height extensively

    // Constructor for easier programmatic creation if needed, not strictly necessary for Inspector setup
    public MapTileData(Vector2Int pos, TileTypeSO type, int height = 0)
    {
        position = pos;
        tileType = type;
        heightLevel = height;
    }
}

[CreateAssetMenu(fileName = "NewMapData", menuName = "MythTactics/Grid/Map Data")]
public class MapDataSO : ScriptableObject
{
    [Header("Map Identification")]
    [Tooltip("A descriptive name for this map (e.g., 'Forest Clearing', 'Mountain Pass').")]
    public string mapDisplayName = "Unnamed Map";
    // public Sprite mapPreviewImage; // For a map selection screen later

    [Header("Grid Dimensions")]
    [Tooltip("The width of the playable area of the map in tiles.")]
    [Min(1)]
    public int playableWidth = 15; // Default to a smaller, more manageable size for testing
    [Tooltip("The height of the playable area of the map in tiles.")]
    [Min(1)]
    public int playableHeight = 10;

    [Header("Default Tile Types")]
    [Tooltip("The TileTypeSO used for all playable tiles unless overridden by 'Specific Tiles' data below. Typically 'Plains' or similar.")]
    public TileTypeSO defaultPlayableTile;
    [Tooltip("The TileTypeSO used for the impassable boundary automatically generated around the playable map area.")]
    public TileTypeSO boundaryTile;

    [Header("Map Layout - Specific Tiles")]
    [Tooltip("Define individual tiles that differ from the 'Default Playable Tile'. This is where you design your map's terrain features.")]
    public List<MapTileData> specificTiles;

    // [Header("Deployment & Encounter Zones (Future)")]
    // public List<Vector2Int> playerSpawnPoints;
    // public List<Vector2Int> enemySpawnPoints;
    // public List<EncounterZoneData> encounterZones; // Could define areas that trigger specific encounters
}
