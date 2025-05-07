using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private float _moveSpeed = 5f; // Tiles per second
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private int _attackRange = 1; // Manhattan distance
    [SerializeField] private int _attackDamage = 20;

    private Tile _currentTile;
    private bool _isMoving;
    private UnitManager _unitManager;
    private int _currentHealth;

    public Tile CurrentTile => _currentTile;
    public int CurrentHealth => _currentHealth;
    public int AttackRange => _attackRange;

    private void Awake()
    {
        _unitManager = FindObjectOfType<UnitManager>();
        _currentHealth = _maxHealth;
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

    public void Attack(Unit target)
    {
        if (target == null || target == this) return;

        GridManager gridManager = FindObjectOfType<GridManager>();
        int distance = gridManager.ManhattanDistance(_currentTile.GridPosition, target.CurrentTile.GridPosition);
        if (distance > _attackRange)
        {
            Debug.LogWarning($"Target at {target.CurrentTile.GridPosition} is out of attack range ({_attackRange})");
            return;
        }

        float combatBonus = _currentTile.GetCombatBonus(target.CurrentTile);
        int damage = Mathf.RoundToInt(_attackDamage * (1f + combatBonus));
        target.TakeDamage(damage);
        Debug.Log($"{name} attacked {target.name} for {damage} damage (Bonus: {combatBonus:P0})");
    }

    public void TakeDamage(int damage)
    {
        _currentHealth = Mathf.Max(0, _currentHealth - damage);
        Debug.Log($"{name} took {damage} damage, health now {_currentHealth}");
        if (_currentHealth <= 0)
        {
            _currentTile.ClearOccupyingUnit();
            Destroy(gameObject);
        }
    }

    private IEnumerator MoveCoroutine(List<Tile> path, System.Action onMoveComplete)
    {
        _isMoving = true;

        if (_currentTile != null)
            _currentTile.ClearOccupyingUnit();

        for (int i = 0; i < path.Count; i++)
        {
            Tile targetTile = path[i];
            bool isFinalTile = i == path.Count - 1;

            // Allow final tile to be occupied (for attacking), but not intermediate tiles
            if (!isFinalTile && !targetTile.CanBeOccupied(this))
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