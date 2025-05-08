// If in its own file, name it TileHighlightState.cs

/// <summary>
/// Defines the different highlight states a tile can be in.
/// As per GDD 5.1.2.
/// </summary>
public enum TileHighlightState
{
    /// <summary>
    /// No special highlight is applied. Default state.
    /// </summary>
    None,

    /// <summary>
    /// Tile is within the current unit's movement range.
    /// </summary>
    MovementRange,

    /// <summary>
    /// Tile is within the current unit's attack range.
    /// </summary>
    AttackRange,

    /// <summary>
    /// Tile is currently occupied by the selected unit.
    /// </summary>
    SelectedUnit,

    /// <summary>
    /// Tile is currently being hovered over by the mouse cursor.
    /// </summary>
    Hovered
}