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
    public TileTypeSO tileTypeData; 

    [Header("Visuals & Highlighting")]
    [SerializeField]
    private SpriteRenderer _spriteRenderer;
    private TileHighlightState _currentHighlightState = TileHighlightState.None;
    public TileHighlightState CurrentHighlightState => _currentHighlightState; 

    public const int IMPASSABLE_COST = 255; 

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
            this.currentTerrainType = data.type; 
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
        _currentHighlightState = TileHighlightState.None; 
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
            }
        }
        SetHighlight(_currentHighlightState); 
    }

    public void SetHighlight(TileHighlightState newState)
    {
        // REMOVED: Verbose Debug.Log($"Tile ({gridPosition}): SetHighlight called with newState = {newState}...");
        if (_currentHighlightState == newState && newState != TileHighlightState.Hovered) return; 

        _currentHighlightState = newState; 

        if (_spriteRenderer != null)
        {
            Color baseColor = Color.white; 

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
    }

    public int GetMovementCost(Unit unit = null) 
    {
        if (tileTypeData != null)
        {
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

    public bool IsWalkableFor(Unit unit) 
    {
        if (tileTypeData == null) return false;
        return GetMovementCost(unit) < IMPASSABLE_COST;
    }

    public bool IsOccupiedOrImpassableFor(Unit unit)
    {
        if (this.occupyingUnit != null && (unit == null || this.occupyingUnit != unit))
        {
            return true; 
        }
        if (GetMovementCost(unit) >= IMPASSABLE_COST)
        {
            return true; 
        }
        return false; 
    }
}