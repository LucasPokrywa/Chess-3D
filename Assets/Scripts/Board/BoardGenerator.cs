
using UnityEngine;

public class BoardGenerator : MonoBehaviour
{
    [Header("Echiquier")]
    [SerializeField] private int boardSize = 8;
    [SerializeField] private float tileSize = 1f;

    [Header("Matériaux")]
    [SerializeField] private Material lightMaterial;
    [SerializeField] private Material darkMaterial;

    [Header("Génération procédurale")]
    [SerializeField, Range(2, 40)]
    private int subdivisions = 24;

    [SerializeField, Range(0f, 0.02f)]
    private float roughness = 0.003f;

    [SerializeField, Range(0f, 1f)]
    private float crackProbability = 0.25f;

    private void Start()
    {
        GenerateBoard();
    }

    private void GenerateBoard()
    {
        for (int x = 0; x < boardSize; x++)
        {
            for (int z = 0; z < boardSize; z++)
            {
                GameObject tile = new GameObject(
                    $"Tile_{x}_{z}"
                );

                tile.transform.SetParent(transform, false);

                tile.transform.localPosition = new Vector3(
                    x * tileSize,
                    0f,
                    z * tileSize
                );

                tile.AddComponent<MeshFilter>();

                MeshRenderer meshRenderer =
                    tile.AddComponent<MeshRenderer>();

                bool isLight = (x + z) % 2 == 0;

                meshRenderer.sharedMaterial =
                    isLight ? lightMaterial : darkMaterial;

                BoardMeshGenerator meshGenerator =
                    tile.AddComponent<BoardMeshGenerator>();

                int seed = x * 1000 + z * 37 + 12345;

                // Génération déterministe des fissures
                System.Random random = new System.Random(seed);

                bool hasCracks =
                    random.NextDouble() < crackProbability;

                meshGenerator.GenerateMesh(
                    tileSize,
                    subdivisions,
                    roughness,
                    hasCracks,
                    seed
                );

                BoardTile boardTile =
                    tile.AddComponent<BoardTile>();

                boardTile.Initialize(x, z);
            }
        }
    }
}
