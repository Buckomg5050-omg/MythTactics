public enum TileHighlightState
{
    None,
    MovementRange,
    AttackRange,
    SelectedUnit,
    Hovered,
    Path,
    ActiveTurnUnit, // Added for the unit whose turn it is
    AbilityRange    // NEW: For highlighting ability target range
}