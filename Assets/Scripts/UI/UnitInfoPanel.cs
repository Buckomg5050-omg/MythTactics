using TMPro;
using UnityEngine;

public class UnitInfoPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _healthText;
    [SerializeField] private TextMeshProUGUI _positionText;
    private UnitManager _unitManager;
    private TurnManager _turnManager;
    private Unit _currentUnit;

    private void Awake()
    {
        _unitManager = FindFirstObjectByType<UnitManager>();
        _turnManager = FindFirstObjectByType<TurnManager>();
    }

    private void OnEnable()
    {
        _unitManager.OnUnitRegistered += UpdateDisplay;
        _unitManager.OnUnitUnregistered += UpdateDisplay;
        _turnManager.OnTurnStart += OnTurnStart;
    }

    private void OnDisable()
    {
        _unitManager.OnUnitRegistered -= UpdateDisplay;
        _unitManager.OnUnitUnregistered -= UpdateDisplay;
        _turnManager.OnTurnStart -= OnTurnStart;
        if (_currentUnit != null)
            _currentUnit.OnHealthChanged -= OnHealthChanged;
    }

    private void OnTurnStart(Unit unit)
    {
        UpdateDisplay(unit);
    }

    private void OnHealthChanged(Unit unit)
    {
        if (unit == _currentUnit)
            UpdateDisplay(unit);
    }

    private void UpdateDisplay(Unit unit)
    {
        if (_currentUnit != null)
            _currentUnit.OnHealthChanged -= OnHealthChanged;

        _currentUnit = unit;

        if (unit != null)
        {
            _healthText.text = $"Health: {unit.CurrentHealth}/{unit.MaxHealth}";
            _positionText.text = $"Position: {unit.CurrentTile.GridPosition}";
            gameObject.SetActive(true);
            unit.OnHealthChanged += OnHealthChanged;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}