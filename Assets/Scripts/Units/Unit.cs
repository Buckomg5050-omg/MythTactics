using UnityEngine;

public class Unit : MonoBehaviour
{
    public Vector2Int GridPos { get; private set; }

    public void Init(Vector2Int pos)
    {
        GridPos = pos;
        transform.position = new Vector3(pos.x, pos.y);
        name = $"Unit_{pos.x}_{pos.y}";
    }
}
