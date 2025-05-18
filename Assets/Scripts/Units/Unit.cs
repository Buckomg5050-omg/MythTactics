// Unit.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MythTactics.Combat;

[RequireComponent(typeof(UnitStats))]
[RequireComponent(typeof(UnitCombat))]
[RequireComponent(typeof(UnitMovement))]
[RequireComponent(typeof(UnitAnimation))]
[RequireComponent(typeof(UnitAI))]
public class Unit : MonoBehaviour
{
    [Header("Core Unit Data")]
    public RaceDataSO raceData;
    public ClassDataSO classData;
    [Tooltip("Initial primary attributes. These will be passed to UnitStats on initialization at Level 1.")]
    public UnitPrimaryAttributes initialPrimaryAttributes = new UnitPrimaryAttributes();

    [Header("Components")]
    public UnitStats Stats { get; private set; }
    public UnitCombat Combat { get; private set; }
    public UnitMovement Movement { get; private set; }
    public UnitAnimation Animation { get; private set; }
    public UnitAI AI { get; private set; }

    [Header("Basic Info")]
    public string unitName = "Unit";
    [Tooltip("Current level of the unit. Used for stat calculation during initialization.")]
    public int level = 1;

    [Header("Progression")] // MODIFIED: New Header
    [Tooltip("Base XP value this unit grants when defeated (if it's an enemy). Player units can have this as 0.")]
    public int xpValue = 10; // MODIFIED: Added xpValue field

    [Header("Abilities")]
    public List<AbilitySO> knownAbilities = new List<AbilitySO>();

    [Header("Equipment & Inventory")] // MODIFIED: Renamed Header
    public WeaponSO equippedWeapon;
    public ArmorSO equippedBodyArmor;
    [Tooltip("Items currently held by this unit.")]
    public List<ItemSO> inventory = new List<ItemSO>();

    [Header("Movement & Animation Timings")]
    public float moveSpeed = 5f;
    public float attackAnimDuration = 0.5f;
    public float hurtAnimDuration = 0.3f;
    public float deathAnimDuration = 1.0f;

    [Header("Turn Order")]
    public int actionCounter = 0;

    public Tile CurrentTile => (Movement != null) ? Movement.CurrentTile : null;
    public bool IsMoving => (Movement != null) ? Movement.IsMoving : false;
    public bool IsAlive => (Stats != null) ? Stats.IsAlive : false;

    public int CurrentActionPoints => (Stats != null) ? Stats.currentActionPoints : 0;
    public int MaxActionPoints => (Stats != null) ? Stats.MaxActionPoints : 0;


    private SpriteRenderer _mainSpriteRenderer;

    void Awake()
    {
        _mainSpriteRenderer = GetComponent<SpriteRenderer>();
        if (_mainSpriteRenderer == null && transform.childCount > 0) _mainSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        Stats = GetComponent<UnitStats>();
        if (Stats == null) Stats = gameObject.AddComponent<UnitStats>();
        // Initialize is called here. Ensure 'level' is set correctly on this Unit component
        // BEFORE Awake runs if this unit is being instantiated and configured at runtime by a spawner.
        // If placed in scene, Inspector value for 'level' will be used.
        Stats.Initialize(this, raceData, classData, initialPrimaryAttributes);

        Combat = GetComponent<UnitCombat>();
        if (Combat == null) Combat = gameObject.AddComponent<UnitCombat>();
        Combat.Initialize(this);

        Movement = GetComponent<UnitMovement>();
        if (Movement == null) Movement = gameObject.AddComponent<UnitMovement>();
        Movement.Initialize(this);
        Movement.MoveSpeed = this.moveSpeed;

        Animation = GetComponent<UnitAnimation>();
        if (Animation == null) Animation = gameObject.AddComponent<UnitAnimation>();
        Animation.Initialize(this, _mainSpriteRenderer);
        Animation.SetAttackAnimDuration(this.attackAnimDuration);
        Animation.SetHurtAnimDuration(this.hurtAnimDuration);
        Animation.SetDeathAnimDuration(this.deathAnimDuration);

        AI = GetComponent<UnitAI>();
        if (AI == null) AI = gameObject.AddComponent<UnitAI>();
        AI.Initialize(this);
    }

    public void StopMovementCoroutines() { Movement?.StopMovementCoroutines(); }
    public void ClearCurrentTileReference() { Movement?.ClearCurrentTileReferenceForDeath(); }

    public void ResetForCombatStart()
    {
        if (Stats != null)
        {
            if (IsAlive)
            {
                Stats.RegenerateActionPointsAtTurnStart();
            }
            else
            {
                Stats.currentActionPoints = 0;
            }
        }
        else
        {
            DebugHelper.LogError($"{unitName} cannot ResetForCombatStart, Stats component is null.", this);
        }
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
            int classRange = (classData != null ? Mathf.Max(0, classData.baseAttackRange) : 0);
            int weaponRange = (equippedWeapon != null && equippedWeapon.range > 0) ? equippedWeapon.range : 0;
            if (weaponRange > 0) return weaponRange;
            if (classRange > 0) return classRange;
            return 1;
        }
    }

    public int RawCalculatedBaseUnitSpeed
    {
        get
        {
            if (!IsAlive) return 1; // Dead units shouldn't break speed calcs, return a minimum

            int raceBonus = (raceData != null) ? raceData.raceSpeedBonus : 0;
            int classBonus = (classData != null) ? classData.classSpeedBonus : 0;

            // Use EffectiveAttributes for speed calculation as Glimmer/Echo might be affected by statuses
            UnitPrimaryAttributes effAttrs = (Stats != null) ? Stats.EffectiveAttributes : null;
            int echoVal = (effAttrs != null) ? effAttrs.Echo : ((Stats?.currentAttributes != null) ? Stats.currentAttributes.Echo : 0);
            int glimmerVal = (effAttrs != null) ? effAttrs.Glimmer : ((Stats?.currentAttributes != null) ? Stats.currentAttributes.Glimmer : 0);
            
            int echoFactor = echoVal * 2;
            int glimmerFactor = glimmerVal * 1;

            int totalRawSpeed = raceBonus + classBonus + echoFactor + glimmerFactor;

            return totalRawSpeed;
        }
    }
    public int FinalCalculatedBaseUnitSpeed => IsAlive ? Mathf.Max(1, RawCalculatedBaseUnitSpeed) : 1;
    public int EffectiveSpeed // GDD 1.1: FinalCalculatedBaseUnitSpeed + TotalEquipmentSpeedModifier (flat) + TotalStatusEffectSpeedModifier (flat)
    {
        get {
            if(!IsAlive) return 1;

            int baseSpeed = FinalCalculatedBaseUnitSpeed;
            // TODO: Add equipment speed modifiers here when equipment system is more advanced
            // TODO: Add status effect speed modifiers here (e.g., by iterating active effects for StatType.EffectiveSpeed with ModifierType.Flat)
            // For now, assuming status effects on Echo/Glimmer are handled by EffectiveAttributes used in RawCalculatedBaseUnitSpeed.
            // If there are direct +Speed flat effects, they need to be added here.
            // int flatSpeedFromEffects = Stats != null ? Stats.CalculateModifiedStat(0, StatType.EffectiveSpeed) : 0; // This would get *only* the flat modifiers if baseValueParam is 0
                                                                                                                 // and CalculateModifiedStat correctly handles summing just those.
            // A more direct way for flat speed from effects:
            int flatSpeedFromEffects = 0;
            if (Stats != null)
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