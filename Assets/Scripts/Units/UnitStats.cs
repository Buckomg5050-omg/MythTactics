// UnitStats.cs
using UnityEngine;
using MythTactics.Combat; // For DebugHelper if used, though it's better if Unit.cs handles logging context

public class UnitStats : MonoBehaviour
{
    [Header("References (Set by Unit.cs)")]
    // These will be set by the main Unit script during its Awake/Initialization
    private RaceDataSO _raceData;
    private ClassDataSO _classData;
    private Unit _unit; // Reference to the main Unit component for context (e.g. unitName for logs)

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

    [Header("Stamina (SP - Stamina Points)")]
    [SerializeField] private int _maxStaminaPoints;
    public int currentStaminaPoints;
    public int MaxStaminaPoints => _maxStaminaPoints;

    [Header("Focus (FP - Focus Points)")]
    [SerializeField] private int _maxFocusPoints;
    public int currentFocusPoints;
    public int MaxFocusPoints => _maxFocusPoints;

    [Header("Influence (IP - Influence Points)")]
    [SerializeField] private int _maxInfluencePoints;
    public int currentInfluencePoints;
    public int MaxInfluencePoints => _maxInfluencePoints;

    private bool _isAlive = true;
    public bool IsAlive => _isAlive;

    public void Initialize(Unit unit, RaceDataSO raceData, ClassDataSO classData, UnitPrimaryAttributes attributes)
    {
        _unit = unit; // Store reference to the Unit for its name in logs, etc.
        _raceData = raceData;
        _classData = classData;
        if (attributes != null)
        {
            currentAttributes = attributes; // Use attributes passed from Unit.cs
        }
        else
        {
            currentAttributes = new UnitPrimaryAttributes(); // Fallback if null
            DebugHelper.LogWarning($"UnitStats for {_unit.unitName} received null attributes, using defaults.", _unit);
        }
        
        InitializeDerivedAttributesAndResources();
    }
    
    private void InitializeDerivedAttributesAndResources()
    {
        if (_raceData == null || _classData == null || currentAttributes == null || _unit == null)
        {
            DebugHelper.LogError($"UnitStats on {_unit?.unitName ?? "Unknown Unit"} cannot initialize resources. Missing RaceData, ClassData, Attributes, or Unit reference.", this);
            _isAlive = false; // Cannot function correctly
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

        // Determine initial alive state based on VP
        _isAlive = (currentVitalityPoints > 0);

        // Ensure a minimum of 1 for VP if calculation results in 0 or less, and unit is intended to be alive
        if (_isAlive) // This check is now more about ensuring a baseline for a living unit
        {
            if (_maxVitalityPoints <= 0) _maxVitalityPoints = 1;
            if (currentVitalityPoints <= 0) currentVitalityPoints = _maxVitalityPoints; // Set to maxVP which is now at least 1

            // Other resources can be 0
            if (_maxManaPoints < 0) _maxManaPoints = 0;
            if (currentManaPoints < 0) currentManaPoints = 0;
            if (_maxStaminaPoints < 0) _maxStaminaPoints = 0;
            if (currentStaminaPoints < 0) currentStaminaPoints = 0;
            if (_maxFocusPoints < 0) _maxFocusPoints = 0;
            if (currentFocusPoints < 0) currentFocusPoints = 0;
            if (_maxInfluencePoints < 0) _maxInfluencePoints = 0;
            if (currentInfluencePoints < 0) currentInfluencePoints = 0;
        }
        else // If not alive (e.g. VP was 0 from start), ensure all current resources are 0
        {
            currentVitalityPoints = 0; // MaxVP could still be calculated >0 but if current is 0, unit is not alive
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

    // --- Calculated Max Resource Properties ---
    public int CalculatedMaxVP
    {
        get
        {
            // Note: IsAlive check here is tricky during initialization. We calculate max potential.
            // The actual _isAlive flag is set after currentVP is determined.
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

    // --- Methods to modify resources (to be called by UnitCombat, Unit, etc.) ---
    // Example for TakeDamage, which now directly affects stats here
    public void ModifyVitality(int amount) // Negative for damage, positive for healing
    {
        if (!_isAlive && amount < 0) return; // Cannot damage a dead unit further

        currentVitalityPoints += amount;
        currentVitalityPoints = Mathf.Clamp(currentVitalityPoints, 0, _maxVitalityPoints);

        if (currentVitalityPoints <= 0)
        {
            _isAlive = false;
            currentVitalityPoints = 0; // Ensure it's exactly 0 if dead
            // The Die() coroutine and visual effects should still be triggered by Unit.cs
        }
        // If healing revives, Unit.cs would need to handle setting _isAlive back to true
        // For now, this just handles the VP change and internal _isAlive.
    }

    public void SetAliveStatus(bool alive)
    {
        _isAlive = alive;
        if (!_isAlive)
        {
            currentVitalityPoints = 0;
            // Consider zeroing out other current resources if that's the design for "death"
            // currentManaPoints = 0; 
            // currentStaminaPoints = 0;
            // etc.
        }
    }

    // Add methods for spending/gaining MP, SP, FP, IP here as needed
    // e.g., public bool TrySpendMP(int amount) { ... }
    // These will be called by UnitCombat or other systems.
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

    public void SpendFocus(int amount) // To be used when abilities cost FP
    {
        if (amount <= 0) return;
        currentFocusPoints -= amount;
        currentFocusPoints = Mathf.Max(0, currentFocusPoints);
    }

    public void SpendInfluence(int amount) // To be used when abilities cost IP
    {
        if (amount <= 0) return;
        currentInfluencePoints -= amount;
        currentInfluencePoints = Mathf.Max(0, currentInfluencePoints);
    }
}