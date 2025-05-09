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
    public Unit occupyingUnit = null;

    public bool IsOccupied
    {
        get
        {
            bool isOcc = occupyingUnit != null;
            DebugHelper.Log($"Tile {gridPosition} IsOccupied check. Occupant: {(occupyingUnit == null ? "NULL" : occupyingUnit.unitName)}. Returning: {isOcc}", this);
            return isOcc;
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
        if (_spriteRenderer == null) {
             DebugHelper.LogError($"Tile at {gridPosition} (Obj: {this.name}) MISSING SpriteRenderer.", this);
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
        if (this.tileTypeData == null) {
            DebugHelper.LogWarning($"Tile {gridPosition} ({this.name}) initialized with NULL TileTypeSO data.", this);
        }
    }

    public void UpdateVisualsFromData()
    {
        if (_spriteRenderer != null) {
            if (tileTypeData != null && tileTypeData.tileSprite != null) {
                _spriteRenderer.sprite = tileTypeData.tileSprite;
            } else {
                _spriteRenderer.sprite = null;
                if (tileTypeData != null && tileTypeData.tileSprite == null && currentTerrainType != TerrainType.Boundary) {
                    DebugHelper.LogWarning($"Tile {gridPosition} ({currentTerrainType}) missing TileSprite.", this);
                }
            }
        }
    }

    public void SetHighlight(TileHighlightState state)
    {
        _currentHighlightState = state;
        if (_spriteRenderer != null) {
            switch (state) {
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
        if (tileTypeData != null) { return tileTypeData.movementCost; }
        DebugHelper.LogWarning($"Tile {gridPosition} ({this.name}) missing TileTypeData for GetMovementCost.", this);
        return 255;
    }

    public void SetOccupyingUnit(Unit unit)
    {
        if(this.occupyingUnit != null && this.occupyingUnit != unit && unit != null) {
             DebugHelper.LogWarning($"!!! Tile {gridPosition} changing occupant FROM {this.occupyingUnit.unitName} TO {unit.unitName} !!!", this);
        } else if (unit != null) {
             DebugHelper.Log($"Tile {gridPosition} SetOccupyingUnit: {unit.unitName}", this);
        } else if (this.occupyingUnit != null && unit == null) {
             DebugHelper.LogWarning($"Tile {gridPosition} SetOccupyingUnit called directly with NULL unit, clearing {this.occupyingUnit.unitName}.", this);
        }
        this.occupyingUnit = unit;
    }

    public void ClearOccupyingUnit()
    {
        if (this.occupyingUnit != null) {
            DebugHelper.Log($"Tile {gridPosition} ClearOccupyingUnit: Was {this.occupyingUnit.unitName}", this);
        }
        this.occupyingUnit = null;
    }
}