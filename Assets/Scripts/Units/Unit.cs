// Unit.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MythTactics.Combat;

[RequireComponent(typeof(UnitStats))] // Ensure UnitStats is present
public class Unit : MonoBehaviour
{
    [Header("Core Unit Data")]
    public RaceDataSO raceData;
    public ClassDataSO classData;
    // MODIFIED: Primary attributes now live in UnitStats, but Unit.cs might still want to initialize them or pass them.
    // For simplicity, we'll assume UnitStats manages its own 'currentAttributes' field, but Unit.cs provides the SOs.
    // If you want to set initial attributes from Unit.cs, you'd pass them to UnitStats.Initialize.
    [Tooltip("Initial primary attributes. These will be passed to UnitStats on initialization.")]
    public UnitPrimaryAttributes initialPrimaryAttributes = new UnitPrimaryAttributes();


    [Header("Stats Component")]
    public UnitStats Stats { get; private set; } // Public getter for the stats component

    [Header("Basic Info")]
    public string unitName = "Unit";
    public int level = 1; // Level might influence stats, so UnitStats might need it or Unit updates it. For now, it's here.

    // REMOVED: VP, MP, SP, FP, IP fields and properties (now in UnitStats)
    // REMOVED: _isAlive (now in UnitStats)

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

    [Header("AI")]
    [SerializeField] private BasicAIHandler aiHandler; // This will likely become its own UnitAI component later

    private Tile _currentTile;
    public Tile CurrentTile => _currentTile;
    private bool _isMoving = false;
    public bool IsMoving => _isMoving;
    private Coroutine _moveCoroutine = null;

    // Public accessor for IsAlive, now fetched from UnitStats
    public bool IsAlive => (Stats != null) ? Stats.IsAlive : false;


    // REMOVED: CalculatedMaxVP, MP, SP, FP, IP (now in UnitStats, accessible via Stats.CalculatedMax...)

    void Awake()
    {
        // Get or Add UnitStats component
        Stats = GetComponent<UnitStats>();
        if (Stats == null)
        {
            Stats = gameObject.AddComponent<UnitStats>();
            DebugHelper.LogWarning($"{unitName} had to add UnitStats component at runtime. Consider adding it in Prefab.", this);
        }
        
        // Initialize UnitStats, passing necessary data
        // The UnitPrimaryAttributes from this Unit script instance will be used to initialize UnitStats's attributes.
        Stats.Initialize(this, raceData, classData, initialPrimaryAttributes);

        // Other initializations for Unit.cs itself
        // currentActionPoints are reset by TurnManager at turn start, but initial value could be set here or be 0.
        // For now, let's assume TurnManager will set them before the first turn.
        // If units need AP before their first turn, initialize currentActionPoints = maxActionPoints;
    }

    // REMOVED: InitializeDerivedAttributesAndResources (now in UnitStats.InitializeDerivedAttributesAndResources called by Stats.Initialize)

    public IEnumerator PerformAttack(Unit targetUnit, PlayerInputHandler attackerPIHContext)
    {
        // MODIFIED: Use Stats.IsAlive and Stats component for resource checks if needed later
        if (!IsAlive || targetUnit == null || !targetUnit.IsAlive || !CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
        {
            yield break;
        }
        SpendAPForAction(PlayerInputHandler.AttackActionCost); // AP is still managed by Unit for now
        DebugHelper.Log($"{unitName} attacks {targetUnit.unitName}. (AP: {currentActionPoints})", this);
        yield return StartCoroutine(PerformAttackAnimation());

        // CombatCalculator.ResolveHit still takes 'this' (Unit) and 'targetUnit' (Unit)
        // It will internally access Stats from these Unit references if needed.
        if (CombatCalculator.ResolveHit(this, targetUnit))
        {
            DebugHelper.Log($"{unitName}'s attack HITS {targetUnit.unitName}!", this);
            bool isCritical = CombatCalculator.CheckCriticalHit(this, targetUnit);
            float criticalDamageMultiplier = 1.0f;
            string critMessage = "";
            if (isCritical)
            {
                criticalDamageMultiplier = DamageCalculator.CRITICAL_HIT_MULTIPLIER;
                critMessage = " (CRITICAL HIT!)";
                DebugHelper.Log($"CRITICAL HIT by {unitName} on {targetUnit.unitName}!", this);
            }
            if (targetUnit != null && targetUnit.IsAlive) // Check targetUnit.IsAlive (which checks its Stats)
            {
                int currentAttackBaseDamage = (equippedWeapon != null) ? equippedWeapon.baseDamage : DamageCalculator.UNARMED_BASE_DAMAGE;
                // DamageCalculator will also need to access Attacker/Defender Stats via their Unit references
                int totalDamage = DamageCalculator.CalculatePhysicalAttackDamage(currentAttackBaseDamage, this, targetUnit, criticalDamageMultiplier);
                DebugHelper.Log($"{unitName} dealing {totalDamage} damage to {targetUnit.unitName}{critMessage}.", this);
                if (targetUnit != null && targetUnit.gameObject.activeInHierarchy)
                {
                    // TakeDamage will now internally call targetUnit.Stats.ModifyVitality
                    yield return targetUnit.StartCoroutine(targetUnit.TakeDamage(totalDamage));
                }
            }
        }
        else
        {
            DebugHelper.Log($"{unitName}'s attack MISSES {targetUnit.unitName}!", this);
        }
        if (attackerPIHContext != null && attackerPIHContext.SelectedUnit == this)
        {
            attackerPIHContext.CheckAndHandleEndOfTurnActionsPIH();
        }
    }

    public IEnumerator PerformAttackAnimation() { yield return new WaitForSeconds(attackAnimDuration); }
    private IEnumerator PerformHurtAnimation() { if (!IsAlive) yield break; yield return new WaitForSeconds(hurtAnimDuration); }
    private IEnumerator PerformDeathAnimation()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.red;
        yield return new WaitForSeconds(deathAnimDuration);
        if (sr != null && this != null && gameObject != null && gameObject.activeInHierarchy) sr.color = Color.black;
    }

    // Derived stats like MoveRange, AttackRange, Speed still use Unit's RaceData, ClassData, and Stats.currentAttributes
    public int CalculatedMoveRange => IsAlive ? Mathf.Max(1, ((raceData != null ? raceData.baseMovementContribution : 0) + (classData != null ? classData.baseMovementContribution : 3) + Mathf.FloorToInt((Stats != null && Stats.currentAttributes != null ? Stats.currentAttributes.Echo : 0) / 5f))) : 0;
    
    public int CalculatedAttackRange // This might move to UnitCombat later
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
    public int RawCalculatedBaseUnitSpeed // This might move to UnitMovement or stay if TurnManager uses it directly
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


    public void PlaceOnTile(Tile tile)
    {
        if (!IsAlive && tile != null && tile.occupyingUnit == this) { tile.ClearOccupyingUnit(); _currentTile = null; return; }
        if (!IsAlive) return; // Check IsAlive which checks Stats.IsAlive
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
    public void SetCurrentTile(Tile tile) { PlaceOnTile(tile); }

    public IEnumerator MoveOnPath(List<Tile> path) // This will largely move to UnitMovement
    {
        if (!IsAlive || _isMoving) { yield break; }
        if (path == null || path.Count == 0) { yield break; }
        _isMoving = true;
        _moveCoroutine = StartCoroutine(PerformMovement(path));
        yield return _moveCoroutine;
        _isMoving = false;
        _moveCoroutine = null;
    }

    private IEnumerator PerformMovement(List<Tile> path) // This will largely move to UnitMovement
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
            if (IsAlive) _currentTile.SetOccupyingUnit(this); // Check IsAlive
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

    public void ResetActionPoints() { if (!IsAlive) { currentActionPoints = 0; return; } currentActionPoints = maxActionPoints; }

    public bool CanAffordAPForAction(int apCost) // AP still managed by Unit for now
    {
        if (!IsAlive) return false;
        return currentActionPoints >= apCost;
    }

    public bool CanAffordAbility(AbilitySO ability, bool logIfNotAffordable = false)
    {
        if (ability == null) { DebugHelper.LogError($"{unitName}: Attempted to check affordability for a NULL ability.", this); return false; }
        if (!IsAlive) return false;
        if (!CanAffordAPForAction(ability.apCost))
        {
            if (logIfNotAffordable) DebugHelper.LogWarning($"{unitName} cannot afford '{ability.abilityName}'. Insufficient AP. Needs: {ability.apCost}, Has: {currentActionPoints}.", this);
            return false;
        }
        // MODIFIED: Access resources via Stats component
        if (Stats.currentManaPoints < ability.mpCost)
        {
            if (logIfNotAffordable) DebugHelper.LogWarning($"{unitName} cannot afford '{ability.abilityName}'. Insufficient MP. Needs: {ability.mpCost}, Has: {Stats.currentManaPoints}.", this);
            return false;
        }
        if (Stats.currentStaminaPoints < ability.spCost)
        {
            if (logIfNotAffordable) DebugHelper.LogWarning($"{unitName} cannot afford '{ability.abilityName}'. Insufficient SP. Needs: {ability.spCost}, Has: {Stats.currentStaminaPoints}.", this);
            return false;
        }
        // Future: Add checks for Stats.currentFocusPoints and Stats.currentInfluencePoints
        return true;
    }

    public void SpendAPForAction(int apCost) // AP still managed by Unit for now
    {
        if (!IsAlive) return;
        if (apCost <= 0) return;
        if (currentActionPoints >= apCost) currentActionPoints -= apCost;
        else DebugHelper.LogWarning($"{unitName} FAILED to spend AP cost {apCost}. Has {currentActionPoints}/{maxActionPoints}. This indicates a logic error.", this);
    }

    public void SpendResourcesForAbility(AbilitySO ability) // This will likely move to UnitCombat
    {
        if (ability == null || !IsAlive) return;
        SpendAPForAction(ability.apCost);
        // MODIFIED: Call spend methods on Stats component
        if (ability.mpCost > 0) Stats.SpendMana(ability.mpCost);
        if (ability.spCost > 0) Stats.SpendStamina(ability.spCost);
        // Future: if (ability.fpCost > 0) Stats.SpendFocus(ability.fpCost);
        // Future: if (ability.ipCost > 0) Stats.SpendInfluence(ability.ipCost);

        DebugHelper.Log($"{unitName} spent resources for {ability.abilityName}. AP: {currentActionPoints}, MP: {Stats.currentManaPoints}, SP: {Stats.currentStaminaPoints}", this);
    }

    public IEnumerator TakeDamage(int damageAmount) // This will likely move to UnitCombat
    {
        if (!IsAlive) yield break; // Check IsAlive which checks Stats.IsAlive

        Stats.ModifyVitality(-damageAmount); // Negative for damage

        DebugHelper.Log($"{unitName} takes {damageAmount} damage, has {Stats.currentVitalityPoints}/{Stats.MaxVitalityPoints} VP remaining. (Alive: {Stats.IsAlive})", this);

        if (Stats.IsAlive) // Check the updated IsAlive status from Stats
        {
            yield return StartCoroutine(PerformHurtAnimation());
        }
        else
        {
            // If ModifyVitality set IsAlive to false, trigger Die()
            yield return StartCoroutine(Die());
        }
    }

    private IEnumerator Die() // This will likely move to UnitCombat or UnitState
    {
        // Note: The IsAlive flag is already false in Stats at this point if called from TakeDamage
        // If Die() is called directly for other reasons, ensure Stats.SetAliveStatus(false) is called.
        // For simplicity, we'll assume TakeDamage is the primary path to Die().
        // If UnitStats.IsAlive is already false, this might be redundant, but safe.
        if (!Stats.IsAlive && currentActionPoints == 0) { /* Already processed as dead by Stats, and possibly previous Die call */ yield break; }

        Stats.SetAliveStatus(false); // Explicitly ensure stats component knows unit is dead.
        
        DebugHelper.Log($"!!!!!! {unitName} has been defeated! !!!!!!", this);
        if (_isMoving && _moveCoroutine != null) { StopCoroutine(_moveCoroutine); _isMoving = false; _moveCoroutine = null; }
        
        yield return StartCoroutine(PerformDeathAnimation());
        
        if (_currentTile != null && _currentTile.occupyingUnit == this) _currentTile.ClearOccupyingUnit();
        _currentTile = null;
        
        if (TurnManager.Instance != null) TurnManager.Instance.UnregisterUnit(this);
        
        // Consider deactivating a parent object if Unit is part of a more complex hierarchy,
        // or if UnitStats/Combat/Movement etc. are children. For now, deactivating self is fine.
        if (gameObject != null) gameObject.SetActive(false);
    }

    public IEnumerator ProcessAITurn() // This will largely move to UnitAI
    {
        if (!IsAlive) { if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this); yield break; }
        if (CompareTag("Player")) { if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this); yield break; }
        if (aiHandler == null)
        {
            if (CanAffordAPForAction(PlayerInputHandler.WaitActionCost)) SpendAPForAction(PlayerInputHandler.WaitActionCost);
            if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this);
            yield break;
        }
        yield return StartCoroutine(aiHandler.ExecuteTurn(this)); // aiHandler will need access to Unit's components (Stats, Combat, Movement)
        if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this);
    }

    public IEnumerator PerformAbility(AbilitySO ability, Unit targetUnit, PlayerInputHandler attackerPIHContext) // This will largely move to UnitCombat
    {
        if (!IsAlive || ability == null )
        {
            DebugHelper.LogWarning($"{unitName} PerformAbility called with null ability or dead unit. This should be caught earlier.", this);
            yield break;
        }
        if (!CanAffordAbility(ability, true))
        {
            DebugHelper.LogWarning($"{unitName} somehow attempted PerformAbility for '{ability.abilityName}' without sufficient resources (redundant check failed). PIH states should prevent this.", this);
            yield break;
        }

        if (ability.targetType == AbilityTargetType.EnemyUnit || ability.targetType == AbilityTargetType.AllyUnit)
        {
            if (targetUnit == null || !targetUnit.IsAlive) { DebugHelper.LogWarning($"{unitName} cannot perform ability {ability.abilityName}: Target unit is invalid or not alive.", this); yield break; }
            if (this.CurrentTile == null || targetUnit.CurrentTile == null) { DebugHelper.LogWarning($"{unitName} or target {targetUnit.unitName} is not on a tile. Cannot perform ability {ability.abilityName}.", this); yield break; }
            if (GridManager.Instance.CalculateManhattanDistance(this.CurrentTile.gridPosition, targetUnit.CurrentTile.gridPosition) > ability.range) { DebugHelper.LogWarning($"{unitName} cannot perform ability {ability.abilityName} on {targetUnit.unitName}: Target out of range.", this); yield break; }
        }

        SpendResourcesForAbility(ability);
        DebugHelper.Log($"{unitName} uses {ability.abilityName}." + (targetUnit != null ? $" Targeting {targetUnit.unitName}." : "") + $" (AP: {currentActionPoints}, MP: {Stats.currentManaPoints}, SP: {Stats.currentStaminaPoints})", this);

        yield return new WaitForSeconds(attackAnimDuration); // Placeholder, will move to UnitAnimation

        bool abilityHits = true;
        if (ability.targetType == AbilityTargetType.EnemyUnit || ability.targetType == AbilityTargetType.AllyUnit)
        {
            if (targetUnit == null)
            {
                DebugHelper.LogError($"PerformAbility: TargetUnit is null for a unit-targeting ability '{ability.abilityName}'. This should not happen.", this);
                abilityHits = false;
            }
            else
            {
                // CombatCalculator will need to access Stats via the Unit references
                abilityHits = CombatCalculator.ResolveAbilityHit(ability, this, targetUnit);
            }
        }

        if (abilityHits)
        {
            if (ability.effectType == AbilityEffectType.Damage)
            {
                if (targetUnit != null && targetUnit.IsAlive)
                {
                    // DamageCalculator will need to access Stats via Unit references
                    int totalDamage = DamageCalculator.CalculateMagicalAbilityDamage(ability, this, targetUnit);

                    if (targetUnit.gameObject.activeInHierarchy)
                    {
                        yield return targetUnit.StartCoroutine(targetUnit.TakeDamage(totalDamage));
                    }
                }
                else if (ability.targetType != AbilityTargetType.Self && (targetUnit == null || !targetUnit.IsAlive) )
                {
                    DebugHelper.LogWarning($"{ability.abilityName} by {unitName} was meant to damage, but target is invalid/null post-hit-check. Ability may have missed or target died.", this);
                }
            }
            else if (ability.effectType == AbilityEffectType.Heal)
            {
                DebugHelper.Log($"{ability.abilityName} would heal (not implemented).", this);
            }
        }
        else
        {
             DebugHelper.Log($"{ability.abilityName} from {unitName} MISSES {targetUnit?.unitName ?? "intended target"}!", this);
        }

        if (attackerPIHContext != null && attackerPIHContext.SelectedUnit == this)
        {
            attackerPIHContext.CheckAndHandleEndOfTurnActionsPIH();
        }
    }
}