
using System;
using System.Collections.Generic;
using UnityEngine;

public class BoardMeshGenerator : MonoBehaviour
{
    private Mesh generatedMesh;

    public void GenerateMesh(
        float tileSize,
        int subdivisions,
        float roughness,
        bool generateCracks,
        int seed)
    {
        // Paramètres conservés pour compatibilité
        // avec BoardGenerator.
        // generateCracks n'est plus utilisé.

        System.Random random = new System.Random(seed);

        float half = tileSize * 0.5f;
        float bottomY = -0.1f;
        float topY = 0.1f;

        float bevel = tileSize * 0.055f;
        float gap = tileSize * 0.008f;

        // 4 coins, avec de petites irrégularités
        float[] cornerDamage = new float[4];

        for (int i = 0; i < 4; i++)
        {
            cornerDamage[i] =
                random.NextDouble() < 0.35
                ? RandomRange(random, 0.01f, 0.045f) * tileSize
                : 0f;
        }

        Vector3[] outer =
        {
            new Vector3(-half + gap, topY - bevel, -half + gap),
            new Vector3(-half + gap, topY - bevel,  half - gap),
            new Vector3( half - gap, topY - bevel,  half - gap),
            new Vector3( half - gap, topY - bevel, -half + gap)
        };

        Vector3[] inner =
        {
            new Vector3(-half + bevel + cornerDamage[0], topY,
                        -half + bevel + cornerDamage[0]),

            new Vector3(-half + bevel + cornerDamage[1], topY,
                         half - bevel - cornerDamage[1]),

            new Vector3( half - bevel - cornerDamage[2], topY,
                         half - bevel - cornerDamage[2]),

            new Vector3( half - bevel - cornerDamage[3], topY,
                        -half + bevel + cornerDamage[3])
        };

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Surface supérieure
        AddQuad(
            vertices, triangles, uvs,
            inner[0], inner[1], inner[2], inner[3]
        );

        // Biseaux entre les contours
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;

            AddQuad(
                vertices, triangles, uvs,
                outer[i],
                outer[next],
                inner[next],
                inner[i]
            );
        }

        // Parois latérales
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;

            Vector3 a = outer[i];
            Vector3 b = outer[next];

            AddQuad(
                vertices, triangles, uvs,
                new Vector3(a.x, bottomY, a.z),
                a,
                b,
                new Vector3(b.x, bottomY, b.z)
            );
        }

        // Dessous
        AddQuad(
            vertices, triangles, uvs,
            new Vector3(outer[3].x, bottomY, outer[3].z),
            new Vector3(outer[2].x, bottomY, outer[2].z),
            new Vector3(outer[1].x, bottomY, outer[1].z),
            new Vector3(outer[0].x, bottomY, outer[0].z)
        );

        if (generatedMesh != null)
            Destroy(generatedMesh);

        generatedMesh = new Mesh();
        generatedMesh.name = "GothicStoneTile";

        generatedMesh.SetVertices(vertices);
        generatedMesh.SetTriangles(triangles, 0);
        generatedMesh.SetUVs(0, uvs);

        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = generatedMesh;

        // Collider simple et stable
        BoxCollider box = GetComponent<BoxCollider>();

        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();

        box.center = Vector3.zero;
        box.size = new Vector3(tileSize, 0.2f, tileSize);
    }

    private void AddQuad(
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> uvs,
        Vector3 a, Vector3 b,
        Vector3 c, Vector3 d)
    {
        int start = vertices.Count;

        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(0, 1));
        uvs.Add(new Vector2(1, 1));
        uvs.Add(new Vector2(1, 0));

        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);

        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    private float RandomRange(
        System.Random random,
        float min,
        float max)
    {
        return min +
            (float)random.NextDouble() * (max - min);
    }
}
