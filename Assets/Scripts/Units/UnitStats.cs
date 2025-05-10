// UnitStats.cs
using UnityEngine;
using System.Text; // For StringBuilder in logging
using System.Collections.Generic; // For List<ActiveStatusEffect>
using System.Linq; // For Linq operations like Sum()

public class UnitStats : MonoBehaviour
{
    [Header("References (Set by Unit.cs)")]
    private RaceDataSO _raceData;
    private ClassDataSO _classData;
    private Unit _unit;

    [Header("Primary Attributes")]
    public UnitPrimaryAttributes currentAttributes = new UnitPrimaryAttributes(); // These are BASE attributes before effects

    // NEW: Effective Attributes - Calculated by applying status effect modifiers to base currentAttributes
    // These are properties that will calculate on the fly.
    public UnitPrimaryAttributes EffectiveAttributes
    {
        get
        {
            // Start with a copy of base attributes
            UnitPrimaryAttributes effective = new UnitPrimaryAttributes
            {
                Core = currentAttributes.Core,
                Echo = currentAttributes.Echo,
                Pulse = currentAttributes.Pulse,
                Spark = currentAttributes.Spark,
                Glimmer = currentAttributes.Glimmer,
                Aura = currentAttributes.Aura
            };

            // Apply flat modifiers first
            foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.Flat))
            {
                switch (mod.stat)
                {
                    case StatType.Core: effective.Core += (int)mod.value; break;
                    case StatType.Echo: effective.Echo += (int)mod.value; break;
                    case StatType.Pulse: effective.Pulse += (int)mod.value; break;
                    case StatType.Spark: effective.Spark += (int)mod.value; break;
                    case StatType.Glimmer: effective.Glimmer += (int)mod.value; break;
                    case StatType.Aura: effective.Aura += (int)mod.value; break;
                }
            }

            // Apply PercentAdd modifiers (based on original base)
            foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.PercentAdd))
            {
                switch (mod.stat)
                {
                    // Example: effective.Core += Mathf.RoundToInt(currentAttributes.Core * mod.value);
                    // Note: PercentAdd on primary attributes might be complex if they also have flat mods.
                    // For simplicity, let's assume PercentAdd on primary attributes applies to the *base* value.
                    case StatType.Core: effective.Core += Mathf.RoundToInt(currentAttributes.Core * mod.value); break;
                    case StatType.Echo: effective.Echo += Mathf.RoundToInt(currentAttributes.Echo * mod.value); break;
                    case StatType.Pulse: effective.Pulse += Mathf.RoundToInt(currentAttributes.Pulse * mod.value); break;
                    case StatType.Spark: effective.Spark += Mathf.RoundToInt(currentAttributes.Spark * mod.value); break;
                    case StatType.Glimmer: effective.Glimmer += Mathf.RoundToInt(currentAttributes.Glimmer * mod.value); break;
                    case StatType.Aura: effective.Aura += Mathf.RoundToInt(currentAttributes.Aura * mod.value); break;
                }
            }
            
            // Apply PercentMult modifiers (based on current effective value after flat and percent_add)
            foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.PercentMult))
            {
                 switch (mod.stat)
                {
                    case StatType.Core: effective.Core = Mathf.RoundToInt(effective.Core * (1 + mod.value)); break;
                    case StatType.Echo: effective.Echo = Mathf.RoundToInt(effective.Echo * (1 + mod.value)); break;
                    case StatType.Pulse: effective.Pulse = Mathf.RoundToInt(effective.Pulse * (1 + mod.value)); break;
                    case StatType.Spark: effective.Spark = Mathf.RoundToInt(effective.Spark * (1 + mod.value)); break;
                    case StatType.Glimmer: effective.Glimmer = Mathf.RoundToInt(effective.Glimmer * (1 + mod.value)); break;
                    case StatType.Aura: effective.Aura = Mathf.RoundToInt(effective.Aura * (1 + mod.value)); break;
                }
            }

            // Ensure attributes don't go below a minimum (e.g., 1 or 0)
            effective.Core = Mathf.Max(0, effective.Core);
            effective.Echo = Mathf.Max(0, effective.Echo);
            effective.Pulse = Mathf.Max(0, effective.Pulse);
            effective.Spark = Mathf.Max(0, effective.Spark);
            effective.Glimmer = Mathf.Max(0, effective.Glimmer);
            effective.Aura = Mathf.Max(0, effective.Aura);
            
            return effective;
        }
    }


    [Header("Action Points (AP)")]
    [Tooltip("Maximum Action Points. Default is 2 as per GDD 3.1.")]
    [SerializeField] private int _baseMaxActionPoints = 2;
    public int currentActionPoints;
    // MaxActionPoints will now be calculated with modifiers
    public int MaxActionPoints => CalculateModifiedStat(_baseMaxActionPoints, StatType.MaxActionPoints);


    [Header("Health (VP - Vitality Points)")]
    [SerializeField] private int _baseMaxVitalityPoints; // Store the value before modifiers
    public int currentVitalityPoints;
    // MaxVitalityPoints is now a calculated property
    public int MaxVitalityPoints => CalculateModifiedStat(_baseMaxVitalityPoints, StatType.MaxVitalityPoints);
    [Tooltip("Amount of VP regenerated at the start of this unit's turn. Default 0.")]
    public int baseVitalityRegenRate = 0;
    public int VitalityRegenRate => CalculateModifiedStat(baseVitalityRegenRate, StatType.VitalityRegenRate);


    [Header("Mana (MP - Mana Points)")]
    [SerializeField] private int _baseMaxManaPoints;
    public int currentManaPoints;
    public int MaxManaPoints => CalculateModifiedStat(_baseMaxManaPoints, StatType.MaxManaPoints);
    [Tooltip("Amount of MP regenerated at the start of this unit's turn.")]
    public int baseManaRegenRate = 1;
    public int ManaRegenRate => CalculateModifiedStat(baseManaRegenRate, StatType.ManaRegenRate);


    [Header("Stamina (SP - Stamina Points)")]
    [SerializeField] private int _baseMaxStaminaPoints;
    public int currentStaminaPoints;
    public int MaxStaminaPoints => CalculateModifiedStat(_baseMaxStaminaPoints, StatType.MaxStaminaPoints);
    [Tooltip("Amount of SP regenerated at the start of this unit's turn.")]
    public int baseStaminaRegenRate = 1;
    public int StaminaRegenRate => CalculateModifiedStat(baseStaminaRegenRate, StatType.StaminaRegenRate);


    [Header("Focus (FP - Focus Points)")]
    [SerializeField] private int _baseMaxFocusPoints;
    public int currentFocusPoints;
    public int MaxFocusPoints => CalculateModifiedStat(_baseMaxFocusPoints, StatType.MaxFocusPoints);
    [Tooltip("Amount of FP regenerated at the start of this unit's turn.")]
    public int baseFocusRegenRate = 1;
    public int FocusRegenRate => CalculateModifiedStat(baseFocusRegenRate, StatType.FocusRegenRate);


    [Header("Influence (IP - Influence Points)")]
    [SerializeField] private int _baseMaxInfluencePoints;
    public int currentInfluencePoints;
    public int MaxInfluencePoints => CalculateModifiedStat(_baseMaxInfluencePoints, StatType.MaxInfluencePoints);
    [Tooltip("Amount of IP regenerated at the start of this unit's turn.")]
    public int baseInfluenceRegenRate = 1;
    public int InfluenceRegenRate => CalculateModifiedStat(baseInfluenceRegenRate, StatType.InfluenceRegenRate);


    // NEW: List to store active status effects
    [Header("Status Effects")]
    private List<ActiveStatusEffect> _activeEffects = new List<ActiveStatusEffect>();
    public IReadOnlyList<ActiveStatusEffect> ActiveEffects => _activeEffects.AsReadOnly();


    private bool _isAlive = true;
    public bool IsAlive => _isAlive;

    public void Initialize(Unit unit, RaceDataSO raceData, ClassDataSO classData, UnitPrimaryAttributes attributes)
    {
        _unit = unit;
        _raceData = raceData;
        _classData = classData;
        if (attributes != null)
        {
            currentAttributes = attributes; // These are the BASE attributes
        }
        else
        {
            currentAttributes = new UnitPrimaryAttributes();
            DebugHelper.LogWarning($"UnitStats for {_unit.unitName} received null attributes, using defaults.", _unit);
        }
        _activeEffects.Clear(); // Ensure no lingering effects on initialization
        InitializeDerivedAttributesAndResources(); // This will now use EffectiveAttributes for calculations
    }

    private void InitializeDerivedAttributesAndResources()
    {
        if (_raceData == null || _classData == null || currentAttributes == null || _unit == null)
        {
            DebugHelper.LogError($"UnitStats on {_unit?.unitName ?? "Unknown Unit"} cannot initialize resources. Missing RaceData, ClassData, Attributes, or Unit reference.", this);
            _isAlive = false;
            return;
        }

        // Initialize base values for stats that can be modified by effects
        // These will be calculated using EffectiveAttributes and equipment bonuses
        _baseMaxVitalityPoints = CalculatedMaxVP_Base;
        _baseMaxManaPoints = CalculatedMaxMP_Base;
        _baseMaxStaminaPoints = CalculatedMaxSP_Base;
        _baseMaxFocusPoints = CalculatedMaxFP_Base;
        _baseMaxInfluencePoints = CalculatedMaxIP_Base;
        // _baseMaxActionPoints is already serialized

        // Current resources are set to their (potentially modified) max values
        currentVitalityPoints = MaxVitalityPoints;
        currentManaPoints = MaxManaPoints;
        currentStaminaPoints = MaxStaminaPoints;
        currentFocusPoints = MaxFocusPoints;
        currentInfluencePoints = MaxInfluencePoints;
        currentActionPoints = MaxActionPoints;


        _isAlive = (currentVitalityPoints > 0);

        if (_isAlive)
        {
            // Clamping based on potentially modified Max values
            if (MaxVitalityPoints <= 0) _baseMaxVitalityPoints = 1; // Ensure base is at least 1 if alive
            currentVitalityPoints = Mathf.Clamp(currentVitalityPoints, 0, MaxVitalityPoints);
            if (currentVitalityPoints <= 0 && MaxVitalityPoints > 0) currentVitalityPoints = MaxVitalityPoints; // If somehow initialized to 0 but max > 0

            if (MaxManaPoints < 0) _baseMaxManaPoints = 0; currentManaPoints = Mathf.Clamp(currentManaPoints, 0, MaxManaPoints);
            if (MaxStaminaPoints < 0) _baseMaxStaminaPoints = 0; currentStaminaPoints = Mathf.Clamp(currentStaminaPoints, 0, MaxStaminaPoints);
            if (MaxFocusPoints < 0) _baseMaxFocusPoints = 0; currentFocusPoints = Mathf.Clamp(currentFocusPoints, 0, MaxFocusPoints);
            if (MaxInfluencePoints < 0) _baseMaxInfluencePoints = 0; currentInfluencePoints = Mathf.Clamp(currentInfluencePoints, 0, MaxInfluencePoints);
        }
        else
        {
            currentVitalityPoints = 0;
            currentManaPoints = 0;
            currentStaminaPoints = 0;
            currentFocusPoints = 0;
            currentInfluencePoints = 0;
            currentActionPoints = 0;
        }

        DebugHelper.Log($"{_unit.unitName} (UnitStats) Initialized Resources: " +
                        $"AP: {currentActionPoints}/{MaxActionPoints}, " +
                        $"VP: {currentVitalityPoints}/{MaxVitalityPoints} (Alive: {_isAlive}), " +
                        $"MP: {currentManaPoints}/{MaxManaPoints}, " +
                        $"SP: {currentStaminaPoints}/{MaxStaminaPoints}, " +
                        $"FP: {currentFocusPoints}/{MaxFocusPoints}, " +
                        $"IP: {currentInfluencePoints}/{MaxInfluencePoints}", _unit);
    }

    // --- Base Calculated Properties (without status effects, but WITH equipment) ---
    // These will be used as the 'baseValue' for CalculateModifiedStat for Max Resources
    // And they use EffectiveAttributes for primary stat contributions
    private int CalculatedMaxVP_Base { get { UnitPrimaryAttributes effAttrs = EffectiveAttributes; int baseVpFromRace = (_raceData != null) ? _raceData.baseVPContribution : 0; int baseVpFromClass = (_classData != null) ? _classData.baseVPContribution : 0; int pulseBonus = (effAttrs != null) ? effAttrs.Pulse * 5 : 0; int vpFromEquipment = 0; /* TODO: Add equipment bonus */ return Mathf.Max(1, baseVpFromRace + baseVpFromClass + pulseBonus + vpFromEquipment); } }
    private int CalculatedMaxMP_Base { get { UnitPrimaryAttributes effAttrs = EffectiveAttributes; int baseMpFromRace = (_raceData != null) ? _raceData.baseMPContribution : 0; int baseMpFromClass = (_classData != null) ? _classData.baseMPContribution : 0; int sparkBonus = (effAttrs != null) ? effAttrs.Spark * 2 : 0; int mpFromEquipment = 0; /* TODO: Add equipment bonus */ return Mathf.Max(0, baseMpFromRace + baseMpFromClass + sparkBonus + mpFromEquipment); } }
    private int CalculatedMaxSP_Base { get { UnitPrimaryAttributes effAttrs = EffectiveAttributes; int baseSpFromRace = (_raceData != null) ? _raceData.baseSPContribution : 0; int baseSpFromClass = (_classData != null) ? _classData.baseSPContribution : 0; int coreBonus = (effAttrs != null) ? effAttrs.Core : 0; int spFromEquipment = 0; /* TODO: Add equipment bonus */ return Mathf.Max(0, baseSpFromRace + baseSpFromClass + coreBonus + spFromEquipment); } }
    private int CalculatedMaxFP_Base { get { UnitPrimaryAttributes effAttrs = EffectiveAttributes; int baseFpFromRace = (_raceData != null) ? _raceData.baseFPContribution : 0; int baseFpFromClass = (_classData != null) ? _classData.baseFPContribution : 0; int glimmerBonus = (effAttrs != null) ? effAttrs.Glimmer : 0; int fpFromEquipment = 0; /* TODO: Add equipment bonus */ return Mathf.Max(0, baseFpFromRace + baseFpFromClass + glimmerBonus + fpFromEquipment); } }
    private int CalculatedMaxIP_Base { get { UnitPrimaryAttributes effAttrs = EffectiveAttributes; int baseIpFromRace = (_raceData != null) ? _raceData.baseIPContribution : 0; int baseIpFromClass = (_classData != null) ? _classData.baseIPContribution : 0; int auraBonus = (effAttrs != null) ? effAttrs.Aura : 0; int ipFromEquipment = 0; /* TODO: Add equipment bonus */ return Mathf.Max(0, baseIpFromRace + baseIpFromClass + auraBonus + ipFromEquipment); } }


    public void ModifyVitality(int amount)
    {
        if (!_isAlive && amount < 0) return;
        currentVitalityPoints += amount;
        currentVitalityPoints = Mathf.Clamp(currentVitalityPoints, 0, MaxVitalityPoints); // Use modified MaxVitalityPoints
        if (currentVitalityPoints <= 0) { SetAliveStatus(false); } // SetAliveStatus will zero AP etc.
        else if (currentVitalityPoints > 0 && !_isAlive) { _isAlive = true; } // Revive logic might need more here
    }

    public void SetAliveStatus(bool alive)
    {
        _isAlive = alive;
        if (!_isAlive)
        {
            currentVitalityPoints = 0;
            currentActionPoints = 0;
            // Consider clearing _activeEffects or marking them inactive on death
            // For now, they persist but might not tick or apply if unit is dead.
        }
    }

    public bool SpendActionPoints(int amount)
    {
        if (amount <= 0) return true;
        if (currentActionPoints >= amount)
        {
            currentActionPoints -= amount;
            DebugHelper.Log($"{_unit.unitName} spent {amount} AP. Remaining: {currentActionPoints}/{MaxActionPoints}", _unit);
            return true;
        }
        DebugHelper.LogWarning($"{_unit.unitName} failed to spend {amount} AP. Has: {currentActionPoints}/{MaxActionPoints}", _unit);
        return false;
    }

    public void RegenerateActionPointsAtTurnStart()
    {
        if (!_isAlive)
        {
            currentActionPoints = 0;
            return;
        }
        // AP Regen might also be modified by effects if StatType.APRegenRate exists
        currentActionPoints = MaxActionPoints; // GDD: Full regen to max
        DebugHelper.Log($"{_unit.unitName} AP regenerated to {currentActionPoints}/{MaxActionPoints} at turn start.", _unit);
    }

    public void SpendMana(int amount) { if (amount <= 0) return; currentManaPoints -= amount; currentManaPoints = Mathf.Max(0, currentManaPoints); }
    public void SpendStamina(int amount) { if (amount <= 0) return; currentStaminaPoints -= amount; currentStaminaPoints = Mathf.Max(0, currentStaminaPoints); }
    public void SpendFocus(int amount) { if (amount <= 0) return; currentFocusPoints -= amount; currentFocusPoints = Mathf.Max(0, currentFocusPoints); }
    public void SpendInfluence(int amount) { if (amount <= 0) return; currentInfluencePoints -= amount; currentInfluencePoints = Mathf.Max(0, currentInfluencePoints); }

    public void RegenerateResourcesAtTurnStart()
    {
        if (!_isAlive || _unit == null) return;

        StringBuilder regenLogBuilder = new StringBuilder();
        regenLogBuilder.Append($"REGEN LOG for {_unit.unitName}: ");
        bool hasRegeneratedAnything = false;

        if (VitalityRegenRate > 0 && currentVitalityPoints < MaxVitalityPoints && currentVitalityPoints > 0)
        {
            int oldVP = currentVitalityPoints;
            currentVitalityPoints = Mathf.Min(currentVitalityPoints + VitalityRegenRate, MaxVitalityPoints);
            if (currentVitalityPoints != oldVP) { regenLogBuilder.Append($"VP +{currentVitalityPoints - oldVP} ({currentVitalityPoints}/{MaxVitalityPoints}). "); hasRegeneratedAnything = true; }
        }
        if (ManaRegenRate > 0 && currentManaPoints < MaxManaPoints)
        {
            int oldMP = currentManaPoints;
            currentManaPoints = Mathf.Min(currentManaPoints + ManaRegenRate, MaxManaPoints);
            if (currentManaPoints != oldMP) { regenLogBuilder.Append($"MP +{currentManaPoints - oldMP} ({currentManaPoints}/{MaxManaPoints}). "); hasRegeneratedAnything = true; }
        }
        if (StaminaRegenRate > 0 && currentStaminaPoints < MaxStaminaPoints)
        {
            int oldSP = currentStaminaPoints;
            currentStaminaPoints = Mathf.Min(currentStaminaPoints + StaminaRegenRate, MaxStaminaPoints);
            if (currentStaminaPoints != oldSP) { regenLogBuilder.Append($"SP +{currentStaminaPoints - oldSP} ({currentStaminaPoints}/{MaxStaminaPoints}). "); hasRegeneratedAnything = true; }
        }
        if (FocusRegenRate > 0 && currentFocusPoints < MaxFocusPoints)
        {
            int oldFP = currentFocusPoints;
            currentFocusPoints = Mathf.Min(currentFocusPoints + FocusRegenRate, MaxFocusPoints);
            if (currentFocusPoints != oldFP) { regenLogBuilder.Append($"FP +{currentFocusPoints - oldFP} ({currentFocusPoints}/{MaxFocusPoints}). "); hasRegeneratedAnything = true; }
        }
        if (InfluenceRegenRate > 0 && currentInfluencePoints < MaxInfluencePoints)
        {
            int oldIP = currentInfluencePoints;
            currentInfluencePoints = Mathf.Min(currentInfluencePoints + InfluenceRegenRate, MaxInfluencePoints);
            if (currentInfluencePoints != oldIP) { regenLogBuilder.Append($"IP +{currentInfluencePoints - oldIP} ({currentInfluencePoints}/{MaxInfluencePoints}). "); hasRegeneratedAnything = true; }
        }

        if (hasRegeneratedAnything) DebugHelper.Log(regenLogBuilder.ToString().TrimEnd(), _unit);
    }

    // --- NEW Status Effect Management Methods ---
    public void AddEffect(ActiveStatusEffect newEffect)
    {
        // Basic add. Full logic (stacking, replacement) will be in EffectSystem.
        _activeEffects.Add(newEffect);
        RecalculateAffectedStats(); // Recalculate max values if effect modifies them
        DebugHelper.Log($"{_unit.unitName} received effect: {newEffect.BaseEffect.effectName}", _unit);
    }

    public void RemoveEffect(ActiveStatusEffect effectToRemove)
    {
        if (_activeEffects.Remove(effectToRemove))
        {
            RecalculateAffectedStats();
            DebugHelper.Log($"{_unit.unitName} lost effect: {effectToRemove.BaseEffect.effectName}", _unit);
        }
    }
    
    public void RemoveAllEffectsFromSource(Unit caster)
    {
        List<ActiveStatusEffect> toRemove = _activeEffects.Where(e => e.Caster == caster).ToList();
        foreach(var effect in toRemove)
        {
            _activeEffects.Remove(effect);
            DebugHelper.Log($"{_unit.unitName} lost effect: {effect.BaseEffect.effectName} (removed by source: {caster.unitName})", _unit);
        }
        if (toRemove.Count > 0) RecalculateAffectedStats();
    }

    public void ClearAllEffects()
    {
        if (_activeEffects.Count > 0)
        {
            _activeEffects.Clear();
            RecalculateAffectedStats();
            DebugHelper.Log($"{_unit.unitName} had all effects cleared.", _unit);
        }
    }

    /// <summary>
    /// This method should be called whenever an effect is added/removed that might change max resource values
    /// or other stats that need immediate re-evaluation.
    /// </summary>
    public void RecalculateAffectedStats()
    {
        // Example: If MaxVP changes, ensure currentVP is not over the new max.
        currentVitalityPoints = Mathf.Clamp(currentVitalityPoints, 0, MaxVitalityPoints);
        currentManaPoints = Mathf.Clamp(currentManaPoints, 0, MaxManaPoints);
        currentStaminaPoints = Mathf.Clamp(currentStaminaPoints, 0, MaxStaminaPoints);
        currentFocusPoints = Mathf.Clamp(currentFocusPoints, 0, MaxFocusPoints);
        currentInfluencePoints = Mathf.Clamp(currentInfluencePoints, 0, MaxInfluencePoints);
        currentActionPoints = Mathf.Clamp(currentActionPoints, 0, MaxActionPoints);

        // Potentially fire an event here for UI to update if stats changed:
        // OnStatsChanged?.Invoke();
        DebugHelper.Log($"{_unit.unitName} stats recalculated due to effect change. VP: {currentVitalityPoints}/{MaxVitalityPoints}, AP: {currentActionPoints}/{MaxActionPoints}", _unit);
    }

    /// <summary>
    /// Gets all stat modifiers of a specific type from all active effects.
    /// </summary>
    private List<StatModifier> GetAllStatModifiersFromEffects(ModifierType modType)
    {
        List<StatModifier> modifiers = new List<StatModifier>();
        foreach (ActiveStatusEffect effect in _activeEffects)
        {
            if (effect.BaseEffect != null && effect.BaseEffect.statModifiers != null)
            {
                foreach (StatModifier mod in effect.BaseEffect.statModifiers)
                {
                    if (mod.type == modType)
                    {
                        // Consider effect stacks for value if applicable by effect design
                        // For now, just add the modifier as is. Stacking multipliers on value is complex.
                        modifiers.Add(mod);
                    }
                }
            }
        }
        return modifiers;
    }
    
    /// <summary>
    /// Calculates a final stat value after applying modifiers from effects.
    /// Order: Flat, then PercentAdd, then PercentMult.
    /// </summary>
    /// <param name="baseValue">The base value of the stat (e.g., from race/class/equipment before temporary effects).</param>
    /// <param name="targetStat">The StatType to calculate for.</param>
    /// <returns>The final calculated stat value.</returns>
    public int CalculateModifiedStat(int baseValue, StatType targetStat)
    {
        float currentValue = baseValue;

        // 1. Apply Flat modifiers
        foreach (ActiveStatusEffect effect in _activeEffects)
        {
            if (effect.BaseEffect?.statModifiers == null) continue;
            foreach (StatModifier mod in effect.BaseEffect.statModifiers)
            {
                if (mod.stat == targetStat && mod.type == ModifierType.Flat)
                {
                    currentValue += mod.value * effect.CurrentStacks; // Apply stacks to flat value
                }
            }
        }

        // 2. Apply PercentAdd modifiers (additive with each other, applied to the value after Flat mods)
        float totalPercentAdd = 0;
        foreach (ActiveStatusEffect effect in _activeEffects)
        {
            if (effect.BaseEffect?.statModifiers == null) continue;
            foreach (StatModifier mod in effect.BaseEffect.statModifiers)
            {
                if (mod.stat == targetStat && mod.type == ModifierType.PercentAdd)
                {
                    totalPercentAdd += mod.value * effect.CurrentStacks; // Apply stacks to percentage
                }
            }
        }
        currentValue += baseValue * totalPercentAdd; // PercentAdd applies to original base or value after flat? GDD implies base usually. Let's use baseValue.

        // 3. Apply PercentMult modifiers (multiplicative with each other)
        foreach (ActiveStatusEffect effect in _activeEffects)
        {
            if (effect.BaseEffect?.statModifiers == null) continue;
            foreach (StatModifier mod in effect.BaseEffect.statModifiers)
            {
                if (mod.stat == targetStat && mod.type == ModifierType.PercentMult)
                {
                    // Apply stacks to multiplier: e.g. 2 stacks of +10% (0.1) mult = (1 + 0.1*2) = 1.2x
                    currentValue *= (1f + (mod.value * effect.CurrentStacks)); 
                }
            }
        }
        
        // Ensure non-negative for typical stats, specific clamping might be needed per stat type
        return Mathf.Max(0, Mathf.RoundToInt(currentValue));
    }
}