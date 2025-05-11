// Tile.cs
using UnityEngine;

public class Tile : MonoBehaviour
{
    [Header("State & Identification")]
    public Vector2Int gridPosition;
    public TerrainType currentTerrainType; 
    public int heightLevel = 0;

    [Header("Occupancy")]
    public Unit occupyingUnit = null;
    public bool IsOccupied => occupyingUnit != null;

    [Header("Data Source")]
    public TileTypeSO tileTypeData; // This SO should define movementCost and other terrain properties

    [Header("Visuals & Highlighting")]
    [SerializeField]
    private SpriteRenderer _spriteRenderer;
    private TileHighlightState _currentHighlightState = TileHighlightState.None;
    public TileHighlightState CurrentHighlightState => _currentHighlightState; // Public getter for current highlight state

    public const int IMPASSABLE_COST = 255; // Standard value for impassable from GDD

    void Awake()
    {
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null && transform.childCount > 0) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer == null)
        {
            DebugHelper.LogError($"Tile at {gridPosition} (Obj: {this.name}) MISSING SpriteRenderer component.", this);
        }
    }

    public void Initialize(Vector2Int position, TileTypeSO data, int height = 0)
    {
        this.gridPosition = position;
        this.tileTypeData = data;
        if (data != null)
        {
            this.currentTerrainType = data.type; // Assuming TileTypeSO has a 'type' field of enum TerrainType
            this.name = $"Tile_{position.x}_{position.y} ({data.type})";
        }
        else
        {
            this.currentTerrainType = default(TerrainType); 
            this.name = $"Tile_{position.x}_{position.y} (UnassignedType)";
            DebugHelper.LogWarning($"Tile at {position} initialized with null TileTypeSO data.", this);
        }
        this.heightLevel = height;
        UpdateVisualsFromData();
        _currentHighlightState = TileHighlightState.None; // Ensure it starts as None
    }

    public void UpdateVisualsFromData()
    {
        if (_spriteRenderer != null)
        {
            if (tileTypeData != null && tileTypeData.tileSprite != null)
            {
                _spriteRenderer.sprite = tileTypeData.tileSprite;
            }
            else
            {
                _spriteRenderer.sprite = null; // Or a default "unknown" sprite
            }
        }
        SetHighlight(_currentHighlightState); // Re-apply current highlight (e.g. if visuals were rebuilt)
    }

    public void SetHighlight(TileHighlightState newState)
    {
        _currentHighlightState = newState; // Update the internal state tracker

        if (_spriteRenderer != null)
        {
            Color baseColor = Color.white; // Assuming base sprite color is white or you manage it differently

            switch (newState)
            {
                case TileHighlightState.None: _spriteRenderer.color = baseColor; break;
                case TileHighlightState.MovementRange: _spriteRenderer.color = new Color(0.6f, 0.6f, 1f, 0.7f); break; 
                case TileHighlightState.AttackRange: _spriteRenderer.color = new Color(1f, 0.6f, 0.6f, 0.7f); break;   
                case TileHighlightState.SelectedUnit: _spriteRenderer.color = new Color(0.6f, 1f, 0.6f, 0.8f); break; 
                case TileHighlightState.Hovered: _spriteRenderer.color = new Color(1f, 1f, 0.6f, 0.7f); break;     
                case TileHighlightState.Path: _spriteRenderer.color = new Color(0.9f, 0.5f, 1f, 0.75f); break;   
                case TileHighlightState.ActiveTurnUnit: _spriteRenderer.color = new Color(1f, 0.85f, 0.4f, 0.85f); break;
                case TileHighlightState.AbilityRange: _spriteRenderer.color = new Color(1f, 0.92f, 0.016f, 0.75f); break; 
                default: _spriteRenderer.color = baseColor; break;
            }
        }
        // DebugHelper.Log($"Tile {gridPosition} highlight set to {newState}", this);
    }

    /// <summary>
    /// Gets the movement cost for this tile. Considers the TileTypeSO.
    /// Unit parameter can be used for unit-specific costs (e.g., flying).
    /// </summary>
    public int GetMovementCost(Unit unit = null) // unit parameter is optional for now
    {
        if (tileTypeData != null)
        {
            // TODO: Future - if unit is not null, check for specific interactions
            // e.g., if (unit.IsFlying && tileTypeData.isWater) return 1;
            return tileTypeData.movementCost;
        }
        DebugHelper.LogWarning($"Tile {gridPosition} has no TileTypeData. Returning IMPASSABLE_COST.", this);
        return IMPASSABLE_COST;
    }

    public void SetOccupyingUnit(Unit unit)
    {
        this.occupyingUnit = unit;
    }

    public void ClearOccupyingUnit()
    {
        this.occupyingUnit = null;
    }

    /// <summary>
    /// Checks if the tile is generally walkable based on its terrain cost.
    /// Does not check for occupancy here; that's separate.
    /// </summary>
    public bool IsWalkableFor(Unit unit) // unit parameter can be used for specific checks
    {
        if (tileTypeData == null) return false;
        return GetMovementCost(unit) < IMPASSABLE_COST;
    }

    /// <summary>
    /// NEW METHOD: Checks if the tile is occupied by another unit OR if the terrain is impassable.
    /// </summary>
    /// <param name="unit">The unit attempting to check this tile (can be null if just checking general impassability).</param>
    /// <returns>True if occupied by another unit or if terrain is impassable.</returns>
    public bool IsOccupiedOrImpassableFor(Unit unit)
    {
        // Check if occupied by another unit (different from the one asking, if 'unit' is provided)
        if (this.occupyingUnit != null && (unit == null || this.occupyingUnit != unit))
        {
            return true; // Tile is occupied by someone else
        }

        // Check if terrain itself is impassable
        if (GetMovementCost(unit) >= IMPASSABLE_COST)
        {
            // Optional: Add unit-specific traversal checks here if a unit is provided
            // e.g., if (unit != null && unit.CanFly() && tileTypeData.AllowsFlyingOverImpassableTerrain) return false;
            return true; // Terrain is impassable
        }

        return false; // Tile is not occupied by another unit (or is occupied by 'unit') and terrain is passable
    }
}

// Ensure TileHighlightState enum is defined and accessible
// (Often in its own file, or alongside related enums)
/*
public enum TileHighlightState
{
    None,
    MovementRange,
    AttackRange,
    AbilityRange,
    SelectedUnit,
    Path,
    Hovered,
    ActiveTurnUnit // Example of another state you might have
}
*/

// Ensure TerrainType enum is defined and accessible (matching TileTypeSO.type)
/*
public enum TerrainType 
{
    Plains, 
    Forest, 
    Hills, 
    RockyGround, 
    Swamp, 
    ShallowWater, 
    MountainPeak, 
    DeepWater, 
    Boundary 
    // Add any other types you have
}
*/