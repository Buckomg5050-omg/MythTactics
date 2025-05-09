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
            return isOcc;
        }
    }

    [Header("Data Source")]
    [Tooltip("Reference to the ScriptableObject defining the properties of this tile's terrain type.")]
    public TileTypeSO tileTypeData; 

    [Header("Visuals & Highlighting")]
    [SerializeField]
    private SpriteRenderer _spriteRenderer;
    private TileHighlightState _currentHighlightState = TileHighlightState.None; // Default state

    void Awake()
    {
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer == null) {
             DebugHelper.LogError($"Tile at {gridPosition} (Obj: {this.name}) MISSING SpriteRenderer component.", this);
        }
    }

    public void Initialize(Vector2Int position, TileTypeSO data, int height = 0)
    {
        this.gridPosition = position;
        this.tileTypeData = data;
        this.currentTerrainType = data != null ? data.type : default(TerrainType); 
        this.heightLevel = height;
        
        if (data != null) {
            this.name = $"Tile_{position.x}_{position.y} ({data.type})";
        } else {
            this.name = $"Tile_{position.x}_{position.y} (UnassignedType)";
        }
        
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
                    DebugHelper.LogWarning($"Tile {gridPosition} ({currentTerrainType}) TileTypeSO '{tileTypeData.name}' is missing its TileSprite.", this);
                }
            }
        }
        SetHighlight(_currentHighlightState); // Re-apply highlight after visual update
    }

    public void SetHighlight(TileHighlightState state)
    {
        _currentHighlightState = state; // Store the new state
        if (_spriteRenderer != null) {
            Color baseColor = Color.white; 

            switch (state) {
                case TileHighlightState.None: 
                    _spriteRenderer.color = baseColor; 
                    break;
                case TileHighlightState.MovementRange: 
                    _spriteRenderer.color = new Color(0.5f * baseColor.r, 0.5f * baseColor.g, 1f * baseColor.b, 0.7f); 
                    break;
                case TileHighlightState.AttackRange: 
                    _spriteRenderer.color = new Color(1f * baseColor.r, 0.5f * baseColor.g, 0.5f * baseColor.b, 0.7f); 
                    break;
                case TileHighlightState.SelectedUnit: // This is when PlayerInputHandler selects a unit for potential action
                    _spriteRenderer.color = new Color(0.5f * baseColor.r, 1f * baseColor.g, 0.5f * baseColor.b, 0.8f); 
                    break;
                case TileHighlightState.Hovered: 
                    _spriteRenderer.color = new Color(1f * baseColor.r, 1f * baseColor.g, 0.5f * baseColor.b, 0.7f); 
                    break;
                case TileHighlightState.Path: 
                    _spriteRenderer.color = new Color(0.8f, 0.3f, 1f, 0.75f); 
                    break;
                case TileHighlightState.ActiveTurnUnit: // Highlight for the unit whose turn it is
                    _spriteRenderer.color = new Color(1f, 0.8f, 0.2f, 0.85f); // Example: Orangey-Yellow, distinct
                    break;
                default: 
                    _spriteRenderer.color = baseColor; 
                    break;
            }
        }
    }

    public int GetMovementCost()
    {
        if (tileTypeData != null) { 
            return tileTypeData.movementCost; 
        }
        DebugHelper.LogWarning($"Tile {gridPosition} ({this.name}) is missing TileTypeData for GetMovementCost. Returning impassable cost (255).", this);
        return 255; 
    }

    public void SetOccupyingUnit(Unit unit)
    {
        if(this.occupyingUnit != null && this.occupyingUnit != unit && unit != null) {
             DebugHelper.LogWarning($"!!! Tile {gridPosition} changing occupant FROM '{this.occupyingUnit.unitName}' TO '{unit.unitName}'. Ensure old unit was cleared properly. !!!", this);
        }
        this.occupyingUnit = unit;
    }

    public void ClearOccupyingUnit()
    {
        this.occupyingUnit = null;
    }
}