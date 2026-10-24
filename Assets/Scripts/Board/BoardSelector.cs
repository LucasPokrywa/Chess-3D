
using UnityEngine;
using UnityEngine.InputSystem;

public class BoardSelector : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Material highlightMaterial;

    private BoardTile selectedTile;

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
            SelectTile();
    }

    private void SelectTile()
    {
        if (mainCamera == null)
            return;

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        Ray ray =
            mainCamera.ScreenPointToRay(mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            BoardTile tile =
                hit.collider.GetComponent<BoardTile>();

            if (tile == null)
                return;

            if (selectedTile != null)
                selectedTile.ResetHighlight();

            selectedTile = tile;
            selectedTile.Highlight(highlightMaterial);
        }
    }
}
