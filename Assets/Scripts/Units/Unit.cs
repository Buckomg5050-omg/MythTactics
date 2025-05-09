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
    public float moveSpeed = 5f; // Visual movement speed on map

    [Header("Action Points")]
    public int maxActionPoints = 2; 
    public int currentActionPoints;

    [Header("Turn Order")]
    [Tooltip("Current action counter for turn order. Reaches 1000 for a turn. GDD 1.1.")]
    public int actionCounter = 0;

    private Tile _currentTile;
    public Tile CurrentTile => _currentTile;
    private bool _isMoving = false;
    public bool IsMoving => _isMoving;
    private Coroutine _moveCoroutine = null;

    public int CalculatedMoveRange // GDD 1.2
    {
        get
        {
            int raceMoveBonus = (raceData != null) ? raceData.baseMovementContribution : 0;
            int classMoveBonus = (classData != null) ? classData.baseMovementContribution : 3; // Default for safety
            int echoStat = (currentAttributes != null) ? currentAttributes.Echo : 0; // Default to 0 if no attributes

            if (raceData == null) DebugHelper.LogWarning($"{unitName} missing RaceData for MoveRange calc.", this);
            if (classData == null) DebugHelper.LogWarning($"{unitName} missing ClassData for MoveRange calc (using default {classMoveBonus}).", this);
            if (currentAttributes == null) DebugHelper.LogWarning($"{unitName} missing Attributes for MoveRange calc (using Echo 0).", this);

            int baseMovement = raceMoveBonus + classMoveBonus;
            int echoBonus = Mathf.FloorToInt(echoStat / 5f);
            return Mathf.Max(1, baseMovement + echoBonus);
        }
    }

    // Speed Calculation Properties (GDD 1.1)
    public int RawCalculatedBaseUnitSpeed
    {
        get
        {
            int raceBonus = (raceData != null) ? raceData.raceSpeedBonus : 0;
            int classBonus = (classData != null) ? classData.classSpeedBonus : 0;
            int echoFactor = 0;
            int glimmerFactor = 0;

            if (currentAttributes != null)
            {
                echoFactor = currentAttributes.Echo * 2;
                glimmerFactor = currentAttributes.Glimmer * 1; 
            }
            else 
            {
                DebugHelper.LogWarning($"{unitName} missing currentAttributes for RawCalculatedBaseUnitSpeed calc. Echo/Glimmer factors will be 0.", this);
            }
            
            if (raceData == null) { DebugHelper.LogWarning($"{unitName} missing RaceData for RawCalculatedBaseUnitSpeed calc (raceBonus 0).", this); }
            if (classData == null) { DebugHelper.LogWarning($"{unitName} missing ClassData for RawCalculatedBaseUnitSpeed calc (classBonus 0).", this); }
            
            return raceBonus + classBonus + echoFactor + glimmerFactor;
        }
    }

    public int FinalCalculatedBaseUnitSpeed
    {
        get
        {
            return Mathf.Max(1, RawCalculatedBaseUnitSpeed); // Ensures minimum speed of 1
        }
    }

    public int EffectiveSpeed
    {
        get
        {
            // For now, equipment and status effect modifiers are 0.
            // These will be added later when those systems are in place.
            int totalEquipmentSpeedModifier = 0; 
            int totalStatusEffectSpeedModifier = 0;

            return FinalCalculatedBaseUnitSpeed + totalEquipmentSpeedModifier + totalStatusEffectSpeedModifier;
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
            if (GridManager.Instance != null)
            {
                transform.position = GridManager.Instance.GridToWorld(tile.gridPosition);
            }
            else
            {
                DebugHelper.LogError($"Unit.PlaceOnTile ({unitName}): GridManager.Instance not found! Cannot accurately set world position. Using tile's transform as fallback.", this);
                transform.position = tile.transform.position; 
            }
            
            tile.SetOccupyingUnit(this);
            ResetActionPoints(); 
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
         Tile nextTileInPath; 
        
        if (CurrentTile != null)
        {
            if (GridManager.Instance != null)
                transform.position = GridManager.Instance.GridToWorld(CurrentTile.gridPosition);
            else
                transform.position = CurrentTile.transform.position;
        }
        else
        {
            DebugHelper.LogWarning($"{unitName} starting PerformMovement with no CurrentTile. Using current transform position.", this);
        }

        for (int i = 0; i < path.Count; i++)
        {
            nextTileInPath = path[i];
            if (nextTileInPath == null) { DebugHelper.LogError($"Movement path for {unitName} contained a null tile at index {i}!", this); break; }

            startPos = transform.position;
            if (GridManager.Instance != null)
                endPos = GridManager.Instance.GridToWorld(nextTileInPath.gridPosition);
            else
                endPos = nextTileInPath.transform.position;

            if (_currentTile != null && _currentTile.occupyingUnit == this)
            {
                _currentTile.ClearOccupyingUnit();
            }
            
            _currentTile = nextTileInPath;
            _currentTile.SetOccupyingUnit(this);

            float journeyLength = Vector3.Distance(startPos, endPos);
            float startTime = Time.time;

            if (journeyLength > 0.01f && moveSpeed > 0) 
            {
                 float duration = journeyLength / moveSpeed;
                 float journeyFraction = 0f;
                 while (journeyFraction < 1.0f)
                 {
                     if (!_isMoving) {
                        DebugHelper.LogWarning($"{unitName} PerformMovement sub-coroutine interrupted.", this);
                        yield break;
                     }
                     float distCovered = (Time.time - startTime) * moveSpeed;
                     journeyFraction = distCovered / journeyLength;
                     transform.position = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(journeyFraction));
                     yield return null;
                 }
            }
            transform.position = endPos;
        }
    }

    public void ResetActionPoints()
    {
        currentActionPoints = maxActionPoints;
        DebugHelper.Log($"{unitName} AP reset to {currentActionPoints}/{maxActionPoints}", this);
    }

    public bool CanAffordAction(int cost)
    {
        bool canAfford = currentActionPoints >= cost;
        return canAfford;
    }

    public void SpendActionPoints(int cost)
    {
        if (cost <= 0)
        {
            DebugHelper.LogWarning($"{unitName} tried to spend invalid AP cost: {cost}", this);
            return;
        }

        if (CanAffordAction(cost))
        {
            currentActionPoints -= cost;
            DebugHelper.Log($"{unitName} spent {cost} AP. Remaining: {currentActionPoints}/{maxActionPoints}", this);
        }
        else
        {
            DebugHelper.LogWarning($"{unitName} FAILED to spend AP cost {cost}. Has {currentActionPoints}/{maxActionPoints}. Action should have been prevented.", this);
        }
    }
}