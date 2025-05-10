// UnitStats.cs
using UnityEngine;
using System.Text; // For StringBuilder in logging

public class UnitStats : MonoBehaviour
{
    [Header("References (Set by Unit.cs)")]
    private RaceDataSO _raceData;
    private ClassDataSO _classData;
    private Unit _unit; 

    [Header("Primary Attributes")]
    public UnitPrimaryAttributes currentAttributes = new UnitPrimaryAttributes();

    [Header("Health (VP - Vitality Points)")]
    [SerializeField] private int _maxVitalityPoints;
    public int currentVitalityPoints;
    public int MaxVitalityPoints => _maxVitalityPoints;

    [Header("Mana (MP - Mana Points)")]
    [SerializeField] private int _maxManaPoints;
    public int currentManaPoints;
    public int MaxManaPoints => _maxManaPoints;
    [Tooltip("Amount of MP regenerated at the start of this unit's turn.")]
    public int manaRegenRate = 1; 

    [Header("Stamina (SP - Stamina Points)")]
    [SerializeField] private int _maxStaminaPoints;
    public int currentStaminaPoints;
    public int MaxStaminaPoints => _maxStaminaPoints;
    [Tooltip("Amount of SP regenerated at the start of this unit's turn.")]
    public int staminaRegenRate = 1; 

    [Header("Focus (FP - Focus Points)")]
    [SerializeField] private int _maxFocusPoints;
    public int currentFocusPoints;
    public int MaxFocusPoints => _maxFocusPoints;
    [Tooltip("Amount of FP regenerated at the start of this unit's turn.")]
    public int focusRegenRate = 1; 

    [Header("Influence (IP - Influence Points)")]
    [SerializeField] private int _maxInfluencePoints;
    public int currentInfluencePoints;
    public int MaxInfluencePoints => _maxInfluencePoints;
    [Tooltip("Amount of IP regenerated at the start of this unit's turn.")]
    public int influenceRegenRate = 1;

    private bool _isAlive = true;
    public bool IsAlive => _isAlive;

    public void Initialize(Unit unit, RaceDataSO raceData, ClassDataSO classData, UnitPrimaryAttributes attributes)
    {
        _unit = unit; 
        _raceData = raceData;
        _classData = classData;
        if (attributes != null)
        {
            currentAttributes = attributes; 
        }
        else
        {
            currentAttributes = new UnitPrimaryAttributes(); 
            DebugHelper.LogWarning($"UnitStats for {_unit.unitName} received null attributes, using defaults.", _unit);
        }
        
        InitializeDerivedAttributesAndResources();
    }
    
    private void InitializeDerivedAttributesAndResources()
    {
        if (_raceData == null || _classData == null || currentAttributes == null || _unit == null)
        {
            DebugHelper.LogError($"UnitStats on {_unit?.unitName ?? "Unknown Unit"} cannot initialize resources. Missing RaceData, ClassData, Attributes, or Unit reference.", this);
            _isAlive = false; 
            return;
        }

        _maxVitalityPoints = CalculatedMaxVP;
        currentVitalityPoints = _maxVitalityPoints;

        _maxManaPoints = CalculatedMaxMP;
        currentManaPoints = _maxManaPoints;

        _maxStaminaPoints = CalculatedMaxSP;
        currentStaminaPoints = _maxStaminaPoints;

        _maxFocusPoints = CalculatedMaxFP;
        currentFocusPoints = _maxFocusPoints;

        _maxInfluencePoints = CalculatedMaxIP;
        currentInfluencePoints = _maxInfluencePoints;

        _isAlive = (currentVitalityPoints > 0);

        if (_isAlive) 
        {
            if (_maxVitalityPoints <= 0) _maxVitalityPoints = 1;
            if (currentVitalityPoints <= 0) currentVitalityPoints = _maxVitalityPoints;

            if (_maxManaPoints < 0) _maxManaPoints = 0; 
            if (currentManaPoints < 0) currentManaPoints = 0;
            if (_maxStaminaPoints < 0) _maxStaminaPoints = 0; 
            if (currentStaminaPoints < 0) currentStaminaPoints = 0;
            if (_maxFocusPoints < 0) _maxFocusPoints = 0; 
            if (currentFocusPoints < 0) currentFocusPoints = 0;
            if (_maxInfluencePoints < 0) _maxInfluencePoints = 0; 
            if (currentInfluencePoints < 0) currentInfluencePoints = 0;
        }
        else 
        {
            currentVitalityPoints = 0; 
            currentManaPoints = 0;
            currentStaminaPoints = 0;
            currentFocusPoints = 0;
            currentInfluencePoints = 0;
        }

        DebugHelper.Log($"{_unit.unitName} (UnitStats) Initialized Resources: " +
                        $"VP: {currentVitalityPoints}/{_maxVitalityPoints} (Alive: {_isAlive}), " +
                        $"MP: {currentManaPoints}/{_maxManaPoints}, " +
                        $"SP: {currentStaminaPoints}/{_maxStaminaPoints}, " +
                        $"FP: {currentFocusPoints}/{_maxFocusPoints}, " +
                        $"IP: {currentInfluencePoints}/{_maxInfluencePoints}", _unit);
    }

    public int CalculatedMaxVP
    {
        get
        {
            int baseVpFromRace = (_raceData != null) ? _raceData.baseVPContribution : 0;
            int baseVpFromClass = (_classData != null) ? _classData.baseVPContribution : 0;
            int pulseBonus = (currentAttributes != null) ? currentAttributes.Pulse * 5 : 0;
            int vpFromEquipment = 0; 
            return baseVpFromRace + baseVpFromClass + pulseBonus + vpFromEquipment;
        }
    }

    public int CalculatedMaxMP
    {
        get
        {
            int baseMpFromRace = (_raceData != null) ? _raceData.baseMPContribution : 0;
            int baseMpFromClass = (_classData != null) ? _classData.baseMPContribution : 0;
            int sparkBonus = (currentAttributes != null) ? currentAttributes.Spark * 2 : 0;
            int mpFromEquipment = 0; 
            return baseMpFromRace + baseMpFromClass + sparkBonus + mpFromEquipment;
        }
    }

    public int CalculatedMaxSP
    {
        get
        {
            int baseSpFromRace = (_raceData != null) ? _raceData.baseSPContribution : 0;
            int baseSpFromClass = (_classData != null) ? _classData.baseSPContribution : 0;
            int coreBonus = (currentAttributes != null) ? currentAttributes.Core : 0;
            int spFromEquipment = 0; 
            return baseSpFromRace + baseSpFromClass + coreBonus + spFromEquipment;
        }
    }

    public int CalculatedMaxFP
    {
        get
        {
            int baseFpFromRace = (_raceData != null) ? _raceData.baseFPContribution : 0;
            int baseFpFromClass = (_classData != null) ? _classData.baseFPContribution : 0;
            int glimmerBonus = (currentAttributes != null) ? currentAttributes.Glimmer : 0; 
            int fpFromEquipment = 0; 
            return baseFpFromRace + baseFpFromClass + glimmerBonus + fpFromEquipment;
        }
    }

    public int CalculatedMaxIP
    {
        get
        {
            int baseIpFromRace = (_raceData != null) ? _raceData.baseIPContribution : 0;
            int baseIpFromClass = (_classData != null) ? _classData.baseIPContribution : 0;
            int auraBonus = (currentAttributes != null) ? currentAttributes.Aura : 0; 
            int ipFromEquipment = 0; 
            return baseIpFromRace + baseIpFromClass + auraBonus + ipFromEquipment;
        }
    }

    public void ModifyVitality(int amount) 
    {
        if (!_isAlive && amount < 0) return; 

        currentVitalityPoints += amount;
        currentVitalityPoints = Mathf.Clamp(currentVitalityPoints, 0, _maxVitalityPoints);

        if (currentVitalityPoints <= 0)
        {
            _isAlive = false;
            currentVitalityPoints = 0; 
        }
    }

    public void SetAliveStatus(bool alive)
    {
        _isAlive = alive;
        if (!_isAlive)
        {
            currentVitalityPoints = 0;
        }
    }

    public void SpendMana(int amount)
    {
        if (amount <= 0) return;
        currentManaPoints -= amount;
        currentManaPoints = Mathf.Max(0, currentManaPoints);
    }

    public void SpendStamina(int amount)
    {
        if (amount <= 0) return;
        currentStaminaPoints -= amount;
        currentStaminaPoints = Mathf.Max(0, currentStaminaPoints);
    }

    public void SpendFocus(int amount) 
    {
        if (amount <= 0) return;
        currentFocusPoints -= amount;
        currentFocusPoints = Mathf.Max(0, currentFocusPoints);
    }

    public void SpendInfluence(int amount)
    {
        if (amount <= 0) return;
        currentInfluencePoints -= amount;
        currentInfluencePoints = Mathf.Max(0, currentInfluencePoints);
    }

    public void RegenerateResourcesAtTurnStart()
    {
        if (_unit != null) 
            DebugHelper.Log($"--- {_unit.unitName} ENTERING RegenerateResourcesAtTurnStart. IsAlive: {_isAlive}. MP: {currentManaPoints}/{MaxManaPoints}", _unit);
        else 
            DebugHelper.Log("--- UnitStats: ENTERING RegenerateResourcesAtTurnStart for a NULL _unit reference.", this);

        if (!_isAlive || _unit == null) 
        {
            if (_unit != null) DebugHelper.Log($"--- {_unit.unitName} EXITING RegenerateResourcesAtTurnStart EARLY (Not Alive or Null Unit).", _unit);
            else DebugHelper.Log("--- UnitStats: EXITING RegenerateResourcesAtTurnStart EARLY (Null Unit).", this);
            return; 
        }

        StringBuilder regenLogBuilder = new StringBuilder();
        regenLogBuilder.Append($"REGEN LOG for {_unit.unitName}: ");
        bool hasRegeneratedAnything = false;

        // Mana Regeneration
        DebugHelper.Log($"--- {_unit.unitName} MP Regen Phase. Rate: {manaRegenRate}, CurrentMP: {currentManaPoints}, MaxMP: {MaxManaPoints}. Condition (Current < Max): {currentManaPoints < MaxManaPoints}", _unit);
        if (manaRegenRate > 0 && currentManaPoints < MaxManaPoints) 
        {
            int oldMP = currentManaPoints;
            currentManaPoints = Mathf.Min(currentManaPoints + manaRegenRate, MaxManaPoints);
            if (currentManaPoints != oldMP) 
            {
                regenLogBuilder.Append($"MP +{currentManaPoints - oldMP} ({currentManaPoints}/{MaxManaPoints}). ");
                hasRegeneratedAnything = true;
            }
        }

        // Stamina Regeneration
        DebugHelper.Log($"--- {_unit.unitName} SP Regen Phase. Rate: {staminaRegenRate}, CurrentSP: {currentStaminaPoints}, MaxSP: {MaxStaminaPoints}. Condition (Current < Max): {currentStaminaPoints < MaxStaminaPoints}", _unit);
        if (staminaRegenRate > 0 && currentStaminaPoints < MaxStaminaPoints)
        {
            int oldSP = currentStaminaPoints;
            currentStaminaPoints = Mathf.Min(currentStaminaPoints + staminaRegenRate, MaxStaminaPoints);
            if (currentStaminaPoints != oldSP)
            {
                regenLogBuilder.Append($"SP +{currentStaminaPoints - oldSP} ({currentStaminaPoints}/{MaxStaminaPoints}). ");
                hasRegeneratedAnything = true;
            }
        }

        // Focus Regeneration
        DebugHelper.Log($"--- {_unit.unitName} FP Regen Phase. Rate: {focusRegenRate}, CurrentFP: {currentFocusPoints}, MaxFP: {MaxFocusPoints}. Condition (Current < Max): {currentFocusPoints < MaxFocusPoints}", _unit);
        if (focusRegenRate > 0 && currentFocusPoints < MaxFocusPoints)
        {
            int oldFP = currentFocusPoints;
            currentFocusPoints = Mathf.Min(currentFocusPoints + focusRegenRate, MaxFocusPoints);
            if (currentFocusPoints != oldFP)
            {
                regenLogBuilder.Append($"FP +{currentFocusPoints - oldFP} ({currentFocusPoints}/{MaxFocusPoints}). ");
                hasRegeneratedAnything = true;
            }
        }

        // Influence Regeneration
        DebugHelper.Log($"--- {_unit.unitName} IP Regen Phase. Rate: {influenceRegenRate}, CurrentIP: {currentInfluencePoints}, MaxIP: {MaxInfluencePoints}. Condition (Current < Max): {currentInfluencePoints < MaxInfluencePoints}", _unit);
        if (influenceRegenRate > 0 && currentInfluencePoints < MaxInfluencePoints)
        {
            int oldIP = currentInfluencePoints;
            currentInfluencePoints = Mathf.Min(currentInfluencePoints + influenceRegenRate, MaxInfluencePoints);
            if (currentInfluencePoints != oldIP)
            {
                regenLogBuilder.Append($"IP +{currentInfluencePoints - oldIP} ({currentInfluencePoints}/{MaxInfluencePoints}). ");
                hasRegeneratedAnything = true;
            }
        }

        if (hasRegeneratedAnything)
        {
            DebugHelper.Log(regenLogBuilder.ToString().TrimEnd(), _unit); 
        }
        DebugHelper.Log($"--- {_unit.unitName} EXITING RegenerateResourcesAtTurnStart (Normal End).", _unit);
    }
}