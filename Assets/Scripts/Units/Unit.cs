// Unit.cs
using UnityEngine;
using System.Collections; // Needed for Coroutine
using System.Collections.Generic; // Needed for List

public class Unit : MonoBehaviour
{
    [Header("Core Unit Data")]
    [Tooltip("Reference to the ScriptableObject defining the unit's race.")]
    public RaceDataSO raceData;
    [Tooltip("Reference to the ScriptableObject defining the unit's class.")]
    public ClassDataSO classData;

    [Header("Stats")]
    [Tooltip("Current primary attributes of the unit.")]
    public UnitPrimaryAttributes currentAttributes = new UnitPrimaryAttributes(); // Initialize with defaults
    [Tooltip("Current level of the unit.")]
    public int level = 1; // Start at level 1

    // We will add calculated stats (like MaxVP, EffectiveSpeed etc.) later as properties or methods

    [Header("Basic Info")] // Moved existing field here
    public string unitName = "Unit";

    [Header("Movement")]
    public float moveSpeed = 5f; // Tiles per second

    private Tile _currentTile;
    public Tile CurrentTile => _currentTile;
    private bool _isMoving = false;
    public bool IsMoving => _isMoving;
    private Coroutine _moveCoroutine = null;

    /// <summary>
    /// Calculates the unit's movement range based on GDD 1.2.
    /// CalculatedMoveRange = BaseMovementFromRaceClass + Floor(Echo / 5)
    /// </summary>
    public int CalculatedMoveRange
    {
        get
        {
            // Use default values if data is missing, but log a warning
            int raceMoveBonus = 0;
            int classMoveBonus = 3; // Default to a reasonable base like 3 if class is missing
            int echoStat = 5; // Default echo

            if (raceData == null) {
                DebugHelper.LogWarning($"{unitName} missing RaceData for MoveRange calc.", this);
            } else {
                raceMoveBonus = raceData.baseMovementContribution;
            }

            if (classData == null) {
                 DebugHelper.LogWarning($"{unitName} missing ClassData for MoveRange calc.", this);
            } else {
                classMoveBonus = classData.baseMovementContribution;
            }

             if (currentAttributes == null) {
                 DebugHelper.LogWarning($"{unitName} missing Attributes for MoveRange calc.", this);
            } else {
                 echoStat = currentAttributes.Echo;
            }

            // GDD 1.2: BaseMovementFromRaceClass = Race Contribution + Class Contribution
            int baseMovement = raceMoveBonus + classMoveBonus;

            // GDD 1.2: Floor(Echo / 5)
            int echoBonus = Mathf.FloorToInt(echoStat / 5f); // Use 5f for float division

            return Mathf.Max(1, baseMovement + echoBonus); // Ensure minimum move range of 1
        }
    }


    /// <summary>
    /// Sets the unit's current tile AND updates occupancy.
    /// Should be the primary way to place/move a unit logically.
    /// </summary>
    public void PlaceOnTile(Tile tile)
    {
        if (_isMoving && _moveCoroutine != null) {
            StopCoroutine(_moveCoroutine);
            _isMoving = false;
            DebugHelper.LogWarning($"{unitName} placement interrupted ongoing movement.", this);
        }

        // Clear previous tile if it exists and this unit occupies it
        if (_currentTile != null && _currentTile.occupyingUnit == this) {
            _currentTile.ClearOccupyingUnit();
        }

        _currentTile = tile; // Set new current tile

        if (tile != null) {
            transform.position = tile.transform.position; // Snap position
            tile.SetOccupyingUnit(this); // Occupy new tile
        }
        else {
             DebugHelper.LogWarning($"{unitName} placed on NULL tile.", this);
        }
    }

    // Simple override for initial placement via GridTester
    // Kept for potential compatibility, but PlaceOnTile is preferred.
    public void SetCurrentTile(Tile tile) {
         PlaceOnTile(tile);
    }


    /// <summary>
    /// Coroutine to move the unit visually along a path and update tile occupancy.
    /// </summary>
    /// <param name="path">List of Tiles representing the path (e.g., from Pathfinder, excluding start, including end).</param>
    public IEnumerator MoveOnPath(List<Tile> path)
    {
        if (_isMoving) {
             DebugHelper.LogWarning($"{unitName} requested move while already moving.", this);
             yield break;
        }
        if (path == null || path.Count == 0) {
             DebugHelper.LogWarning($"{unitName} requested move with empty/null path.", this);
             yield break;
        }

        _isMoving = true;
        _moveCoroutine = StartCoroutine(PerformMovement(path));
        yield return _moveCoroutine; // Wait for the movement coroutine to finish
        _moveCoroutine = null;
        _isMoving = false;
        DebugHelper.Log($"{unitName} finished movement coroutine. Final Tile: {CurrentTile?.gridPosition}", this);
    }

    // Internal coroutine for the movement step-by-step logic
    private IEnumerator PerformMovement(List<Tile> path)
    {
         Vector3 startPos, endPos;
         Tile nextTile;

        if (_currentTile != null) transform.position = _currentTile.transform.position;

        for (int i = 0; i < path.Count; i++)
        {
            nextTile = path[i];
            if (nextTile == null) {
                DebugHelper.LogError("Movement path contained a null tile!", this);
                break;
            }

            startPos = transform.position;
            endPos = nextTile.transform.position;

            // Update logical position and occupancy BEFORE visual movement for this step
            if (_currentTile != null && _currentTile.occupyingUnit == this) {
                 _currentTile.ClearOccupyingUnit();
            }
             _currentTile = nextTile;
             _currentTile.SetOccupyingUnit(this);

            // --- Visual Interpolation ---
            float journeyLength = Vector3.Distance(startPos, endPos);
            float startTime = Time.time;

            if (journeyLength > 0.01f && moveSpeed > 0)
            {
                 float duration = journeyLength / moveSpeed;
                 float journeyFraction = 0f;

                 while (journeyFraction < 1.0f)
                 {
                     float distCovered = (Time.time - startTime) * moveSpeed;
                     journeyFraction = distCovered / journeyLength;
                     transform.position = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(journeyFraction));
                     yield return null;
                 }
            }
            transform.position = endPos;
        }
    }
} // End of Unit class