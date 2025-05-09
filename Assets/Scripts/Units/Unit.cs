// Unit.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Unit : MonoBehaviour
{
    [Header("Core Unit Data")]
    [Tooltip("Reference to the ScriptableObject defining the unit's race.")]
    public RaceDataSO raceData;
    [Tooltip("Reference to the ScriptableObject defining the unit's class.")]
    public ClassDataSO classData;

    [Header("Stats")]
    [Tooltip("Current primary attributes of the unit.")]
    public UnitPrimaryAttributes currentAttributes = new UnitPrimaryAttributes();
    [Tooltip("Current level of the unit.")]
    public int level = 1;

    [Header("Basic Info")]
    public string unitName = "Unit";

    [Header("Health (VP - Vitality Points)")] // NEW
    [Tooltip("Maximum Vitality Points of the unit. GDD 2.3")]
    public int maxVitalityPoints = 50; // Default value, can be set in Inspector or calculated later
    public int currentVitalityPoints;

    [Header("Movement")]
    public float moveSpeed = 5f; // Visual movement speed on map

    [Header("Action Points")]
    public int maxActionPoints = 2;
    public int currentActionPoints;

    [Header("Turn Order")]
    [Tooltip("Current action counter for turn order. Reaches 1000 for a turn. GDD 1.1.")]
    public int actionCounter = 0;

    private Tile _currentTile;
    public Tile CurrentTile => _currentTile;
    private bool _isMoving = false;
    public bool IsMoving => _isMoving;
    private Coroutine _moveCoroutine = null;
    private bool _isAlive = true; // NEW
    public bool IsAlive => _isAlive; // NEW


    void Awake() // NEW: Initialize health in Awake
    {
        currentVitalityPoints = maxVitalityPoints;
    }

    public int CalculatedMoveRange // GDD 1.2
    {
        get
        {
            if (!_isAlive) return 0; // NEW: Dead units can't move
            int raceMoveBonus = (raceData != null) ? raceData.baseMovementContribution : 0;
            int classMoveBonus = (classData != null) ? classData.baseMovementContribution : 3;
            int echoStat = (currentAttributes != null) ? currentAttributes.Echo : 0;

            if (raceData == null) DebugHelper.LogWarning($"{unitName} missing RaceData for MoveRange calc.", this);
            if (classData == null) DebugHelper.LogWarning($"{unitName} missing ClassData for MoveRange calc (using default {classMoveBonus}).", this);
            if (currentAttributes == null) DebugHelper.LogWarning($"{unitName} missing Attributes for MoveRange calc (using Echo 0).", this);

            int baseMovement = raceMoveBonus + classMoveBonus;
            int echoBonus = Mathf.FloorToInt(echoStat / 5f);
            return Mathf.Max(1, baseMovement + echoBonus);
        }
    }

    public int CalculatedAttackRange // GDD 7.1.1 (simplified)
    {
        get
        {
            if (!_isAlive) return 0; // NEW: Dead units can't attack
            if (classData != null)
            {
                return Mathf.Max(0, classData.baseAttackRange);
            }
            else
            {
                DebugHelper.LogWarning($"{unitName} missing ClassData for AttackRange calculation. Defaulting to 0 range.", this);
                return 0;
            }
        }
    }

    public int RawCalculatedBaseUnitSpeed
    {
        get
        {
            if (!_isAlive) return 1; // NEW: Dead units have minimal speed impact if somehow still in calculation
            int raceBonus = (raceData != null) ? raceData.raceSpeedBonus : 0;
            int classBonus = (classData != null) ? classData.classSpeedBonus : 0;
            int echoFactor = 0;
            int glimmerFactor = 0;

            if (currentAttributes != null)
            {
                echoFactor = currentAttributes.Echo * 2;
                glimmerFactor = currentAttributes.Glimmer * 1;
            }
            else
            {
                DebugHelper.LogWarning($"{unitName} missing currentAttributes for RawCalculatedBaseUnitSpeed calc. Echo/Glimmer factors will be 0.", this);
            }

            if (raceData == null) { DebugHelper.LogWarning($"{unitName} missing RaceData for RawCalculatedBaseUnitSpeed calc (raceBonus 0).", this); }
            if (classData == null) { DebugHelper.LogWarning($"{unitName} missing ClassData for RawCalculatedBaseUnitSpeed calc (classBonus 0).", this); }

            return raceBonus + classBonus + echoFactor + glimmerFactor;
        }
    }

    public int FinalCalculatedBaseUnitSpeed
    {
        get
        {
            if (!_isAlive) return 1; // NEW
            return Mathf.Max(1, RawCalculatedBaseUnitSpeed);
        }
    }

    public int EffectiveSpeed
    {
        get
        {
            if (!_isAlive) return 1; // NEW
            int totalEquipmentSpeedModifier = 0;
            int totalStatusEffectSpeedModifier = 0;
            return FinalCalculatedBaseUnitSpeed + totalEquipmentSpeedModifier + totalStatusEffectSpeedModifier;
        }
    }

    public void PlaceOnTile(Tile tile)
    {
        if (!_isAlive) return; // NEW
        if (_isMoving && _moveCoroutine != null) {
            StopCoroutine(_moveCoroutine);
            _isMoving = false;
            DebugHelper.LogWarning($"{unitName} placement interrupted ongoing movement.", this);
        }
        if (_currentTile != null && _currentTile.occupyingUnit == this) {
            _currentTile.ClearOccupyingUnit();
        }
        _currentTile = tile;
        if (tile != null) {
            if (GridManager.Instance != null)
            {
                transform.position = GridManager.Instance.GridToWorld(tile.gridPosition);
            }
            else
            {
                DebugHelper.LogError($"Unit.PlaceOnTile ({unitName}): GridManager.Instance not found! Cannot accurately set world position. Using tile's transform as fallback.", this);
                transform.position = tile.transform.position;
            }

            tile.SetOccupyingUnit(this);
            // ResetActionPoints(); // AP is reset by TurnManager at turn start
        } else { DebugHelper.LogWarning($"{unitName} placed on NULL tile.", this); }
    }

    public void SetCurrentTile(Tile tile) { PlaceOnTile(tile); } // Alias for now

    public IEnumerator MoveOnPath(List<Tile> path)
    {
        if (!_isAlive || _isMoving) { yield break; } // NEW: Check IsAlive
        if (path == null || path.Count == 0) { DebugHelper.LogWarning($"{unitName} requested move with empty/null path.", this); yield break; }

        _isMoving = true;
        _moveCoroutine = StartCoroutine(PerformMovement(path));
        yield return _moveCoroutine;
        _moveCoroutine = null;
        _isMoving = false;
        if (_isAlive) // Only log if still alive after potential interruption
        {
            DebugHelper.Log($"{unitName} finished movement coroutine. Final Tile: {CurrentTile?.gridPosition}", this);
        }
    }

    private IEnumerator PerformMovement(List<Tile> path)
    {
         Vector3 startPos, endPos;
         Tile nextTileInPath;

        if (CurrentTile != null)
        {
            if (GridManager.Instance != null)
                transform.position = GridManager.Instance.GridToWorld(CurrentTile.gridPosition);
            else
                transform.position = CurrentTile.transform.position;
        }
        else
        {
            DebugHelper.LogWarning($"{unitName} starting PerformMovement with no CurrentTile. Using current transform position.", this);
        }

        for (int i = 0; i < path.Count; i++)
        {
            if (!_isAlive) { DebugHelper.Log($"{unitName} died during movement.", this); yield break; } // NEW: Check during path
            nextTileInPath = path[i];
            if (nextTileInPath == null) { DebugHelper.LogError($"Movement path for {unitName} contained a null tile at index {i}!", this); break; }

            startPos = transform.position;
            if (GridManager.Instance != null)
                endPos = GridManager.Instance.GridToWorld(nextTileInPath.gridPosition);
            else
                endPos = nextTileInPath.transform.position;

            if (_currentTile != null && _currentTile.occupyingUnit == this)
            {
                _currentTile.ClearOccupyingUnit();
            }

            _currentTile = nextTileInPath;
            _currentTile.SetOccupyingUnit(this);

            float journeyLength = Vector3.Distance(startPos, endPos);
            float startTime = Time.time;

            if (journeyLength > 0.01f && moveSpeed > 0)
            {
                 float journeyFraction = 0f;
                 while (journeyFraction < 1.0f)
                 {
                     if (!_isMoving || !_isAlive) { // NEW: check isAlive
                        if(!_isAlive) DebugHelper.Log($"{unitName} died, interrupting movement lerp.", this);
                        else DebugHelper.LogWarning($"{unitName} PerformMovement sub-coroutine interrupted.", this);
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
        if (!_isAlive) { currentActionPoints = 0; return; } // NEW
        currentActionPoints = maxActionPoints;
    }

    public bool CanAffordAction(int cost)
    {
        if (!_isAlive) return false; // NEW
        bool canAfford = currentActionPoints >= cost;
        return canAfford;
    }

    public void SpendActionPoints(int cost)
    {
        if (!_isAlive) return; // NEW
        if (cost <= 0)
        {
            DebugHelper.LogWarning($"{unitName} tried to spend invalid AP cost: {cost}", this);
            return;
        }

        if (CanAffordAction(cost))
        {
            currentActionPoints -= cost;
            DebugHelper.Log($"{unitName} spent {cost} AP. Remaining: {currentActionPoints}/{maxActionPoints}", this);
        }
        else
        {
            DebugHelper.LogWarning($"{unitName} FAILED to spend AP cost {cost}. Has {currentActionPoints}/{maxActionPoints}. Action should have been prevented.", this);
        }
    }

    // NEW: TakeDamage method
    public void TakeDamage(int damageAmount)
    {
        if (!_isAlive) return; // Cannot take damage if already dead

        currentVitalityPoints -= damageAmount;
        currentVitalityPoints = Mathf.Max(0, currentVitalityPoints); // Clamp VP above 0

        DebugHelper.Log($"{unitName} takes {damageAmount} damage, has {currentVitalityPoints}/{maxVitalityPoints} VP remaining.", this);

        if (currentVitalityPoints <= 0)
        {
            Die();
        }
    }

    // NEW: Die method (placeholder logic)
    private void Die()
    {
        if (!_isAlive) return; // Already dead, do nothing

        _isAlive = false;
        DebugHelper.Log($"!!!!!! {unitName} has been defeated! !!!!!!", this);

        // Clear from tile
        if (_currentTile != null && _currentTile.occupyingUnit == this)
        {
            _currentTile.ClearOccupyingUnit();
            DebugHelper.Log($"{unitName} cleared from tile {_currentTile.gridPosition} upon death.", this);
        }
        _currentTile = null; // Unit is no longer on any tile

        // Unregister from TurnManager
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.UnregisterUnit(this);
            // Note: TurnManager.UnregisterUnit will handle clearing ActiveUnit if this unit was active
        }

        // Basic visual removal - could be an animation trigger later
        // For example, play death animation, then disable after a delay.
        // For now, just disable the renderer or the whole GameObject.
        // If you have a SpriteRenderer directly on this GameObject:
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.gray; // Indicate death visually
            // Or sr.enabled = false;
        }
        // Or disable the whole GameObject after a short delay to see the color change or allow other effects
        // StartCoroutine(DisableAfterDelay(1.0f)); // Example
        // For now, immediate disable is fine for testing core logic
         gameObject.SetActive(false); // Simplest removal for now

        // Future: Drop loot, award XP, trigger game over conditions etc.
    }

    // Example coroutine for delayed disable (optional, can be added later)
    /*
    private IEnumerator DisableAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if(!_isAlive) // Check again in case of resurrection logic later
        {
            gameObject.SetActive(false);
        }
    }
    */
}