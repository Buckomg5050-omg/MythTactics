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
    public UnitPrimaryAttributes currentAttributes = new UnitPrimaryAttributes(); // Ensure this is initialized if not done elsewhere
    [Tooltip("Current level of the unit.")]
    public int level = 1;

    [Header("Basic Info")]
    public string unitName = "Unit";

    [Header("Movement")]
    public float moveSpeed = 5f;

    // Action Points as per GDD 3.1
    [Header("Action Points")]
    public int maxActionPoints = 2; // Default based on GDD 3.1
    public int currentActionPoints;

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
            // Default classMoveBonus to 0 if classData is null or doesn't provide it, to avoid errors if not fully set up.
            // GDD implies BaseMovementFromRaceClass is a sum, so initialize classMoveBonus appropriately.
            // For testing, let's assume it defaults to a reasonable value if data is missing.
            int classMoveBonus = (classData != null) ? classData.baseMovementContribution : 3; // Default to 3 as per GDD example if no classData
            int echoStat = (currentAttributes != null) ? currentAttributes.Echo : 5; // Default to 5 if no attributes

            if (raceData != null)
            {
                raceMoveBonus = raceData.baseMovementContribution;
            }
            else
            {
                // DebugHelper.LogWarning($"{unitName} missing RaceData for MoveRange calc. Using default 0.", this);
                // No warning for default, assume 0 if no race data is a valid state for some units.
            }
            
            if (classData == null)
            {
                 // DebugHelper.LogWarning($"{unitName} missing ClassData for MoveRange calc. Using default {classMoveBonus}.", this);
            }
            
            if (currentAttributes == null)
            {
                 // DebugHelper.LogWarning($"{unitName} missing Attributes for MoveRange calc. Using default Echo {echoStat}.", this);
            }

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
            // Use GridManager to get world position
            // Assuming GridManager has a static Instance. If not, this needs to be adjusted.
            if (GridManager.Instance != null) // Replace GridManager.Instance with your actual access pattern
            {
                transform.position = GridManager.Instance.GridToWorld(tile.gridPosition);
            }
            else
            {
                DebugHelper.LogError($"Unit.PlaceOnTile ({unitName}): GridManager.Instance not found! Cannot accurately set world position. Using tile's transform as fallback.", this);
                transform.position = tile.transform.position; // Fallback
            }
            
            tile.SetOccupyingUnit(this);
            ResetActionPoints(); // Initialize AP when placed
        } else { DebugHelper.LogWarning($"{unitName} placed on NULL tile.", this); }
    }

    // Potentially redundant if PlaceOnTile is always used for initial setup.
    // Kept for now if it serves another purpose.
    public void SetCurrentTile(Tile tile) { PlaceOnTile(tile); }

    public IEnumerator MoveOnPath(List<Tile> path)
    {
        if (_isMoving) { DebugHelper.LogWarning($"{unitName} requested move while already moving.", this); yield break; }
        if (path == null || path.Count == 0) { DebugHelper.LogWarning($"{unitName} requested move with empty/null path.", this); yield break; }

        _isMoving = true;
        _moveCoroutine = StartCoroutine(PerformMovement(path));
        yield return _moveCoroutine; // Wait for the PerformMovement coroutine to complete
        _moveCoroutine = null;
        _isMoving = false;
        DebugHelper.Log($"{unitName} finished movement coroutine. Final Tile: {CurrentTile?.gridPosition}", this);
    }

    private IEnumerator PerformMovement(List<Tile> path)
    {
         Vector3 startPos, endPos;
         Tile nextTileInPath; // Renamed to avoid confusion with class field _currentTile
        
        // Ensure unit starts from its current tile's world position if it's valid
        if (CurrentTile != null)
        {
            // Assuming GridManager has a static Instance for GridToWorld
            if (GridManager.Instance != null)
                transform.position = GridManager.Instance.GridToWorld(CurrentTile.gridPosition);
            else
                transform.position = CurrentTile.transform.position; // Fallback
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
            // Assuming GridManager has a static Instance for GridToWorld
            if (GridManager.Instance != null)
                endPos = GridManager.Instance.GridToWorld(nextTileInPath.gridPosition);
            else
                endPos = nextTileInPath.transform.position; // Fallback


            // Clear occupancy from the tile the unit is *leaving*
            if (_currentTile != null && _currentTile.occupyingUnit == this)
            {
                _currentTile.ClearOccupyingUnit();
            }
            
            // Update the unit's current tile reference *before* moving visually to it
            _currentTile = nextTileInPath;
            // Set occupancy on the *new* tile
            _currentTile.SetOccupyingUnit(this);

            float journeyLength = Vector3.Distance(startPos, endPos);
            float startTime = Time.time;

            if (journeyLength > 0.01f && moveSpeed > 0) // Only Lerp if there's a distance to cover
            {
                 float duration = journeyLength / moveSpeed;
                 float journeyFraction = 0f;
                 while (journeyFraction < 1.0f)
                 {
                     // Ensure _isMoving is still true; an external interruption might have stopped the parent coroutine
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
            // Ensure final position is exact
            transform.position = endPos;
        }
    }

    // --- Action Point Methods ---
    public void ResetActionPoints()
    {
        currentActionPoints = maxActionPoints;
        DebugHelper.Log($"{unitName} AP reset to {currentActionPoints}/{maxActionPoints}", this);
    }

    public bool CanAffordAction(int cost)
    {
        bool canAfford = currentActionPoints >= cost;
        // DebugHelper.Log($"{unitName} CanAffordAction check: cost={cost}, currentAP={currentActionPoints}, canAfford={canAfford}", this); // Optional: for debugging
        return canAfford;
    }

    public void SpendActionPoints(int cost)
    {
        if (cost <= 0)
        {
            DebugHelper.LogWarning($"{unitName} tried to spend invalid AP cost: {cost}", this);
            return; // Do not proceed if cost is invalid
        }

        if (CanAffordAction(cost))
        {
            currentActionPoints -= cost;
            DebugHelper.Log($"{unitName} spent {cost} AP. Remaining: {currentActionPoints}/{maxActionPoints}", this);
        }
        else
        {
            // This case should ideally be prevented by calling CanAffordAction before SpendActionPoints.
            DebugHelper.LogWarning($"{unitName} FAILED to spend AP cost {cost}. Has {currentActionPoints}/{maxActionPoints}. Action should have been prevented.", this);
        }
    }
}