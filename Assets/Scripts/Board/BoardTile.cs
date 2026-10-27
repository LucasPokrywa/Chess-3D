
using UnityEngine;

public class BoardTile : MonoBehaviour
{
    public int X { get; private set; }
    public int Z { get; private set; }

    private Renderer tileRenderer;
    private Material originalMaterial;

    public void Initialize(int x, int z)
    {
        X = x;
        Z = z;

        tileRenderer = GetComponent<Renderer>();
        originalMaterial = tileRenderer.sharedMaterial;
    }

    public void Highlight(Material highlightMaterial)
    {
        if (highlightMaterial == null)
            return;

        tileRenderer.sharedMaterial = highlightMaterial;
    }

    public void ResetHighlight()
    {
        tileRenderer.sharedMaterial = originalMaterial;
    }
}
