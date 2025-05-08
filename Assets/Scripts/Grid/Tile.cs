// Tile.cs
using UnityEngine;

public class Tile : MonoBehaviour
{
    [Header("State & Identification")]
    public Vector2Int gridPosition;
    public TerrainType currentTerrainType;
    public int heightLevel = 0;

    public bool IsOccupied => false; // occupyingUnit == null; // Placeholder

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
            // Use DebugHelper if available, otherwise fallback
            if (FindObjectOfType<GridManager>() != null) // Crude check if DebugHelper might be usable
                 DebugHelper.LogWarning($"Tile at {gridPosition} is missing a SpriteRenderer. Please add one.", this);
            else
                 Debug.LogWarning($"Tile at {gridPosition} is missing a SpriteRenderer. Please add one.", this);
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
    }

    public void UpdateVisualsFromData()
    {
        if (_spriteRenderer != null && tileTypeData != null && tileTypeData.tileSprite != null)
        {
            _spriteRenderer.sprite = tileTypeData.tileSprite;
        }
        else if (_spriteRenderer != null)
        {
            _spriteRenderer.sprite = null;
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
                case TileHighlightState.MovementRange: _spriteRenderer.color = Color.blue; break;
                case TileHighlightState.AttackRange: _spriteRenderer.color = Color.red; break;
                case TileHighlightState.SelectedUnit: _spriteRenderer.color = Color.green; break;
                case TileHighlightState.Hovered: _spriteRenderer.color = Color.yellow; break;
                default: _spriteRenderer.color = Color.white; break;
            }
        }
    }

    public int GetMovementCost()
    {
        if (tileTypeData != null) return tileTypeData.movementCost;
        // Use DebugHelper if available
        if (FindObjectOfType<GridManager>() != null)
            DebugHelper.LogWarning($"Tile {gridPosition} is missing TileTypeData. Returning high movement cost.", this);
        else
            Debug.LogWarning($"Tile {gridPosition} is missing TileTypeData. Returning high movement cost.", this);
        return 255;
    }
}