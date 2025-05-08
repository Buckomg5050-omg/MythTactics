// Enums.cs (or any other general enum script name)

// Optional: You can wrap your enums in a namespace for better organization,
// especially in larger projects.
// namespace YourGameName.Enums
// {

    /// <summary>
    /// Defines the different types of terrain available in the game.
    /// As per GDD 5.1.2 (Terrain Types) and GDD 1.2 (World Boundaries).
    /// </summary>
    public enum TerrainType
    {
        /// <summary>
        /// Open, relatively flat land with good visibility and movement.
        /// </summary>
        Plains,

        /// <summary>
        /// Densely wooded area, may offer cover and hinder movement/visibility.
        /// </summary>
        Forest,

        /// <summary>
        /// Rolling or steep elevations, can affect line of sight and movement.
        /// </summary>
        Hills,

        /// <summary>
        /// Uneven, stony ground, potentially difficult to traverse.
        /// </summary>
        RockyGround,

        /// <summary>
        /// Wet, marshy land, significantly hinders movement.
        /// </summary>
        Swamp,

        /// <summary>
        /// Water that can be traversed, but typically at a slower pace.
        /// </summary>
        ShallowWater,

        /// <summary>
        /// High, often snow-capped elevations, typically impassable or very difficult.
        /// </summary>
        MountainPeak,

        /// <summary>
        /// Water that is too deep to traverse without special means (e.g., boats, flying).
        /// </summary>
        DeepWater,

        /// <summary>
        /// Marks the edge of the playable game world, typically impassable.
        /// </summary>
        Boundary
    }

    // You could add other enums here as well, for example:
    // public enum ResourceType { Wood, Stone, Gold }
    // public enum UnitRole { Melee, Ranged, Support }

// } // End of namespace (if you used one)