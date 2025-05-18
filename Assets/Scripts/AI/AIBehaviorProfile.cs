// AIBehaviorProfile.cs
// Purpose: Defines the general strategy or decision-making style for an AI-controlled unit.

public enum AIBehaviorProfile
{
    None,        // No specific profile, might use default logic or be passive
    Balanced,    // Attempts to balance offense, defense, and support based on situation
    Aggressive,  // Prioritizes offensive actions and engaging enemies
    Defensive,   // Prioritizes self-preservation, protecting allies, and holding positions
    Support,     // Prioritizes healing, buffing allies, and debuffing enemies
    Skirmisher,  // Prefers hit-and-run tactics, may try to maintain range
    Guardian     // Focuses on protecting a specific unit or objective
}