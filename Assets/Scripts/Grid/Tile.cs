// Tile.cs
using UnityEngine;

public class Tile : MonoBehaviour
{
    [Header("State & Identification")]
    public Vector2Int gridPosition;
    public TerrainType currentTerrainType; // Set by GridManager via TileTypeSO
    public int heightLevel = 0;

    [Header("Occupancy")]
    [Tooltip("Reference to the unit currently occupying this tile, if any.")]
    public Unit occupyingUnit = null;

    public bool IsOccupied
    {
        get
        {
            bool isOcc = occupyingUnit != null;
            // Verbose log, enable if specific IsOccupied checks are problematic
            // DebugHelper.Log($"Tile {gridPosition} IsOccupied check. Occupant: {(occupyingUnit == null ? "NULL" : occupyingUnit.unitName)}. Returning: {isOcc}", this);
            return isOcc;
        }
    }

    [Header("Data Source")]
    [Tooltip("Reference to the ScriptableObject defining the properties of this tile's terrain type.")]
    public TileTypeSO tileTypeData; // Assigned by GridManager during initialization

    [Header("Visuals & Highlighting")]
    [SerializeField]
    private SpriteRenderer _spriteRenderer;
    private TileHighlightState _currentHighlightState = TileHighlightState.None;

    void Awake()
    {
        // Attempt to get SpriteRenderer in various common configurations
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
        this.currentTerrainType = data != null ? data.type : default(TerrainType); // Ensure currentTerrainType matches SO
        this.heightLevel = height;
        
        // Construct a descriptive name for the tile GameObject
        if (data != null) {
            this.name = $"Tile_{position.x}_{position.y} ({data.type})";
        } else {
            this.name = $"Tile_{position.x}_{position.y} (UnassignedType)";
        }
        
        UpdateVisualsFromData(); // Apply sprite from TileTypeSO

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
                _spriteRenderer.sprite = null; // Clear sprite if no data or sprite in data
                if (tileTypeData != null && tileTypeData.tileSprite == null && currentTerrainType != TerrainType.Boundary) { // Log warning if SO exists but sprite is missing (excluding boundaries which might intentionally have no sprite)
                    DebugHelper.LogWarning($"Tile {gridPosition} ({currentTerrainType}) TileTypeSO '{tileTypeData.name}' is missing its TileSprite.", this);
                }
            }
        }
        // After updating sprite, re-apply current highlight state (which sets color)
        SetHighlight(_currentHighlightState);
    }

    public void SetHighlight(TileHighlightState state)
    {
        _currentHighlightState = state;
        if (_spriteRenderer != null) {
            // Base color is white, assuming sprite itself has the primary colors.
            // Highlights will tint this.
            Color baseColor = Color.white; 

            switch (state) {
                case TileHighlightState.None: 
                    _spriteRenderer.color = baseColor; 
                    break;
                case TileHighlightState.MovementRange: 
                    _spriteRenderer.color = new Color(0.5f * baseColor.r, 0.5f * baseColor.g, 1f * baseColor.b, 0.7f); // Light blueish tint
                    break;
                case TileHighlightState.AttackRange: 
                    _spriteRenderer.color = new Color(1f * baseColor.r, 0.5f * baseColor.g, 0.5f * baseColor.b, 0.7f); // Light reddish tint
                    break;
                case TileHighlightState.SelectedUnit: 
                    _spriteRenderer.color = new Color(0.5f * baseColor.r, 1f * baseColor.g, 0.5f * baseColor.b, 0.8f); // Light greenish tint
                    break;
                case TileHighlightState.Hovered: 
                    _spriteRenderer.color = new Color(1f * baseColor.r, 1f * baseColor.g, 0.5f * baseColor.b, 0.7f); // Light yellowish tint
                    break;
                case TileHighlightState.Path: // Added Path state color
                    _spriteRenderer.color = new Color(0.8f, 0.3f, 1f, 0.75f); // Distinct purple/magenta tint for path
                    break;
                default: 
                    _spriteRenderer.color = baseColor; // Fallback to no tint
                    break;
            }
        }
    }

    public int GetMovementCost()
    {
        // Occupancy is handled by Pathfinder; this method returns terrain cost.
        if (tileTypeData != null) { 
            return tileTypeData.movementCost; 
        }
        // Fallback if no TileTypeData assigned (should not happen with proper grid init)
        DebugHelper.LogWarning($"Tile {gridPosition} ({this.name}) is missing TileTypeData for GetMovementCost. Returning impassable cost (255).", this);
        return 255; // Impassable default
    }

    public void SetOccupyingUnit(Unit unit)
    {
        if(this.occupyingUnit != null && this.occupyingUnit != unit && unit != null) {
             // This indicates a potential issue, like a unit being moved onto an already occupied tile without clearing first.
             DebugHelper.LogWarning($"!!! Tile {gridPosition} changing occupant FROM '{this.occupyingUnit.unitName}' TO '{unit.unitName}'. Ensure old unit was cleared properly. !!!", this);
        } else if (unit != null && this.occupyingUnit == null) { // Standard occupation
             // DebugHelper.Log($"Tile {gridPosition} SetOccupyingUnit: {unit.unitName}", this); // Can be verbose
        } else if (this.occupyingUnit != null && unit == null) {
             // This specific scenario (setting to null directly) is less common if ClearOccupyingUnit is used.
             // DebugHelper.LogWarning($"Tile {gridPosition} SetOccupyingUnit called directly with NULL, clearing '{this.occupyingUnit.unitName}'. Use ClearOccupyingUnit for clarity.", this);
        }
        this.occupyingUnit = unit;
    }

    public void ClearOccupyingUnit()
    {
        if (this.occupyingUnit != null) {
            // DebugHelper.Log($"Tile {gridPosition} ClearOccupyingUnit: Was '{this.occupyingUnit.unitName}'", this); // Can be verbose
        }
        this.occupyingUnit = null;
    }
}