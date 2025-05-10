// UnitMovement.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MythTactics.Combat; // For Unit, Tile, GridManager, DebugHelper

[RequireComponent(typeof(Unit))]
public class UnitMovement : MonoBehaviour
{
    private Unit _unitMain;
    private UnitStats _unitStats;
    private Transform _unitTransform;

    [Header("Movement Properties")]
    [SerializeField] private float _moveSpeed = 5f; // Default value, can be overridden by Unit's public field if needed
    public const int MoveActionAPCost = 1; // GDD 3.1: Move Action (use full MoveRange): 1 AP

    private Tile _currentTile;
    private bool _isMoving = false;
    private Coroutine _moveCoroutine = null;

    public Tile CurrentTile => _currentTile;
    public bool IsMoving => _isMoving;
    public float MoveSpeed
    {
        get => _moveSpeed;
        set => _moveSpeed = value;
    }

    public void Initialize(Unit mainUnit)
    {
        _unitMain = mainUnit;
        if (_unitMain == null)
        {
            Debug.LogError("UnitMovement.Initialize: Main Unit reference is null!", this);
            enabled = false; return;
        }
        _unitStats = _unitMain.Stats;
        if (_unitStats == null)
        {
            Debug.LogError($"UnitMovement.Initialize: UnitStats not found on {_unitMain.unitName}!", this);
            enabled = false; return;
        }
        _unitTransform = _unitMain.transform;
        _moveSpeed = _unitMain.moveSpeed;
    }

    public int CalculatedMoveRange
    {
        get
        {
            if (!_unitStats.IsAlive || _unitMain.raceData == null || _unitMain.classData == null || _unitStats.currentAttributes == null)
                return 0;

            return Mathf.Max(1,
                (_unitMain.raceData.baseMovementContribution +
                 _unitMain.classData.baseMovementContribution +
                 Mathf.FloorToInt(_unitStats.currentAttributes.Echo / 5f)));
        }
    }

    public void PlaceOnTile(Tile tile)
    {
        if (!_unitStats.IsAlive && tile != null && tile.occupyingUnit == _unitMain)
        {
            tile.ClearOccupyingUnit();
            _currentTile = null;
            return;
        }
        if (!_unitStats.IsAlive) return;

        StopMovementCoroutines();

        if (_currentTile != null && _currentTile.occupyingUnit == _unitMain)
        {
            _currentTile.ClearOccupyingUnit();
        }

        _currentTile = tile;
        if (tile != null)
        {
            _unitTransform.position = GridManager.Instance != null ? GridManager.Instance.GridToWorld(tile.gridPosition) : tile.transform.position;
            tile.SetOccupyingUnit(_unitMain);
        }
        else
        {
            DebugHelper.LogWarning($"{_unitMain.unitName} (Movement) placed on NULL tile.", _unitMain);
        }
    }

    public void SetCurrentTileForced(Tile tile)
    {
        PlaceOnTile(tile);
    }

    public void ClearCurrentTileReferenceForDeath()
    {
        if (_currentTile != null && _currentTile.occupyingUnit == _unitMain)
        {
            _currentTile.ClearOccupyingUnit();
        }
        _currentTile = null;
    }


    public IEnumerator MoveOnPath(List<Tile> path)
    {
        if (!_unitStats.IsAlive || _isMoving) { yield break; }
        if (path == null || path.Count == 0) { yield break; }

        // Check and spend AP for movement
        if (_unitStats == null) // Safety check, though Initialize should prevent this
        {
            DebugHelper.LogError($"{_unitMain.unitName} cannot move. UnitStats is null.", _unitMain);
            yield break;
        }

        if (!_unitStats.SpendActionPoints(MoveActionAPCost))
        {
            // UnitStats.SpendActionPoints already logs a warning with current/max AP.
            // No need for an additional log here if SpendActionPoints is comprehensive.
            // If more context is needed for movement failure specifically:
            // DebugHelper.LogWarning($"{_unitMain.unitName} cannot move. Insufficient AP for Move Action. Needs {MoveActionAPCost}, Has {_unitStats.currentActionPoints}.", _unitMain);
            yield break; // Not enough AP
        }
        // Log successful AP expenditure for movement
        DebugHelper.Log($"{_unitMain.unitName} performs Move Action (Cost: {MoveActionAPCost} AP). Remaining AP: {_unitStats.currentActionPoints}/{_unitStats.MaxActionPoints}.", _unitMain);


        _isMoving = true;
        _moveCoroutine = StartCoroutine(PerformMovementCoroutine(path));
        yield return _moveCoroutine;
        _isMoving = false;
        _moveCoroutine = null;
    }

    private IEnumerator PerformMovementCoroutine(List<Tile> path)
    {
        if (CurrentTile != null)
        {
            _unitTransform.position = GridManager.Instance != null ? GridManager.Instance.GridToWorld(CurrentTile.gridPosition) : CurrentTile.transform.position;
        }

        for (int i = 0; i < path.Count; i++)
        {
            if (!_unitStats.IsAlive)
            {
                DebugHelper.Log($"{_unitMain.unitName} died during movement (PerformMovementCoroutine).", _unitMain);
                _isMoving = false;
                yield break;
            }

            Tile nextTileInPath = path[i];
            if (nextTileInPath == null)
            {
                DebugHelper.LogError($"Movement path for {_unitMain.unitName} contained a null tile at index {i}!", _unitMain);
                break;
            }

            Vector3 startPos = _unitTransform.position;
            Vector3 endPos = GridManager.Instance != null ? GridManager.Instance.GridToWorld(nextTileInPath.gridPosition) : nextTileInPath.transform.position;

            if (_currentTile != null && _currentTile.occupyingUnit == _unitMain)
            {
                _currentTile.ClearOccupyingUnit();
            }
            _currentTile = nextTileInPath;
            if (_unitStats.IsAlive)
            {
                _currentTile.SetOccupyingUnit(_unitMain);
            }


            float journeyLength = Vector3.Distance(startPos, endPos);
            float startTime = Time.time;

            if (journeyLength > 0.01f && _moveSpeed > 0)
            {
                float journeyFraction = 0f;
                while (journeyFraction < 1.0f)
                {
                    if (!_isMoving || !_unitStats.IsAlive)
                    {
                        if (!_unitStats.IsAlive) DebugHelper.Log($"{_unitMain.unitName} died, interrupting movement lerp.", _unitMain);
                        else DebugHelper.Log($"{_unitMain.unitName} movement lerp interrupted.", _unitMain);
                        _isMoving = false;
                        yield break;
                    }
                    float distCovered = (Time.time - startTime) * _moveSpeed;
                    journeyFraction = distCovered / journeyLength;
                    _unitTransform.position = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(journeyFraction));
                    yield return null;
                }
            }
            _unitTransform.position = endPos;
        }
    }

    public void StopMovementCoroutines()
    {
        if (_isMoving && _moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _isMoving = false;
            _moveCoroutine = null;
            DebugHelper.Log($"{_unitMain.unitName} movement coroutines stopped by UnitMovement.", _unitMain);
        }
    }
}