// UnitMovement.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MythTactics.Combat; 

[RequireComponent(typeof(Unit))]
public class UnitMovement : MonoBehaviour
{
    private Unit _unitMain;
    private UnitStats _unitStats;
    private Transform _unitTransform;

    [Header("Movement Properties")]
    [SerializeField] private float _moveSpeed = 5f; 
    public const int MoveActionAPCost = 1; 

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

    // Cached RaceDataSO and ClassDataSO for movement calculation
    private RaceDataSO _raceDataForMovement; // ADDED
    private ClassDataSO _classDataForMovement; // ADDED

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

        // Cache these for CalculatedMoveRange
        _raceDataForMovement = _unitMain.raceData;     // ADDED
        _classDataForMovement = _unitMain.classData;   // ADDED
    }

    public int CalculatedMoveRange
    {
        get
        {
            // Ensure _unitStats and currentAttributes are valid before accessing them.
            if (_unitStats == null || !_unitStats.IsAlive || _raceDataForMovement == null || _classDataForMovement == null || _unitStats.currentAttributes == null)
                return 0;

            // Use cached _raceDataForMovement and _classDataForMovement
            int baseMove = (_raceDataForMovement.baseMovementContribution + _classDataForMovement.baseMovementContribution);
            int echoBonus = Mathf.FloorToInt(_unitStats.EffectiveAttributes.Echo / 5f); // Use Effective Echo
            
            return Mathf.Max(1, baseMove + echoBonus);
        }
    }

    public void PlaceOnTile(Tile tile)
    {
        if (_unitStats == null) { Debug.LogError("UnitMovement.PlaceOnTile: _unitStats is null!", this); return; }

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
            // DebugHelper.LogWarning($"{_unitMain.unitName} (Movement) placed on NULL tile.", _unitMain);
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
        if (_unitStats == null || !_unitStats.IsAlive || _isMoving) { yield break; }
        if (path == null || path.Count == 0) { yield break; }

        if (!_unitStats.SpendActionPoints(MoveActionAPCost))
        {
            yield break; 
        }
        // DebugHelper.Log($"{_unitMain.unitName} performs Move Action (Cost: {MoveActionAPCost} AP). Remaining AP: {_unitStats.currentActionPoints}/{_unitStats.MaxActionPoints}.", _unitMain);
        // AP spending is already logged by UnitStats.SpendActionPoints

        _isMoving = true;
        _moveCoroutine = StartCoroutine(PerformMovementCoroutine(path));
        yield return _moveCoroutine; // Wait for the movement to complete

        // ADDED: Log movement after it's done
        if (_isMoving) // If it wasn't interrupted
        {
            CombatLogger.LogMovement(_unitMain, path.Count);
        }

        _isMoving = false;
        _moveCoroutine = null;
    }

    private IEnumerator PerformMovementCoroutine(List<Tile> path)
    {
        if (CurrentTile != null && GridManager.Instance != null) // Ensure GridManager is available
        {
            _unitTransform.position = GridManager.Instance.GridToWorld(CurrentTile.gridPosition);
        }
        else if (CurrentTile != null)
        {
             _unitTransform.position = CurrentTile.transform.position; // Fallback if no GridManager
        }


        for (int i = 0; i < path.Count; i++)
        {
            if (_unitStats == null || !_unitStats.IsAlive) // Check stats null as well
            {
                // DebugHelper.Log($"{_unitMain.unitName} died during movement (PerformMovementCoroutine).", _unitMain);
                _isMoving = false; // Ensure flag is reset
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
            if (_unitStats.IsAlive) // Re-check before setting new occupying unit
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
                    if (!_isMoving || _unitStats == null || !_unitStats.IsAlive) // Check stats null
                    {
                        // if (_unitStats == null || !_unitStats.IsAlive) DebugHelper.Log($"{_unitMain.unitName} died or stats became null, interrupting movement lerp.", _unitMain);
                        // else DebugHelper.Log($"{_unitMain.unitName} movement lerp interrupted.", _unitMain);
                        _isMoving = false; // Ensure flag is reset
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
        // Note: _isMoving is set to false in the calling MoveOnPath coroutine after this one finishes.
        // The CombatLogger call will happen there.
    }

    public void StopMovementCoroutines()
    {
        if (_isMoving && _moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _isMoving = false;
            _moveCoroutine = null;
            // DebugHelper.Log($"{_unitMain.unitName} movement coroutines stopped by UnitMovement.", _unitMain);
        }
    }
}