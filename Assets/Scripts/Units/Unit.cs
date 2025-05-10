// Unit.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MythTactics.Combat;

public class Unit : MonoBehaviour
{
    [Header("Core Unit Data")]
    public RaceDataSO raceData;
    public ClassDataSO classData;

    [Header("Stats")]
    public UnitPrimaryAttributes currentAttributes = new UnitPrimaryAttributes();
    public int level = 1;

    [Header("Basic Info")]
    public string unitName = "Unit";

    [Header("Health (VP - Vitality Points)")]
    public int maxVitalityPoints = 50;
    public int currentVitalityPoints;

    [Header("Mana (MP - Mana Points)")]
    [SerializeField] private int _maxManaPoints;
    public int currentManaPoints;

    [Header("Stamina (SP - Stamina Points)")]
    [SerializeField] private int _maxStaminaPoints;
    public int currentStaminaPoints;

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
    [SerializeField] private BasicAIHandler aiHandler;

    private Tile _currentTile;
    public Tile CurrentTile => _currentTile;
    private bool _isMoving = false;
    public bool IsMoving => _isMoving;
    private Coroutine _moveCoroutine = null;
    private bool _isAlive = true;
    public bool IsAlive => _isAlive;

    public int MaxManaPoints => _maxManaPoints;
    public int MaxStaminaPoints => _maxStaminaPoints;


    public int CalculatedMaxMP
    {
        get
        {
            if (!_isAlive) return 0;
            int baseMpFromRace = (raceData != null) ? raceData.baseMPContribution : 0;
            int baseMpFromClass = (classData != null) ? classData.baseMPContribution : 0;
            int sparkBonus = (currentAttributes != null) ? currentAttributes.Spark * 2 : 0;
            int mpFromEquipment = 0;
            return baseMpFromRace + baseMpFromClass + sparkBonus + mpFromEquipment;
        }
    }

    public int CalculatedMaxSP
    {
        get
        {
            if (!_isAlive) return 0;
            int baseSpFromRace = (raceData != null) ? raceData.baseSPContribution : 0;
            int baseSpFromClass = (classData != null) ? classData.baseSPContribution : 0;
            int coreBonus = (currentAttributes != null) ? currentAttributes.Core : 0;
            int spFromEquipment = 0;
            return baseSpFromRace + baseSpFromClass + coreBonus + spFromEquipment;
        }
    }

    void Awake()
    {
        if (currentAttributes == null) currentAttributes = new UnitPrimaryAttributes();
        InitializeResources();
    }

    void InitializeResources()
    {
        currentVitalityPoints = maxVitalityPoints;
        _maxManaPoints = CalculatedMaxMP;
        currentManaPoints = _maxManaPoints;
        _maxStaminaPoints = CalculatedMaxSP;
        currentStaminaPoints = _maxStaminaPoints;
        DebugHelper.Log($"{unitName} Initialized Resources: VP: {currentVitalityPoints}/{maxVitalityPoints}, MP: {currentManaPoints}/{_maxManaPoints}, SP: {currentStaminaPoints}/{_maxStaminaPoints}", this);
    }

    public IEnumerator PerformAttack(Unit targetUnit, PlayerInputHandler attackerPIHContext)
    {
        if (!_isAlive || targetUnit == null || !targetUnit.IsAlive || !CanAffordAPForAction(PlayerInputHandler.AttackActionCost))
        {
            yield break;
        }
        SpendAPForAction(PlayerInputHandler.AttackActionCost);
        DebugHelper.Log($"{unitName} attacks {targetUnit.unitName}. (AP: {currentActionPoints})", this);
        yield return StartCoroutine(PerformAttackAnimation());
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
            if (targetUnit != null && targetUnit.IsAlive)
            {
                int currentAttackBaseDamage = (equippedWeapon != null) ? equippedWeapon.baseDamage : DamageCalculator.UNARMED_BASE_DAMAGE;
                int totalDamage = DamageCalculator.CalculatePhysicalAttackDamage(currentAttackBaseDamage, this, targetUnit, criticalDamageMultiplier);
                DebugHelper.Log($"{unitName} dealing {totalDamage} damage to {targetUnit.unitName}{critMessage}.", this);
                if (targetUnit != null && targetUnit.gameObject.activeInHierarchy)
                {
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
    private IEnumerator PerformHurtAnimation() { if (!_isAlive) yield break; yield return new WaitForSeconds(hurtAnimDuration); }
    private IEnumerator PerformDeathAnimation()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.red;
        yield return new WaitForSeconds(deathAnimDuration);
        if (sr != null && this != null && gameObject != null && gameObject.activeInHierarchy) sr.color = Color.black;
    }

    public int CalculatedMoveRange => _isAlive ? Mathf.Max(1, ((raceData != null ? raceData.baseMovementContribution : 0) + (classData != null ? classData.baseMovementContribution : 3) + Mathf.FloorToInt((currentAttributes != null ? currentAttributes.Echo : 0) / 5f))) : 0;
    public int CalculatedAttackRange
    {
        get
        {
            if (!_isAlive) return 0;
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
            if (!_isAlive) return 1;
            int raceBonus = (raceData != null) ? raceData.raceSpeedBonus : 0;
            int classBonus = (classData != null) ? classData.classSpeedBonus : 0;
            int echoFactor = (currentAttributes != null) ? currentAttributes.Echo * 2 : 0;
            int glimmerFactor = (currentAttributes != null) ? currentAttributes.Glimmer * 1 : 0;
            return raceBonus + classBonus + echoFactor + glimmerFactor;
        }
    }
    public int FinalCalculatedBaseUnitSpeed => _isAlive ? Mathf.Max(1, RawCalculatedBaseUnitSpeed) : 1;
    public int EffectiveSpeed => _isAlive ? (FinalCalculatedBaseUnitSpeed) : 1;

    public void PlaceOnTile(Tile tile)
    {
        if (!_isAlive && tile != null && tile.occupyingUnit == this) { tile.ClearOccupyingUnit(); _currentTile = null; return; }
        if (!_isAlive) return;
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

    public IEnumerator MoveOnPath(List<Tile> path)
    {
        if (!_isAlive || _isMoving) { yield break; }
        if (path == null || path.Count == 0) { yield break; }
        _isMoving = true;
        _moveCoroutine = StartCoroutine(PerformMovement(path));
        yield return _moveCoroutine;
        _isMoving = false; 
        _moveCoroutine = null;
    }

    private IEnumerator PerformMovement(List<Tile> path)
    {
        if (CurrentTile != null) transform.position = GridManager.Instance != null ? GridManager.Instance.GridToWorld(CurrentTile.gridPosition) : CurrentTile.transform.position;
        for (int i = 0; i < path.Count; i++)
        {
            if (!_isAlive) { DebugHelper.Log($"{unitName} died during movement.", this); yield break; }
            Tile nextTileInPath = path[i];
            if (nextTileInPath == null) { DebugHelper.LogError($"Movement path for {unitName} contained a null tile at index {i}!", this); break; }
            Vector3 startPos = transform.position;
            Vector3 endPos = GridManager.Instance != null ? GridManager.Instance.GridToWorld(nextTileInPath.gridPosition) : nextTileInPath.transform.position;
            if (_currentTile != null && _currentTile.occupyingUnit == this) _currentTile.ClearOccupyingUnit();
            _currentTile = nextTileInPath;
            if (_isAlive) _currentTile.SetOccupyingUnit(this);
            float journeyLength = Vector3.Distance(startPos, endPos);
            float startTime = Time.time;
            if (journeyLength > 0.01f && moveSpeed > 0)
            {
                float journeyFraction = 0f;
                while (journeyFraction < 1.0f)
                {
                    if (!_isMoving || !_isAlive) { if (!_isAlive) DebugHelper.Log($"{unitName} died, interrupting movement lerp.", this); yield break; }
                    float distCovered = (Time.time - startTime) * moveSpeed;
                    journeyFraction = distCovered / journeyLength;
                    transform.position = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(journeyFraction));
                    yield return null;
                }
            }
            transform.position = endPos;
        }
    }

    public void ResetActionPoints() { if (!_isAlive) { currentActionPoints = 0; return; } currentActionPoints = maxActionPoints; }

    public bool CanAffordAPForAction(int apCost)
    {
        if (!_isAlive) return false;
        return currentActionPoints >= apCost;
    }

    public bool CanAffordAbility(AbilitySO ability, bool logIfNotAffordable = false)
    {
        if (ability == null) { DebugHelper.LogError($"{unitName}: Attempted to check affordability for a NULL ability.", this); return false; }
        if (!_isAlive) return false; 
        if (!CanAffordAPForAction(ability.apCost))
        {
            if (logIfNotAffordable) DebugHelper.LogWarning($"{unitName} cannot afford '{ability.abilityName}'. Insufficient AP. Needs: {ability.apCost}, Has: {currentActionPoints}.", this);
            return false;
        }
        if (currentManaPoints < ability.mpCost)
        {
            if (logIfNotAffordable) DebugHelper.LogWarning($"{unitName} cannot afford '{ability.abilityName}'. Insufficient MP. Needs: {ability.mpCost}, Has: {currentManaPoints}.", this);
            return false;
        }
        if (currentStaminaPoints < ability.spCost)
        {
            if (logIfNotAffordable) DebugHelper.LogWarning($"{unitName} cannot afford '{ability.abilityName}'. Insufficient SP. Needs: {ability.spCost}, Has: {currentStaminaPoints}.", this);
            return false;
        }
        return true;
    }

    public void SpendAPForAction(int apCost)
    {
        if (!_isAlive) return;
        if (apCost <= 0) return;
        if (currentActionPoints >= apCost) currentActionPoints -= apCost;
        else DebugHelper.LogWarning($"{unitName} FAILED to spend AP cost {apCost}. Has {currentActionPoints}/{maxActionPoints}. This indicates a logic error.", this);
    }

    public void SpendResourcesForAbility(AbilitySO ability)
    {
        if (ability == null || !_isAlive) return;
        SpendAPForAction(ability.apCost);
        if (ability.mpCost > 0) { currentManaPoints -= ability.mpCost; currentManaPoints = Mathf.Max(0, currentManaPoints); }
        if (ability.spCost > 0) { currentStaminaPoints -= ability.spCost; currentStaminaPoints = Mathf.Max(0, currentStaminaPoints); }
        DebugHelper.Log($"{unitName} spent resources for {ability.abilityName}. AP: {currentActionPoints}, MP: {currentManaPoints}, SP: {currentStaminaPoints}", this);
    }

    public IEnumerator TakeDamage(int damageAmount)
    {
        if (!_isAlive) yield break;
        currentVitalityPoints -= damageAmount;
        currentVitalityPoints = Mathf.Max(0, currentVitalityPoints);
        DebugHelper.Log($"{unitName} takes {damageAmount} damage, has {currentVitalityPoints}/{maxVitalityPoints} VP remaining.", this);
        if (currentVitalityPoints > 0) yield return StartCoroutine(PerformHurtAnimation());
        else yield return StartCoroutine(Die());
    }

    private IEnumerator Die()
    {
        if (!_isAlive) yield break;
        _isAlive = false;
        DebugHelper.Log($"!!!!!! {unitName} has been defeated! !!!!!!", this);
        if (_isMoving && _moveCoroutine != null) { StopCoroutine(_moveCoroutine); _isMoving = false; _moveCoroutine = null; }
        yield return StartCoroutine(PerformDeathAnimation());
        if (_currentTile != null && _currentTile.occupyingUnit == this) _currentTile.ClearOccupyingUnit();
        _currentTile = null;
        if (TurnManager.Instance != null) TurnManager.Instance.UnregisterUnit(this);
        if (gameObject != null) gameObject.SetActive(false);
    }

    public IEnumerator ProcessAITurn()
    {
        if (!_isAlive) { if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this); yield break; }
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

    public IEnumerator PerformAbility(AbilitySO ability, Unit targetUnit, PlayerInputHandler attackerPIHContext)
    {
        if (!_isAlive || ability == null ) 
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
        DebugHelper.Log($"{unitName} uses {ability.abilityName}." + (targetUnit != null ? $" Targeting {targetUnit.unitName}." : "") + $" (AP: {currentActionPoints}, MP: {currentManaPoints}, SP: {currentStaminaPoints})", this);
        
        yield return new WaitForSeconds(attackAnimDuration); // Placeholder for ability animation

        if (ability.effectType == AbilityEffectType.Damage)
        {
            if (targetUnit != null && targetUnit.IsAlive) 
            {
                DebugHelper.Log($"{ability.abilityName} from {unitName} HITS {targetUnit.unitName}!", this); // Still useful to log the general hit

                int totalDamage = DamageCalculator.CalculateMagicalAbilityDamage(ability, this, targetUnit);
                // Detailed damage breakdown (base, bonus, crit status, final) is now logged by DamageCalculator.CalculateMagicalAbilityDamage
                
                if (targetUnit != null && targetUnit.gameObject.activeInHierarchy) 
                {
                    yield return targetUnit.StartCoroutine(targetUnit.TakeDamage(totalDamage));
                }
            }
            else if (ability.targetType != AbilityTargetType.Self && targetUnit == null)
            { 
                DebugHelper.LogWarning($"{ability.abilityName} by {unitName} requires a target but none was provided or target is invalid.", this); 
            }
        }
        else if (ability.effectType == AbilityEffectType.Heal) 
        { 
            DebugHelper.Log($"{ability.abilityName} would heal (not implemented).", this); 
        }
        
        if (attackerPIHContext != null && attackerPIHContext.SelectedUnit == this) 
        {
            attackerPIHContext.CheckAndHandleEndOfTurnActionsPIH();
        }
    }
}