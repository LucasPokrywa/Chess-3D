using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Complete procedural chess lounge for the classic chess scene.
/// Does not touch the chessboard, pieces, gameplay or TurnLightIndicator.
/// Creates only meshes, materials and up to two shadowless ambient lights.
/// Attach to ClassicEnvironment (at world origin, scale 1).
/// </summary>
public class ClassicLoungeGenerator : MonoBehaviour
{
    [Header("Generation")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool hidePreviousDecoration = true;
    [SerializeField] private bool addWallSconceLights = true;

    [Header("Camera - show the whole table")]
    [SerializeField] private bool frameMainCamera = true;
    [SerializeField] private Vector3 cameraPosition = new Vector3(3.5f, 8.5f, -9.0f);
    [SerializeField] private Vector3 cameraLookAt = new Vector3(3.5f, -0.45f, 4.8f);
    [SerializeField, Range(35f, 75f)] private float cameraFov = 52f;

    [Header("Room")]
    [SerializeField, Range(9.5f, 15f)] private float tableWidth = 12.2f;
    [SerializeField, Range(9.0f, 14f)] private float tableDepth = 11.5f;
    [SerializeField, Range(0f, 2f)] private float wallLightIntensity = 1.05f;

    private const string GeneratedName = "GeneratedChessLounge";
    private const float Cx = 3.5f;
    private const float Cz = 3.5f;
    private const float FloorY = -2.70f;
    private const float WallZ = 15.2f;

    private enum Surface
    {
        FloorA, FloorB, Walnut, WalnutLight, WalnutDark, WallGreen,
        WallDeep, Brass, BrassBright, Rug, RugBorder, Velvet, Leather,
        Paper, BookRed, BookGreen, BookBlue, BookBrown, Marble, Glass, Lamp
    }

    private readonly Dictionary<Surface, MeshBucket> buckets =
        new Dictionary<Surface, MeshBucket>();

    private Transform generatedRoot;
    private Texture2D woodTexture;
    private Texture2D fabricTexture;

    private void Awake()
    {
        if (hidePreviousDecoration)
            HideOldEnvironment();
    }

    private void Start()
    {
        if (!generateOnStart) return;
        GenerateLounge();
        if (frameMainCamera) FrameCamera();
    }

    [ContextMenu("Generate / Regenerate Complete Lounge")]
    public void GenerateLounge()
    {
        RemoveOldGenerated();
        if (hidePreviousDecoration) HideOldEnvironment();

        GameObject root = new GameObject(GeneratedName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        generatedRoot = root.transform;
        buckets.Clear();
        woodTexture = BuildNoiseTexture(true);
        fabricTexture = BuildNoiseTexture(false);

        BuildFloor();
        BuildRug();
        BuildWalls();
        BuildBackWallDecor();
        BuildBookcase(-4.9f, 14.05f, "Left");
        BuildBookcase(11.9f, 14.05f, "Right");
        BuildSideWallDecor();
        BuildTable();
        BuildArmchair(-4.35f, 5.2f, 88f);
        BuildArmchair(11.35f, 5.2f, -88f);
        BuildPlanter(-6.8f, 11.4f);
        BuildPlanter(13.8f, 11.4f);
        BuildPendant();
        BuildSconces();
        FlushMeshes();

        Debug.Log("ClassicLoungeGenerator: complete lounge generated. " +
                  "Existing Board and TurnVisuals have not been modified.");
    }

    [ContextMenu("Frame Main Camera Around Table")]
    public void FrameCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject named = GameObject.Find("Main Camera");
            if (named != null) cam = named.GetComponent<Camera>();
        }
        if (cam == null)
        {
            Debug.LogWarning("ClassicLoungeGenerator: Main Camera not found.");
            return;
        }

        Vector3 worldPosition = transform.TransformPoint(cameraPosition);
        Vector3 worldTarget = transform.TransformPoint(cameraLookAt);
        cam.transform.position = worldPosition;
        cam.transform.rotation = Quaternion.LookRotation(worldTarget - worldPosition);
        cam.fieldOfView = cameraFov;
    }

    [ContextMenu("Remove Generated Lounge")]
    public void RemoveOldGenerated()
    {
        Transform old = transform.Find(GeneratedName);
        if (old == null) return;
        old.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(old.gameObject);
        else DestroyImmediate(old.gameObject);
    }

    private void HideOldEnvironment()
    {
        string[] oldNames = {
            "ClassicBackdrop", "TableTop", "Leg_FrontLeft", "Leg_FrontRight",
            "Leg_BackLeft", "Leg_BackRight", "WoodFloor", "BackWall",
            "LeftWall", "RightWall", "ClassicRug"
        };
        foreach (string n in oldNames)
        {
            Transform found = transform.Find(n);
            if (found != null) found.gameObject.SetActive(false);
        }
    }

    private MeshBucket B(Surface s)
    {
        if (!buckets.TryGetValue(s, out MeshBucket bucket))
        {
            bucket = new MeshBucket();
            buckets.Add(s, bucket);
        }
        return bucket;
    }

    private void Box(Surface s, Vector3 center, Vector3 size)
    {
        B(s).AddBox(center, size, Quaternion.identity);
    }

    private void Box(Surface s, Vector3 center, Vector3 size, float yaw)
    {
        B(s).AddBox(center, size, Quaternion.Euler(0, yaw, 0));
    }

    private void Sphere(Surface s, Vector3 center, Vector3 radius,
                        int rings = 10, int slices = 16)
    {
        B(s).AddEllipsoid(center, radius, rings, slices);
    }

    private void Cylinder(Surface s, Vector3 center, float radius, float height,
                          int sides = 20)
    {
        B(s).AddCylinder(center, radius, height, sides, Quaternion.identity);
    }

    private void Beam(Surface s, Vector3 from, Vector3 to, float radius, int sides = 10)
    {
        Vector3 dir = to - from;
        if (dir.sqrMagnitude < 0.00001f) return;
        B(s).AddCylinder((from + to) * 0.5f, radius, dir.magnitude, sides,
                         Quaternion.FromToRotation(Vector3.up, dir));
    }

    private void BuildFloor()
    {
        Box(Surface.WalnutDark, new Vector3(Cx, FloorY - 0.11f, Cz),
            new Vector3(32f, 0.22f, 32f));

        // 21 x 23 staggered wooden planks, merged into just two meshes.
        float startX = -10.5f;
        float startZ = -12.5f;
        int ix = 0;
        for (float x = startX; x < 18f; x += 1.35f, ix++)
        {
            int iz = 0;
            for (float z = startZ; z < 19.2f; z += 1.38f, iz++)
            {
                float skew = ((ix + iz) % 3) * 0.003f;
                Box((ix + iz) % 4 == 0 ? Surface.FloorB : Surface.FloorA,
                    new Vector3(x + 0.66f, FloorY + 0.012f + skew, z + 0.68f),
                    new Vector3(1.29f, 0.028f, 1.32f));
            }
        }
    }

    private void BuildRug()
    {
        // Rug below the table; all layers are thin and above the parquet.
        Box(Surface.RugBorder, new Vector3(Cx, FloorY + 0.048f, Cz),
            new Vector3(16.0f, 0.035f, 16.3f));
        Box(Surface.Brass, new Vector3(Cx, FloorY + 0.068f, Cz),
            new Vector3(15.45f, 0.014f, 15.72f));
        Box(Surface.Rug, new Vector3(Cx, FloorY + 0.079f, Cz),
            new Vector3(15.12f, 0.014f, 15.39f));
        Box(Surface.RugBorder, new Vector3(Cx, FloorY + 0.089f, Cz),
            new Vector3(13.90f, 0.012f, 14.13f));
        Box(Surface.Rug, new Vector3(Cx, FloorY + 0.102f, Cz),
            new Vector3(13.55f, 0.013f, 13.78f));

        for (int i = 0; i < 9; i++)
        {
            float x = -2.65f + i * 1.55f;
            Box(Surface.BrassBright, new Vector3(x, FloorY + 0.113f, -3.45f),
                new Vector3(0.065f, 0.008f, 0.31f), 45f);
            Box(Surface.BrassBright, new Vector3(x, FloorY + 0.113f, 10.45f),
                new Vector3(0.065f, 0.008f, 0.31f), 45f);
        }
    }

    private void BuildWalls()
    {
        // Three walls: front remains open so the gameplay camera cannot be occluded.
        Box(Surface.WallGreen, new Vector3(Cx, 1.86f, WallZ),
            new Vector3(30.0f, 9.2f, 0.30f));
        Box(Surface.WallGreen, new Vector3(-9.75f, 1.86f, Cz),
            new Vector3(0.30f, 9.2f, 27.4f));
        Box(Surface.WallGreen, new Vector3(16.75f, 1.86f, Cz),
            new Vector3(0.30f, 9.2f, 27.4f));

        // Baseboard and cornice wrap around the room.
        Box(Surface.WalnutDark, new Vector3(Cx, -2.26f, WallZ - 0.19f),
            new Vector3(29.9f, 0.65f, 0.21f));
        Box(Surface.Brass, new Vector3(Cx, -1.96f, WallZ - 0.30f),
            new Vector3(29.9f, 0.052f, 0.075f));
        Box(Surface.WalnutDark, new Vector3(Cx, 6.23f, WallZ - 0.19f),
            new Vector3(29.9f, 0.22f, 0.25f));
        Box(Surface.Brass, new Vector3(Cx, 6.07f, WallZ - 0.28f),
            new Vector3(29.9f, 0.04f, 0.065f));

        foreach (float x in new float[] { -9.47f, 16.47f })
        {
            Box(Surface.WalnutDark, new Vector3(x, -2.28f, Cz),
                new Vector3(0.23f, 0.6f, 27f));
            Box(Surface.Brass, new Vector3(x + (x < 0 ? 0.12f : -0.12f),
                -1.96f, Cz), new Vector3(0.05f, 0.05f, 27f));
            Box(Surface.WalnutDark, new Vector3(x, 6.25f, Cz),
                new Vector3(0.23f, 0.21f, 27f));
        }

        // Back wall paneled wainscot and pilasters.
        Box(Surface.WalnutDark, new Vector3(Cx, -1.0f, 14.99f),
            new Vector3(29.5f, 2.65f, 0.12f));
        Box(Surface.WalnutLight, new Vector3(Cx, 0.37f, 14.92f),
            new Vector3(29.5f, 0.14f, 0.18f));
        Box(Surface.Brass, new Vector3(Cx, 0.46f, 14.88f),
            new Vector3(29.5f, 0.026f, 0.14f));

        for (int i = 0; i < 8; i++)
        {
            float x = -8.8f + i * 3.52f;
            FrameBack(x, -0.95f, 2.75f, 2.0f, Surface.WalnutLight);
            Box(Surface.WalnutLight, new Vector3(x, 3.05f, 14.91f),
                new Vector3(0.18f, 5.80f, 0.22f));
            Box(Surface.Brass, new Vector3(x, 3.05f, 14.76f),
                new Vector3(0.025f, 5.45f, 0.055f));
        }
    }

    private void FrameBack(float x, float y, float width, float height, Surface s)
    {
        float z = 14.82f;
        Box(s, new Vector3(x, y + height / 2, z), new Vector3(width, 0.075f, 0.09f));
        Box(s, new Vector3(x, y - height / 2, z), new Vector3(width, 0.075f, 0.09f));
        Box(s, new Vector3(x - width / 2, y, z), new Vector3(0.075f, height, 0.09f));
        Box(s, new Vector3(x + width / 2, y, z), new Vector3(0.075f, height, 0.09f));
    }

    private void BuildBackWallDecor()
    {
        // Central artwork: navy velvet, double brass frame, abstract golden crown.
        Box(Surface.WalnutDark, new Vector3(Cx, 3.45f, 14.76f),
            new Vector3(5.55f, 3.35f, 0.18f));
        Box(Surface.Brass, new Vector3(Cx, 3.45f, 14.64f),
            new Vector3(5.28f, 3.08f, 0.10f));
        Box(Surface.WallDeep, new Vector3(Cx, 3.45f, 14.56f),
            new Vector3(5.02f, 2.83f, 0.07f));

        // Crown crest, clearly legible instead of a random primitive.
        Box(Surface.BrassBright, new Vector3(Cx, 2.75f, 14.48f),
            new Vector3(1.65f, 0.16f, 0.08f));
        for (int i = -2; i <= 2; i++)
        {
            float x = Cx + i * 0.37f;
            float top = i == 0 ? 4.38f : (Mathf.Abs(i) == 2 ? 3.90f : 4.15f);
            Beam(Surface.Brass, new Vector3(x, 2.9f, 14.45f),
                 new Vector3(x, top, 14.45f), 0.043f, 12);
            Sphere(Surface.BrassBright, new Vector3(x, top, 14.46f),
                   new Vector3(0.105f, 0.105f, 0.09f), 8, 12);
        }
        Box(Surface.Brass, new Vector3(Cx, 2.56f, 14.46f),
            new Vector3(1.9f, 0.12f, 0.08f));

        // Sconce area wall panels, symmetrical.
        FrameBack(-0.1f, 2.5f, 1.85f, 2.8f, Surface.Brass);
        FrameBack(7.1f, 2.5f, 1.85f, 2.8f, Surface.Brass);
    }

    private void BuildBookcase(float x, float z, string side)
    {
        float y0 = FloorY + 0.06f;
        float w = 3.5f, d = 0.75f, h = 5.7f;
        Box(Surface.WalnutDark, new Vector3(x, y0 + h / 2, z + 0.35f),
            new Vector3(w, h, 0.12f));
        foreach (float edge in new float[] { -w / 2, w / 2 })
        {
            Box(Surface.Walnut, new Vector3(x + edge, y0 + h / 2, z),
                new Vector3(0.22f, h, d));
            Box(Surface.Brass, new Vector3(x + edge, y0 + h / 2, z - 0.41f),
                new Vector3(0.024f, h - 0.15f, 0.04f));
        }
        Box(Surface.WalnutLight, new Vector3(x, y0 + h + 0.08f, z),
            new Vector3(w + 0.28f, 0.19f, d + 0.12f));
        Box(Surface.Brass, new Vector3(x, y0 + h + 0.19f, z - 0.30f),
            new Vector3(w + 0.38f, 0.035f, 0.10f));

        // Books arranged on six shelves and merged by material.
        System.Random rng = new System.Random(side == "Left" ? 1729 : 2399);
        Surface[] colors = {Surface.BookRed, Surface.BookGreen, Surface.BookBlue,
                            Surface.BookBrown, Surface.WalnutDark};
        for (int shelf = 0; shelf < 6; shelf++)
        {
            float sy = y0 + 0.12f + shelf * 0.87f;
            Box(Surface.WalnutLight, new Vector3(x, sy, z),
                new Vector3(w - 0.20f, 0.11f, d));
            Box(Surface.Brass, new Vector3(x, sy + 0.062f, z - 0.39f),
                new Vector3(w - 0.20f, 0.024f, 0.05f));
            float cursor = x - w / 2 + 0.26f;
            while (cursor < x + w / 2 - 0.35f)
            {
                float bw = 0.12f + (float)rng.NextDouble() * 0.12f;
                float bh = 0.48f + (float)rng.NextDouble() * 0.22f;
                if (cursor + bw > x + w / 2 - 0.23f) break;
                Surface book = colors[rng.Next(colors.Length)];
                Box(book, new Vector3(cursor + bw / 2, sy + 0.07f + bh / 2,
                    z - 0.09f), new Vector3(bw, bh, 0.36f));
                if (rng.Next(4) == 0)
                    Box(Surface.Brass, new Vector3(cursor + bw / 2, sy + 0.16f,
                        z - 0.286f), new Vector3(bw * 0.86f, 0.018f, 0.015f));
                cursor += bw + 0.024f;
            }
        }
        // Decorative pot and antique globe on top.
        Cylinder(Surface.Brass, new Vector3(x - 0.8f, y0 + h + 0.48f, z), 0.17f, 0.56f, 20);
        Sphere(Surface.WalnutLight, new Vector3(x + 0.8f, y0 + h + 0.52f, z),
            new Vector3(0.32f, 0.32f, 0.32f), 12, 20);
    }

    private void BuildSideWallDecor()
    {
        // Vertical mouldings and large dark window panels, without extra lights.
        foreach (int side in new int[] { -1, 1 })
        {
            float wallX = side < 0 ? -9.55f : 16.55f;
            float facing = side < 0 ? 1f : -1f;
            for (int i = 0; i < 5; i++)
            {
                float z = -6.5f + i * 5.25f;
                Box(Surface.WalnutDark, new Vector3(wallX, 1.0f, z),
                    new Vector3(0.16f, 4.95f, 0.17f));
                Box(Surface.Brass, new Vector3(wallX + facing * 0.12f, 1.0f, z),
                    new Vector3(0.035f, 4.63f, 0.06f));
            }

            for (int i = 0; i < 2; i++)
            {
                float z = -2.9f + i * 10.2f;
                Box(Surface.WalnutDark, new Vector3(wallX + facing * 0.10f,
                    2.2f, z), new Vector3(0.18f, 4.15f, 2.9f));
                Box(Surface.Glass, new Vector3(wallX + facing * 0.23f,
                    2.2f, z), new Vector3(0.045f, 3.7f, 2.54f));
                Box(Surface.Brass, new Vector3(wallX + facing * 0.275f,
                    2.2f, z), new Vector3(0.035f, 3.7f, 0.07f));
                Box(Surface.Brass, new Vector3(wallX + facing * 0.275f,
                    2.2f, z), new Vector3(0.035f, 0.07f, 2.54f));
                // Floor-length velvet curtains.
                foreach (float off in new float[] { -1.53f, 1.53f })
                    Box(Surface.Velvet, new Vector3(wallX + facing * 0.33f,
                        1.8f, z + off), new Vector3(0.12f, 4.1f, 0.46f));
            }
        }
    }

    private void BuildTable()
    {
        // Larger than the existing 8x8 board, so wood remains visible all around.
        Box(Surface.WalnutDark, new Vector3(Cx, -1.08f, Cz),
            new Vector3(tableWidth + 0.3f, 0.25f, tableDepth + 0.3f));
        Box(Surface.Brass, new Vector3(Cx, -0.94f, Cz),
            new Vector3(tableWidth + 0.05f, 0.032f, tableDepth + 0.05f));
        Box(Surface.WalnutLight, new Vector3(Cx, -0.88f, Cz),
            new Vector3(tableWidth, 0.095f, tableDepth));
        Box(Surface.Walnut, new Vector3(Cx, -0.818f, Cz),
            new Vector3(tableWidth - 0.34f, 0.028f, tableDepth - 0.34f));

        // Fine inlaid brass frame near the table edges.
        float hx = tableWidth / 2 - 0.32f;
        float hz = tableDepth / 2 - 0.32f;
        foreach (float z in new float[] { Cz - hz, Cz + hz })
            Box(Surface.Brass, new Vector3(Cx, -0.798f, z),
                new Vector3(tableWidth - 0.6f, 0.012f, 0.033f));
        foreach (float x in new float[] { Cx - hx, Cx + hx })
            Box(Surface.Brass, new Vector3(x, -0.798f, Cz),
                new Vector3(0.033f, 0.012f, tableDepth - 0.6f));

        // Aprons, four turned legs, feet, and an under-table stretcher.
        foreach (float z in new float[] { Cz - tableDepth / 2 + 0.36f,
                                           Cz + tableDepth / 2 - 0.36f })
            Box(Surface.WalnutDark, new Vector3(Cx, -1.35f, z),
                new Vector3(tableWidth - 1.05f, 0.55f, 0.20f));
        foreach (float x in new float[] { Cx - tableWidth / 2 + 0.36f,
                                           Cx + tableWidth / 2 - 0.36f })
            Box(Surface.WalnutDark, new Vector3(x, -1.35f, Cz),
                new Vector3(0.20f, 0.55f, tableDepth - 1.05f));

        float legX = tableWidth / 2 - 0.82f;
        float legZ = tableDepth / 2 - 0.82f;
        foreach (float dx in new float[] { -legX, legX })
            foreach (float dz in new float[] { -legZ, legZ })
            {
                float x = Cx + dx, z = Cz + dz;
                Box(Surface.WalnutDark, new Vector3(x, -1.78f, z),
                    new Vector3(0.55f, 1.80f, 0.55f));
                Box(Surface.Brass, new Vector3(x, -2.53f, z),
                    new Vector3(0.58f, 0.055f, 0.58f));
                Cylinder(Surface.WalnutLight, new Vector3(x, -1.48f, z),
                    0.32f, 0.13f, 20);
            }
        Box(Surface.WalnutDark, new Vector3(Cx, -2.27f, Cz),
            new Vector3(tableWidth - 2f, 0.16f, 0.18f));
    }

    private void BuildArmchair(float x, float z, float yaw)
    {
        // Chair meshes rotate about their own center; placement is outside the board.
        Quaternion q = Quaternion.Euler(0, yaw, 0);
        Vector3 origin = new Vector3(x, 0, z);
        Action<Surface, Vector3, Vector3> part = (s, p, size) =>
            B(s).AddBox(origin + q * p, size, q);
        Action<Surface, Vector3, Vector3> cushion = (s, p, radius) =>
            B(s).AddEllipsoid(origin + q * p, radius, 12, 18);

        part(Surface.WalnutDark, new Vector3(0, -1.62f, 0),
            new Vector3(1.6f, 0.22f, 1.48f));
        cushion(Surface.Velvet, new Vector3(0, -1.43f, 0.10f),
            new Vector3(0.71f, 0.24f, 0.62f));
        part(Surface.Walnut, new Vector3(0, -0.92f, 0.69f),
            new Vector3(1.64f, 1.50f, 0.28f));
        cushion(Surface.Velvet, new Vector3(0, -0.99f, 0.53f),
            new Vector3(0.72f, 0.72f, 0.22f));
        foreach (float sx in new float[] { -0.77f, 0.77f })
        {
            part(Surface.Walnut, new Vector3(sx, -1.13f, -0.06f),
                new Vector3(0.23f, 0.48f, 1.47f));
            cushion(Surface.Velvet, new Vector3(sx, -0.91f, -0.05f),
                new Vector3(0.15f, 0.11f, 0.62f));
            foreach (float sz in new float[] { -0.47f, 0.47f })
                part(Surface.WalnutDark, new Vector3(sx * 0.85f, -2.10f, sz),
                    new Vector3(0.17f, 1.04f, 0.17f));
            for (int i = 0; i < 4; i++)
                cushion(Surface.Brass, new Vector3(sx * 1.19f,
                    -1.27f + i * 0.17f, -0.05f),
                    new Vector3(0.026f, 0.026f, 0.026f));
        }
    }

    private void BuildPlanter(float x, float z)
    {
        Cylinder(Surface.WalnutDark, new Vector3(x, -2.13f, z),
            0.52f, 1.0f, 22);
        Cylinder(Surface.Brass, new Vector3(x, -1.63f, z),
            0.56f, 0.09f, 22);
        Beam(Surface.Walnut, new Vector3(x, -1.62f, z),
             new Vector3(x, 0.28f, z), 0.065f);
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * 2f / 10f;
            float h = 0.1f + (i % 3) * 0.17f;
            Vector3 from = new Vector3(x, -0.72f, z);
            Vector3 to = new Vector3(x + Mathf.Cos(a) * (0.52f + i % 2 * 0.12f),
                                     h, z + Mathf.Sin(a) * 0.57f);
            Beam(Surface.Walnut, from, to, 0.032f, 8);
            Sphere(Surface.WallGreen, to, new Vector3(0.26f, 0.53f, 0.16f),
                8, 12);
        }
    }

    private void BuildPendant()
    {
        // Decorative pendant behind the board: no additional Light component.
        float x = Cx, z = 10.9f;
        Cylinder(Surface.Brass, new Vector3(x, 6.12f, z), 0.21f, 0.10f, 24);
        Beam(Surface.Brass, new Vector3(x, 6.10f, z),
            new Vector3(x, 4.20f, z), 0.035f, 12);
        Cylinder(Surface.Brass, new Vector3(x, 4.18f, z), 0.54f, 0.12f, 30);
        Cylinder(Surface.WalnutDark, new Vector3(x, 4.05f, z), 0.62f, 0.20f, 30);
        Cylinder(Surface.Lamp, new Vector3(x, 3.91f, z), 0.39f, 0.065f, 28);
        Cylinder(Surface.Brass, new Vector3(x, 3.81f, z), 0.67f, 0.055f, 30);
    }

    private void BuildSconces()
    {
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -0.1f : 7.1f;
            float z = 14.25f;
            Box(Surface.Brass, new Vector3(x, 2.38f, 14.73f),
                new Vector3(0.20f, 0.65f, 0.14f));
            Beam(Surface.Brass, new Vector3(x, 2.35f, 14.70f),
                new Vector3(x, 2.31f, z), 0.065f, 14);
            Cylinder(Surface.Brass, new Vector3(x, 2.40f, z),
                0.20f, 0.11f, 20);
            Sphere(Surface.Lamp, new Vector3(x, 2.60f, z),
                new Vector3(0.14f, 0.21f, 0.14f), 10, 16);
            Cylinder(Surface.Brass, new Vector3(x, 2.82f, z),
                0.14f, 0.035f, 18);

            if (addWallSconceLights)
            {
                GameObject lightObject = new GameObject("WarmWallSconce_" + i);
                lightObject.transform.SetParent(generatedRoot, false);
                lightObject.transform.localPosition = new Vector3(x, 2.59f, z - 0.30f);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 4.8f;
                light.intensity = wallLightIntensity;
                light.color = new Color(1f, 0.72f, 0.47f);
                light.shadows = LightShadows.None;
            }
        }
    }

    private Texture2D BuildNoiseTexture(bool wood)
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.name = wood ? "ProceduralWoodGrain" : "ProceduralFabric";
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float noise = Mathf.PerlinNoise(x * .037f + 8f, y * .052f + 3f);
                float grain = wood
                    ? Mathf.Sin(y * .25f + Mathf.Sin(x * .09f) * 2.0f) * .045f
                    : Mathf.Sin(x * .4f) * Mathf.Sin(y * .4f) * .018f;
                float v = Mathf.Clamp(.82f + noise * .22f + grain, .72f, 1.0f);
                pixels[y * size + x] = new Color(v, v, v);
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private Material CreateMaterial(Surface s)
    {
        Color color;
        float smooth = 0.3f;
        float metallic = 0f;
        float emission = 0f;
        switch (s)
        {
            case Surface.FloorA: color = Hex("#493224"); break;
            case Surface.FloorB: color = Hex("#533929"); break;
            case Surface.Walnut: color = Hex("#4D3021"); smooth = .43f; break;
            case Surface.WalnutLight: color = Hex("#654530"); smooth = .43f; break;
            case Surface.WalnutDark: color = Hex("#251811"); smooth = .4f; break;
            case Surface.WallGreen: color = Hex("#253D34"); smooth = .16f; break;
            case Surface.WallDeep: color = Hex("#142932"); smooth = .2f; break;
            case Surface.Brass: color = Hex("#A6864B"); metallic = .65f; smooth = .68f; break;
            case Surface.BrassBright: color = Hex("#D8B96D"); metallic = .65f; smooth = .7f; break;
            case Surface.Rug: color = Hex("#622935"); smooth = .12f; break;
            case Surface.RugBorder: color = Hex("#281825"); smooth = .12f; break;
            case Surface.Velvet: color = Hex("#194B43"); smooth = .16f; break;
            case Surface.Leather: color = Hex("#38251D"); break;
            case Surface.Paper: color = Hex("#DBC6A0"); break;
            case Surface.BookRed: color = Hex("#813B39"); break;
            case Surface.BookGreen: color = Hex("#376048"); break;
            case Surface.BookBlue: color = Hex("#334D6E"); break;
            case Surface.BookBrown: color = Hex("#775332"); break;
            case Surface.Marble: color = Hex("#C8C6B7"); smooth = .6f; break;
            case Surface.Glass: color = Hex("#263C4C"); smooth = .70f; break;
            default: color = Hex("#FFD7A1"); emission = 1.6f; break;
        }
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.name = "Lounge_" + s;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
        bool useWood = s == Surface.FloorA || s == Surface.FloorB ||
            s == Surface.Walnut || s == Surface.WalnutLight || s == Surface.WalnutDark ||
            s == Surface.BookBrown;
        bool useFabric = s == Surface.Rug || s == Surface.RugBorder ||
            s == Surface.Velvet || s == Surface.WallGreen;
        if (useWood || useFabric)
        {
            Texture2D texture = useWood ? woodTexture : fabricTexture;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
        }
        if (emission > 0f && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * emission);
        }
        return mat;
    }

    private static Color Hex(string s)
    {
        ColorUtility.TryParseHtmlString(s, out Color c);
        return c;
    }

    private void FlushMeshes()
    {
        int totalTris = 0;
        foreach (KeyValuePair<Surface, MeshBucket> entry in buckets)
        {
            if (entry.Value.Vertices.Count == 0) continue;
            GameObject obj = new GameObject("Mesh_" + entry.Key);
            obj.transform.SetParent(generatedRoot, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;

            Mesh mesh = new Mesh();
            mesh.name = "Procedural_" + entry.Key;
            if (entry.Value.Vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(entry.Value.Vertices);
            mesh.SetUVs(0, entry.Value.UVs);
            mesh.SetTriangles(entry.Value.Triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateMaterial(entry.Key);
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            totalTris += entry.Value.Triangles.Count / 3;
        }
        Debug.Log("Classic lounge: " + buckets.Count + " material meshes, " +
                  totalTris + " triangles, " + (addWallSconceLights ? 2 : 0) +
                  " new shadowless lights. No new colliders.");
    }

    // Meshes are built by hand; no GameObject.CreatePrimitive is used.
    private sealed class MeshBucket
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector2> UVs = new List<Vector2>();
        public readonly List<int> Triangles = new List<int>();

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
            float u = Mathf.Max(.03f, Vector3.Distance(a, b) * 0.6f);
            float v = Mathf.Max(.03f, Vector3.Distance(a, d) * 0.6f);
            UVs.Add(new Vector2(0, 0)); UVs.Add(new Vector2(u, 0));
            UVs.Add(new Vector2(u, v)); UVs.Add(new Vector2(0, v));
            Triangles.Add(i); Triangles.Add(i + 1); Triangles.Add(i + 2);
            Triangles.Add(i); Triangles.Add(i + 2); Triangles.Add(i + 3);
        }

        public void AddBox(Vector3 center, Vector3 size, Quaternion rotation)
        {
            Vector3 h = size * 0.5f;
            Vector3[] p = {
                new Vector3(-h.x,-h.y,-h.z), new Vector3(h.x,-h.y,-h.z),
                new Vector3(h.x,h.y,-h.z),   new Vector3(-h.x,h.y,-h.z),
                new Vector3(-h.x,-h.y,h.z),  new Vector3(h.x,-h.y,h.z),
                new Vector3(h.x,h.y,h.z),    new Vector3(-h.x,h.y,h.z)
            };
            for (int i = 0; i < 8; i++) p[i] = center + rotation * p[i];
            Quad(p[0], p[3], p[2], p[1]);
            Quad(p[4], p[5], p[6], p[7]);
            Quad(p[0], p[4], p[7], p[3]);
            Quad(p[1], p[2], p[6], p[5]);
            Quad(p[3], p[7], p[6], p[2]);
            Quad(p[0], p[1], p[5], p[4]);
        }

        public void AddCylinder(Vector3 center, float radius, float height,
                                int sides, Quaternion rotation)
        {
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float b = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a) * radius, -height / 2f,
                                         Mathf.Sin(a) * radius);
                Vector3 p1 = new Vector3(Mathf.Cos(a) * radius, height / 2f,
                                         Mathf.Sin(a) * radius);
                Vector3 p2 = new Vector3(Mathf.Cos(b) * radius, height / 2f,
                                         Mathf.Sin(b) * radius);
                Vector3 p3 = new Vector3(Mathf.Cos(b) * radius, -height / 2f,
                                         Mathf.Sin(b) * radius);
                Quad(center + rotation * p0, center + rotation * p1,
                     center + rotation * p2, center + rotation * p3);
                Vector3 top = center + rotation * new Vector3(0, height / 2f, 0);
                Vector3 bot = center + rotation * new Vector3(0, -height / 2f, 0);
                AddTri(top, center + rotation * p2, center + rotation * p1);
                AddTri(bot, center + rotation * p0, center + rotation * p3);
            }
        }

        private void AddTri(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
            UVs.Add(Vector2.zero); UVs.Add(Vector2.right); UVs.Add(Vector2.up);
            Triangles.Add(i); Triangles.Add(i + 1); Triangles.Add(i + 2);
        }

        public void AddEllipsoid(Vector3 center, Vector3 radius, int rings, int slices)
        {
            for (int i = 0; i < rings; i++)
            {
                float t0 = Mathf.PI * i / rings;
                float t1 = Mathf.PI * (i + 1) / rings;
                for (int j = 0; j < slices; j++)
                {
                    float p0 = Mathf.PI * 2f * j / slices;
                    float p1 = Mathf.PI * 2f * (j + 1) / slices;
                    Vector3 a = EllipsePoint(center, radius, t0, p0);
                    Vector3 b = EllipsePoint(center, radius, t0, p1);
                    Vector3 c = EllipsePoint(center, radius, t1, p1);
                    Vector3 d = EllipsePoint(center, radius, t1, p0);
                    if (i == 0) AddTri(a, c, d);
                    else if (i == rings - 1) AddTri(a, b, c);
                    else Quad(a, b, c, d);
                }
            }
        }

        private static Vector3 EllipsePoint(Vector3 center, Vector3 radii,
                                            float theta, float phi)
        {
            return center + new Vector3(
                radii.x * Mathf.Sin(theta) * Mathf.Cos(phi),
                radii.y * Mathf.Cos(theta),
                radii.z * Mathf.Sin(theta) * Mathf.Sin(phi));
        }
    }
}
