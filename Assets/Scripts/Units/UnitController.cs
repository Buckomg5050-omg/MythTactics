using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Unit))]
public class UnitController : MonoBehaviour
{
    private Unit _unit;
    private GridManager _grid;
    private Pathfinder _pathfinder;
    private bool _isMoving;

    private void Awake()
    {
        _unit = GetComponent<Unit>();
        _grid = Object.FindFirstObjectByType<GridManager>();
        _pathfinder = new Pathfinder(_grid);
    }

    private void Start()
    {
        var tiles = _grid.GetTilesInRange(_unit.GridPos, 3);
        foreach (var t in tiles)
            t.ShowMoveRange();
    }

    private void Update()
    {
        if (_isMoving) return;

        Vector2Int dir = Vector2Int.zero;

        if (Keyboard.current.upArrowKey.wasPressedThisFrame)    dir = Vector2Int.up;
        if (Keyboard.current.downArrowKey.wasPressedThisFrame)  dir = Vector2Int.down;
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)  dir = Vector2Int.left;
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame) dir = Vector2Int.right;

        if (dir != Vector2Int.zero)
        {
            Vector2Int target = _unit.GridPos + dir;
            var path = _pathfinder.FindPath(_unit.GridPos, target);

            if (path != null)
                StartCoroutine(FollowPath(path));
        }
    }

    private IEnumerator FollowPath(List<Tile> path)
    {
        _isMoving = true;

        foreach (var tile in path)
        {
            Vector3 targetPos = new(tile.GridPos.x, tile.GridPos.y);
            while (Vector3.Distance(transform.position, targetPos) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPos, 5f * Time.deltaTime);
                yield return null;
            }

            transform.position = targetPos;
            _unit.Init(tile.GridPos);
        }

        _isMoving = false;
    }
}
