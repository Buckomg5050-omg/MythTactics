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
    public int maxVitalityPoints = 50; // This could also become a calculated property later
    public int currentVitalityPoints;

    // NEW: Mana Points (MP)
    [Header("Mana (MP - Mana Points)")]
    [SerializeField] private int _maxManaPoints; // Backing field for editor viewing if needed, calculated primarily
    public int currentManaPoints;

    // NEW: Stamina Points (SP)
    [Header("Stamina (SP - Stamina Points)")]
    [SerializeField] private int _maxStaminaPoints; // Backing field
    public int currentStaminaPoints;

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

    // --- Calculated Properties for Resources ---
    public int CalculatedMaxMP
    {
        get
        {
            if (!_isAlive) return 0;
            int baseMpFromRace = (raceData != null) ? raceData.baseMPContribution : 0;
            int baseMpFromClass = (classData != null) ? classData.baseMPContribution : 0;
            int sparkBonus = (currentAttributes != null) ? currentAttributes.Spark * 2 : 0;
            int mpFromEquipment = 0; // Placeholder for when equipment can grant MaxMP

            // GDD: BaseMP_From_Race_And_Class + (Spark * 2) + MaxMP_From_Equipment
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
            int coreBonus = (currentAttributes != null) ? currentAttributes.Core : 0; // GDD: + Core
            int spFromEquipment = 0; // Placeholder for when equipment can grant MaxSP

            // GDD: BaseSP_From_Race_And_Class + Core + MaxSP_From_Equipment
            return baseSpFromRace + baseSpFromClass + coreBonus + spFromEquipment;
        }
    }
    // --- End Calculated Properties ---

    void Awake()
    {
        if (currentAttributes == null)
        {
            currentAttributes = new UnitPrimaryAttributes();
        }
        InitializeResources();
    }

    // NEW: Method to initialize all resources
    void InitializeResources()
    {
        // VP
        currentVitalityPoints = maxVitalityPoints; // Assuming maxVitalityPoints is set or calculated elsewhere

        // MP
        _maxManaPoints = CalculatedMaxMP;
        currentManaPoints = _maxManaPoints;

        // SP
        _maxStaminaPoints = CalculatedMaxSP;
        currentStaminaPoints = _maxStaminaPoints;

        // AP
        // ResetActionPoints(); // Called by TurnManager at start of combat/turn

        DebugHelper.Log($"{unitName} Initialized Resources: VP: {currentVitalityPoints}/{maxVitalityPoints}, MP: {currentManaPoints}/{_maxManaPoints}, SP: {currentStaminaPoints}/{_maxStaminaPoints}", this);
    }


    public IEnumerator PerformAttack(Unit targetUnit, PlayerInputHandler attackerPIHContext)
    {
        if (!_isAlive || targetUnit == null || !targetUnit.IsAlive || !CanAffordAction(PlayerInputHandler.AttackActionCost))
        {
            yield break;
        }

        SpendActionPoints(PlayerInputHandler.AttackActionCost);
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
                int currentAttackBaseDamage;
                string damageSourceMessage;

                if (equippedWeapon != null)
                {
                    currentAttackBaseDamage = equippedWeapon.baseDamage;
                    damageSourceMessage = equippedWeapon.name;
                }
                else
                {
                    currentAttackBaseDamage = DamageCalculator.UNARMED_BASE_DAMAGE;
                    damageSourceMessage = "Unarmed";
                }

                int totalDamage = DamageCalculator.CalculatePhysicalAttackDamage(currentAttackBaseDamage, this, targetUnit, criticalDamageMultiplier);

                int coreBonus = (currentAttributes != null) ? Mathf.FloorToInt(currentAttributes.Core / 4f) : 0;
                DebugHelper.Log($"{unitName} dealing {totalDamage} damage to {targetUnit.unitName} using {damageSourceMessage}{critMessage} (Base: {currentAttackBaseDamage}, CoreBonus: {coreBonus}, CritMult: {criticalDamageMultiplier}).", this);

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
    }

    public IEnumerator PerformAttackAnimation()
    {
        yield return new WaitForSeconds(attackAnimDuration);
    }

    private IEnumerator PerformHurtAnimation()
    {
        if (!_isAlive) yield break;
        yield return new WaitForSeconds(hurtAnimDuration);
    }

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
        if (!_isAlive && tile != null && tile.occupyingUnit == this)
        {
            tile.ClearOccupyingUnit();
            _currentTile = null;
            return;
        }
        if (!_isAlive) return;

        if (_isMoving && _moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _isMoving = false;
        }
        if (_currentTile != null && _currentTile.occupyingUnit == this)
        {
            _currentTile.ClearOccupyingUnit();
        }
        _currentTile = tile;
        if (tile != null)
        {
            if (GridManager.Instance != null)
                transform.position = GridManager.Instance.GridToWorld(tile.gridPosition);
            else
                transform.position = tile.transform.position;
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
        Coroutine localMoveCoroutine = StartCoroutine(PerformMovement(path));
        _moveCoroutine = localMoveCoroutine;
        yield return localMoveCoroutine;
        if (_moveCoroutine == localMoveCoroutine) _moveCoroutine = null;
        _isMoving = false;
    }

    private IEnumerator PerformMovement(List<Tile> path)
    {
        Vector3 startPos, endPos;
        Tile nextTileInPath;

        if (CurrentTile != null)
            transform.position = GridManager.Instance != null ? GridManager.Instance.GridToWorld(CurrentTile.gridPosition) : CurrentTile.transform.position;

        for (int i = 0; i < path.Count; i++)
        {
            if (!_isAlive) { DebugHelper.Log($"{unitName} died during movement.", this); yield break; }
            nextTileInPath = path[i];
            if (nextTileInPath == null) { DebugHelper.LogError($"Movement path for {unitName} contained a null tile at index {i}!", this); break; }

            startPos = transform.position;
            endPos = GridManager.Instance != null ? GridManager.Instance.GridToWorld(nextTileInPath.gridPosition) : nextTileInPath.transform.position;

            if (_currentTile != null && _currentTile.occupyingUnit == this)
                _currentTile.ClearOccupyingUnit();
            _currentTile = nextTileInPath;
            if (_isAlive)
            {
                _currentTile.SetOccupyingUnit(this);
            }

            float journeyLength = Vector3.Distance(startPos, endPos);
            float startTime = Time.time;

            if (journeyLength > 0.01f && moveSpeed > 0)
            {
                float journeyFraction = 0f;
                while (journeyFraction < 1.0f)
                {
                    if (!_isMoving || !_isAlive)
                    {
                        if (!_isAlive) DebugHelper.Log($"{unitName} died, interrupting movement lerp.", this);
                        yield break;
                    }
                    float distCovered = (Time.time - startTime) * moveSpeed;
                    journeyFraction = distCovered / journeyLength;
                    transform.position = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(journeyFraction));
                    yield return null;
                }
            }
            transform.position = endPos;
        }
    }

    public void ResetActionPoints()
    {
        if (!_isAlive) { currentActionPoints = 0; return; }
        currentActionPoints = maxActionPoints;
    }

    public bool CanAffordAction(int cost)
    {
        if (!_isAlive) return false;
        return currentActionPoints >= cost;
    }

    public void SpendActionPoints(int cost)
    {
        if (!_isAlive) return;
        if (cost <= 0) return;
        if (CanAffordAction(cost))
        {
            currentActionPoints -= cost;
        }
        else
        {
            DebugHelper.LogWarning($"{unitName} FAILED to spend AP cost {cost}. Has {currentActionPoints}/{maxActionPoints}.", this);
        }
    }

    public IEnumerator TakeDamage(int damageAmount)
    {
        if (!_isAlive) yield break;
        currentVitalityPoints -= damageAmount;
        currentVitalityPoints = Mathf.Max(0, currentVitalityPoints);
        DebugHelper.Log($"{unitName} takes {damageAmount} damage, has {currentVitalityPoints}/{maxVitalityPoints} VP remaining.", this);
        if (currentVitalityPoints > 0)
        {
            yield return StartCoroutine(PerformHurtAnimation());
        }
        else
        {
            yield return StartCoroutine(Die());
        }
    }

    private IEnumerator Die()
    {
        if (!_isAlive) yield break;
        _isAlive = false;
        DebugHelper.Log($"!!!!!! {unitName} has been defeated! !!!!!!", this);
        if (_isMoving && _moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _isMoving = false;
            _moveCoroutine = null;
        }
        yield return StartCoroutine(PerformDeathAnimation());
        if (_currentTile != null && _currentTile.occupyingUnit == this)
        {
            _currentTile.ClearOccupyingUnit();
        }
        _currentTile = null;
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.UnregisterUnit(this);
        }
        if (gameObject != null)
        {
            gameObject.SetActive(false);
        }
    }

    public IEnumerator ProcessAITurn()
    {
        if (!_isAlive)
        {
            if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this);
            yield break;
        }
        if (CompareTag("Player"))
        {
            if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this);
            yield break;
        }
        if (aiHandler == null)
        {
            if (CanAffordAction(PlayerInputHandler.WaitActionCost)) SpendActionPoints(PlayerInputHandler.WaitActionCost);
            if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this) TurnManager.Instance.EndUnitTurn(this);
            yield break;
        }
        yield return StartCoroutine(aiHandler.ExecuteTurn(this));
        if (TurnManager.Instance != null && TurnManager.Instance.ActiveUnit == this)
        {
            TurnManager.Instance.EndUnitTurn(this);
        }
    }
}