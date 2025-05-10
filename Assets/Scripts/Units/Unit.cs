// Unit.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MythTactics.Combat;

[RequireComponent(typeof(UnitStats))]
[RequireComponent(typeof(UnitCombat))]
[RequireComponent(typeof(UnitMovement))] // Ensure UnitMovement is present
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
    public UnitMovement Movement { get; private set; } // NEW: UnitMovement component

    [Header("Basic Info")]
    public string unitName = "Unit";
    public int level = 1;

    [Header("Abilities")]
    public List<AbilitySO> knownAbilities = new List<AbilitySO>();

    [Header("Equipment")]
    public WeaponSO equippedWeapon;
    public ArmorSO equippedBodyArmor;

    // MODIFIED: moveSpeed is now primarily managed by UnitMovement, but Unit can still expose it if needed for Inspector setup.
    [Header("Movement & Animation Timings")] 
    [Tooltip("Base movement speed for visual lerping. Initial value set for UnitMovement component.")]
    public float moveSpeed = 5f; 

    // Animation timings might move to UnitAnimation or be referenced by it
    public float attackAnimDuration = 0.5f;
    public float hurtAnimDuration = 0.3f;
    public float deathAnimDuration = 1.0f;

    [Header("Action Points")]
    public int maxActionPoints = 2;
    public int currentActionPoints;

    [Header("Turn Order")]
    public int actionCounter = 0;

    [Header("AI")]
    [SerializeField] private BasicAIHandler aiHandler;

    // REMOVED: _currentTile, _isMoving, _moveCoroutine (now in UnitMovement)
    // Public accessors for these now go through UnitMovement
    public Tile CurrentTile => (Movement != null) ? Movement.CurrentTile : null;
    public bool IsMoving => (Movement != null) ? Movement.IsMoving : false;

    public bool IsAlive => (Stats != null) ? Stats.IsAlive : false;

    void Awake()
    {
        Stats = GetComponent<UnitStats>();
        if (Stats == null) Stats = gameObject.AddComponent<UnitStats>();
        Stats.Initialize(this, raceData, classData, initialPrimaryAttributes);

        Combat = GetComponent<UnitCombat>();
        if (Combat == null) Combat = gameObject.AddComponent<UnitCombat>();
        Combat.Initialize(this);

        // NEW: Initialize UnitMovement
        Movement = GetComponent<UnitMovement>();
        if (Movement == null) Movement = gameObject.AddComponent<UnitMovement>();
        Movement.Initialize(this);
        Movement.MoveSpeed = this.moveSpeed; // Pass the inspector value to UnitMovement
    }

    // --- Animation Placeholders (to be moved to UnitAnimation) ---
    public IEnumerator PerformAttackAnimation() { yield return new WaitForSeconds(attackAnimDuration); }
    public IEnumerator PerformHurtAnimation() { if (!IsAlive) yield break; yield return new WaitForSeconds(hurtAnimDuration); }
    public IEnumerator PerformDeathAnimation()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.red;
        yield return new WaitForSeconds(deathAnimDuration);
        if (sr != null && this != null && gameObject != null && gameObject.activeInHierarchy) sr.color = Color.black;
    }
    
    // --- Methods that UnitCombat might call on Unit (related to movement consequences of dying) ---
    public void StopMovementCoroutines() // Called by UnitCombat.Die
    {
        Movement?.StopMovementCoroutines();
    }
    public void ClearCurrentTileReference() // Called by UnitCombat.Die
    {
        Movement?.ClearCurrentTileReferenceForDeath();
    }


    // --- AP Management (still on Unit) ---
    public void ResetActionPoints() { if (!IsAlive) { currentActionPoints = 0; return; } currentActionPoints = maxActionPoints; }
    public bool CanAffordAPForAction(int apCost)
    {
        if (!IsAlive) return false;
        return currentActionPoints >= apCost;
    }
    public void SpendAPForAction(int apCost)
    {
        if (!IsAlive) return;
        if (apCost <= 0) return;
        if (currentActionPoints >= apCost) currentActionPoints -= apCost;
        else DebugHelper.LogWarning($"{unitName} FAILED to spend AP cost {apCost}. Has {currentActionPoints}/{maxActionPoints}.", this);
    }
    
    // --- AI (to be moved to UnitAI) ---
    public IEnumerator ProcessAITurn() 
    {
        if (!IsAlive) { if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this); yield break; }
        if (CompareTag("Player")) { if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this); yield break; }
        if (aiHandler == null)
        {
            if (CanAffordAPForAction(PlayerInputHandler.WaitActionCost)) SpendAPForAction(PlayerInputHandler.WaitActionCost);
            if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this);
            yield break;
        }
        yield return StartCoroutine(aiHandler.ExecuteTurn(this)); 
        if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this);
    }

    // --- Other Derived Stats (some might move to relevant components if they don't need Unit's direct Race/Class SOs) ---
    // CalculatedMoveRange is now in UnitMovement
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
            int echoFactor = (Stats != null && Stats.currentAttributes != null) ? Stats.currentAttributes.Echo * 2 : 0;
            int glimmerFactor = (Stats != null && Stats.currentAttributes != null) ? Stats.currentAttributes.Glimmer * 1 : 0;
            return raceBonus + classBonus + echoFactor + glimmerFactor;
        }
    }
    public int FinalCalculatedBaseUnitSpeed => IsAlive ? Mathf.Max(1, RawCalculatedBaseUnitSpeed) : 1;
    public int EffectiveSpeed => IsAlive ? (FinalCalculatedBaseUnitSpeed) : 1;

    // REMOVED: PlaceOnTile, SetCurrentTile, MoveOnPath, PerformMovement, CalculatedMoveRange
    // (They are now in UnitMovement and accessed via unit.Movement.*)
}