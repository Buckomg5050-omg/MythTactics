using UnityEngine;

public class Tile : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }

    public void Initialize(Vector2Int gridPos)
    {
        GridPosition = gridPos;
    }
}