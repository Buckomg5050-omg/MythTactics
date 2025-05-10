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

    private Tile _currentTile;
    private bool _isMoving = false;
    private Coroutine _moveCoroutine = null;

    public Tile CurrentTile => _currentTile;
    public bool IsMoving => _isMoving;
    public float MoveSpeed // Allow Unit to set this if needed, or manage it here
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

        // If Unit.cs still has a public moveSpeed field, you could sync it here:
        // _moveSpeed = _unitMain.moveSpeed; 
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

        StopMovementCoroutines(); // Stop any ongoing movement

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

    public void SetCurrentTileForced(Tile tile) // Used by Unit.cs for initial setup or direct placement
    {
        PlaceOnTile(tile);
    }
    
    public void ClearCurrentTileReferenceForDeath() // Called by UnitCombat when unit dies
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

        _isMoving = true;
        _moveCoroutine = StartCoroutine(PerformMovementCoroutine(path));
        yield return _moveCoroutine; // Wait for the movement to complete
        _isMoving = false; 
        _moveCoroutine = null;
    }

    private IEnumerator PerformMovementCoroutine(List<Tile> path)
    {
        if (CurrentTile != null) // Ensure starting position is accurate
        {
             _unitTransform.position = GridManager.Instance != null ? GridManager.Instance.GridToWorld(CurrentTile.gridPosition) : CurrentTile.transform.position;
        }

        for (int i = 0; i < path.Count; i++)
        {
            if (!_unitStats.IsAlive) 
            { 
                DebugHelper.Log($"{_unitMain.unitName} died during movement (PerformMovementCoroutine).", _unitMain); 
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

            // Clear previous tile, set new current tile
            if (_currentTile != null && _currentTile.occupyingUnit == _unitMain)
            {
                _currentTile.ClearOccupyingUnit();
            }
            _currentTile = nextTileInPath;
            if (_unitStats.IsAlive) // Only occupy if still alive
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
                    if (!_isMoving || !_unitStats.IsAlive) // Check if movement was cancelled or unit died
                    { 
                        if (!_unitStats.IsAlive) DebugHelper.Log($"{_unitMain.unitName} died, interrupting movement lerp.", _unitMain);
                        else DebugHelper.Log($"{_unitMain.unitName} movement lerp interrupted.", _unitMain);
                        _isMoving = false; // Ensure flag is reset
                        yield break; 
                    }
                    float distCovered = (Time.time - startTime) * _moveSpeed;
                    journeyFraction = distCovered / journeyLength;
                    _unitTransform.position = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(journeyFraction));
                    yield return null; // Wait for next frame
                }
            }
            _unitTransform.position = endPos; // Snap to final position
        }
        // _isMoving will be set to false by the calling MoveOnPath coroutine after this one completes
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