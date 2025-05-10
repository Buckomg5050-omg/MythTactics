// EffectSO.cs
using UnityEngine;
using System.Collections.Generic; // For List<StatModifier> and List<EffectTypeTag>

public enum EffectDurationType
{
    Rounds,
    Permanent,
}

public enum EffectStackingBehavior
{
    None,
    RefreshDuration,
    AddNewInstance,
    IncreaseStacks,
}

[CreateAssetMenu(fileName = "NewEffect", menuName = "MythTactics/Effect")]
public class EffectSO : ScriptableObject
{
    [Header("Identification")]
    public string effectID;
    public string effectName = "New Effect";
    [TextArea(2, 4)]
    public string description = "Effect description.";
    public Sprite icon;

    [Header("Duration & Stacking")]
    // CORRECTED LINE:
    public EffectDurationType durationType = EffectDurationType.Rounds;
    [Tooltip("Number of rounds/turns the effect lasts. Not used if durationType is Permanent.")]
    public int duration = 3;
    [Tooltip("Maximum number of times this effect can stack on a single unit.")]
    public int maxStacks = 1;
    public EffectStackingBehavior stackingBehavior = EffectStackingBehavior.RefreshDuration;

    [Header("Core Mechanics")]
    [Tooltip("Modifiers to primary or derived stats.")]
    public List<StatModifier> statModifiers;

    [Tooltip("Tags to categorize the effect (e.g., Buff, Debuff, Poison, Magic, Physical). Used for immunities, cleansing, interactions.")]
    public List<string> effectTypeTags;
}