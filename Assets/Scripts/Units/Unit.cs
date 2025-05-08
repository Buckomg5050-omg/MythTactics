// Unit.cs
using UnityEngine;
using System.Collections; // Needed for Coroutine
using System.Collections.Generic; // Needed for List

public class Unit : MonoBehaviour
{
    [Header("Basic Info")]
    public string unitName = "Unit";

    [Header("Movement")]
    public float moveSpeed = 5f; // Tiles per second

    private Tile _currentTile;
    public Tile CurrentTile => _currentTile;
    private bool _isMoving = false;
    public bool IsMoving => _isMoving;
    private Coroutine _moveCoroutine = null; // Store reference to stop if needed

    /// <summary>
    /// Sets the unit's current tile AND updates occupancy.
    /// Should be the primary way to place/move a unit logically.
    /// </summary>
    public void PlaceOnTile(Tile tile)
    {
        if (_isMoving && _moveCoroutine != null) {
            StopCoroutine(_moveCoroutine); // Stop current move if placing directly
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
            // DebugHelper.Log($"{unitName} placed on tile {tile.gridPosition}", this);
        }
        else {
             DebugHelper.LogWarning($"{unitName} placed on NULL tile.", this);
        }
    }

    // Simple override for initial placement via GridTester
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
             yield break; // Don't start a new move
        }
        if (path == null || path.Count == 0) {
             DebugHelper.LogWarning($"{unitName} requested move with empty/null path.", this);
             yield break;
        }

        _isMoving = true;
        _moveCoroutine = StartCoroutine(PerformMovement(path)); // Start the actual movement logic
        yield return _moveCoroutine; // Wait for the movement coroutine to finish
        _moveCoroutine = null; // Clear reference after completion
        _isMoving = false; // Movement finished
        DebugHelper.Log($"{unitName} finished movement coroutine. Final Tile: {CurrentTile?.gridPosition}", this);
    }

    // Internal coroutine for the movement step-by-step logic
    private IEnumerator PerformMovement(List<Tile> path)
    {
         Vector3 startPos, endPos;
         Tile nextTile;

        // Ensure the unit starts at its current tile's position visually
        if (_currentTile != null) transform.position = _currentTile.transform.position;

        for (int i = 0; i < path.Count; i++)
        {
            nextTile = path[i];
            if (nextTile == null) {
                DebugHelper.LogError("Movement path contained a null tile!", this);
                break; // Exit loop if path is broken
            }

            startPos = transform.position;
            endPos = nextTile.transform.position;

            // Update logical position and occupancy BEFORE visual movement for this step
            if (_currentTile != null && _currentTile.occupyingUnit == this) {
                 _currentTile.ClearOccupyingUnit();
            }
             _currentTile = nextTile; // Update internal reference
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
                     // Ensure journeyFraction doesn't exceed 1 due to frame delays
                     transform.position = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(journeyFraction));
                     yield return null; // Wait for next frame
                 }
            }
            transform.position = endPos; // Snap to final position for the step
        }
        // Final state should be: _currentTile is the last tile in the path, and it's occupied by this unit.
    }
}