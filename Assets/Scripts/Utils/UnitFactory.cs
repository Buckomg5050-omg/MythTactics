// UnitFactory.cs
using UnityEngine;
using Random = UnityEngine.Random; // Explicitly for Random.Range

public static class UnitFactory
{
    public static Unit CreateUnit(UnitTemplateSO template,
                                  int level = 1,
                                  Vector3 position = default,
                                  Quaternion rotation = default,
                                  Transform parent = null,
                                  FactionType? factionOverride = null)
    {
        if (template == null)
        {
            Debug.LogError("[UnitFactory] Cannot create unit: UnitTemplateSO is null.");
            return null;
        }

        if (template.unitPrefab == null)
        {
            Debug.LogError($"[UnitFactory] Cannot create unit for template '{template.name}': unitPrefab is null in the template.");
            return null;
        }

        // Step 1: Instantiate the Prefab
        GameObject unitGO = Object.Instantiate(template.unitPrefab, position, rotation, parent);
        Unit unitComponent = unitGO.GetComponent<Unit>();

        if (unitComponent == null)
        {
            Debug.LogError($"[UnitFactory] Failed to get Unit component from instantiated prefab for template '{template.name}'. Destroying GameObject.", unitGO);
            Object.Destroy(unitGO);
            return null;
        }

        // Determine actual faction
        FactionType actualFaction = factionOverride ?? template.defaultFaction;

        // Determine if AI stat variation should apply (this decision is made here, application is in PrimeData)
        bool applyAIStatVariation = (actualFaction != FactionType.Player && template.allowsStatVariationForAI);

        // Step 2: Prime the Unit component with data from the template.
        // PrimeDataFromTemplate on Unit.cs handles setting all fields (including initialPrimaryAttributes correctly as a new instance)
        // and then calls its own internal initialization for subsystems.
        unitComponent.PrimeDataFromTemplate(template, level, actualFaction, applyAIStatVariation);
        
        // All detailed setup, including initialPrimaryAttributes, AI config, etc., is now handled within unitComponent.PrimeDataFromTemplate

        Debug.Log($"[UnitFactory] Successfully created and primed unit '{unitComponent.unitName}' from template '{template.name}' at Lvl {unitComponent.level}, Faction: {unitComponent.CurrentFaction}.", unitComponent);
        return unitComponent;
    }
}