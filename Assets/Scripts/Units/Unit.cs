// Unit.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MythTactics.Combat;
using Random = UnityEngine.Random;

[RequireComponent(typeof(UnitStats))]
[RequireComponent(typeof(UnitCombat))]
[RequireComponent(typeof(UnitMovement))]
[RequireComponent(typeof(UnitAnimation))]
[RequireComponent(typeof(UnitAI))]
public class Unit : MonoBehaviour
{
    [Header("Core Unit Data (Populated by Factory or Inspector for direct placement)")]
    public RaceDataSO raceData;
    public ClassDataSO classData;
    public UnitPrimaryAttributes initialPrimaryAttributes = new UnitPrimaryAttributes();

    [Header("Components")]
    public UnitStats Stats { get; private set; }
    public UnitCombat Combat { get; private set; }
    public UnitMovement Movement { get; private set; }
    public UnitAnimation Animation { get; private set; }
    public UnitAI AI { get; private set; }

    [Header("Basic Info")]
    public string unitName = "Unit";
    public int level = 1;

    [Header("Progression")]
    public int xpValue = 10;

    [Header("Affiliation (Populated by Factory or Inspector)")]
    public FactionType CurrentFaction;
    public AlignmentType CurrentAlignment;

    [Header("Abilities & Items (Populated by Factory or Inspector)")]
    public List<AbilitySO> knownAbilities = new List<AbilitySO>();
    public WeaponSO equippedWeapon;
    public ArmorSO equippedBodyArmor;
    public List<ItemSO> inventory = new List<ItemSO>();

    [Header("Movement & Animation Timings (Can be on prefab by default)")]
    public float moveSpeed = 5f;
    public float attackAnimDuration = 0.5f;
    public float hurtAnimDuration = 0.3f;
    public float deathAnimDuration = 1.0f;

    [Header("Turn Order (Runtime)")]
    public int actionCounter = 0;

    public Tile CurrentTile => (Movement != null) ? Movement.CurrentTile : null;
    public bool IsMoving => (Movement != null) ? Movement.IsMoving : false;
    public bool IsAlive => (Stats != null) ? Stats.IsAlive : false;
    public int CurrentActionPoints => (Stats != null) ? Stats.currentActionPoints : 0;
    public int MaxActionPoints => (Stats != null) ? Stats.MaxActionPoints : 0;

    // MODIFIED: Changed to public property, fetched in Awake
    public SpriteRenderer MainSpriteRenderer { get; private set; }
    private bool _isInitialized = false;

    void Awake()
    {
        // Fetch SpriteRenderer here to ensure it's available early
        MainSpriteRenderer = GetComponent<SpriteRenderer>();
        if (MainSpriteRenderer == null && transform.childCount > 0) MainSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (MainSpriteRenderer == null) Debug.LogWarning($"Unit {this.name} could not find a SpriteRenderer in Awake. Visuals might not update.", this);

        Stats = GetComponent<UnitStats>();
        Combat = GetComponent<UnitCombat>();
        Movement = GetComponent<UnitMovement>();
        Animation = GetComponent<UnitAnimation>();
        AI = GetComponent<UnitAI>();
    }

    void Start()
    {
        if (!_isInitialized)
        {
            if (raceData == null || classData == null) {
                 Debug.LogWarning($"Unit '{this.name}' is being initialized via Start() but lacks RaceData or ClassData. Stats might be minimal.", this);
            }
            if (this.initialPrimaryAttributes == null)
            {
                this.initialPrimaryAttributes = new UnitPrimaryAttributes();
            }
            InitializeUnitSystems();
        }
    }

    public void PrimeDataFromTemplate(UnitTemplateSO template, int newLevel, FactionType factionToSet, bool applyAIStatVariation = false)
    {
        if (template == null)
        {
            Debug.LogError($"PrimeDataFromTemplate called on '{this.name}' with a null template!", this);
            return;
        }

        this.gameObject.name = $"[UNIT] {template.unitName} (Lvl {newLevel})";
        this.unitName = template.unitName;

        // Set In-World Sprite from template
        if (MainSpriteRenderer != null) // Ensure we have a renderer
        {
            if (template.inWorldCombatSprite != null)
            {
                MainSpriteRenderer.sprite = template.inWorldCombatSprite;
            }
            else if (template.portrait != null) // Fallback to portrait if inWorldCombatSprite is null
            {
                MainSpriteRenderer.sprite = template.portrait;
                Debug.Log($"[PrimeData] Unit {this.unitName} using 'portrait' for in-world sprite as 'inWorldCombatSprite' was not set in template '{template.name}'.", this);
            }
            else
            {
                // If both are null, the prefab's default sprite remains.
                Debug.LogWarning($"[PrimeData] Unit {this.unitName} using default sprite from prefab. Neither 'inWorldCombatSprite' nor 'portrait' assigned in template '{template.name}'.", this);
            }
        }
        else
        {
            Debug.LogWarning($"[PrimeData] Unit {this.unitName} has no SpriteRenderer component found to set its visual sprite.", this);
        }


        this.raceData = template.raceData;
        this.classData = template.classData;
        
        if (template.baseLevel1Attributes != null)
        {
            this.initialPrimaryAttributes = new UnitPrimaryAttributes(
                template.baseLevel1Attributes.Core, template.baseLevel1Attributes.Echo,
                template.baseLevel1Attributes.Pulse, template.baseLevel1Attributes.Spark,
                template.baseLevel1Attributes.Glimmer, template.baseLevel1Attributes.Aura
            );
        }
        else
        {
            this.initialPrimaryAttributes = new UnitPrimaryAttributes();
            Debug.LogWarning($"Template '{template.name}' has null baseLevel1Attributes field. '{this.unitName}' will use default attributes.", this);
        }

        if (applyAIStatVariation && template.allowsStatVariationForAI && factionToSet != FactionType.Player)
        {
            if (this.initialPrimaryAttributes == null) this.initialPrimaryAttributes = new UnitPrimaryAttributes();
            float variation = template.aiStatVariationPercent;
            this.initialPrimaryAttributes.Core = Mathf.Max(1, Mathf.RoundToInt(this.initialPrimaryAttributes.Core * (1 + Random.Range(-variation, variation))));
            this.initialPrimaryAttributes.Echo = Mathf.Max(1, Mathf.RoundToInt(this.initialPrimaryAttributes.Echo * (1 + Random.Range(-variation, variation))));
            this.initialPrimaryAttributes.Pulse = Mathf.Max(1, Mathf.RoundToInt(this.initialPrimaryAttributes.Pulse * (1 + Random.Range(-variation, variation))));
            this.initialPrimaryAttributes.Spark = Mathf.Max(1, Mathf.RoundToInt(this.initialPrimaryAttributes.Spark * (1 + Random.Range(-variation, variation))));
            this.initialPrimaryAttributes.Glimmer = Mathf.Max(1, Mathf.RoundToInt(this.initialPrimaryAttributes.Glimmer * (1 + Random.Range(-variation, variation))));
            this.initialPrimaryAttributes.Aura = Mathf.Max(1, Mathf.RoundToInt(this.initialPrimaryAttributes.Aura * (1 + Random.Range(-variation, variation))));
        }

        this.level = Mathf.Max(1, newLevel);
        this.CurrentFaction = factionToSet;
        this.CurrentAlignment = template.alignment;
        this.xpValue = template.defaultXpValueOnDefeat;
         // MODIFIED: Set GameObject tag based on faction
    switch (this.CurrentFaction)
    {
        case FactionType.Player:
            this.gameObject.tag = "Player"; // Ensure "Player" tag exists in your project
            break;
        case FactionType.Enemy:
            this.gameObject.tag = "Enemy";  // Ensure "Enemy" tag exists if you use it
            break;
        // Add cases for Ally, Neutral if they need specific tags
        default:
            this.gameObject.tag = "Untagged"; // Or a default tag
            break;
    }
    // Debug.Log($"[PrimeData] Set tag for {this.unitName} to '{this.gameObject.tag}' based on faction {this.CurrentFaction}.", this);
        this.equippedWeapon = template.startingWeapon;
        this.equippedBodyArmor = template.startingBodyArmor;

        this.knownAbilities.Clear();
        if (template.startingAbilities != null) { foreach (AbilitySO ability in template.startingAbilities) { if (ability != null) this.knownAbilities.Add(ability); } }
        this.inventory.Clear();
        if (template.startingInventory != null) { foreach (ItemSO item in template.startingInventory) { if (item != null) this.inventory.Add(item); } }

        if (AI != null && this.CurrentFaction != FactionType.Player)
        {
            AI.enabled = true; 
            AI.SetBehaviorProfile(template.aiProfile);
        }
        else if (AI != null && this.CurrentFaction == FactionType.Player)
        {
            AI.enabled = false;
        }

        InitializeUnitSystems();
    }

    private void InitializeUnitSystems()
    {
        if (_isInitialized) return;

        if (Stats == null || Combat == null || Movement == null || Animation == null || AI == null || MainSpriteRenderer == null /* Crucial check if needed by Animation Init */)
        {
            Debug.LogError($"Unit '{unitName}' is missing one or more core components (Stats, Combat, Movement, Animation, AI, or SpriteRenderer). Initialization aborted.", this);
             _isInitialized = true; 
            return;
        }
        if (this.initialPrimaryAttributes == null) { this.initialPrimaryAttributes = new UnitPrimaryAttributes(); }
        
        Stats.Initialize(this, raceData, classData, this.initialPrimaryAttributes);
        Combat.Initialize(this);
        Movement.Initialize(this);
        Movement.MoveSpeed = this.moveSpeed;
        
        // MODIFIED: Pass MainSpriteRenderer to UnitAnimation.Initialize
        Animation.Initialize(this, MainSpriteRenderer); 
        Animation.SetAttackAnimDuration(this.attackAnimDuration);
        Animation.SetHurtAnimDuration(this.hurtAnimDuration);
        Animation.SetDeathAnimDuration(this.deathAnimDuration);
        
        AI.Initialize(this);

        _isInitialized = true;
    }

    public void StopMovementCoroutines() { Movement?.StopMovementCoroutines(); }
    public void ClearCurrentTileReference() { Movement?.ClearCurrentTileReferenceForDeath(); }

    public void ResetForCombatStart()
    {
        if (!_isInitialized) {
            Debug.LogWarning($"Unit '{unitName}' ResetForCombatStart called before full initialization. Attempting to initialize now.", this);
             if (this.initialPrimaryAttributes == null) this.initialPrimaryAttributes = new UnitPrimaryAttributes();
            InitializeUnitSystems();
            if(!_isInitialized) {
                Debug.LogError($"Unit '{unitName}' failed to initialize during ResetForCombatStart.", this);
                return;
            }
        }
        if (Stats != null)
        {
            if (IsAlive) Stats.RegenerateActionPointsAtTurnStart();
            else Stats.currentActionPoints = 0;
        }
        else DebugHelper.LogError($"{unitName} cannot ResetForCombatStart, Stats component is null.", this);
    }

    public bool CanAffordAPForAction(int apCost)
    {
        if (!IsAlive || Stats == null) return false;
        return Stats.currentActionPoints >= apCost;
    }

    public void SpendAPForAction(int apCost)
    {
        if (!IsAlive || Stats == null) return;
        if (apCost <= 0) return;
        Stats.SpendActionPoints(apCost);
    }

    public int CalculatedAttackRange
    {
        get
        {
            if (!IsAlive) return 0;
            if (equippedWeapon != null && equippedWeapon.range > 0) return equippedWeapon.range;
            if (classData != null && classData.baseAttackRange > 0) return classData.baseAttackRange;
            return 1;
        }
    }

    public int RawCalculatedBaseUnitSpeed
    {
        get
        {
            if (!IsAlive) return 1;
            int raceBonus = (raceData != null) ? raceData.raceSpeedBonus : 0;
            int classBonus = (classData != null) ? classData.classSpeedBonus : 0;
            UnitPrimaryAttributes effAttrs;
            if (Stats != null && Stats.IsAlive && _isInitialized)
            {
                effAttrs = Stats.EffectiveAttributes;
            }
            else
            {
                effAttrs = (this.initialPrimaryAttributes != null) ? this.initialPrimaryAttributes : new UnitPrimaryAttributes();
            }
            int echoVal = effAttrs.Echo;
            int glimmerVal = effAttrs.Glimmer;
            int echoFactor = echoVal * 2;
            int glimmerFactor = glimmerVal * 1;
            int totalRawSpeed = raceBonus + classBonus + echoFactor + glimmerFactor;
            return totalRawSpeed;
        }
    }
    public int FinalCalculatedBaseUnitSpeed => IsAlive ? Mathf.Max(1, RawCalculatedBaseUnitSpeed) : 1;
    public int EffectiveSpeed
    {
        get {
            if(!IsAlive) return 1;
            int baseSpeed = FinalCalculatedBaseUnitSpeed;
            int flatSpeedFromEffects = 0;
            if (Stats != null && _isInitialized)
            {
                foreach(var activeEffect in Stats.ActiveEffects)
                {
                    if(activeEffect.BaseEffect.statModifiers != null)
                    {
                        foreach(var mod in activeEffect.BaseEffect.statModifiers)
                        {
                            if(mod.stat == StatType.EffectiveSpeed && mod.type == ModifierType.Flat)
                            {
                                flatSpeedFromEffects += (int)mod.value;
                            }
                        }
                    }
                }
            }
            return Mathf.Max(1, baseSpeed + flatSpeedFromEffects);
        }
    }
}