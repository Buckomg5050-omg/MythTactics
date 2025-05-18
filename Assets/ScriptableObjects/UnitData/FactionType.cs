// FactionType.cs
// Purpose: Defines the allegiance or team of a unit.

public enum FactionType
{
    Player,     // Controlled by the human player
    Enemy,      // Hostile to Player faction
    Ally,       // AI controlled, friendly to Player faction
    Neutral     // Not initially aligned with/against anyone (e.g., civilians, wild animals not immediately hostile)
}