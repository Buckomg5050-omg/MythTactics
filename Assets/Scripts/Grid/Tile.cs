// Tile.cs
using UnityEngine;

// Assuming TileHighlightState is defined globally or in a shared namespace
// public enum TileHighlightState { None, MovementRange, AttackRange, SelectedUnit, Hovered, Path, ActiveTurnUnit } 

public class Tile : MonoBehaviour
{
    [Header("State & Identification")]
    public Vector2Int gridPosition;
    public TerrainType currentTerrainType; // Should be derived from tileTypeData
    public int heightLevel = 0;

    [Header("Occupancy")]
    public Unit occupyingUnit = null;
    public bool IsOccupied => occupyingUnit != null;

    [Header("Data Source")]
    public TileTypeSO tileTypeData;

    [Header("Visuals & Highlighting")]
    [SerializeField]
    private SpriteRenderer _spriteRenderer;
    private TileHighlightState _currentHighlightState = TileHighlightState.None;
    public TileHighlightState CurrentHighlightState => _currentHighlightState;

    // NEW: Constant for impassable movement cost
    public const int IMPASSABLE_COST = 255;

    void Awake()
    {
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null && transform.childCount > 0) _spriteRenderer = GetComponentInChildren<SpriteRenderer>(); // Check children if not on parent
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
            this.currentTerrainType = data.type;
            this.name = $"Tile_{position.x}_{position.y} ({data.type})";
        }
        else
        {
            // Default terrain type if data is null, or handle as error
            this.currentTerrainType = default(TerrainType); 
            this.name = $"Tile_{position.x}_{position.y} (UnassignedType)";
            DebugHelper.LogWarning($"Tile at {position} initialized with null TileTypeSO data.", this);
        }
        this.heightLevel = height;
        UpdateVisualsFromData();
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
                // Fallback sprite or color if no data/sprite?
                _spriteRenderer.sprite = null; 
            }
        }
        // Re-apply current highlight state as base color might change if sprite changes
        SetHighlight(_currentHighlightState); 
    }

    public void SetHighlight(TileHighlightState newState)
    {
        if (_currentHighlightState != newState)
        {
            // if (newState != TileHighlightState.None || _currentHighlightState != TileHighlightState.None) {
            //      DebugHelper.Log($"Tile {gridPosition}: Highlight changing from {_currentHighlightState} to {newState}", this);
            // }
        }
        _currentHighlightState = newState;

        if (_spriteRenderer != null)
        {
            Color baseColor = Color.white; // Assuming base color is white, actual sprite color takes precedence

            // Apply tint based on highlight state
            // These colors should probably be defined in a central place (e.g., a ThemeSO or HighlightManager)
            // For now, hardcoded is fine.
            switch (newState)
            {
                case TileHighlightState.None: _spriteRenderer.color = baseColor; break;
                case TileHighlightState.MovementRange: _spriteRenderer.color = new Color(0.6f, 0.6f, 1f, 0.7f); break; // Lighter blue
                case TileHighlightState.AttackRange: _spriteRenderer.color = new Color(1f, 0.6f, 0.6f, 0.7f); break;   // Lighter red
                case TileHighlightState.SelectedUnit: _spriteRenderer.color = new Color(0.6f, 1f, 0.6f, 0.8f); break; // Lighter green
                case TileHighlightState.Hovered: _spriteRenderer.color = new Color(1f, 1f, 0.6f, 0.7f); break;     // Lighter yellow
                case TileHighlightState.Path: _spriteRenderer.color = new Color(0.9f, 0.5f, 1f, 0.75f); break;   // Purple-ish
                case TileHighlightState.ActiveTurnUnit: _spriteRenderer.color = new Color(1f, 0.85f, 0.4f, 0.85f); break; // Orange-Yellow
                default: _spriteRenderer.color = baseColor; break;
            }
        }
    }

    // MODIFIED: Added Unit parameter, though not used yet.
    public int GetMovementCost(Unit unit = null)
    {
        // Future: if (unit != null && unit.IsFlying) return 1; (except for specific blocking tiles)
        if (tileTypeData != null)
        {
            return tileTypeData.movementCost;
        }
        // If no tileTypeData, assume impassable
        DebugHelper.LogWarning($"Tile {gridPosition} has no TileTypeData. Returning IMPASSABLE_COST.", this);
        return IMPASSABLE_COST;
    }

    public void SetOccupyingUnit(Unit unit)
    {
        this.occupyingUnit = unit;
        // Optionally, if unit is not null, update unit's current tile reference:
        // if (unit != null) unit.InternalSetCurrentTile(this);
    }

    public void ClearOccupyingUnit()
    {
        // Optionally, if the old occupying unit had this tile as its current, clear that too.
        // if (this.occupyingUnit != null) this.occupyingUnit.InternalClearCurrentTileIfMatches(this);
        this.occupyingUnit = null;
    }

    // IsWalkableFor is used by PlayerInputHandler, Pathfinder uses GetMovementCost < IMPASSABLE_COST
    // This method can be kept for clarity or specific checks if needed.
    public bool IsWalkableFor(Unit unit) // unit parameter not currently used
    {
        if (tileTypeData == null) return false;
        // A tile is walkable if its movement cost is less than impassable.
        // Occupancy is checked separately by the calling system (Pathfinder or PlayerInputHandler).
        return GetMovementCost(unit) < IMPASSABLE_COST;
    }
}