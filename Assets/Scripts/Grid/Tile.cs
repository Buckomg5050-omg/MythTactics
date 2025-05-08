// Tile.cs
using UnityEngine;

public class Tile : MonoBehaviour
{
    [Header("State & Identification")]
    public Vector2Int gridPosition;
    public TerrainType currentTerrainType;
    public int heightLevel = 0;
    [Header("Occupancy")]
    [Tooltip("Reference to the unit currently occupying this tile, if any.")]
    public Unit occupyingUnit = null; // Default to null
    // GDD 5.1.2: OccupyingUnit (Unit) - We'll add this later when the Unit class exists
    // public Unit occupyingUnit;
    public bool IsOccupied
    {
        get
        {
            // return occupyingUnit != null; // When Unit class exists
           return occupyingUnit != null;
        }
    }

    [Header("Data Source")]
    public TileTypeSO tileTypeData;

    [Header("Visuals & Highlighting")]
    [SerializeField]
    private SpriteRenderer _spriteRenderer;
    private TileHighlightState _currentHighlightState = TileHighlightState.None;

    void Awake()
    {
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        
        if (_spriteRenderer == null)
        {
            DebugHelper.LogError($"Tile at {gridPosition} (Obj: {this.name}) is MISSING a SpriteRenderer. Visuals will fail.", this);
        }
    }

    public void Initialize(Vector2Int position, TileTypeSO data, int height = 0)
    {
        this.gridPosition = position;
        this.tileTypeData = data;
        this.currentTerrainType = data != null ? data.type : default(TerrainType);
        this.heightLevel = height;
        this.name = $"Tile_{position.x}_{position.y} ({currentTerrainType})";
        
        UpdateVisualsFromData();

        if (this.tileTypeData == null)
        {
            DebugHelper.LogWarning($"Tile at {gridPosition} (Obj: {this.name}) was initialized with NULL TileTypeSO data.", this);
        }
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
                _spriteRenderer.sprite = null;
                if (tileTypeData != null && tileTypeData.tileSprite == null && currentTerrainType != TerrainType.Boundary)
                {
                    DebugHelper.LogWarning($"Tile {gridPosition} ({currentTerrainType}) has TileTypeData but is missing a TileSprite.", this);
                }
            }
        }
    }

    public void SetHighlight(TileHighlightState state)
    {
        _currentHighlightState = state;
        if (_spriteRenderer != null)
        {
            switch (state)
            {
                case TileHighlightState.None: _spriteRenderer.color = Color.white; break;
                case TileHighlightState.MovementRange: _spriteRenderer.color = new Color(0.5f, 0.5f, 1f, 0.7f); break;
                case TileHighlightState.AttackRange: _spriteRenderer.color = new Color(1f, 0.5f, 0.5f, 0.7f); break;
                case TileHighlightState.SelectedUnit: _spriteRenderer.color = new Color(0.5f, 1f, 0.5f, 0.8f); break;
                case TileHighlightState.Hovered: _spriteRenderer.color = new Color(1f, 1f, 0.5f, 0.7f); break;
                default: _spriteRenderer.color = Color.white; break;
            }
        }
    }

    public int GetMovementCost()
    {
        if (tileTypeData != null)
        {
            return tileTypeData.movementCost;
        }
        DebugHelper.LogWarning($"Tile {gridPosition} (Obj: {this.name}) is missing TileTypeData when GetMovementCost called. Returning high movement cost.", this);
        return 255;
    }
        /// <summary>
    /// Sets the unit that is now occupying this tile.
    /// </summary>
    /// <param name="unit">The unit occupying the tile.</param>
    public void SetOccupyingUnit(Unit unit)
    {
        this.occupyingUnit = unit;
        // Optional: Fire OnTileDataChanged event here in the future
        // DebugHelper.Log($"Tile {gridPosition} ({this.name}) is now occupied by {(unit != null ? unit.unitName : "NULL")}", this);
    }

    /// <summary>
    /// Clears the occupying unit from this tile.
    /// </summary>
    public void ClearOccupyingUnit()
    {
        // Unit previousUnit = this.occupyingUnit; // Store if needed before clearing
        this.occupyingUnit = null;
        // Optional: Fire OnTileDataChanged event here in the future
        // DebugHelper.Log($"Tile {gridPosition} ({this.name}) is no longer occupied by {(previousUnit != null ? previousUnit.unitName : "already empty")}", this);
    }
} // This is the final closing brace of the Tile class
