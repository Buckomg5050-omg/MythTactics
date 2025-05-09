// Unit.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Unit : MonoBehaviour
{
    [Header("Core Unit Data")]
    [Tooltip("Reference to the ScriptableObject defining the unit's race.")]
    public RaceDataSO raceData;
    [Tooltip("Reference to the ScriptableObject defining the unit's class.")]
    public ClassDataSO classData;

    [Header("Stats")]
    [Tooltip("Current primary attributes of the unit.")]
    public UnitPrimaryAttributes currentAttributes = new UnitPrimaryAttributes();
    [Tooltip("Current level of the unit.")]
    public int level = 1;

    [Header("Basic Info")]
    public string unitName = "Unit";

    [Header("Movement")]
    public float moveSpeed = 5f;

    private Tile _currentTile;
    public Tile CurrentTile => _currentTile;
    private bool _isMoving = false;
    public bool IsMoving => _isMoving;
    private Coroutine _moveCoroutine = null;

    public int CalculatedMoveRange
    {
        get
        {
            int raceMoveBonus = 0;
            int classMoveBonus = 3;
            int echoStat = 5;

            if (raceData != null) raceMoveBonus = raceData.baseMovementContribution;
            else DebugHelper.LogWarning($"{unitName} missing RaceData for MoveRange calc.", this);
            
            if (classData != null) classMoveBonus = classData.baseMovementContribution;
            else DebugHelper.LogWarning($"{unitName} missing ClassData for MoveRange calc.", this);
            
            if (currentAttributes != null) echoStat = currentAttributes.Echo;
            else DebugHelper.LogWarning($"{unitName} missing Attributes for MoveRange calc.", this);

            int baseMovement = raceMoveBonus + classMoveBonus;
            int echoBonus = Mathf.FloorToInt(echoStat / 5f);
            return Mathf.Max(1, baseMovement + echoBonus);
        }
    }

    public void PlaceOnTile(Tile tile)
    {
        if (_isMoving && _moveCoroutine != null) {
            StopCoroutine(_moveCoroutine);
            _isMoving = false;
            DebugHelper.LogWarning($"{unitName} placement interrupted ongoing movement.", this);
        }
        if (_currentTile != null && _currentTile.occupyingUnit == this) {
            _currentTile.ClearOccupyingUnit();
        }
        _currentTile = tile;
        if (tile != null) {
            transform.position = tile.transform.position;
            tile.SetOccupyingUnit(this);
        } else { DebugHelper.LogWarning($"{unitName} placed on NULL tile.", this); }
    }

    public void SetCurrentTile(Tile tile) { PlaceOnTile(tile); }

    public IEnumerator MoveOnPath(List<Tile> path)
    {
        if (_isMoving) { DebugHelper.LogWarning($"{unitName} requested move while already moving.", this); yield break; }
        if (path == null || path.Count == 0) { DebugHelper.LogWarning($"{unitName} requested move with empty/null path.", this); yield break; }

        _isMoving = true;
        _moveCoroutine = StartCoroutine(PerformMovement(path));
        yield return _moveCoroutine;
        _moveCoroutine = null;
        _isMoving = false;
        DebugHelper.Log($"{unitName} finished movement coroutine. Final Tile: {CurrentTile?.gridPosition}", this);
    }

    private IEnumerator PerformMovement(List<Tile> path)
    {
         Vector3 startPos, endPos;
         Tile nextTile;
        if (_currentTile != null) transform.position = _currentTile.transform.position;

        for (int i = 0; i < path.Count; i++)
        {
            nextTile = path[i];
            if (nextTile == null) { DebugHelper.LogError("Movement path contained a null tile!", this); break; }

            startPos = transform.position;
            endPos = nextTile.transform.position;

            if (_currentTile != null && _currentTile.occupyingUnit == this) { _currentTile.ClearOccupyingUnit(); }
            _currentTile = nextTile;
            _currentTile.SetOccupyingUnit(this);

            float journeyLength = Vector3.Distance(startPos, endPos);
            float startTime = Time.time;
            if (journeyLength > 0.01f && moveSpeed > 0) {
                 float duration = journeyLength / moveSpeed;
                 float journeyFraction = 0f;
                 while (journeyFraction < 1.0f) {
                     float distCovered = (Time.time - startTime) * moveSpeed;
                     journeyFraction = distCovered / journeyLength;
                     transform.position = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(journeyFraction));
                     yield return null;
                 }
            }
            transform.position = endPos;
        }
    }
}