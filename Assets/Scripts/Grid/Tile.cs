using UnityEngine;

public class Tile : MonoBehaviour
{
    public Vector2Int GridPos { get; private set; }

    public void Init(Vector2Int pos)
    {
        GridPos = pos;
        name = $"Tile_{pos.x}_{pos.y}";
    }
}
