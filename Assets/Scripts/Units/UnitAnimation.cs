// UnitAnimation.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Unit))]
public class UnitAnimation : MonoBehaviour
{
    private Unit _unitMain;
    private SpriteRenderer _spriteRenderer; // For visual effects like color changes

    // Animation timings - these could be public if you want to set them from Unit.cs's Inspector values
    [SerializeField] private float _attackAnimDuration = 0.5f;
    [SerializeField] private float _hurtAnimDuration = 0.3f;
    [SerializeField] private float _deathAnimDuration = 1.0f;

    public void Initialize(Unit mainUnit, SpriteRenderer spriteRendererToAffect)
    {
        _unitMain = mainUnit;
        if (_unitMain == null)
        {
            Debug.LogError("UnitAnimation.Initialize: Main Unit reference is null!", this);
            enabled = false; return;
        }
        _spriteRenderer = spriteRendererToAffect;
        if (_spriteRenderer == null)
        {
            Debug.LogWarning($"UnitAnimation on {_unitMain.unitName} could not find a SpriteRenderer to affect. Death/Hurt color changes might not work.", this);
        }

        // Sync durations from Unit if they are still public there, or manage them here.
        // For now, using serialized fields here. If Unit.cs still holds them:
        // _attackAnimDuration = _unitMain.attackAnimDuration;
        // _hurtAnimDuration = _unitMain.hurtAnimDuration;
        // _deathAnimDuration = _unitMain.deathAnimDuration;
    }

    // Public methods to trigger animations
    public Coroutine PlayAttackAnimation()
    {
        return StartCoroutine(PerformAttackAnimationCoroutine());
    }

    public Coroutine PlayHurtAnimation()
    {
        // Only play if alive (UnitCombat will usually check this before calling)
        if (!_unitMain.IsAlive) return null; 
        return StartCoroutine(PerformHurtAnimationCoroutine());
    }

    public Coroutine PlayDeathAnimation()
    {
        return StartCoroutine(PerformDeathAnimationCoroutine());
    }

    // Internal Coroutines
    private IEnumerator PerformAttackAnimationCoroutine()
    {
        // Placeholder: In future, trigger actual animator states
        // DebugHelper.Log($"{_unitMain.unitName} performing attack animation.", _unitMain);
        yield return new WaitForSeconds(_attackAnimDuration);
    }

    private IEnumerator PerformHurtAnimationCoroutine()
    {
        // Placeholder: Trigger animator, maybe a brief color flash
        if (_spriteRenderer != null)
        {
            // Example: flash red
            Color originalColor = _spriteRenderer.color;
            _spriteRenderer.color = Color.Lerp(originalColor, Color.red, 0.7f); // More intense red
            yield return new WaitForSeconds(_hurtAnimDuration / 2f);
            _spriteRenderer.color = originalColor;
            yield return new WaitForSeconds(_hurtAnimDuration / 2f);
        }
        else
        {
            yield return new WaitForSeconds(_hurtAnimDuration);
        }
    }

    private IEnumerator PerformDeathAnimationCoroutine()
    {
        // Placeholder: Trigger animator, change sprite color, etc.
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = Color.red; // Initial death flash
        }
        yield return new WaitForSeconds(_deathAnimDuration / 2); // Halfway through, turn darker
        if (_spriteRenderer != null)
        {
             // Ensure unit is still active in hierarchy before changing color again
            if (this != null && gameObject != null && gameObject.activeInHierarchy)
            {
                _spriteRenderer.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Dark grey / almost black
            }
        }
        yield return new WaitForSeconds(_deathAnimDuration / 2);
        // Final visual state (e.g., fully transparent or different sprite) handled by Unit becoming inactive.
    }

    // Public setters if you want to adjust durations at runtime or from Unit.cs inspector
    public void SetAttackAnimDuration(float duration) => _attackAnimDuration = duration;
    public void SetHurtAnimDuration(float duration) => _hurtAnimDuration = duration;
    public void SetDeathAnimDuration(float duration) => _deathAnimDuration = duration;
}