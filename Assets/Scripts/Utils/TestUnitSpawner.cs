// TestUnitSpawner.cs
using UnityEngine;

public class TestUnitSpawner : MonoBehaviour
{
    [Header("Templates to Spawn")]
    public UnitTemplateSO playerTemplate;
    public Vector3 playerSpawnPosition = new Vector3(0, 0.5f, 0); // Adjust Y for your ground

    public UnitTemplateSO enemyTemplate1;
    public Vector3 enemy1SpawnPosition = new Vector3(2, 0.5f, 5);

    public UnitTemplateSO enemyTemplate2;
    public Vector3 enemy2SpawnPosition = new Vector3(-2, 0.5f, 5);

    [Header("Spawn Settings")]
    public int playerLevel = 1;
    public int enemyLevel = 1;

    void Start()
    {
        if (playerTemplate != null)
        {
            Debug.Log($"[TestSpawner] Attempting to spawn Player unit from template: {playerTemplate.name}");
            Unit playerUnit = UnitFactory.CreateUnit(
                playerTemplate,
                playerLevel,
                playerSpawnPosition,
                Quaternion.identity,
                null, // No parent for now, or assign a "Units" empty GameObject transform
                FactionType.Player // Explicitly set faction for clarity, though template might already be Player
            );

            if (playerUnit != null)
            {
                Debug.Log($"[TestSpawner] Player Unit '{playerUnit.unitName}' spawned successfully. Level: {playerUnit.level}, Faction: {playerUnit.CurrentFaction}", playerUnit.gameObject);
                // You could add it to TurnManager's lists here if needed for your test setup,
                // though ideally units might register themselves or a CombatSetup manager handles this.
                // Example: if (TurnManager.Instance != null) TurnManager.Instance.AddUnitToCombat(playerUnit);
            }
            else
            {
                Debug.LogError($"[TestSpawner] Failed to spawn Player unit from template: {playerTemplate.name}");
            }
        }
        else
        {
            Debug.LogWarning("[TestSpawner] Player template not assigned.");
        }

        if (enemyTemplate1 != null)
        {
            Debug.Log($"[TestSpawner] Attempting to spawn Enemy 1 unit from template: {enemyTemplate1.name}");
            Unit enemyUnit1 = UnitFactory.CreateUnit(
                enemyTemplate1,
                enemyLevel,
                enemy1SpawnPosition,
                Quaternion.identity,
                null,
                FactionType.Enemy // Override faction if template wasn't Enemy, or confirm
            );

            if (enemyUnit1 != null)
            {
                Debug.Log($"[TestSpawner] Enemy Unit 1 '{enemyUnit1.unitName}' spawned successfully. Level: {enemyUnit1.level}, Faction: {enemyUnit1.CurrentFaction}", enemyUnit1.gameObject);
                 // Example: if (TurnManager.Instance != null) TurnManager.Instance.AddUnitToCombat(enemyUnit1);
            }
             else
            {
                Debug.LogError($"[TestSpawner] Failed to spawn Enemy 1 unit from template: {enemyTemplate1.name}");
            }
        }
        else
        {
            Debug.LogWarning("[TestSpawner] Enemy template 1 not assigned.");
        }

        // Add similar block for enemyTemplate2 if you want to spawn it too
    }
}