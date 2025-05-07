using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private float _moveSpeed = 5f; // Tiles per second

    private Tile _currentTile;
    private bool _isMoving;
    private UnitManager _unitManager;

    public Tile CurrentTile => _currentTile;

    private void Awake()
    {
        _unitManager = FindObjectOfType<UnitManager>();
    }

    public void Initialize(Tile startTile)
    {
        if (startTile == null || !startTile.CanBeOccupied(this))
        {
            Debug.LogError($"Cannot initialize unit on tile {startTile?.GridPosition}");
            return;
        }

        _currentTile = startTile;
        _currentTile.SetOccupyingUnit(this);
        transform.position = _currentTile.transform.position;
        name = $"Unit_{startTile.GridPosition.x}_{startTile.GridPosition.y}";
        _unitManager?.RegisterUnit(this);
    }

    private void OnDestroy()
    {
        _unitManager?.UnregisterUnit(this);
    }

    public void MoveAlongPath(List<Tile> path, System.Action onMoveComplete = null)
    {
        if (_isMoving || path == null || path.Count == 0) return;

        StartCoroutine(MoveCoroutine(path, onMoveComplete));
    }

    private IEnumerator MoveCoroutine(List<Tile> path, System.Action onMoveComplete)
    {
        _isMoving = true;

        // Clear current tile occupancy
        if (_currentTile != null)
            _currentTile.ClearOccupyingUnit();

        foreach (Tile targetTile in path)
        {
            if (!targetTile.CanBeOccupied(this))
            {
                Debug.LogWarning($"Cannot move to tile {targetTile.GridPosition}: occupied or impassable");
                break;
            }

            Vector3 startPos = transform.position;
            Vector3 endPos = targetTile.transform.position;
            float distance = Vector3.Distance(startPos, endPos);
            float duration = distance / _moveSpeed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }

            transform.position = endPos;
            _currentTile = targetTile;
            _currentTile.SetOccupyingUnit(this);
        }

        _isMoving = false;
        onMoveComplete?.Invoke();
    }

    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();
    }
    #endif
}