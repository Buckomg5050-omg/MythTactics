using UnityEngine;

public enum TileHighlight { None, MovementRange, AttackRange, Selected }

public class Tile : MonoBehaviour
{
    [SerializeField] private TileTypeSO _tileType;
    [SerializeField] private SpriteRenderer _spriteRenderer;

    private TileHighlight _highlightState = TileHighlight.None;

    public Vector2Int GridPosition { get; private set; }
    public TileTypeSO TileType => _tileType;
    public int MovementCost => _tileType.IsPassable ? Mathf.RoundToInt(_tileType.MovementCostMultiplier) : 255;
    public Unit OccupyingUnit { get; private set; }
    public bool IsOccupied => OccupyingUnit != null;

    public void Initialize(Vector2Int gridPos, TileTypeSO tileType)
    {
        GridPosition = gridPos;
        _tileType = tileType;
        name = $"Tile_{gridPos.x}_{gridPos.y}";
        UpdateVisual();
    }

    public bool CanBeOccupied(Unit unit)
    {
        return !IsOccupied && _tileType.IsPassable;
    }

    public void SetOccupyingUnit(Unit unit)
    {
        OccupyingUnit = unit;
    }

    public void ClearOccupyingUnit()
    {
        OccupyingUnit = null;
    }

    public void SetHighlight(TileHighlight highlight)
    {
        _highlightState = highlight;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (_spriteRenderer == null || _tileType == null) return;

        _spriteRenderer.sprite = _tileType.TileSprite;

        // Apply highlight color
        switch (_highlightState)
        {
            case TileHighlight.None:
                _spriteRenderer.color = Color.white;
                break;
            case TileHighlight.MovementRange:
                _spriteRenderer.color = new Color(0f, 0.8f, 0f, 0.5f); // Semi-transparent green
                break;
            case TileHighlight.AttackRange:
                _spriteRenderer.color = new Color(0.8f, 0f, 0f, 0.5f); // Semi-transparent red
                break;
            case TileHighlight.Selected:
                _spriteRenderer.color = new Color(0f, 0f, 0.8f, 0.5f); // Semi-transparent blue
                break;
        }
    }

    #if UNITY_EDITOR
    private void OnValidate()
    {
        UpdateVisual();
    }
    #endif
}