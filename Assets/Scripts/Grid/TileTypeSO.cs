using UnityEngine;

[CreateAssetMenu(fileName = "NewTileType", menuName = "TRPG/Tile Type")]
public class TileTypeSO : ScriptableObject
{
    public string DisplayName;
    public Sprite TileSprite;
    public float MovementCostMultiplier = 1f;
    public bool IsPassable = true;
}