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
    [Tooltip("Initial primary attributes. These will be passed to UnitStats on initialization.")]
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

    [Header("Abilities")]
    public List<AbilitySO> knownAbilities = new List<AbilitySO>();

    [Header("Equipment")]
    public WeaponSO equippedWeapon;
    public ArmorSO equippedBodyArmor;

    [Header("Movement & Animation Timings")] 
    public float moveSpeed = 5f; 
    public float attackAnimDuration = 0.5f;
    public float hurtAnimDuration = 0.3f;
    public float deathAnimDuration = 1.0f;

    [Header("Action Points")]
    public int maxActionPoints = 2;
    public int currentActionPoints;

    [Header("Turn Order")]
    public int actionCounter = 0;

    public Tile CurrentTile => (Movement != null) ? Movement.CurrentTile : null;
    public bool IsMoving => (Movement != null) ? Movement.IsMoving : false;
    public bool IsAlive => (Stats != null) ? Stats.IsAlive : false;

    private SpriteRenderer _mainSpriteRenderer;

    void Awake()
    {
        _mainSpriteRenderer = GetComponent<SpriteRenderer>(); 
        if (_mainSpriteRenderer == null && transform.childCount > 0) _mainSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        Stats = GetComponent<UnitStats>();
        if (Stats == null) Stats = gameObject.AddComponent<UnitStats>();
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

    public void ResetActionPoints() { if (!IsAlive) { currentActionPoints = 0; return; } currentActionPoints = maxActionPoints; }
    public bool CanAffordAPForAction(int apCost) { if (!IsAlive) return false; return currentActionPoints >= apCost; }
    public void SpendAPForAction(int apCost)
    {
        if (!IsAlive) return;
        if (apCost <= 0) return;
        if (currentActionPoints >= apCost) currentActionPoints -= apCost;
        else DebugHelper.LogWarning($"{unitName} FAILED to spend AP cost {apCost}. Has {currentActionPoints}/{maxActionPoints}.", this);
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
            if (!IsAlive) return 1; 
            
            int raceBonus = (raceData != null) ? raceData.raceSpeedBonus : 0;
            int classBonus = (classData != null) ? classData.classSpeedBonus : 0;

            int echoVal = 0;
            int glimmerVal = 0;
            // bool attributesValid = (Stats != null && Stats.currentAttributes != null); // Keep for clarity if desired

            if (Stats != null && Stats.currentAttributes != null)
            {
                echoVal = Stats.currentAttributes.Echo;
                glimmerVal = Stats.currentAttributes.Glimmer;
            }
            
            int echoFactor = echoVal * 2;
            int glimmerFactor = glimmerVal * 1;
            
            int totalRawSpeed = raceBonus + classBonus + echoFactor + glimmerFactor;

            // REMOVED DEBUG LOG FROM HERE
                            
            return totalRawSpeed;
        }
    }
    public int FinalCalculatedBaseUnitSpeed => IsAlive ? Mathf.Max(1, RawCalculatedBaseUnitSpeed) : 1;
    public int EffectiveSpeed => IsAlive ? (FinalCalculatedBaseUnitSpeed) : 1;
}