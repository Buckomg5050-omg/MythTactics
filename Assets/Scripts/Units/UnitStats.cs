// UnitStats.cs
using UnityEngine;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using MythTactics.Combat; // Assuming StatType, ModifierType, ActiveStatusEffect, EffectSO are here or accessible

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

            float baseForPercentAddCore = effective.Core;
            float baseForPercentAddEcho = effective.Echo;
            float baseForPercentAddPulse = effective.Pulse;
            float baseForPercentAddSpark = effective.Spark;
            float baseForPercentAddGlimmer = effective.Glimmer;
            float baseForPercentAddAura = effective.Aura;

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
            DebugHelper.LogWarning($"UnitStats for {_unit.unitName} received null initial attributes, using defaults.", _unit);
        }
        _activeEffects.Clear();
        InitializeDerivedAttributesAndResources(); 
    }

    private void InitializeDerivedAttributesAndResources()
    {
        if (_raceData == null || _classData == null || currentAttributes == null || _unit == null)
        {
            DebugHelper.LogError($"UnitStats on {_unit?.unitName ?? "Unknown Unit"} cannot initialize resources. Missing critical data.", this);
            _isAlive = false;
            return;
        }

        _baseMaxVitalityPoints = CalculatedMaxVP_Base;
        _baseMaxManaPoints = CalculatedMaxMP_Base;
        _baseMaxStaminaPoints = CalculatedMaxSP_Base;
        _baseMaxFocusPoints = CalculatedMaxFP_Base;
        _baseMaxInfluencePoints = CalculatedMaxIP_Base;

        currentVitalityPoints = MaxVitalityPoints;
        currentManaPoints = MaxManaPoints;
        currentStaminaPoints = MaxStaminaPoints;
        currentFocusPoints = MaxFocusPoints;
        currentInfluencePoints = MaxInfluencePoints;
        currentActionPoints = MaxActionPoints;

        _isAlive = (currentVitalityPoints > 0);

        if (_isAlive)
        {
            if (MaxVitalityPoints <= 0) { _baseMaxVitalityPoints = 1; currentVitalityPoints = MaxVitalityPoints; } 
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
        RecalculateAffectedStats(); 
    }

    private int CalculatedMaxVP_Base { get { UnitPrimaryAttributes effAttrs = EffectiveAttributes; int baseVpFromRace = (_raceData != null) ? _raceData.baseVPContribution : 0; int baseVpFromClass = (_classData != null) ? _classData.baseVPContribution : 0; int pulseBonus = (effAttrs != null) ? effAttrs.Pulse * 5 : 0; int vpFromEquipment = 0; return Mathf.Max(1, baseVpFromRace + baseVpFromClass + pulseBonus + vpFromEquipment); } }
    private int CalculatedMaxMP_Base { get { UnitPrimaryAttributes effAttrs = EffectiveAttributes; int baseMpFromRace = (_raceData != null) ? _raceData.baseMPContribution : 0; int baseMpFromClass = (_classData != null) ? _classData.baseMPContribution : 0; int sparkBonus = (effAttrs != null) ? effAttrs.Spark * 2 : 0; int mpFromEquipment = 0; return Mathf.Max(0, baseMpFromRace + baseMpFromClass + sparkBonus + mpFromEquipment); } }
    private int CalculatedMaxSP_Base { get { UnitPrimaryAttributes effAttrs = EffectiveAttributes; int baseSpFromRace = (_raceData != null) ? _raceData.baseSPContribution : 0; int baseSpFromClass = (_classData != null) ? _classData.baseSPContribution : 0; int coreBonus = (effAttrs != null) ? effAttrs.Core : 0; int spFromEquipment = 0; return Mathf.Max(0, baseSpFromRace + baseSpFromClass + coreBonus + spFromEquipment); } }
    private int CalculatedMaxFP_Base { get { UnitPrimaryAttributes effAttrs = EffectiveAttributes; int baseFpFromRace = (_raceData != null) ? _raceData.baseFPContribution : 0; int baseFpFromClass = (_classData != null) ? _classData.baseFPContribution : 0; int glimmerBonus = (effAttrs != null) ? effAttrs.Glimmer : 0; int fpFromEquipment = 0; return Mathf.Max(0, baseFpFromRace + baseFpFromClass + glimmerBonus + fpFromEquipment); } }
    private int CalculatedMaxIP_Base { get { UnitPrimaryAttributes effAttrs = EffectiveAttributes; int baseIpFromRace = (_raceData != null) ? _raceData.baseIPContribution : 0; int baseIpFromClass = (_classData != null) ? _classData.baseIPContribution : 0; int auraBonus = (effAttrs != null) ? effAttrs.Aura : 0; int ipFromEquipment = 0; return Mathf.Max(0, baseIpFromRace + baseIpFromClass + auraBonus + ipFromEquipment); } }

    public void ModifyVitality(int amount)
    {
        if (!_isAlive && amount < 0) return; // Don't damage dead units further
        if (!_isAlive && amount > 0 && currentVitalityPoints <=0) { _isAlive = true; } // Revive if healing from 0 or less while marked dead

        int oldVP = currentVitalityPoints;
        currentVitalityPoints += amount;
        currentVitalityPoints = Mathf.Clamp(currentVitalityPoints, 0, MaxVitalityPoints);

        if (currentVitalityPoints <= 0 && _isAlive) // Only trigger death if was alive
        { 
            SetAliveStatus(false); 
        }
        // else if (currentVitalityPoints > 0 && !_isAlive) // Handled above
        // { 
        //     _isAlive = true; 
        // }
    }

    public void SetAliveStatus(bool alive)
    {
        bool oldStatus = _isAlive;
        _isAlive = alive;
        if (!_isAlive)
        {
            currentVitalityPoints = 0;
            currentActionPoints = 0;
            // Consider clearing effects or other death-related logic here or in UnitCombat.Die
        }
        if (oldStatus && !_isAlive)
        {
            // Unit just died
            // CombatLogger.LogEvent($"{_unit.unitName} has been defeated!", Color.magenta); // Moved to UnitCombat.TakeDamage
        }
    }

    public bool SpendActionPoints(int amount)
    {
        if (amount <= 0) return true;
        if (currentActionPoints >= amount)
        {
            currentActionPoints -= amount;
            // DebugHelper.Log($"{_unit.unitName} spent {amount} AP. Remaining: {currentActionPoints}/{MaxActionPoints}", _unit);
            CombatLogger.LogEvent($"{_unit.unitName} spent {amount} AP (Now: {currentActionPoints}/{MaxActionPoints}).", new Color(0.8f, 0.8f, 0.3f)); // Yellowish for AP
            return true;
        }
        DebugHelper.LogWarning($"{_unit.unitName} failed to spend {amount} AP. Has: {currentActionPoints}/{MaxActionPoints}", _unit);
        return false;
    }

    public void RegenerateActionPointsAtTurnStart()
    {
        if (!_isAlive) { currentActionPoints = 0; return; }
        currentActionPoints = MaxActionPoints;
        // DebugHelper.Log($"{_unit.unitName} AP regenerated to {currentActionPoints}/{MaxActionPoints} at turn start.", _unit); // Logged by TurnManager
    }

    public void SpendMana(int amount) { if (amount <= 0) return; currentManaPoints -= amount; currentManaPoints = Mathf.Max(0, currentManaPoints); }
    public void SpendStamina(int amount) { if (amount <= 0) return; currentStaminaPoints -= amount; currentStaminaPoints = Mathf.Max(0, currentStaminaPoints); }
    public void SpendFocus(int amount) { if (amount <= 0) return; currentFocusPoints -= amount; currentFocusPoints = Mathf.Max(0, currentFocusPoints); }
    public void SpendInfluence(int amount) { if (amount <= 0) return; currentInfluencePoints -= amount; currentInfluencePoints = Mathf.Max(0, currentInfluencePoints); }

    public void RegenerateResourcesAtTurnStart()
    {
        if (!_isAlive || _unit == null) return;
        StringBuilder regenLogBuilder = new StringBuilder();
        regenLogBuilder.Append($"{_unit.unitName} regenerates: ");
        bool hasRegeneratedAnything = false;
        int oldVal;

        if (VitalityRegenRate != 0 && currentVitalityPoints > 0) // Allow negative regen (damage) too
        {
            oldVal = currentVitalityPoints; 
            ModifyVitality(VitalityRegenRate); // Use ModifyVitality to handle death checks
            if (currentVitalityPoints != oldVal) { regenLogBuilder.Append($"VP {currentVitalityPoints-oldVal:+#;-#;0} ({currentVitalityPoints}/{MaxVitalityPoints}). "); hasRegeneratedAnything = true; }
        }
        if (ManaRegenRate != 0)
        {
            oldVal = currentManaPoints; currentManaPoints = Mathf.Clamp(currentManaPoints + ManaRegenRate, 0, MaxManaPoints);
            if (currentManaPoints != oldVal) { regenLogBuilder.Append($"MP {currentManaPoints-oldVal:+#;-#;0} ({currentManaPoints}/{MaxManaPoints}). "); hasRegeneratedAnything = true; }
        }
        if (StaminaRegenRate != 0)
        {
            oldVal = currentStaminaPoints; currentStaminaPoints = Mathf.Clamp(currentStaminaPoints + StaminaRegenRate, 0, MaxStaminaPoints);
            if (currentStaminaPoints != oldVal) { regenLogBuilder.Append($"SP {currentStaminaPoints-oldVal:+#;-#;0} ({currentStaminaPoints}/{MaxStaminaPoints}). "); hasRegeneratedAnything = true; }
        }
        if (FocusRegenRate != 0)
        {
            oldVal = currentFocusPoints; currentFocusPoints = Mathf.Clamp(currentFocusPoints + FocusRegenRate, 0, MaxFocusPoints);
            if (currentFocusPoints != oldVal) { regenLogBuilder.Append($"FP {currentFocusPoints-oldVal:+#;-#;0} ({currentFocusPoints}/{MaxFocusPoints}). "); hasRegeneratedAnything = true; }
        }
        if (InfluenceRegenRate != 0)
        {
            oldVal = currentInfluencePoints; currentInfluencePoints = Mathf.Clamp(currentInfluencePoints + InfluenceRegenRate, 0, MaxInfluencePoints);
            if (currentInfluencePoints != oldVal) { regenLogBuilder.Append($"IP {currentInfluencePoints-oldVal:+#;-#;0} ({currentInfluencePoints}/{MaxInfluencePoints}). "); hasRegeneratedAnything = true; }
        }

        if (hasRegeneratedAnything) CombatLogger.LogEvent(regenLogBuilder.ToString().TrimEnd(), Color.blue);
    }

    public void AddEffect(ActiveStatusEffect newEffect)
    {
        if (newEffect == null || newEffect.BaseEffect == null) return; 

        _activeEffects.Add(newEffect);
        
        // MODIFIED: Use CombatLogger
        CombatLogger.LogStatusApplied(_unit, newEffect.BaseEffect.effectName, newEffect.Caster);
        // DebugHelper.Log($"{_unit.unitName} received effect: {newEffect.BaseEffect.effectName} (Duration: {newEffect.RemainingDuration}, Stacks: {newEffect.CurrentStacks})", _unit);
        
        RecalculateAffectedStats(); 
    }

    public void RemoveEffect(ActiveStatusEffect effectToRemove)
    {
        if (effectToRemove == null || effectToRemove.BaseEffect == null) return; 

        if (_activeEffects.Remove(effectToRemove))
        {
            // MODIFIED: Use CombatLogger
            CombatLogger.LogEvent($"{_unit.unitName} loses effect: {effectToRemove.BaseEffect.effectName}.", Color.gray);
            // DebugHelper.Log($"{_unit.unitName} lost effect: {effectToRemove.BaseEffect.effectName}", _unit);
            RecalculateAffectedStats();
        }
    }
    
    public void RemoveAllEffectsFromSource(Unit caster)
    {
        List<ActiveStatusEffect> toRemove = _activeEffects.Where(e => e.Caster == caster).ToList();
        if (toRemove.Count > 0)
        {
            foreach(var effect in toRemove) 
            {
                // RemoveEffect will log individual removals
                RemoveEffect(effect); 
            }
            // CombatLogger.LogEvent($"{_unit.unitName} lost {toRemove.Count} effects from source: {caster.unitName}", Color.gray); // Optional summary
        }
    }

    public void ClearAllEffects()
    {
        if (_activeEffects.Count > 0)
        {
            // Create a copy to iterate over, as RemoveEffect modifies the original list
            List<ActiveStatusEffect> effectsToClear = new List<ActiveStatusEffect>(_activeEffects);
            foreach(var effect in effectsToClear)
            {
                RemoveEffect(effect); // This will log each removal
            }
            // _activeEffects.Clear(); // RemoveEffect handles removal from list
            // RecalculateAffectedStats(); // Called by RemoveEffect
            CombatLogger.LogEvent($"{_unit.unitName} had all effects cleared.", Color.gray); // Summary log
        }
    }

    public void RecalculateAffectedStats()
    {
        // Clamp current resources to their (potentially new) maximums
        // Order matters: Max must be calculated before clamping current
        int oldMaxVP = MaxVitalityPoints; // Cache old max for comparison if needed for currentVP adjustment
        currentVitalityPoints = Mathf.Clamp(currentVitalityPoints, 0, MaxVitalityPoints);
        // If MaxVP decreased below currentVP, currentVP is clamped. If MaxVP increased, currentVP stays.
        // If unit was at full health and MaxVP increases, they remain at the new full health (unless specific game rule says otherwise)
        // This is implicitly handled if currentVP was already MaxVP, and MaxVP changes.
        // However, if MaxVP increases, currentVP might not automatically go to the new MaxVP unless explicitly set.
        // For now, clamping is the primary concern after effects change.

        currentManaPoints = Mathf.Clamp(currentManaPoints, 0, MaxManaPoints);
        currentStaminaPoints = Mathf.Clamp(currentStaminaPoints, 0, MaxStaminaPoints);
        currentFocusPoints = Mathf.Clamp(currentFocusPoints, 0, MaxFocusPoints);
        currentInfluencePoints = Mathf.Clamp(currentInfluencePoints, 0, MaxInfluencePoints);
        currentActionPoints = Mathf.Clamp(currentActionPoints, 0, MaxActionPoints);

        if (_isAlive && currentVitalityPoints <= 0) // If effects kill the unit
        {
            SetAliveStatus(false);
            CombatLogger.LogEvent($"{_unit.unitName} succumbs to effects!", Color.red);
        }


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
            // DebugHelper.Log(sb.ToString().TrimEnd(), _unit); // This can be very spammy, consider when to log it.
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
                        // Apply stack multiplication to the modifier's value
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
        float valueAfterFlat = currentValue; 

        foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.Flat))
        {
            if (mod.stat == targetStat)
            {
                currentValue += mod.value; 
            }
        }
        valueAfterFlat = currentValue; 

        float totalPercentAdd = 0;
        foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.PercentAdd))
        {
            if (mod.stat == targetStat)
            {
                totalPercentAdd += mod.value; 
            }
        }
        currentValue += valueAfterFlat * totalPercentAdd;

        foreach (StatModifier mod in GetAllStatModifiersFromEffects(ModifierType.PercentMult))
        {
            if (mod.stat == targetStat) 
            {
                currentValue *= (1f + mod.value); 
            }
        }
        
        if (targetStat == StatType.MaxVitalityPoints) return Mathf.Max(1, Mathf.RoundToInt(currentValue));
        return Mathf.Max(0, Mathf.RoundToInt(currentValue));
    }
}