// UnitStats.cs
using UnityEngine;
using System.Text;
using System.Collections.Generic;
using System.Linq;

public class UnitStats : MonoBehaviour
{
    [Header("References (Set by Unit.cs)")]
    private RaceDataSO _raceData;
    private ClassDataSO _classData;
    private Unit _unit;

    [Header("Primary Attributes")]
    public UnitPrimaryAttributes currentAttributes = new UnitPrimaryAttributes(); // BASE attributes

    public UnitPrimaryAttributes EffectiveAttributes
    {
        get
        {
            UnitPrimaryAttributes effective = new UnitPrimaryAttributes
            {
                Core = currentAttributes.Core,
                Echo = currentAttributes.Echo,
                Pulse = currentAttributes.Pulse,
                Spark = currentAttributes.Spark,
                Glimmer = currentAttributes.Glimmer,
                Aura = currentAttributes.Aura
            };

            // Apply flat modifiers (value is stack-multiplied in GetAllStatModifiersFromEffects)
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

            // Store values after flat mods to be base for PercentAdd
            float baseForPercentAddCore = effective.Core;
            float baseForPercentAddEcho = effective.Echo;
            float baseForPercentAddPulse = effective.Pulse;
            float baseForPercentAddSpark = effective.Spark;
            float baseForPercentAddGlimmer = effective.Glimmer;
            float baseForPercentAddAura = effective.Aura;

            // Apply PercentAdd modifiers (value is stack-multiplied in GetAllStatModifiersFromEffects)
            // PercentAdd applies to the value *after* flat mods for primary attributes here.
            foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.PercentAdd))
            {
                switch (mod.stat)
                {
                    case StatType.Core: effective.Core += Mathf.RoundToInt(baseForPercentAddCore * mod.value); break;
                    case StatType.Echo: effective.Echo += Mathf.RoundToInt(baseForPercentAddEcho * mod.value); break;
                    case StatType.Pulse: effective.Pulse += Mathf.RoundToInt(baseForPercentAddPulse * mod.value); break;
                    case StatType.Spark: effective.Spark += Mathf.RoundToInt(baseForPercentAddSpark * mod.value); break;
                    case StatType.Glimmer: effective.Glimmer += Mathf.RoundToInt(baseForPercentAddGlimmer * mod.value); break;
                    case StatType.Aura: effective.Aura += Mathf.RoundToInt(baseForPercentAddAura * mod.value); break;
                }
            }
            
            // Apply PercentMult modifiers (value is stack-multiplied in GetAllStatModifiersFromEffects)
            foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.PercentMult))
            {
                 switch (mod.stat) // mod.value here is the percentage, e.g., 0.1 for +10%
                {
                    case StatType.Core: effective.Core = Mathf.RoundToInt(effective.Core * (1f + mod.value)); break;
                    case StatType.Echo: effective.Echo = Mathf.RoundToInt(effective.Echo * (1f + mod.value)); break;
                    case StatType.Pulse: effective.Pulse = Mathf.RoundToInt(effective.Pulse * (1f + mod.value)); break;
                    case StatType.Spark: effective.Spark = Mathf.RoundToInt(effective.Spark * (1f + mod.value)); break;
                    case StatType.Glimmer: effective.Glimmer = Mathf.RoundToInt(effective.Glimmer * (1f + mod.value)); break;
                    case StatType.Aura: effective.Aura = Mathf.RoundToInt(effective.Aura * (1f + mod.value)); break;
                }
            }

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
    [SerializeField] private int _baseMaxActionPoints = 2;
    public int currentActionPoints;
    public int MaxActionPoints => CalculateModifiedStat(_baseMaxActionPoints, StatType.MaxActionPoints);

    [Header("Health (VP - Vitality Points)")]
    [SerializeField] private int _baseMaxVitalityPoints;
    public int currentVitalityPoints;
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

        // currentAttributes is initialized by its field declaration "new UnitPrimaryAttributes()"
        // If attributes are provided, copy their values into currentAttributes.
        if (attributes != null)
        {
            currentAttributes.Core = attributes.Core;
            currentAttributes.Echo = attributes.Echo;
            currentAttributes.Pulse = attributes.Pulse;
            currentAttributes.Spark = attributes.Spark;
            currentAttributes.Glimmer = attributes.Glimmer;
            currentAttributes.Aura = attributes.Aura;
        }
        else
        {
            // No attributes provided, currentAttributes will use its default initialized values (likely all 0s or what you set in UnitPrimaryAttributes constructor)
            DebugHelper.LogWarning($"UnitStats for {_unit.unitName} received null initial attributes, using defaults (Core: {currentAttributes.Core}, etc.).", _unit);
        }
        _activeEffects.Clear();
        InitializeDerivedAttributesAndResources(); // This also calls RecalculateAffectedStats
    }

    private void InitializeDerivedAttributesAndResources()
    {
        if (_raceData == null || _classData == null || currentAttributes == null || _unit == null)
        {
            DebugHelper.LogError($"UnitStats on {_unit?.unitName ?? "Unknown Unit"} cannot initialize resources. Missing RaceData, ClassData, Attributes, or Unit reference.", this);
            _isAlive = false;
            return;
        }

        // Initialize _baseMax... fields. These use EffectiveAttributes which in turn use currentAttributes (base)
        // So, at this very first initialization, EffectiveAttributes will be the same as currentAttributes.
        _baseMaxVitalityPoints = CalculatedMaxVP_Base;
        _baseMaxManaPoints = CalculatedMaxMP_Base;
        _baseMaxStaminaPoints = CalculatedMaxSP_Base;
        _baseMaxFocusPoints = CalculatedMaxFP_Base;
        _baseMaxInfluencePoints = CalculatedMaxIP_Base;

        // Now that _baseMax... are set, Max... properties will use them plus any (currently none) effects.
        currentVitalityPoints = MaxVitalityPoints;
        currentManaPoints = MaxManaPoints;
        currentStaminaPoints = MaxStaminaPoints;
        currentFocusPoints = MaxFocusPoints;
        currentInfluencePoints = MaxInfluencePoints;
        currentActionPoints = MaxActionPoints;

        _isAlive = (currentVitalityPoints > 0);

        if (_isAlive)
        {
            if (MaxVitalityPoints <= 0) { _baseMaxVitalityPoints = 1; currentVitalityPoints = MaxVitalityPoints; } // Adjust base if necessary
            if (currentVitalityPoints <= 0 && MaxVitalityPoints > 0) currentVitalityPoints = MaxVitalityPoints;
            
            if (MaxManaPoints < 0) _baseMaxManaPoints = 0; currentManaPoints = Mathf.Clamp(currentManaPoints, 0, MaxManaPoints);
            if (MaxStaminaPoints < 0) _baseMaxStaminaPoints = 0; currentStaminaPoints = Mathf.Clamp(currentStaminaPoints, 0, MaxStaminaPoints);
            if (MaxFocusPoints < 0) _baseMaxFocusPoints = 0; currentFocusPoints = Mathf.Clamp(currentFocusPoints, 0, MaxFocusPoints);
            if (MaxInfluencePoints < 0) _baseMaxInfluencePoints = 0; currentInfluencePoints = Mathf.Clamp(currentInfluencePoints, 0, MaxInfluencePoints);
        }
        else
        {
            currentVitalityPoints = 0; currentManaPoints = 0; currentStaminaPoints = 0;
            currentFocusPoints = 0; currentInfluencePoints = 0; currentActionPoints = 0;
        }
        // RecalculateAffectedStats will be called effectively by the above setters to Max... properties
        // But call it explicitly here to ensure the detailed log runs once after full init.
        RecalculateAffectedStats(); 
    }

    private int CalculatedMaxVP_Base { get { UnitPrimaryAttributes effAttrs = EffectiveAttributes; int baseVpFromRace = (_raceData != null) ? _raceData.baseVPContribution : 0; int baseVpFromClass = (_classData != null) ? _classData.baseVPContribution : 0; int pulseBonus = (effAttrs != null) ? effAttrs.Pulse * 5 : 0; int vpFromEquipment = 0; return Mathf.Max(1, baseVpFromRace + baseVpFromClass + pulseBonus + vpFromEquipment); } }
    private int CalculatedMaxMP_Base { get { UnitPrimaryAttributes effAttrs = EffectiveAttributes; int baseMpFromRace = (_raceData != null) ? _raceData.baseMPContribution : 0; int baseMpFromClass = (_classData != null) ? _classData.baseMPContribution : 0; int sparkBonus = (effAttrs != null) ? effAttrs.Spark * 2 : 0; int mpFromEquipment = 0; return Mathf.Max(0, baseMpFromRace + baseMpFromClass + sparkBonus + mpFromEquipment); } }
    private int CalculatedMaxSP_Base { get { UnitPrimaryAttributes effAttrs = EffectiveAttributes; int baseSpFromRace = (_raceData != null) ? _raceData.baseSPContribution : 0; int baseSpFromClass = (_classData != null) ? _classData.baseSPContribution : 0; int coreBonus = (effAttrs != null) ? effAttrs.Core : 0; int spFromEquipment = 0; return Mathf.Max(0, baseSpFromRace + baseSpFromClass + coreBonus + spFromEquipment); } }
    private int CalculatedMaxFP_Base { get { UnitPrimaryAttributes effAttrs = EffectiveAttributes; int baseFpFromRace = (_raceData != null) ? _raceData.baseFPContribution : 0; int baseFpFromClass = (_classData != null) ? _classData.baseFPContribution : 0; int glimmerBonus = (effAttrs != null) ? effAttrs.Glimmer : 0; int fpFromEquipment = 0; return Mathf.Max(0, baseFpFromRace + baseFpFromClass + glimmerBonus + fpFromEquipment); } }
    private int CalculatedMaxIP_Base { get { UnitPrimaryAttributes effAttrs = EffectiveAttributes; int baseIpFromRace = (_raceData != null) ? _raceData.baseIPContribution : 0; int baseIpFromClass = (_classData != null) ? _classData.baseIPContribution : 0; int auraBonus = (effAttrs != null) ? effAttrs.Aura : 0; int ipFromEquipment = 0; return Mathf.Max(0, baseIpFromRace + baseIpFromClass + auraBonus + ipFromEquipment); } }

    public void ModifyVitality(int amount)
    {
        if (!_isAlive && amount < 0) return;
        currentVitalityPoints += amount;
        currentVitalityPoints = Mathf.Clamp(currentVitalityPoints, 0, MaxVitalityPoints);
        if (currentVitalityPoints <= 0) { SetAliveStatus(false); }
        else if (currentVitalityPoints > 0 && !_isAlive) { _isAlive = true; }
    }

    public void SetAliveStatus(bool alive)
    {
        _isAlive = alive;
        if (!_isAlive)
        {
            currentVitalityPoints = 0;
            currentActionPoints = 0;
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
        if (!_isAlive) { currentActionPoints = 0; return; }
        currentActionPoints = MaxActionPoints;
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
            int oldVP = currentVitalityPoints; currentVitalityPoints = Mathf.Min(currentVitalityPoints + VitalityRegenRate, MaxVitalityPoints);
            if (currentVitalityPoints != oldVP) { regenLogBuilder.Append($"VP +{currentVitalityPoints - oldVP} ({currentVitalityPoints}/{MaxVitalityPoints}). "); hasRegeneratedAnything = true; }
        }
        if (ManaRegenRate > 0 && currentManaPoints < MaxManaPoints)
        {
            int oldMP = currentManaPoints; currentManaPoints = Mathf.Min(currentManaPoints + ManaRegenRate, MaxManaPoints);
            if (currentManaPoints != oldMP) { regenLogBuilder.Append($"MP +{currentManaPoints - oldMP} ({currentManaPoints}/{MaxManaPoints}). "); hasRegeneratedAnything = true; }
        }
        // ... (SP, FP, IP regen code remains the same) ...
        if (StaminaRegenRate > 0 && currentStaminaPoints < MaxStaminaPoints)
        {
            int oldSP = currentStaminaPoints; currentStaminaPoints = Mathf.Min(currentStaminaPoints + StaminaRegenRate, MaxStaminaPoints);
            if (currentStaminaPoints != oldSP) { regenLogBuilder.Append($"SP +{currentStaminaPoints - oldSP} ({currentStaminaPoints}/{MaxStaminaPoints}). "); hasRegeneratedAnything = true; }
        }
        if (FocusRegenRate > 0 && currentFocusPoints < MaxFocusPoints)
        {
            int oldFP = currentFocusPoints; currentFocusPoints = Mathf.Min(currentFocusPoints + FocusRegenRate, MaxFocusPoints);
            if (currentFocusPoints != oldFP) { regenLogBuilder.Append($"FP +{currentFocusPoints - oldFP} ({currentFocusPoints}/{MaxFocusPoints}). "); hasRegeneratedAnything = true; }
        }
        if (InfluenceRegenRate > 0 && currentInfluencePoints < MaxInfluencePoints)
        {
            int oldIP = currentInfluencePoints; currentInfluencePoints = Mathf.Min(currentInfluencePoints + InfluenceRegenRate, MaxInfluencePoints);
            if (currentInfluencePoints != oldIP) { regenLogBuilder.Append($"IP +{currentInfluencePoints - oldIP} ({currentInfluencePoints}/{MaxInfluencePoints}). "); hasRegeneratedAnything = true; }
        }

        if (hasRegeneratedAnything) DebugHelper.Log(regenLogBuilder.ToString().TrimEnd(), _unit);
    }

    public void AddEffect(ActiveStatusEffect newEffect)
    {
        _activeEffects.Add(newEffect);
        RecalculateAffectedStats(); // This will log the new effective stats
        DebugHelper.Log($"{_unit.unitName} received effect: {newEffect.BaseEffect.effectName} (Duration: {newEffect.RemainingDuration}, Stacks: {newEffect.CurrentStacks})", _unit);
    }

    public void RemoveEffect(ActiveStatusEffect effectToRemove)
    {
        if (_activeEffects.Remove(effectToRemove))
        {
            RecalculateAffectedStats(); // This will log the new effective stats
            DebugHelper.Log($"{_unit.unitName} lost effect: {effectToRemove.BaseEffect.effectName}", _unit);
        }
    }
    
    public void RemoveAllEffectsFromSource(Unit caster)
    {
        // ToList() creates a copy, so we can modify _activeEffects while iterating.
        List<ActiveStatusEffect> toRemove = _activeEffects.Where(e => e.Caster == caster).ToList();
        if (toRemove.Count > 0)
        {
            foreach(var effect in toRemove) { _activeEffects.Remove(effect); }
            RecalculateAffectedStats();
            DebugHelper.Log($"{_unit.unitName} lost {toRemove.Count} effects from source: {caster.unitName}", _unit);
        }
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

    public void RecalculateAffectedStats()
    {
        currentVitalityPoints = Mathf.Clamp(currentVitalityPoints, 0, MaxVitalityPoints);
        currentManaPoints = Mathf.Clamp(currentManaPoints, 0, MaxManaPoints);
        currentStaminaPoints = Mathf.Clamp(currentStaminaPoints, 0, MaxStaminaPoints);
        currentFocusPoints = Mathf.Clamp(currentFocusPoints, 0, MaxFocusPoints);
        currentInfluencePoints = Mathf.Clamp(currentInfluencePoints, 0, MaxInfluencePoints);
        currentActionPoints = Mathf.Clamp(currentActionPoints, 0, MaxActionPoints);

        if (_unit != null)
        {
            UnitPrimaryAttributes baseAttrs = currentAttributes;
            UnitPrimaryAttributes effAttrs = EffectiveAttributes;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{_unit.unitName} Stats Recalculated ---");
            sb.AppendLine($"  Primary Attributes (Effective | Base):");
            sb.Append($"    Core:     {effAttrs.Core.ToString().PadRight(3)} | {baseAttrs.Core}");
            if (effAttrs.Core != baseAttrs.Core) sb.Append($" ({effAttrs.Core - baseAttrs.Core:+#;-#;0})"); sb.AppendLine();
            sb.Append($"    Echo:     {effAttrs.Echo.ToString().PadRight(3)} | {baseAttrs.Echo}");
            if (effAttrs.Echo != baseAttrs.Echo) sb.Append($" ({effAttrs.Echo - baseAttrs.Echo:+#;-#;0})"); sb.AppendLine();
            sb.Append($"    Pulse:    {effAttrs.Pulse.ToString().PadRight(3)} | {baseAttrs.Pulse}");
            if (effAttrs.Pulse != baseAttrs.Pulse) sb.Append($" ({effAttrs.Pulse - baseAttrs.Pulse:+#;-#;0})"); sb.AppendLine();
            sb.Append($"    Spark:    {effAttrs.Spark.ToString().PadRight(3)} | {baseAttrs.Spark}");
            if (effAttrs.Spark != baseAttrs.Spark) sb.Append($" ({effAttrs.Spark - baseAttrs.Spark:+#;-#;0})"); sb.AppendLine();
            sb.Append($"    Glimmer:  {effAttrs.Glimmer.ToString().PadRight(3)} | {baseAttrs.Glimmer}");
            if (effAttrs.Glimmer != baseAttrs.Glimmer) sb.Append($" ({effAttrs.Glimmer - baseAttrs.Glimmer:+#;-#;0})"); sb.AppendLine();
            sb.Append($"    Aura:     {effAttrs.Aura.ToString().PadRight(3)} | {baseAttrs.Aura}");
            if (effAttrs.Aura != baseAttrs.Aura) sb.Append($" ({effAttrs.Aura - baseAttrs.Aura:+#;-#;0})"); sb.AppendLine();
            sb.AppendLine($"  Resources:");
            sb.AppendLine($"    VP: {currentVitalityPoints}/{MaxVitalityPoints}");
            sb.AppendLine($"    MP: {currentManaPoints}/{MaxManaPoints}");
            sb.AppendLine($"    AP: {currentActionPoints}/{MaxActionPoints}");
            DebugHelper.Log(sb.ToString().TrimEnd(), _unit);
        }
        else
        {
            DebugHelper.LogWarning("UnitStats.RecalculateAffectedStats: _unit is null, cannot log detailed attributes.", this);
        }
    }

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
                        StatModifier stackedMod = new StatModifier(mod.stat, mod.type, mod.value * effect.CurrentStacks);
                        modifiers.Add(stackedMod);
                    }
                }
            }
        }
        return modifiers;
    }
    
    public int CalculateModifiedStat(int baseValueParam, StatType targetStat)
    {
        float currentValue = baseValueParam;
        float valueAfterFlat = currentValue; // Initialize for PercentAdd base

        // 1. Apply Flat modifiers
        // Using GetAllStatModifiersFromEffects directly now handles stack multiplication
        foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.Flat))
        {
            if (mod.stat == targetStat)
            {
                currentValue += mod.value; // mod.value is already stack-multiplied
            }
        }
        valueAfterFlat = currentValue; // Update base for PercentAdd after flat mods

        // 2. Apply PercentAdd modifiers
        float totalPercentAdd = 0;
        foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.PercentAdd))
        {
            if (mod.stat == targetStat)
            {
                totalPercentAdd += mod.value; // mod.value is already stack-multiplied
            }
        }
        currentValue += valueAfterFlat * totalPercentAdd;

        // 3. Apply PercentMult modifiers
        foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.PercentMult))
        {
            if (mod.stat == targetStat) // mod.value is already stack-multiplied percentage (e.g. 0.1 * stacks)
            {
                currentValue *= (1f + mod.value); 
            }
        }
        
        if (targetStat == StatType.MaxVitalityPoints) return Mathf.Max(1, Mathf.RoundToInt(currentValue));
        return Mathf.Max(0, Mathf.RoundToInt(currentValue));
    }
}