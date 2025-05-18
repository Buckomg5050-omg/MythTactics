// UnitStats.cs
using UnityEngine;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using MythTactics.Combat;

public class UnitStats : MonoBehaviour
{
    [Header("References (Set by Unit.cs)")]
    private RaceDataSO _raceData;
    private ClassDataSO _classData;
    private Unit _unit;

    [Header("Primary Attributes")]
    public UnitPrimaryAttributes currentAttributes = new UnitPrimaryAttributes();

    // MODIFIED: Added XP and Leveling related fields
    [Header("Progression")]
    [Tooltip("Current experience points of the unit.")]
    public int currentXP = 0;
    [Tooltip("Experience points needed to reach the next level.")]
    public int xpToNextLevel = 100; // Default for Lvl 1 -> Lvl 2, can be adjusted by formulas later
    // public int allocatableStatPoints = 0; // For later when player can allocate points
    // End of MODIFICATION

    // EffectiveAttributes getter remains the same
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

            foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.Flat))
            {
                switch (mod.stat)
                {
                    case StatType.Core: effective.Core += Mathf.RoundToInt(mod.value); break;
                    case StatType.Echo: effective.Echo += Mathf.RoundToInt(mod.value); break;
                    case StatType.Pulse: effective.Pulse += Mathf.RoundToInt(mod.value); break;
                    case StatType.Spark: effective.Spark += Mathf.RoundToInt(mod.value); break;
                    case StatType.Glimmer: effective.Glimmer += Mathf.RoundToInt(mod.value); break;
                    case StatType.Aura: effective.Aura += Mathf.RoundToInt(mod.value); break;
                }
            }

            UnitPrimaryAttributes baseForPercentAdd = new UnitPrimaryAttributes(
                effective.Core, effective.Echo, effective.Pulse, effective.Spark, effective.Glimmer, effective.Aura
            );

            foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.PercentAdd))
            {
                switch (mod.stat)
                {
                    case StatType.Core: effective.Core += Mathf.RoundToInt(baseForPercentAdd.Core * mod.value); break;
                    case StatType.Echo: effective.Echo += Mathf.RoundToInt(baseForPercentAdd.Echo * mod.value); break;
                    case StatType.Pulse: effective.Pulse += Mathf.RoundToInt(baseForPercentAdd.Pulse * mod.value); break;
                    case StatType.Spark: effective.Spark += Mathf.RoundToInt(baseForPercentAdd.Spark * mod.value); break;
                    case StatType.Glimmer: effective.Glimmer += Mathf.RoundToInt(baseForPercentAdd.Glimmer * mod.value); break;
                    case StatType.Aura: effective.Aura += Mathf.RoundToInt(baseForPercentAdd.Aura * mod.value); break;
                }
            }
            
            foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.PercentMult))
            {
                 switch (mod.stat) 
                {
                    case StatType.Core: effective.Core = Mathf.RoundToInt(effective.Core * (1f + mod.value)); break;
                    case StatType.Echo: effective.Echo = Mathf.RoundToInt(effective.Echo * (1f + mod.value)); break;
                    case StatType.Pulse: effective.Pulse = Mathf.RoundToInt(effective.Pulse * (1f + mod.value)); break;
                    case StatType.Spark: effective.Spark = Mathf.RoundToInt(effective.Spark * (1f + mod.value)); break;
                    case StatType.Glimmer: effective.Glimmer = Mathf.RoundToInt(effective.Glimmer * (1f + mod.value)); break;
                    case StatType.Aura: effective.Aura = Mathf.RoundToInt(effective.Aura * (1f + mod.value)); break;
                }
            }

            effective.Core = Mathf.Max(1, effective.Core);
            effective.Echo = Mathf.Max(1, effective.Echo);
            effective.Pulse = Mathf.Max(1, effective.Pulse);
            effective.Spark = Mathf.Max(1, effective.Spark);
            effective.Glimmer = Mathf.Max(1, effective.Glimmer);
            effective.Aura = Mathf.Max(1, effective.Aura);
            
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
    public int baseVitalityRegenRate = 0;
    public int VitalityRegenRate => CalculateModifiedStat(baseVitalityRegenRate, StatType.VitalityRegenRate);

    [Header("Mana (MP - Mana Points)")]
    [SerializeField] private int _baseMaxManaPoints;
    public int currentManaPoints;
    public int MaxManaPoints => CalculateModifiedStat(_baseMaxManaPoints, StatType.MaxManaPoints);
    public int baseManaRegenRate = 1;
    public int ManaRegenRate => CalculateModifiedStat(baseManaRegenRate, StatType.ManaRegenRate);

    // ... Other resource headers (SP, FP, IP) remain the same ...
    [Header("Stamina (SP - Stamina Points)")]
    [SerializeField] private int _baseMaxStaminaPoints;
    public int currentStaminaPoints;
    public int MaxStaminaPoints => CalculateModifiedStat(_baseMaxStaminaPoints, StatType.MaxStaminaPoints);
    public int baseStaminaRegenRate = 1;
    public int StaminaRegenRate => CalculateModifiedStat(baseStaminaRegenRate, StatType.StaminaRegenRate);

    [Header("Focus (FP - Focus Points)")]
    [SerializeField] private int _baseMaxFocusPoints;
    public int currentFocusPoints;
    public int MaxFocusPoints => CalculateModifiedStat(_baseMaxFocusPoints, StatType.MaxFocusPoints);
    public int baseFocusRegenRate = 1;
    public int FocusRegenRate => CalculateModifiedStat(baseFocusRegenRate, StatType.FocusRegenRate);

    [Header("Influence (IP - Influence Points)")]
    [SerializeField] private int _baseMaxInfluencePoints;
    public int currentInfluencePoints;
    public int MaxInfluencePoints => CalculateModifiedStat(_baseMaxInfluencePoints, StatType.MaxInfluencePoints);
    public int baseInfluenceRegenRate = 1;
    public int InfluenceRegenRate => CalculateModifiedStat(baseInfluenceRegenRate, StatType.InfluenceRegenRate);


    [Header("Status Effects")]
    private List<ActiveStatusEffect> _activeEffects = new List<ActiveStatusEffect>();
    public IReadOnlyList<ActiveStatusEffect> ActiveEffects => _activeEffects.AsReadOnly();

    private bool _isAlive = true;
    public bool IsAlive => _isAlive;

    public void Initialize(Unit unit, RaceDataSO raceData, ClassDataSO classData, UnitPrimaryAttributes templateBaseAttributes)
    {
        _unit = unit; 
        _raceData = raceData;
        _classData = classData;

        // Debug.Log($"DEBUG UnitStats.Initialize for {_unit.unitName}: Unit Level is {_unit.level}, Class is '{_classData?.name}', primaryStatGains is null? {(_classData?.primaryStatGains == null)}", _unit);
        // if (_classData?.primaryStatGains != null) {
        //     Debug.Log($"DEBUG UnitStats.Initialize: Gains - Core:{_classData.primaryStatGains.coreGain}, Echo:{_classData.primaryStatGains.echoGain}, etc.");
        // }
    
        if (templateBaseAttributes == null)
        {
            DebugHelper.LogWarning($"UnitStats for {_unit.unitName} received null templateBaseAttributes, using defaults for Lvl 1 base.", _unit);
            templateBaseAttributes = new UnitPrimaryAttributes();
        }

        currentAttributes.Core = templateBaseAttributes.Core;
        currentAttributes.Echo = templateBaseAttributes.Echo;
        currentAttributes.Pulse = templateBaseAttributes.Pulse;
        currentAttributes.Spark = templateBaseAttributes.Spark;
        currentAttributes.Glimmer = templateBaseAttributes.Glimmer;
        currentAttributes.Aura = templateBaseAttributes.Aura;

        if (_classData != null && _classData.primaryStatGains != null && _unit != null && _unit.level > 1)
        {
            int levelsToGainStats = _unit.level - 1;
            if (levelsToGainStats > 0)
            {
                currentAttributes.Core    += _classData.primaryStatGains.coreGain * levelsToGainStats;
                currentAttributes.Echo    += _classData.primaryStatGains.echoGain * levelsToGainStats;
                currentAttributes.Pulse   += _classData.primaryStatGains.pulseGain * levelsToGainStats;
                currentAttributes.Spark   += _classData.primaryStatGains.sparkGain * levelsToGainStats;
                currentAttributes.Glimmer += _classData.primaryStatGains.glimmerGain * levelsToGainStats;
                currentAttributes.Aura    += _classData.primaryStatGains.auraGain * levelsToGainStats;
                // Debug.Log($"UnitStats Initialize for {_unit.unitName} (Lvl {_unit.level}): Applied {levelsToGainStats} levels of class stat gains.");
            }
        }
        
        // MODIFIED: Initialize XP related values
        currentXP = 0; // Start with 0 XP for the current level
        xpToNextLevel = CalculateXPForNextLevel(_unit != null ? _unit.level : 1); // Calculate initial XP needed
        // End of MODIFICATION

        _activeEffects.Clear();
        InitializeDerivedAttributesAndResources(); 
    }

    private void InitializeDerivedAttributesAndResources()
    {
        if (_raceData == null || _classData == null || currentAttributes == null || _unit == null)
        {
            DebugHelper.LogError($"UnitStats on {_unit?.unitName ?? "Unknown Unit"} cannot initialize resources. Missing critical data for derived stats calculation.", this);
            _isAlive = false;
            return;
        }

        int baseVpFromRace = (_raceData != null) ? _raceData.baseVPContribution : 0;
        int baseVpFromClass = (_classData != null) ? _classData.baseVPContribution : 0;
        _baseMaxVitalityPoints = baseVpFromRace + baseVpFromClass + (currentAttributes.Pulse * 5);
        _baseMaxVitalityPoints = Mathf.Max(1, _baseMaxVitalityPoints);

        int baseMpFromRace = (_raceData != null) ? _raceData.baseMPContribution : 0;
        int baseMpFromClass = (_classData != null) ? _classData.baseMPContribution : 0;
        _baseMaxManaPoints = baseMpFromRace + baseMpFromClass + (currentAttributes.Spark * 2);
        _baseMaxManaPoints = Mathf.Max(0, _baseMaxManaPoints);

        // ... (SP, FP, IP base calculations remain the same) ...
        int baseSpFromRace = (_raceData != null) ? _raceData.baseSPContribution : 0;
        int baseSpFromClass = (_classData != null) ? _classData.baseSPContribution : 0;
        _baseMaxStaminaPoints = baseSpFromRace + baseSpFromClass + currentAttributes.Core;
        _baseMaxStaminaPoints = Mathf.Max(0, _baseMaxStaminaPoints);

        int baseFpFromRace = (_raceData != null) ? _raceData.baseFPContribution : 0;
        int baseFpFromClass = (_classData != null) ? _classData.baseFPContribution : 0;
        _baseMaxFocusPoints = baseFpFromRace + baseFpFromClass + currentAttributes.Glimmer;
        _baseMaxFocusPoints = Mathf.Max(0, _baseMaxFocusPoints);
        
        int baseIpFromRace = (_raceData != null) ? _raceData.baseIPContribution : 0;
        int baseIpFromClass = (_classData != null) ? _classData.baseIPContribution : 0;
        _baseMaxInfluencePoints = baseIpFromRace + baseIpFromClass + currentAttributes.Aura;
        _baseMaxInfluencePoints = Mathf.Max(0, _baseMaxInfluencePoints);


        currentVitalityPoints = MaxVitalityPoints;
        currentManaPoints = MaxManaPoints;
        currentStaminaPoints = MaxStaminaPoints;
        currentFocusPoints = MaxFocusPoints;
        currentInfluencePoints = MaxInfluencePoints;
        currentActionPoints = MaxActionPoints;

        _isAlive = (currentVitalityPoints > 0);
        if (!_isAlive)
        {
            currentVitalityPoints = 0; currentManaPoints = 0; currentStaminaPoints = 0;
            currentFocusPoints = 0; currentInfluencePoints = 0; currentActionPoints = 0;
        }
        RecalculateAffectedStats();
    }

    // ModifyVitality, SetAliveStatus, SpendActionPoints, RegenerateActionPointsAtTurnStart remain the same
    public void ModifyVitality(int amount)
    {
        if (!_isAlive && amount < 0) return; 
        if (!_isAlive && amount > 0 && currentVitalityPoints <=0) { _isAlive = true; } 
        int oldVP = currentVitalityPoints;
        currentVitalityPoints += amount;
        currentVitalityPoints = Mathf.Clamp(currentVitalityPoints, 0, MaxVitalityPoints);
        if (currentVitalityPoints <= 0 && _isAlive) { SetAliveStatus(false); }
    }

    public void SetAliveStatus(bool alive)
    {
        bool oldStatus = _isAlive;
        _isAlive = alive;
        if (!_isAlive) { currentVitalityPoints = 0; currentActionPoints = 0; }
    }

    public bool SpendActionPoints(int amount)
    {
        if (amount <= 0) return true;
        if (currentActionPoints >= amount)
        {
            currentActionPoints -= amount;
            CombatLogger.LogEvent($"{_unit.unitName} spent {amount} AP (Now: {currentActionPoints}/{MaxActionPoints}).", new Color(0.8f, 0.8f, 0.3f), LogMessageType.System);
            return true;
        }
        DebugHelper.LogWarning($"{_unit.unitName} failed to spend {amount} AP. Has: {currentActionPoints}/{MaxActionPoints}", _unit);
        return false;
    }
    public void RegenerateActionPointsAtTurnStart() { if (!_isAlive) { currentActionPoints = 0; return; } currentActionPoints = MaxActionPoints; }
    public void SpendMana(int amount) { if (amount <= 0) return; currentManaPoints -= amount; currentManaPoints = Mathf.Max(0, currentManaPoints); }
    public void SpendStamina(int amount) { if (amount <= 0) return; currentStaminaPoints -= amount; currentStaminaPoints = Mathf.Max(0, currentStaminaPoints); }
    public void SpendFocus(int amount) { if (amount <= 0) return; currentFocusPoints -= amount; currentFocusPoints = Mathf.Max(0, currentFocusPoints); }
    public void SpendInfluence(int amount) { if (amount <= 0) return; currentInfluencePoints -= amount; currentInfluencePoints = Mathf.Max(0, currentInfluencePoints); }
    public void RegenerateResourcesAtTurnStart() { /* ... same as before ... */ 
        if (!_isAlive || _unit == null) return;
        StringBuilder regenLogBuilder = new StringBuilder();
        regenLogBuilder.Append($"{_unit.unitName} regenerates: ");
        bool hasRegeneratedAnything = false;
        int oldVal;

        if (VitalityRegenRate != 0 && currentVitalityPoints < MaxVitalityPoints) 
        {
            oldVal = currentVitalityPoints; ModifyVitality(VitalityRegenRate); 
            if (currentVitalityPoints != oldVal) { regenLogBuilder.Append($"VP {currentVitalityPoints-oldVal:+#;-#;0} ({currentVitalityPoints}/{MaxVitalityPoints}). "); hasRegeneratedAnything = true; }
        }
        if (ManaRegenRate != 0 && currentManaPoints < MaxManaPoints)
        {
            oldVal = currentManaPoints; currentManaPoints = Mathf.Clamp(currentManaPoints + ManaRegenRate, 0, MaxManaPoints);
            if (currentManaPoints != oldVal) { regenLogBuilder.Append($"MP {currentManaPoints-oldVal:+#;-#;0} ({currentManaPoints}/{MaxManaPoints}). "); hasRegeneratedAnything = true; }
        }
        if (StaminaRegenRate != 0 && currentStaminaPoints < MaxStaminaPoints)
        {
            oldVal = currentStaminaPoints; currentStaminaPoints = Mathf.Clamp(currentStaminaPoints + StaminaRegenRate, 0, MaxStaminaPoints);
            if (currentStaminaPoints != oldVal) { regenLogBuilder.Append($"SP {currentStaminaPoints-oldVal:+#;-#;0} ({currentStaminaPoints}/{MaxStaminaPoints}). "); hasRegeneratedAnything = true; }
        }
        if (FocusRegenRate != 0 && currentFocusPoints < MaxFocusPoints)
        {
            oldVal = currentFocusPoints; currentFocusPoints = Mathf.Clamp(currentFocusPoints + FocusRegenRate, 0, MaxFocusPoints);
            if (currentFocusPoints != oldVal) { regenLogBuilder.Append($"FP {currentFocusPoints-oldVal:+#;-#;0} ({currentFocusPoints}/{MaxFocusPoints}). "); hasRegeneratedAnything = true; }
        }
        if (InfluenceRegenRate != 0 && currentInfluencePoints < MaxInfluencePoints)
        {
            oldVal = currentInfluencePoints; currentInfluencePoints = Mathf.Clamp(currentInfluencePoints + InfluenceRegenRate, 0, MaxInfluencePoints);
            if (currentInfluencePoints != oldVal) { regenLogBuilder.Append($"IP {currentInfluencePoints-oldVal:+#;-#;0} ({currentInfluencePoints}/{MaxInfluencePoints}). "); hasRegeneratedAnything = true; }
        }

        if (hasRegeneratedAnything) CombatLogger.LogEvent(regenLogBuilder.ToString().TrimEnd(), Color.blue, LogMessageType.StatusChange);
    }
    public void AddEffect(ActiveStatusEffect newEffect) { /* ... same as before ... */ 
        if (newEffect == null || newEffect.BaseEffect == null) return; 
        _activeEffects.Add(newEffect);
        CombatLogger.LogStatusApplied(_unit, newEffect.BaseEffect.effectName, newEffect.Caster);
        RecalculateAffectedStats(); 
    }
    public void RemoveEffect(ActiveStatusEffect effectToRemove) { /* ... same as before ... */ 
        if (effectToRemove == null || effectToRemove.BaseEffect == null) return; 
        if (_activeEffects.Remove(effectToRemove))
        {
            CombatLogger.LogEvent($"{_unit.unitName} loses effect: {effectToRemove.BaseEffect.effectName}.", Color.gray, LogMessageType.StatusChange);
            RecalculateAffectedStats();
        }
    }
    public void RemoveAllEffectsFromSource(Unit caster) { /* ... same as before ... */ 
        List<ActiveStatusEffect> toRemove = _activeEffects.Where(e => e.Caster == caster).ToList();
        if (toRemove.Count > 0) { foreach(var effect in toRemove) RemoveEffect(effect); }
    }
    public void ClearAllEffects() { /* ... same as before ... */ 
        if (_activeEffects.Count > 0)
        {
            List<ActiveStatusEffect> effectsToClear = new List<ActiveStatusEffect>(_activeEffects);
            foreach(var effect in effectsToClear) RemoveEffect(effect);
            CombatLogger.LogEvent($"{_unit.unitName} had all effects cleared.", Color.gray, LogMessageType.StatusChange);
        }
    }
    public void RecalculateAffectedStats() { /* ... same as before, ensure clamping occurs ... */ 
        currentVitalityPoints = Mathf.Clamp(currentVitalityPoints, 0, MaxVitalityPoints);
        currentManaPoints = Mathf.Clamp(currentManaPoints, 0, MaxManaPoints);
        currentStaminaPoints = Mathf.Clamp(currentStaminaPoints, 0, MaxStaminaPoints);
        currentFocusPoints = Mathf.Clamp(currentFocusPoints, 0, MaxFocusPoints);
        currentInfluencePoints = Mathf.Clamp(currentInfluencePoints, 0, MaxInfluencePoints);
        currentActionPoints = Mathf.Clamp(currentActionPoints, 0, MaxActionPoints);
        if (_isAlive && currentVitalityPoints <= 0) { SetAliveStatus(false); }
    }
    private List<StatModifier> GetAllStatModifiersFromEffects(ModifierType modType) { /* ... same as before ... */ 
        List<StatModifier> modifiers = new List<StatModifier>();
        if (_activeEffects == null) return modifiers;
        foreach (ActiveStatusEffect effect in _activeEffects) {
            if (effect.BaseEffect != null && effect.BaseEffect.statModifiers != null) {
                foreach (StatModifier mod in effect.BaseEffect.statModifiers) {
                    if (mod.type == modType) {
                        modifiers.Add(new StatModifier(mod.stat, mod.type, mod.value * effect.CurrentStacks));
                    }
                }
            }
        }
        return modifiers;
    }
    public int CalculateModifiedStat(int baseValueParam, StatType targetStat) { /* ... same as before ... */ 
        float currentValue = baseValueParam;
        foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.Flat)) {
            if (mod.stat == targetStat) { currentValue += mod.value; }
        }
        float valueAfterFlatAndBase = currentValue;
        float totalPercentAddBonus = 0;
        foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.PercentAdd)) {
            if (mod.stat == targetStat) { totalPercentAddBonus += valueAfterFlatAndBase * mod.value; }
        }
        currentValue += totalPercentAddBonus;
        foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.PercentMult)) {
            if (mod.stat == targetStat) { currentValue *= (1f + mod.value); }
        }
        if (targetStat == StatType.MaxVitalityPoints) return Mathf.Max(1, Mathf.RoundToInt(currentValue));
        return Mathf.Max(0, Mathf.RoundToInt(currentValue));
    }

    // MODIFIED: New method to add XP and check for level up
    public void AddXP(int xpAmount)
    {
        if (!_isAlive || xpAmount <= 0 || _unit == null) return; // Only living units gain XP

        currentXP += xpAmount;
        CombatLogger.LogEvent($"{_unit.unitName} gained {xpAmount} XP. (Total: {currentXP}/{xpToNextLevel})", Color.magenta, LogMessageType.System);

        // Basic level up check (actual level up mechanics will be a separate step)
        // if (currentXP >= xpToNextLevel)
        // {
        //     // LevelUp(); // Placeholder for future LevelUp method
        // }
    }

    // MODIFIED: Placeholder for calculating XP to next level (can be a complex formula later)
    private int CalculateXPForNextLevel(int currentLevel)
    {
        // Example: Simple scaling: 100 * level, or 100 * (level^1.5)
        // For now, a flat 100 for simplicity, or slightly increasing.
        if (currentLevel < 1) currentLevel = 1;
        return 50 + (currentLevel * 50); // e.g., Lvl 1->2 needs 100, Lvl 2->3 needs 150
    }
    // End of MODIFICATION
}