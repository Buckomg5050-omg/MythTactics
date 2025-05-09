// Unit.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MythTactics.Combat; // NEW: To access DamageCalculator if it's in this namespace

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

    [Header("Movement & Animation Timings")]
    public float moveSpeed = 5f;
    public float attackAnimDuration = 0.5f; 
    public float hurtAnimDuration = 0.3f;   
    public float deathAnimDuration = 1.0f;  

    // REMOVED: private const int UNARMED_BASE_DAMAGE = 1; 

    [Header("Action Points")]
    public int maxActionPoints = 2;
    public int currentActionPoints;

    [Header("Turn Order")]
    public int actionCounter = 0;

    private Tile _currentTile;
    public Tile CurrentTile => _currentTile;
    private bool _isMoving = false;
    public bool IsMoving => _isMoving;
    private Coroutine _moveCoroutine = null;
    private bool _isAlive = true;
    public bool IsAlive => _isAlive;


    void Awake() 
    {
        currentVitalityPoints = maxVitalityPoints;
        if (currentAttributes == null) 
        {
            currentAttributes = new UnitPrimaryAttributes();
            DebugHelper.LogWarning($"{unitName} currentAttributes was null, initialized to default.", this);
        }
    }

    public IEnumerator PerformAttack(Unit targetUnit, PlayerInputHandler attackerPIH) 
    {
        if (!_isAlive || targetUnit == null || !targetUnit.IsAlive || !CanAffordAction(PlayerInputHandler.AttackActionCost))
        {
            DebugHelper.LogWarning($"{unitName} cannot perform attack (dead, invalid target, or not enough AP).", this);
            yield break;
        }
        
        SpendActionPoints(PlayerInputHandler.AttackActionCost);
        DebugHelper.Log($"{unitName} attacks {targetUnit.unitName}. (AP: {currentActionPoints})", this);

        yield return StartCoroutine(PerformAttackAnimation()); 

        if (targetUnit != null && targetUnit.IsAlive) 
        {
            // MODIFIED: Use DamageCalculator
            int totalDamage = DamageCalculator.CalculateBasicUnarmedAttackDamage(this, targetUnit);

            DebugHelper.Log($"{unitName} dealing {totalDamage} (calculated) to {targetUnit.unitName}.", this);
            yield return targetUnit.StartCoroutine(targetUnit.TakeDamage(totalDamage));
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
        if (sr != null && this != null && gameObject != null) sr.color = Color.black; 
    }

    public int CalculatedMoveRange => _isAlive ? Mathf.Max(1, ((raceData != null ? raceData.baseMovementContribution : 0) + (classData != null ? classData.baseMovementContribution : 3) + Mathf.FloorToInt((currentAttributes != null ? currentAttributes.Echo : 0) / 5f))) : 0;
    public int CalculatedAttackRange => _isAlive ? (classData != null ? Mathf.Max(0, classData.baseAttackRange) : 0) : 0;
    
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
        if (!_isAlive) return; 
        if (_isMoving && _moveCoroutine != null) {
            StopCoroutine(_moveCoroutine);
            _isMoving = false;
        }
        if (_currentTile != null && _currentTile.occupyingUnit == this) {
            _currentTile.ClearOccupyingUnit();
        }
        _currentTile = tile;
        if (tile != null) {
            if (GridManager.Instance != null)
                transform.position = GridManager.Instance.GridToWorld(tile.gridPosition);
            else
                transform.position = tile.transform.position; 
            
            tile.SetOccupyingUnit(this);
        } else { DebugHelper.LogWarning($"{unitName} placed on NULL tile.", this); }
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
                     if (!_isMoving || !_isAlive) { 
                        if(!_isAlive) DebugHelper.Log($"{unitName} died, interrupting movement lerp.", this);
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
            DebugHelper.LogWarning($"{unitName} FAILED to spend AP cost {cost}. Has {currentActionPoints}/{maxActionPoints}. This indicates a logic error where SpendActionPoints was called without a prior CanAffordAction check.", this);
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
        if (!_isAlive) 
        {
            yield break; 
        }

        _isAlive = false; 
        DebugHelper.Log($"!!!!!! {unitName} has been defeated! !!!!!!", this);

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
        
        if(gameObject != null) 
        {
            gameObject.SetActive(false); 
        }
    }
}