// PlayerInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign the GridManager from the scene.")]
    public GridManager gridManager;
    [Tooltip("Assign the GridTester from the scene to get player unit ref.")]
    public GridTester gridTester;
    private Pathfinder _pathfinder;

    // Removed defaultMovementRange field

    private enum InputState { None, UnitSelected, UnitMoving }
    private InputState _currentState = InputState.None;

    private Unit _selectedUnit = null;
    private Vector2Int? _endTilePos = null;

    private List<Tile> _highlightedReachableTiles = new List<Tile>();
    private List<Tile> _highlightedPathTiles = new List<Tile>();
    private Tile _highlightedStartTile = null;

    private PlayerControls _playerControls;
    private Camera _mainCamera;

    void Awake()
    {
        _mainCamera = Camera.main;
        _playerControls = new PlayerControls();

        if (gridManager == null) { DebugHelper.LogError("InputHandler: GridManager missing!", this); this.enabled = false; return; }
        if (gridTester == null) { DebugHelper.LogError("InputHandler: GridTester missing!", this); this.enabled = false; return; }
    }

    void Start()
    {
        _pathfinder = new Pathfinder(gridManager);
        StartCoroutine(AssignPlayerUnitReference());
    }

     IEnumerator AssignPlayerUnitReference()
    {
        yield return null;
        while (gridTester.PlayerUnitInstance == null) {
            DebugHelper.LogWarning("InputHandler waiting for Player Unit from GridTester...", this);
            yield return new WaitForSeconds(0.2f);
        }
        if (gridTester.PlayerUnitInstance != null) {
             DebugHelper.Log("InputHandler ready, found Player Unit.", this);
        } else {
            DebugHelper.LogError("InputHandler could not get Player Unit reference!", this);
             this.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_playerControls == null) _playerControls = new PlayerControls();
        _playerControls.Gameplay.Enable();
        _playerControls.Gameplay.Click.performed += HandleClick;
    }

    private void OnDisable()
    {
        if (_playerControls == null) return;
        _playerControls.Gameplay.Click.performed -= HandleClick;
        _playerControls.Gameplay.Disable();
    }

    private void HandleClick(InputAction.CallbackContext context)
    {
        if (_currentState == InputState.UnitMoving || gridManager == null || _pathfinder == null || _mainCamera == null || gridTester.PlayerUnitInstance == null) return;

        Vector2 screenPosition = _playerControls.Gameplay.Point.ReadValue<Vector2>();
        Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
        Plane xyPlane = new Plane(Vector3.forward, Vector3.zero);

        if (!xyPlane.Raycast(ray, out float distance)) { ClearInteractionState(); /* DebugHelper.Log("Click miss.", this); */ return; }

        Vector3 worldPoint = ray.GetPoint(distance);
        Vector2Int clickedGridPos = gridManager.WorldToGrid(worldPoint);
        Tile clickedTile = gridManager.GetTile(clickedGridPos);

        switch (_currentState)
        {
            case InputState.None:
                if (clickedTile != null && clickedTile.IsOccupied && clickedTile.occupyingUnit == gridTester.PlayerUnitInstance)
                {
                    ClearInteractionState();
                    _selectedUnit = gridTester.PlayerUnitInstance;
                    _highlightedStartTile = clickedTile;
                    clickedTile.SetHighlight(TileHighlightState.SelectedUnit);

                    int unitMoveRange = _selectedUnit.CalculatedMoveRange;
                    ShowReachableRange(clickedGridPos, unitMoveRange, _selectedUnit);
                    
                    _currentState = InputState.UnitSelected;
                    DebugHelper.Log($"Selected Player Unit at: {clickedGridPos} (Move Range: {unitMoveRange})", this);
                }
                else { ClearInteractionState(); _selectedUnit = null; }
                break;

            case InputState.UnitSelected:
                 if (_selectedUnit != null && clickedTile != null)
                 {
                    bool isReachable = _highlightedReachableTiles.Contains(clickedTile) || clickedTile == _highlightedStartTile;

                    if (clickedTile == _highlightedStartTile) {
                        DebugHelper.Log("Clicked start tile again. Deselecting.", this);
                        ClearInteractionState(); break;
                    }

                    if (isReachable)
                    {
                        // DebugHelper.Log($"Checking target tile {clickedGridPos} for occupancy.", clickedTile);
                        if (clickedTile.IsOccupied)
                        {
                             DebugHelper.Log($"Occupancy Check PASSED for {clickedGridPos}. Occupied by: {clickedTile.occupyingUnit?.unitName ?? "NULL Unit"}.", clickedTile);
                             DebugHelper.Log($"Cannot move to occupied tile {clickedGridPos}. Resetting.", this);
                             ClearInteractionState();
                             break;
                        }
                        // else {
                        //     DebugHelper.LogWarning($"Occupancy Check FAILED for {clickedGridPos}. Tile reports IsOccupied: {clickedTile.IsOccupied}. Proceeding.", clickedTile);
                        // }

                        _endTilePos = clickedGridPos;
                        ClearReachableHighlight();

                        DebugHelper.Log($"Target Tile selected: {clickedGridPos}. Calculating path...", this);
                        List<Tile> path = _pathfinder.FindPath(_selectedUnit.CurrentTile.gridPosition, _endTilePos.Value, _selectedUnit);

                        if (path != null && path.Count > 0) {
                            ShowPath(_selectedUnit.CurrentTile.gridPosition, _endTilePos.Value);
                            _currentState = InputState.UnitMoving;
                            DebugHelper.Log($"Path found. Requesting move.", this);
                            StartCoroutine(MoveUnitAlongPath(_selectedUnit, path));
                        }
                        else {
                             DebugHelper.LogWarning($"No path calculated from {_selectedUnit.CurrentTile.gridPosition} to {_endTilePos}. Resetting.", this);
                             ClearInteractionState();
                        }
                    }
                    else { DebugHelper.Log("Clicked non-reachable tile. Resetting.", this); ClearInteractionState(); }
                 }
                 else { DebugHelper.Log("Clicked outside/invalid state. Resetting.", this); ClearInteractionState(); }
                break;

             case InputState.UnitMoving:
                 DebugHelper.Log("Clicked while unit moving. Input ignored.", this);
                break;
        }
    }

    private void ShowReachableRange(Vector2Int startPos, int range, Unit requestingUnit)
    {
         if (_pathfinder == null) return;
        List<Tile> reachable = _pathfinder.GetReachableTiles(startPos, range, requestingUnit);
        _highlightedReachableTiles.Clear();
        DebugHelper.Log($"Found {reachable.Count} tiles reachable from {startPos} with range {range} for {requestingUnit?.unitName}.", this);

        foreach (Tile tile in reachable) {
             if (tile != null) {
                if (tile != _highlightedStartTile) {
                    _highlightedReachableTiles.Add(tile);
                    tile.SetHighlight(TileHighlightState.MovementRange);
                }
             }
        }
    }

    private void ShowPath(Vector2Int startPos, Vector2Int endPos)
    {
        if (_pathfinder == null) return;
        List<Tile> path = _pathfinder.FindPath(startPos, endPos, _selectedUnit);
        _highlightedPathTiles.Clear();

        if (path != null && path.Count > 0) {
            // DebugHelper.Log($"ShowPath: {path.Count} steps.", this);
            foreach (Tile tile in path) {
                if (tile != _highlightedStartTile) {
                    tile.SetHighlight(TileHighlightState.AttackRange);
                    _highlightedPathTiles.Add(tile); } }
        }
        else {
             DebugHelper.LogWarning($"ShowPath: No path found from {startPos} to {endPos}.", this);
        }
    }

    private void ClearInteractionState()
    {
        List<Tile> tilesToClear = new List<Tile>();
        if (_highlightedStartTile != null) tilesToClear.Add(_highlightedStartTile);
        tilesToClear.AddRange(_highlightedReachableTiles);
        tilesToClear.AddRange(_highlightedPathTiles);

        _highlightedStartTile = null;
        _highlightedReachableTiles.Clear();
        _highlightedPathTiles.Clear();

        HashSet<Tile> processedTiles = new HashSet<Tile>();
        foreach (Tile tile in tilesToClear) {
            if (tile != null && processedTiles.Add(tile)) {
                tile.SetHighlight(TileHighlightState.None); } }

        _selectedUnit = null;
        _endTilePos = null;
        _currentState = InputState.None;
    }

    private void ClearReachableHighlight()
    {
         List<Tile> tilesToClear = new List<Tile>(_highlightedReachableTiles);
        _highlightedReachableTiles.Clear();
        foreach (Tile tile in tilesToClear) {
            if (tile != null && tile != _highlightedStartTile) {
                tile.SetHighlight(TileHighlightState.None); } }
    }

     private IEnumerator MoveUnitAlongPath(Unit unitToMove, List<Tile> path)
    {
        if (unitToMove == null || path == null || path.Count == 0) {
            DebugHelper.LogWarning("MoveUnitAlongPath: Invalid unit or path.", this);
            ClearInteractionState(); yield break; }

        DebugHelper.Log($"Starting movement for {unitToMove.unitName}...", this);

        yield return StartCoroutine(unitToMove.MoveOnPath(path));

        DebugHelper.Log($"{unitToMove.unitName} finished movement.", this);
        ClearInteractionState();
    }
}