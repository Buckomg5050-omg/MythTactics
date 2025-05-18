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

    [Header("Progression")]
    [Tooltip("Current experience points of the unit.")]
    public int currentXP = 0;
    [Tooltip("Experience points needed to reach the next level.")]
    public int xpToNextLevel = 100;
    [Tooltip("Attribute points available for manual allocation upon level up. (Player units only)")]
    public int allocatableAttributePoints = 0;

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

        if (_classData != null && _unit != null && _unit.level > 1)
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
            }
        }

        currentXP = 0;
        xpToNextLevel = CalculateXPForNextLevel(_unit != null ? _unit.level : 1);
        allocatableAttributePoints = 0;

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

        RecalculateBaseMaxResources(); // MODIFIED: Extracted to new method

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
        RecalculateAffectedStats(); // Ensures effects are applied to initial values
    }

    // MODIFIED: New method to recalculate base max resources from attributes, race, and class
    private void RecalculateBaseMaxResources()
    {
        if (_raceData == null || _classData == null || currentAttributes == null || _unit == null)
        {
            // This check is important if called independently, though InitializeDerivedAttributesAndResources already checks
            DebugHelper.LogWarning($"UnitStats on {_unit?.unitName ?? "Unknown Unit"} cannot recalculate base max resources. Missing data.", this);
            return;
        }

        int baseVpFromRace = _raceData.baseVPContribution;
        int baseVpFromClass = _classData.baseVPContribution;
        _baseMaxVitalityPoints = baseVpFromRace + baseVpFromClass + (currentAttributes.Pulse * 5); // GDD 2.3
        _baseMaxVitalityPoints = Mathf.Max(1, _baseMaxVitalityPoints);

        int baseMpFromRace = _raceData.baseMPContribution;
        int baseMpFromClass = _classData.baseMPContribution;
        _baseMaxManaPoints = baseMpFromRace + baseMpFromClass + (currentAttributes.Spark * 2); // GDD 2.3
        _baseMaxManaPoints = Mathf.Max(0, _baseMaxManaPoints);

        int baseSpFromRace = _raceData.baseSPContribution;
        int baseSpFromClass = _classData.baseSPContribution;
        _baseMaxStaminaPoints = baseSpFromRace + baseSpFromClass + currentAttributes.Core; // GDD 2.3
        _baseMaxStaminaPoints = Mathf.Max(0, _baseMaxStaminaPoints);

        int baseFpFromRace = _raceData.baseFPContribution;
        int baseFpFromClass = _classData.baseFPContribution;
        _baseMaxFocusPoints = baseFpFromRace + baseFpFromClass + currentAttributes.Glimmer; // GDD 2.3
        _baseMaxFocusPoints = Mathf.Max(0, _baseMaxFocusPoints);

        int baseIpFromRace = _raceData.baseIPContribution;
        int baseIpFromClass = _classData.baseIPContribution;
        _baseMaxInfluencePoints = baseIpFromRace + baseIpFromClass + currentAttributes.Aura; // GDD 2.3
        _baseMaxInfluencePoints = Mathf.Max(0, _baseMaxInfluencePoints);
    }


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
    public void RegenerateResourcesAtTurnStart() {
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
    public void AddEffect(ActiveStatusEffect newEffect) {
        if (newEffect == null || newEffect.BaseEffect == null) return;
        _activeEffects.Add(newEffect);
        CombatLogger.LogStatusApplied(_unit, newEffect.BaseEffect.effectName, newEffect.Caster);
        RecalculateAffectedStats();
    }
    public void RemoveEffect(ActiveStatusEffect effectToRemove) {
        if (effectToRemove == null || effectToRemove.BaseEffect == null) return;
        if (_activeEffects.Remove(effectToRemove))
        {
            CombatLogger.LogEvent($"{_unit.unitName} loses effect: {effectToRemove.BaseEffect.effectName}.", Color.gray, LogMessageType.StatusChange);
            RecalculateAffectedStats();
        }
    }
    public void RemoveAllEffectsFromSource(Unit caster) {
        List<ActiveStatusEffect> toRemove = _activeEffects.Where(e => e.Caster == caster).ToList();
        if (toRemove.Count > 0) { foreach(var effect in toRemove) RemoveEffect(effect); }
    }
    public void ClearAllEffects() {
        if (_activeEffects.Count > 0)
        {
            List<ActiveStatusEffect> effectsToClear = new List<ActiveStatusEffect>(_activeEffects);
            foreach(var effect in effectsToClear) RemoveEffect(effect);
            CombatLogger.LogEvent($"{_unit.unitName} had all effects cleared.", Color.gray, LogMessageType.StatusChange);
        }
    }
    public void RecalculateAffectedStats() {
        int oldMaxVP = MaxVitalityPoints;
        currentVitalityPoints = Mathf.Clamp(currentVitalityPoints, 0, oldMaxVP);

        int oldMaxMP = MaxManaPoints;
        currentManaPoints = Mathf.Clamp(currentManaPoints, 0, oldMaxMP);

        int oldMaxSP = MaxStaminaPoints;
        currentStaminaPoints = Mathf.Clamp(currentStaminaPoints, 0, oldMaxSP);

        int oldMaxFP = MaxFocusPoints;
        currentFocusPoints = Mathf.Clamp(currentFocusPoints, 0, oldMaxFP);

        int oldMaxIP = MaxInfluencePoints;
        currentInfluencePoints = Mathf.Clamp(currentInfluencePoints, 0, oldMaxIP);

        currentActionPoints = Mathf.Clamp(currentActionPoints, 0, MaxActionPoints);

        if (_isAlive && currentVitalityPoints <= 0) { SetAliveStatus(false); }
    }
    private List<StatModifier> GetAllStatModifiersFromEffects(ModifierType modType) {
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
    public int CalculateModifiedStat(int baseValueParam, StatType targetStat) {
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
        if (targetStat == StatType.MaxActionPoints ||
            targetStat == StatType.MaxManaPoints ||
            targetStat == StatType.MaxStaminaPoints ||
            targetStat == StatType.MaxFocusPoints ||
            targetStat == StatType.MaxInfluencePoints)
        {
            return Mathf.Max(0, Mathf.RoundToInt(currentValue));
        }
        return Mathf.RoundToInt(currentValue);
    }

    public void AddXP(int xpAmount)
    {
        if (!_isAlive || xpAmount <= 0 || _unit == null) return;

        currentXP += xpAmount;
        CombatLogger.LogEvent($"{_unit.unitName} gained {xpAmount} XP. (Current: {currentXP}/{xpToNextLevel})", Color.magenta, LogMessageType.System); // MODIFIED: Changed "Total" to "Current" for clarity

        CheckForLevelUp();
    }

    // MODIFIED: Implemented CheckForLevelUp
    public void CheckForLevelUp()
    {
        if (!_isAlive || _unit == null) return; // Basic safety checks

        // Loop to handle multiple level-ups if enough XP is gained
        bool hasLeveledUpThisCheck = false;
        while (currentXP >= xpToNextLevel)
        {
            LevelUp();
            hasLeveledUpThisCheck = true;
        }

        if (hasLeveledUpThisCheck)
        {
            // After all potential level-ups, ensure derived stats and resources are fully updated and clamped.
            // RecalculateBaseMaxResources(); // Already called within LevelUp
            // RecalculateAffectedStats(); // Already called within LevelUp
            // The healing part of LevelUp sets current to max, so this should be fine.
            // One final RecalculateAffectedStats might be redundant if LevelUp handles it perfectly.
            // However, if LevelUp only updates base maxes and current values, this ensures effects on maxes are applied.
            // Let's ensure LevelUp does call RecalculateAffectedStats AFTER updating base maxes and healing.
        }
    }

    // MODIFIED: Implemented LevelUp
    private void LevelUp()
    {
        if (_unit == null || _classData == null)
        {
            DebugHelper.LogError($"LevelUp called on {_unit?.unitName ?? "Unknown Unit"} but Unit or ClassData is missing.", _unit);
            return;
        }

        _unit.level++;
        CombatLogger.LogLevelUp(_unit, _unit.level);

        // Store excess XP before resetting currentXP relative to the old xpToNextLevel
        int excessXP = currentXP - xpToNextLevel;
        currentXP = excessXP;
        xpToNextLevel = CalculateXPForNextLevel(_unit.level);

        // Apply automatic stat gains
        StringBuilder statGainsLog = new StringBuilder();
        statGainsLog.Append($"{_unit.unitName} (Lvl {_unit.level}) gained stats: ");
        bool gainsApplied = false;

        // Using a temporary struct to hold gains for logging clarity
        SerializablePrimaryStatGain gains = _classData.primaryStatGains;

        if (gains.coreGain != 0) { currentAttributes.Core += gains.coreGain; statGainsLog.Append($"Core +{gains.coreGain}, "); gainsApplied = true; }
        if (gains.echoGain != 0) { currentAttributes.Echo += gains.echoGain; statGainsLog.Append($"Echo +{gains.echoGain}, "); gainsApplied = true; }
        if (gains.pulseGain != 0) { currentAttributes.Pulse += gains.pulseGain; statGainsLog.Append($"Pulse +{gains.pulseGain}, "); gainsApplied = true; }
        if (gains.sparkGain != 0) { currentAttributes.Spark += gains.sparkGain; statGainsLog.Append($"Spark +{gains.sparkGain}, "); gainsApplied = true; }
        if (gains.glimmerGain != 0) { currentAttributes.Glimmer += gains.glimmerGain; statGainsLog.Append($"Glimmer +{gains.glimmerGain}, "); gainsApplied = true; }
        if (gains.auraGain != 0) { currentAttributes.Aura += gains.auraGain; statGainsLog.Append($"Aura +{gains.auraGain}, "); gainsApplied = true; }

        if (gainsApplied)
        {
            CombatLogger.LogEvent(statGainsLog.ToString().TrimEnd(' ', ','), Color.green, LogMessageType.System);
        }
        else
        {
            CombatLogger.LogEvent($"{_unit.unitName} (Lvl {_unit.level}) had no automatic stat gains for this class level.", Color.yellow, LogMessageType.System);
        }

        // Award allocatable points (GDD 2.2: +2 for player units)
        // Assuming player units are tagged "Player". Adjust if you have a different way to identify player units.
        if (_unit.CompareTag("Player")) // TODO: Potentially replace with a more robust check (e.g., Faction enum, IsPlayerControlled bool)
        {
            allocatableAttributePoints += 2;
            CombatLogger.LogEvent($"{_unit.unitName} gained 2 allocatable attribute points. (Total: {allocatableAttributePoints})", Color.cyan, LogMessageType.System);
        }

        // Recalculate base max resources based on new attributes
        RecalculateBaseMaxResources();

        // Heal unit to full VP/MP (GDD common practice)
        currentVitalityPoints = MaxVitalityPoints; // MaxVitalityPoints will use the new _baseMaxVitalityPoints and effects
        currentManaPoints = MaxManaPoints;     // MaxManaPoints will use the new _baseMaxManaPoints and effects
        CombatLogger.LogEvent($"{_unit.unitName} was fully healed (VP/MP) on level up.", Color.green, LogMessageType.System);

        // Final recalculation to ensure all derived stats (including those from effects on new maxes) are up-to-date
        RecalculateAffectedStats();

        // Log new XP status
        CombatLogger.LogEvent($"{_unit.unitName} XP: {currentXP}/{xpToNextLevel}. Level: {_unit.level}.", Color.magenta, LogMessageType.System);

        // TODO: Future - Check for new skills/abilities becoming available.
    }
    // End of MODIFICATION

    private int CalculateXPForNextLevel(int forLevel) // Renamed parameter for clarity
    {
        // Current GDD does not specify a formula. Using placeholder.
        // This formula calculates XP needed to go FROM (forLevel) TO (forLevel + 1)
        if (forLevel < 1) forLevel = 1;
        return 50 + (forLevel * 50); // e.g., To reach Lvl 2 (from Lvl 1) needs 100XP. To reach Lvl 3 (from Lvl 2) needs 150XP.
    }
}