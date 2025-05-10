// Unit.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MythTactics.Combat;

[RequireComponent(typeof(UnitStats))]
[RequireComponent(typeof(UnitCombat))] // Ensure UnitCombat is present
public class Unit : MonoBehaviour
{
    [Header("Core Unit Data")]
    public RaceDataSO raceData;
    public ClassDataSO classData;
    [Tooltip("Initial primary attributes. These will be passed to UnitStats on initialization.")]
    public UnitPrimaryAttributes initialPrimaryAttributes = new UnitPrimaryAttributes();

    [Header("Components")]
    public UnitStats Stats { get; private set; }
    public UnitCombat Combat { get; private set; } // NEW: UnitCombat component

    [Header("Basic Info")]
    public string unitName = "Unit";
    public int level = 1;

    [Header("Abilities")]
    public List<AbilitySO> knownAbilities = new List<AbilitySO>();

    [Header("Equipment")]
    public WeaponSO equippedWeapon;
    public ArmorSO equippedBodyArmor;

    [Header("Movement & Animation Timings")] // Movement parts will move to UnitMovement
    public float moveSpeed = 5f;
    private Tile _currentTile; // Will be managed by UnitMovement
    public Tile CurrentTile => _currentTile;
    private bool _isMoving = false; // Will be managed by UnitMovement
    public bool IsMoving => _isMoving;
    private Coroutine _moveCoroutine = null; // Will be managed by UnitMovement

    // Animation timings might move to UnitAnimation or be referenced by it
    public float attackAnimDuration = 0.5f;
    public float hurtAnimDuration = 0.3f;
    public float deathAnimDuration = 1.0f;


    [Header("Action Points")] // AP is still managed directly by Unit for now
    public int maxActionPoints = 2;
    public int currentActionPoints;

    [Header("Turn Order")]
    public int actionCounter = 0;

    [Header("AI")] // Will become UnitAI component
    [SerializeField] private BasicAIHandler aiHandler;

    public bool IsAlive => (Stats != null) ? Stats.IsAlive : false;

    void Awake()
    {
        Stats = GetComponent<UnitStats>();
        if (Stats == null) Stats = gameObject.AddComponent<UnitStats>();
        Stats.Initialize(this, raceData, classData, initialPrimaryAttributes);

        // NEW: Initialize UnitCombat
        Combat = GetComponent<UnitCombat>();
        if (Combat == null) Combat = gameObject.AddComponent<UnitCombat>();
        Combat.Initialize(this);
    }

    // --- Methods to be called by UnitCombat or other future components ---
    // These are the "animation placeholders" currently in Unit.cs
    // They will eventually be replaced by calls to a UnitAnimation component.
    public IEnumerator PerformAttackAnimation() { yield return new WaitForSeconds(attackAnimDuration); }
    public IEnumerator PerformHurtAnimation() { if (!IsAlive) yield break; yield return new WaitForSeconds(hurtAnimDuration); }
    public IEnumerator PerformDeathAnimation()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>(); // This assumes Unit has the main sprite
        if (sr != null) sr.color = Color.red;
        yield return new WaitForSeconds(deathAnimDuration);
        if (sr != null && this != null && gameObject != null && gameObject.activeInHierarchy) sr.color = Color.black;
    }
    public void StopMovementCoroutines() // Needed by UnitCombat.Die()
    {
        if (_isMoving && _moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _isMoving = false;
            _moveCoroutine = null;
            DebugHelper.Log($"{unitName} movement coroutine stopped explicitly.", this);
        }
    }
    public void ClearCurrentTileReference() // Needed by UnitCombat.Die()
    {
        _currentTile = null;
    }


    // --- AP Management (still on Unit for now) ---
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

    // --- Movement (to be moved to UnitMovement) ---
    public int CalculatedMoveRange => IsAlive ? Mathf.Max(1, ((raceData != null ? raceData.baseMovementContribution : 0) + (classData != null ? classData.baseMovementContribution : 3) + Mathf.FloorToInt((Stats != null && Stats.currentAttributes != null ? Stats.currentAttributes.Echo : 0) / 5f))) : 0;
    public void PlaceOnTile(Tile tile) // Temporary - will move to UnitMovement
    {
        if (!IsAlive && tile != null && tile.occupyingUnit == this) { tile.ClearOccupyingUnit(); _currentTile = null; return; }
        if (!IsAlive) return;
        if (_isMoving && _moveCoroutine != null) { StopCoroutine(_moveCoroutine); _isMoving = false; }
        if (_currentTile != null && _currentTile.occupyingUnit == this) _currentTile.ClearOccupyingUnit();
        _currentTile = tile;
        if (tile != null)
        {
            transform.position = GridManager.Instance != null ? GridManager.Instance.GridToWorld(tile.gridPosition) : tile.transform.position;
            tile.SetOccupyingUnit(this);
        }
        else { DebugHelper.LogWarning($"{unitName} placed on NULL tile.", this); }
    }
    public void SetCurrentTile(Tile tile) { PlaceOnTile(tile); } // Temporary
    public IEnumerator MoveOnPath(List<Tile> path) // Temporary
    {
        if (!IsAlive || _isMoving) { yield break; }
        if (path == null || path.Count == 0) { yield break; }
        _isMoving = true;
        _moveCoroutine = StartCoroutine(PerformMovement(path));
        yield return _moveCoroutine;
        _isMoving = false;
        _moveCoroutine = null;
    }
    private IEnumerator PerformMovement(List<Tile> path) // Temporary
    {
        if (CurrentTile != null) transform.position = GridManager.Instance != null ? GridManager.Instance.GridToWorld(CurrentTile.gridPosition) : CurrentTile.transform.position;
        for (int i = 0; i < path.Count; i++)
        {
            if (!IsAlive) { DebugHelper.Log($"{unitName} died during movement.", this); yield break; }
            Tile nextTileInPath = path[i];
            if (nextTileInPath == null) { DebugHelper.LogError($"Movement path for {unitName} contained a null tile at index {i}!", this); break; }
            Vector3 startPos = transform.position;
            Vector3 endPos = GridManager.Instance != null ? GridManager.Instance.GridToWorld(nextTileInPath.gridPosition) : nextTileInPath.transform.position;
            if (_currentTile != null && _currentTile.occupyingUnit == this) _currentTile.ClearOccupyingUnit();
            _currentTile = nextTileInPath;
            if (IsAlive) _currentTile.SetOccupyingUnit(this);
            float journeyLength = Vector3.Distance(startPos, endPos);
            float startTime = Time.time;
            if (journeyLength > 0.01f && moveSpeed > 0)
            {
                float journeyFraction = 0f;
                while (journeyFraction < 1.0f)
                {
                    if (!_isMoving || !IsAlive) { if (!IsAlive) DebugHelper.Log($"{unitName} died, interrupting movement lerp.", this); yield break; }
                    float distCovered = (Time.time - startTime) * moveSpeed;
                    journeyFraction = distCovered / journeyLength;
                    transform.position = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(journeyFraction));
                    yield return null;
                }
            }
            transform.position = endPos;
        }
    }


    // --- AI (to be moved to UnitAI) ---
    public IEnumerator ProcessAITurn() // Temporary
    {
        if (!IsAlive) { if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this); yield break; }
        if (CompareTag("Player")) { if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this); yield break; }
        if (aiHandler == null)
        {
            if (CanAffordAPForAction(PlayerInputHandler.WaitActionCost)) SpendAPForAction(PlayerInputHandler.WaitActionCost);
            if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this);
            yield break;
        }
        // BasicAIHandler will need to be updated to call unit.Combat.PerformAttack etc.
        yield return StartCoroutine(aiHandler.ExecuteTurn(this)); 
        if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this);
    }


    // --- Other Derived Stats (some might move to relevant components) ---
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

    // REMOVED: PerformAttack, PerformAbility, TakeDamage, Die (moved to UnitCombat)
    // REMOVED: CanAffordAbility, SpendResourcesForAbility (moved to UnitCombat)
}