// PlayerInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem; // Required for the new Input System

public class PlayerInputHandler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign the GridManager from the scene.")]
    public GridManager gridManager;

    private PlayerControls _playerControls; // The generated C# class from your Input Actions asset
    private Camera _mainCamera;

    void Awake()
    {
        _mainCamera = Camera.main; // Cache the main camera

        // --- Input System Setup ---
        // Ensure you have generated the C# class for your Input Actions asset
        // Replace 'PlayerControls' if your asset was named differently
        _playerControls = new PlayerControls();

        if (gridManager == null)
        {
             DebugHelper.LogError("PlayerInputHandler: GridManager reference is not set!", this);
             // Optionally try to find it, but assignment is better
             // gridManager = FindObjectOfType<GridManager>();
        }
    }

    private void OnEnable()
    {
        // Enable the 'Gameplay' action map (replace if you named yours differently)
        _playerControls.Gameplay.Enable();

        // Subscribe the 'HandleClick' method to the 'performed' event of the 'Click' action
        _playerControls.Gameplay.Click.performed += HandleClick;
    }

    private void OnDisable()
    {
        // Unsubscribe when the object is disabled or destroyed to prevent memory leaks
        _playerControls.Gameplay.Click.performed -= HandleClick;

        // Disable the action map
        _playerControls.Gameplay.Disable();
    }

    /// <summary>
    /// Called when the 'Click' input action is performed (e.g., left mouse button released).
    /// </summary>
    private void HandleClick(InputAction.CallbackContext context)
    {
        // Read the current mouse position using the 'Point' action
        Vector2 screenPosition = _playerControls.Gameplay.Point.ReadValue<Vector2>();

        // Try to convert screen position to world position and then to grid position
        if (gridManager != null && _mainCamera != null)
        {
            // Note: For perspective cameras, ScreenToWorldPoint needs a Z distance.
            // For Orthographic cameras, Z doesn't matter as much for the XY plane.
            // We'll assume an orthographic camera setup suitable for a 2D grid view for now.
            // Raycasting is a more robust method for both camera types, especially with 3D elements.
            
            // --- Method 1: ScreenToWorldPoint (Simpler for Orthographic Camera at Z=0) ---
            // Vector3 worldPoint = _mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, _mainCamera.nearClipPlane + 10f)); // Adjust Z based on camera setup
            // Vector2Int gridPos = gridManager.WorldToGrid(worldPoint);

            // --- Method 2: Raycasting (More Robust) ---
            Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
            // We need to know what layer our tiles are on, or use a Plane for intersection.
            // Let's assume the grid is on the XY plane at Z=0 for simplicity.
            Plane xyPlane = new Plane(Vector3.forward, Vector3.zero); // Plane facing camera at Z=0
            float distance;

            Vector2Int gridPos = new Vector2Int(-999,-999); // Default to invalid

            if (xyPlane.Raycast(ray, out distance))
            {
                 Vector3 worldPoint = ray.GetPoint(distance);
                 gridPos = gridManager.WorldToGrid(worldPoint);
            }
            // --- End Method 2 ---


            if (gridManager.IsInPlayableBounds(gridPos))
            {
                Tile clickedTile = gridManager.GetTile(gridPos);
                if (clickedTile != null)
                {
                    DebugHelper.Log($"Clicked on PLAYABLE tile: {gridPos}, Terrain: {clickedTile.currentTerrainType}", clickedTile);
                    // TODO: Add logic here - select tile, show info, etc.
                }
                else
                {
                    // Should not happen if IsInPlayableBounds is true and GetTile works
                     DebugHelper.LogWarning($"Click mapped to playable bounds {gridPos}, but GetTile returned null?", this);
                }
            }
            else if (gridPos.x == -998) // Check for boundary marker from WorldToGrid
            {
                 DebugHelper.Log($"Clicked on BOUNDARY tile.", this);
            }
            else
            {
                DebugHelper.Log($"Clicked outside playable grid area (Screen: {screenPosition}). Grid attempt: {gridPos}", this);
            }
        }
         else
        {
            DebugHelper.LogWarning($"Cannot handle click: GridManager or MainCamera reference missing.", this);
        }
    }
}