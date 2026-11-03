using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A complete, dramatic Halloween crypt environment built entirely from procedural meshes.
/// Designed around the existing 8x8 chess board whose center is (3.5, 0, 3.5).
/// Keeps the board, gameplay, rune effects, wisps and existing four candles untouched.
/// No colliders and NO additional real-time lights are created by this generator.
/// Attach to an empty HalloweenEnvironment object at world origin (scale 1).
/// </summary>
public class HalloweenCryptRoomGenerator : MonoBehaviour
{
    [Header("Generation")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool disablePreviousCryptEnvironment = true;
    [SerializeField] private bool createCeilingRibs = true;
    [SerializeField] private bool createTombs = true;
    [SerializeField] private bool createSkullsAndChains = true;
    [SerializeField] private int seed = 314159;

    [Header("Camera - optional")]
    [SerializeField] private bool frameMainCamera = true;
    [SerializeField] private Vector3 cameraPosition = new Vector3(3.5f, 8.8f, -7.3f);
    [SerializeField] private Vector3 cameraLookAt = new Vector3(3.5f, 0.24f, 4.45f);
    [SerializeField, Range(40f, 70f)] private float cameraFieldOfView = 53f;

    [Header("Architecture")]
    [SerializeField, Range(4.0f, 10f)] private float wallHeight = 8.2f;
    [SerializeField] private bool glowStainedGlass = true;

    [Header("Alien existant - glisser l'objet depuis la Hierarchy")]
    [Tooltip("L'instance de l'alien deja presente dans ta scene, PAS le prefab du Project.")]
    [SerializeField] private GameObject alien;
    [Tooltip("Place cet alien derriere le plateau lors de la generation, sans creer de copie.")]
    [SerializeField] private bool positionAlienOnGenerate = true;
    [Tooltip("Position du SOL au pied de l'alien, en coordonnees locales de HalloweenEnvironment.")]
    [SerializeField] private Vector3 alienFloorPosition = new Vector3(3.5f, -0.80f, 10.1f);
    [Tooltip("Le personnage regardera vers le centre de l'echiquier. Ajuster si le modele regarde a l'envers.")]
    [SerializeField, Range(-180f, 180f)] private float alienYawOffset = 0f;
    [Tooltip("Ajuste uniquement l'echelle de cet alien existant pour une hauteur visible cible.")]
    [SerializeField] private bool autoFitAlienHeight = true;
    [SerializeField, Range(0.5f, 7f)] private float alienTargetHeight = 3.3f;
    [Tooltip("Calcule le bas du mesh pour poser l'alien sur le sol plutot que sur son pivot.")]
    [SerializeField] private bool alignAlienFeetToFloor = true;
    [Tooltip("Ajoute un cercle rituel emissif sous l'alien, sans creer de Light.")]
    [SerializeField] private bool createAlienRitualCircle = true;
    [SerializeField, Range(0.8f, 2.5f)] private float alienRitualRadius = 1.5f;

    private const string GeneratedName = "GeneratedHauntedCathedral";
    private const float Cx = 3.5f;
    private const float Cz = 3.5f;
    private const float Floor = -0.83f;
    private const float Back = 16.4f;
    private const float Left = -10.2f;
    private const float Right = 17.2f;
    private const float StoneFront = Back - 0.25f;

    private enum Surface
    {
        FloorDark, FloorMid, FloorEdge, Wall, WallAlt, StoneLight,
        Basalt, BlackIron, IronBright, Bone, Rust, VioletGlass,
        VioletRune, VioletDeep, Ember, Moss, Velvet
    }

    private readonly Dictionary<Surface, Bucket> buckets = new Dictionary<Surface, Bucket>();
    private readonly List<Material> createdMaterials = new List<Material>();
    private readonly List<Mesh> createdMeshes = new List<Mesh>();
    private Transform generatedRoot;
    private Camera adjustedCamera;
    private CameraClearFlags oldClearFlags;
    private Color oldBackground;
    private bool savedCamera;
    private System.Random random;

    private void Awake()
    {
        // Disable legacy *room* generators early, so their Start() cannot create
        // a second overlapping floor. Keep all board-attached generators as they are.
        if (disablePreviousCryptEnvironment)
            DisableOldRooms();
    }

    private void Start()
    {
        if (!generateOnStart) return;
        GenerateRoom();
        if (frameMainCamera) FrameCamera();
    }

    [ContextMenu("Generate / Regenerate Haunted Cathedral")]
    public void GenerateRoom()
    {
        RemoveGenerated();
        if (disablePreviousCryptEnvironment) DisableOldRooms();
        random = new System.Random(seed);
        buckets.Clear();

        GameObject root = new GameObject(GeneratedName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        generatedRoot = root.transform;

        BuildFloor();
        BuildWalls();
        BuildButtresses();
        BuildGothicArches();
        BuildRoseWindow();
        BuildSideWindows();
        BuildCentralAltar();
        BuildStatuesAndNiches();
        if (createTombs) BuildTombs();
        if (createSkullsAndChains) BuildSkullsAndChains();
        if (createCeilingRibs) BuildCeilingRibs();
        BuildOccultAccents();
        if (alien != null && createAlienRitualCircle)
            BuildAlienRitualCircle();
        FlushMeshes();

        // Place l'instance EXISTANTE de l'alien; ne touche ni a ses composants
        // de combat, ni a son Animator, ni a ses references de scene.
        if (positionAlienOnGenerate)
            PositionExistingAlien();

        // Use a very dark, non-gray background if the room does not fill the view.
        Camera cam = Camera.main;
        if (cam != null)
        {
            if (!savedCamera)
            {
                oldClearFlags = cam.clearFlags;
                oldBackground = cam.backgroundColor;
                adjustedCamera = cam;
                savedCamera = true;
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#090812");
        }
    }

    [ContextMenu("Repositionner l'alien existant")]
    public void PositionExistingAlien()
    {
        if (alien == null)
        {
            Debug.Log("HalloweenCryptRoomGenerator : pas d'alien assigne. " +
                "Glisse l'objet Alien de la Hierarchy dans le champ Alien de l'Inspector.", this);
            return;
        }

        // Un prefab dans le Project n'est pas une instance de scene. Le modifier
        // risquerait de changer un asset et non le personnage utilise par le jeu.
        if (!alien.scene.IsValid())
        {
            Debug.LogWarning("HalloweenCryptRoomGenerator : selectionne l'ALIEN de la " +
                "Hierarchy (instance de scene), pas son prefab dans Project.", this);
            return;
        }

        if (alien == gameObject || alien.transform.IsChildOf(transform))
        {
            // Un enfant de HalloweenEnvironment est possible, mais jamais
            // un enfant de GeneratedHauntedCathedral (supprime a la regeneration).
            Transform oldGenerated = transform.Find(GeneratedName);
            if (alien == gameObject ||
                (oldGenerated != null && alien.transform.IsChildOf(oldGenerated)))
            {
                Debug.LogWarning("HalloweenCryptRoomGenerator : l'alien ne doit pas " +
                    "etre l'objet generate ou l'un de ses enfants. " +
                    "Mets-le a la racine de la scene.", this);
                return;
            }
        }

        // Aucune instanciation, aucun SetParent, aucune modification des scripts,
        // composants de degats, colliders ou animations du personnage.
        Transform t = alien.transform;
        Vector3 worldFloor = transform.TransformPoint(alienFloorPosition);
        Vector3 boardCenter = transform.TransformPoint(new Vector3(Cx, 0f, Cz));
        Vector3 forward = boardCenter - worldFloor;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.TransformDirection(Vector3.back);

        t.position = worldFloor;
        t.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up) *
                     Quaternion.Euler(0f, alienYawOffset, 0f);

        Bounds modelBounds;
        bool hasVisualBounds = TryGetAlienVisualBounds(out modelBounds);
        if (autoFitAlienHeight && hasVisualBounds && modelBounds.size.y > 0.001f)
        {
            // Le ratio est recalcule a partir de la hauteur ACTUELLE : une
            // regeneration ne fait pas grossir/retrecir l'alien a chaque fois.
            float factor = alienTargetHeight / modelBounds.size.y;
            if (!float.IsNaN(factor) && !float.IsInfinity(factor) && factor > 0.001f && factor < 1000f)
            {
                t.localScale *= factor;
                hasVisualBounds = TryGetAlienVisualBounds(out modelBounds);
            }
        }

        if (alignAlienFeetToFloor && hasVisualBounds)
            t.position += Vector3.up * (worldFloor.y - modelBounds.min.y);

        if (!alien.activeInHierarchy)
            Debug.LogWarning("HalloweenCryptRoomGenerator : l'alien est inactif " +
                "dans la Hierarchy. Verifie qu'il n'est pas enfant de l'ancien " +
                "CryptEnvironment desactive par le generateur.", alien);

        Debug.Log("Alien repositionne derriere l'echiquier : " + alien.name +
            " (objet existant, composants de degats conserves).", alien);
    }

    private bool TryGetAlienVisualBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        if (alien == null) return false;

        // On exclut les particules, lignes et VFX : leurs grandes bounds
        // fausseraient completement la taille et la pose du personnage.
        Renderer[] renderers = alien.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r == null || !(r is MeshRenderer || r is SkinnedMeshRenderer))
                continue;
            Bounds b = r.bounds;
            if (b.size.sqrMagnitude < 0.000001f) continue;
            if (!found) { bounds = b; found = true; }
            else bounds.Encapsulate(b);
        }
        return found;
    }

    [ContextMenu("Frame Camera For Haunted Room")]
    public void FrameCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject go = GameObject.Find("Main Camera");
            if (go != null) cam = go.GetComponent<Camera>();
        }
        if (cam == null) return;
        cam.transform.position = transform.TransformPoint(cameraPosition);
        cam.transform.rotation = Quaternion.LookRotation(
            transform.TransformPoint(cameraLookAt) - cam.transform.position, Vector3.up);
        cam.fieldOfView = cameraFieldOfView;
    }

    [ContextMenu("Remove Generated Haunted Room")]
    public void RemoveGenerated()
    {
        Transform old = transform.Find(GeneratedName);
        if (old != null)
        {
            old.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }
        generatedRoot = null;
        foreach (Mesh mesh in createdMeshes)
            if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        createdMeshes.Clear();
        foreach (Material material in createdMaterials)
            if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        createdMaterials.Clear();
    }

    private void OnDestroy()
    {
        if (savedCamera && adjustedCamera != null)
        {
            adjustedCamera.clearFlags = oldClearFlags;
            adjustedCamera.backgroundColor = oldBackground;
        }
        foreach (Mesh mesh in createdMeshes) if (mesh != null) Destroy(mesh);
        foreach (Material material in createdMaterials) if (material != null) Destroy(material);
    }

    private void DisableOldRooms()
    {
        string[] names =
        {
            "CryptEnvironment", "GothicArchitecture",
            "GothicCryptArchitecture", "GothicCrypt",
            "HalloweenRoom", "OldHalloweenEnvironment"
        };
        foreach (string n in names)
        {
            GameObject go = GameObject.Find(n);
            if (go != null && go != gameObject && !go.transform.IsChildOf(transform))
                go.SetActive(false);
        }
    }

    private float R(float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private Bucket B(Surface s)
    {
        if (!buckets.TryGetValue(s, out Bucket b))
        {
            b = new Bucket();
            buckets.Add(s, b);
        }
        return b;
    }

    private void Box(Surface s, Vector3 c, Vector3 dimensions)
    {
        B(s).AddBox(c, dimensions, Quaternion.identity);
    }
    private void RotBox(Surface s, Vector3 c, Vector3 dimensions, Quaternion rot)
    {
        B(s).AddBox(c, dimensions, rot);
    }
    private void Sphere(Surface s, Vector3 c, Vector3 radii, int rings = 9, int slices = 14)
    {
        B(s).AddEllipsoid(c, radii, rings, slices);
    }
    private void Cylinder(Surface s, Vector3 c, float radius, float height, int segments = 18)
    {
        B(s).AddCylinder(c, radius, height, segments, Quaternion.identity);
    }
    private void Beam(Surface s, Vector3 from, Vector3 to, float radius, int segments = 9)
    {
        Vector3 d = to - from;
        if (d.sqrMagnitude < 0.000001f) return;
        B(s).AddCylinder((from + to) * .5f, radius, d.magnitude, segments,
            Quaternion.FromToRotation(Vector3.up, d));
    }
    private void Line(Surface s, Vector3 a, Vector3 b, float width)
    {
        Beam(s, a, b, width * .5f, 8);
    }

    private void BuildFloor()
    {
        // Uneven stone slabs built in large merged meshes. They stay below the
        // existing chessboard pedestal. No mesh collider is generated.
        Box(Surface.Basalt, new Vector3(Cx, Floor - 0.18f, Cz + 1.5f),
            new Vector3(30f, .40f, 31f));
        for (int ix = 0; ix < 24; ix++)
            for (int iz = 0; iz < 25; iz++)
            {
                float x = -10.0f + ix * 1.18f;
                float z = -10.8f + iz * 1.18f;
                float lift = R(-.013f, .013f);
                Surface s = ((ix + 2 * iz) % 8 == 0) ? Surface.FloorEdge :
                    ((ix + iz) % 3 == 0) ? Surface.FloorMid : Surface.FloorDark;
                Box(s, new Vector3(x, Floor - .060f + lift, z),
                    new Vector3(1.145f, .14f, 1.145f));
            }
        // Huge engraved stone frame OUTSIDE the 8x8 board, not over the squares.
        for (int k = 0; k < 2; k++)
        {
            float r = 5.55f + k * .52f;
            float y = Floor + .022f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f + Mathf.PI / 8f;
                float b = (i + 1) * Mathf.PI / 4f + Mathf.PI / 8f;
                Line(k == 0 ? Surface.StoneLight : Surface.IronBright,
                    new Vector3(Cx + r * Mathf.Cos(a), y, Cz + r * Mathf.Sin(a)),
                    new Vector3(Cx + r * Mathf.Cos(b), y, Cz + r * Mathf.Sin(b)),
                    k == 0 ? .16f : .035f);
            }
        }
        // Intermittent floor cracks far from the game area.
        for (int i = 0; i < 85; i++)
        {
            float x = R(-9f, 16f), z = R(-8.5f, 16f);
            if (x > -1.5f && x < 8.5f && z > -1.5f && z < 8.5f) continue;
            Vector3 a = new Vector3(x, Floor + .02f, z);
            Vector3 b = a + new Vector3(R(-.4f, .4f), 0, R(-.4f, .4f));
            Line(Surface.Basalt, a, b, R(.017f, .04f));
        }
    }

    private void BuildWalls()
    {
        float cy = Floor + wallHeight * .5f;
        // Three enclosing walls, front side intentionally absent.
        Box(Surface.Wall, new Vector3(Cx, cy, Back),
            new Vector3(30.4f, wallHeight, .48f));
        Box(Surface.Wall, new Vector3(Left, cy, Cz + 2f),
            new Vector3(.48f, wallHeight, 30f));
        Box(Surface.Wall, new Vector3(Right, cy, Cz + 2f),
            new Vector3(.48f, wallHeight, 30f));
        // Multi-level medieval masonry seams, sculptural depth near the floor.
        for (int i = 0; i < 19; i++)
        {
            float y = Floor + .28f + i * .39f;
            if (y > Floor + wallHeight) break;
            Box(i % 3 == 0 ? Surface.StoneLight : Surface.WallAlt,
                new Vector3(Cx, y, StoneFront - .06f),
                new Vector3(30.1f, .037f, .08f));
        }
        for (int j = 0; j < 32; j++)
        {
            float x = -10.5f + j * .95f;
            for (int r = 0; r < 11; r++)
            {
                float y = Floor + .18f + r * .71f;
                if (y > Floor + wallHeight - .10f) continue;
                float xOffset = (r % 2 == 0) ? .20f : -.20f;
                Box(Surface.Basalt, new Vector3(x + xOffset, y, StoneFront - .08f),
                    new Vector3(.035f, .40f, .07f));
            }
        }
        // Perimeter stone cornices and aged floor skirting.
        Box(Surface.Basalt, new Vector3(Cx, Floor + .27f, Back - .31f),
            new Vector3(30.1f, .58f, .26f));
        Box(Surface.StoneLight, new Vector3(Cx, Floor + .62f, Back - .32f),
            new Vector3(30.1f, .09f, .28f));
        Box(Surface.StoneLight,
            new Vector3(Cx, Floor + wallHeight - .22f, Back - .30f),
            new Vector3(30.1f, .24f, .32f));
        foreach (float x in new float[] { Left + .28f, Right - .28f })
        {
            Box(Surface.Basalt, new Vector3(x, Floor + .23f, Cz + 2f),
                new Vector3(.35f, .48f, 29.7f));
            Box(Surface.StoneLight, new Vector3(x, Floor + wallHeight - .22f, Cz + 2f),
                new Vector3(.37f, .22f, 29.7f));
        }
    }

    private void BuildButtresses()
    {
        // Monumental pillars break the monotonous rectangular wall surface.
        float[] xs = { -8.8f, -5.0f, -1.05f, 8.05f, 12f, 15.5f };
        foreach (float x in xs)
        {
            float z = Back - .66f;
            Box(Surface.Basalt, new Vector3(x, Floor + 2.4f, z),
                new Vector3(.76f, 5.0f, .9f));
            for (int k = 0; k < 3; k++)
            {
                float y = Floor + .24f + k * 2.25f;
                Box(Surface.StoneLight, new Vector3(x, y, z - .09f),
                    new Vector3(1.07f, .24f, 1.18f));
                Cylinder(Surface.IronBright, new Vector3(x, y + .15f, z - .50f),
                    .10f, .12f, 12);
            }
            // Decorative spikes are only at the remote wall, not around the board.
            for (float sx = -1; sx <= 1; sx += 2)
                Beam(Surface.StoneLight, new Vector3(x + sx * .25f, Floor + 4.9f, z),
                    new Vector3(x + sx * .05f, Floor + 6.8f, z + .17f), .095f, 10);
        }
        // Side wall flying ribs and heavy pilasters.
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 6; i++)
            {
                float x = side < 0 ? Left + .68f : Right - .68f;
                float z = -6f + i * 4.15f;
                Box(Surface.Basalt, new Vector3(x, Floor + 2.8f, z),
                    new Vector3(.92f, 6.0f, .95f));
                Box(Surface.StoneLight, new Vector3(x, Floor + 5.65f, z),
                    new Vector3(1.14f, .21f, 1.15f));
            }
    }

    private void BuildGothicArches()
    {
        // Arched stone recesses. Layering dark portals in front of the back wall
        // makes the voids read as depth without requiring expensive boolean meshes.
        BuildBackArch(Cx, 5.5f, 5.1f, StoneFront - .45f, true);
        BuildBackArch(-5.9f, 3.05f, 5.4f, StoneFront - .43f, false);
        BuildBackArch(12.9f, 3.05f, 5.4f, StoneFront - .43f, false);
        foreach (float x in new float[] { -1.8f, 8.8f })
            BuildBackArch(x, 1.7f, 4.5f, StoneFront - .39f, false);
    }

    private void BuildBackArch(float cx, float width, float height, float z, bool main)
    {
        float bottom = Floor + .35f;
        float shoulder = bottom + height * .70f;
        float peak = bottom + height;
        float half = width * .5f;

        Box(Surface.VioletDeep, new Vector3(cx, bottom + (height * .45f), z + .07f),
            new Vector3(width - .18f, height * .82f, .07f));
        // Columns on both sides with ornate capitals.
        foreach (float sx in new float[] { -1f, 1f })
        {
            float x = cx + sx * half;
            Cylinder(Surface.StoneLight, new Vector3(x, (bottom + shoulder) / 2f, z - .03f),
                main ? .23f : .15f, shoulder - bottom, 14);
            Box(Surface.IronBright, new Vector3(x, bottom + .08f, z - .03f),
                new Vector3(main ? .7f : .48f, .16f, main ? .7f : .48f));
            Box(Surface.StoneLight, new Vector3(x, shoulder, z - .03f),
                new Vector3(main ? .69f : .47f, .20f, main ? .69f : .47f));
            Vector3 spring = new Vector3(x, shoulder, z - .04f);
            Vector3 tip = new Vector3(cx, peak, z - .04f);
            // Segmented ogival arch, carved as continuous rib with outer trim.
            for (int i = 0; i < 11; i++)
            {
                float t0 = i / 11f, t1 = (i + 1f) / 11f;
                Vector3 a = Vector3.Lerp(spring, tip, t0);
                Vector3 b = Vector3.Lerp(spring, tip, t1);
                a.y += Mathf.Sin(t0 * Mathf.PI) * height * .06f;
                b.y += Mathf.Sin(t1 * Mathf.PI) * height * .06f;
                Beam(Surface.StoneLight, a, b, main ? .21f : .13f, 10);
                Beam(Surface.Basalt, a + Vector3.back * .19f,
                    b + Vector3.back * .19f, main ? .06f : .04f, 8);
            }
        }
        // Angular keystone at the pointed apex.
        Sphere(Surface.StoneLight, new Vector3(cx, peak, z - .07f),
            new Vector3(main ? .27f : .17f, .35f, .17f), 10, 14);
        if (!main)
        {
            // Violet-black stain-glass pane near the pointed arch.
            Box(Surface.VioletGlass, new Vector3(cx, bottom + height * .55f, z - .11f),
                new Vector3(width * .56f, height * .44f, .02f));
        }
    }

    private void BuildRoseWindow()
    {
        // High circular occult eye over the central arch.
        Vector3 c = new Vector3(Cx, Floor + 6.55f, StoneFront - .82f);
        // It is deliberately only a relief, never another real-time light.
        for (int ring = 0; ring < 3; ring++)
        {
            float r = 1.00f + ring * .19f;
            for (int i = 0; i < 40; i++)
            {
                float a = i * Mathf.PI * 2f / 40f;
                float b = (i + 1) * Mathf.PI * 2f / 40f;
                Line(ring == 2 ? Surface.StoneLight : Surface.IronBright,
                    c + new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), 0),
                    c + new Vector3(r * Mathf.Cos(b), r * Mathf.Sin(b), 0),
                    ring == 2 ? .12f : .045f);
            }
        }
        Sphere(Surface.VioletRune, c + Vector3.back * .04f,
            new Vector3(.19f, .20f, .05f), 11, 16);
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI / 6f;
            Vector3 p = c + new Vector3(.93f * Mathf.Cos(a), .93f * Mathf.Sin(a), 0);
            Line(Surface.IronBright, c, p, .06f);
            Sphere(i % 2 == 0 ? Surface.VioletRune : Surface.Ember,
                p + Vector3.back * .08f, new Vector3(.08f, .09f, .06f), 7, 10);
        }
    }

    private void BuildSideWindows()
    {
        // Tall stained-glass windows on each lateral wall.
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 4; i++)
            {
                float x = side < 0 ? Left + .29f : Right - .29f;
                float z = -3.6f + 4.9f * i;
                float y0 = Floor + 1.75f;
                float top = Floor + 5.85f;
                Box(Surface.VioletDeep, new Vector3(x, (y0 + top) * .5f, z),
                    new Vector3(.08f, top - y0, 2.5f));
                Box(Surface.VioletGlass, new Vector3(x + (side < 0 ? .07f : -.07f),
                    y0 + 1.85f, z), new Vector3(.03f, 3.10f, 1.94f));
            foreach (float d in new float[] { -1.12f, 1.12f })
                    Beam(Surface.StoneLight, new Vector3(x, y0, z + d),
                        new Vector3(x, top, z + d), .105f, 10);
                for (float h = 0f; h < 4; h++)
                    Line(Surface.BlackIron,
                        new Vector3(x + (side < 0 ? .10f : -.10f), y0 + .20f + h * .89f, z - .94f),
                        new Vector3(x + (side < 0 ? .10f : -.10f), y0 + .20f + h * .89f, z + .94f), .055f);
                // pointed silhouette framing the window
                Beam(Surface.StoneLight, new Vector3(x, top, z - 1.12f),
                    new Vector3(x, top + .75f, z), .11f, 10);
                Beam(Surface.StoneLight, new Vector3(x, top + .75f, z),
                    new Vector3(x, top, z + 1.12f), .11f, 10);
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI * 2f / 8f;
                    Sphere(Surface.VioletRune,
                        new Vector3(x + (side < 0 ? .13f : -.13f), y0 + 1.8f + Mathf.Cos(a) * .64f,
                            z + Mathf.Sin(a) * .64f), new Vector3(.038f, .065f, .035f), 7, 9);
                }
            }
    }

    private void BuildCentralAltar()
    {
        // Rear altar behind the board, away from the playing area.
        float x = Cx, z = 12.15f;
        Box(Surface.Basalt, new Vector3(x, Floor + .18f, z),
            new Vector3(4.9f, .35f, 2.3f));
        Box(Surface.FloorEdge, new Vector3(x, Floor + .47f, z + .3f),
            new Vector3(4.1f, .22f, 1.5f));
        Box(Surface.Basalt, new Vector3(x, Floor + .78f, z + .45f),
            new Vector3(3.2f, .4f, 1.05f));
        Box(Surface.IronBright, new Vector3(x, Floor + .99f, z + .45f),
            new Vector3(3.25f, .04f, 1.15f));
        // Stone monolith and skull reliquary, behind the chessboard.
        Box(Surface.BlackIron, new Vector3(x, Floor + 1.85f, z + .64f),
            new Vector3(.86f, 1.90f, .57f));
        Box(Surface.IronBright, new Vector3(x, Floor + 2.88f, z + .64f),
            new Vector3(.97f, .13f, .68f));
        BuildSkull(new Vector3(x, Floor + 2.24f, z + .25f), .59f);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
            float sx = x + Mathf.Cos(a) * 1.3f;
            float sz = z + .46f + Mathf.Sin(a) * .44f;
            Cylinder(Surface.IronBright, new Vector3(sx, Floor + 1.1f, sz),
                .13f, .28f, 14);
            Sphere(i % 2 == 0 ? Surface.VioletRune : Surface.Ember,
                new Vector3(sx, Floor + 1.35f, sz), new Vector3(.10f, .23f, .10f), 10, 13);
        }
    }

    private void BuildStatuesAndNiches()
    {
        // Torso-sized cursed effigies in two back-wall alcoves.
        BuildDeathStatue(-6.0f, StoneFront - .95f);
        BuildDeathStatue(13.0f, StoneFront - .95f);
    }

    private void BuildDeathStatue(float x, float z)
    {
        float y = Floor + .82f;
        Box(Surface.Basalt, new Vector3(x, Floor + .34f, z),
            new Vector3(1.5f, .68f, 1.20f));
        Box(Surface.StoneLight, new Vector3(x, Floor + .71f, z),
            new Vector3(1.28f, .11f, 1.02f));
        Sphere(Surface.StoneLight, new Vector3(x, y + .90f, z),
            new Vector3(.37f, .75f, .27f), 11, 16);
        Sphere(Surface.Bone, new Vector3(x, y + 1.87f, z - .05f),
            new Vector3(.24f, .27f, .23f), 12, 18);
        foreach (float sx in new float[] { -1f, 1f })
        {
            Sphere(Surface.BlackIron,
                new Vector3(x + sx * .095f, y + 1.93f, z - .24f),
                new Vector3(.075f, .068f, .025f), 7, 10);
            Beam(Surface.Bone,
                new Vector3(x + sx * .22f, y + 1.46f, z - .05f),
                new Vector3(x + sx * .31f, y + .40f, z - .22f), .07f, 10);
            Beam(Surface.BlackIron,
                new Vector3(x + sx * .10f, y + 2.05f, z),
                new Vector3(x + sx * .30f, y + 2.50f, z + .06f), .08f, 10);
        }
        Beam(Surface.IronBright,
            new Vector3(x, y + 1.48f, z - .15f),
            new Vector3(x, y + .28f, z - .15f), .06f, 9);
    }

    private void BuildTombs()
    {
        // Stone coffins and plinths confined to lateral aisles, outside game space.
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 4; i++)
            {
                float x = side < 0 ? -6.9f : 13.9f;
                float z = -3f + i * 4.6f;
                Box(Surface.Basalt, new Vector3(x, Floor + .16f, z),
                    new Vector3(2.5f, .35f, 1.4f));
                Box(Surface.StoneLight, new Vector3(x, Floor + .38f, z),
                    new Vector3(2.30f, .14f, 1.25f));
                Box(Surface.WallAlt, new Vector3(x, Floor + .54f, z),
                    new Vector3(2.08f, .20f, 1.03f));
                Line(Surface.BlackIron,
                    new Vector3(x - .6f, Floor + .66f, z),
                    new Vector3(x + .6f, Floor + .66f, z), .07f);
                Line(Surface.BlackIron,
                    new Vector3(x, Floor + .66f, z - .42f),
                    new Vector3(x, Floor + .66f, z + .42f), .07f);
                foreach (float sx in new float[] { -1f, 1f })
                    Sphere(Surface.Bone, new Vector3(x + sx * .75f, Floor + .75f, z),
                        new Vector3(.08f, .095f, .085f), 8, 11);
            }
    }

    private void BuildSkullsAndChains()
    {
        // Hanging chains and gothic skull ornaments, concentrated at back wall.
        foreach (float x in new float[] { -7.9f, -1.5f, 8.5f, 14.9f })
        {
            Vector3 top = new Vector3(x, Floor + 7.3f, Back - 1.25f);
            Vector3 prev = top;
            for (int j = 1; j <= 15; j++)
            {
                Vector3 cur = top + new Vector3(.06f * Mathf.Sin(j * .43f),
                    -j * .27f, .03f * Mathf.Cos(j * .39f));
                Line(Surface.BlackIron, prev, cur, .055f);
                Sphere(Surface.IronBright, cur, new Vector3(.047f, .065f, .035f), 6, 8);
                prev = cur;
            }
            BuildSkull(prev + new Vector3(0, -.18f, 0), .33f);
        }
    }

    private void BuildSkull(Vector3 c, float scale)
    {
        Sphere(Surface.Bone, c, new Vector3(scale * .62f, scale * .70f, scale * .51f), 12, 18);
        Sphere(Surface.Bone, c + new Vector3(0, -scale * .45f, -scale * .21f),
            new Vector3(scale * .48f, scale * .30f, scale * .37f), 8, 12);
        for (int side = -1; side <= 1; side += 2)
        {
            Sphere(Surface.BlackIron, c + new Vector3(side * scale * .25f,
                scale * .12f, -scale * .42f),
                new Vector3(scale * .19f, scale * .19f, scale * .075f), 8, 11);
            Sphere(Surface.VioletRune, c + new Vector3(side * scale * .26f,
                scale * .12f, -scale * .49f),
                new Vector3(scale * .055f, scale * .056f, scale * .025f), 7, 9);
            Beam(Surface.Bone,
                c + new Vector3(side * scale * .36f, scale * .46f, scale * .02f),
                c + new Vector3(side * scale * .85f, scale * 1.30f, scale * .28f),
                scale * .13f, 9);
        }
        for (int i = -2; i <= 2; i++)
        {
            Box(Surface.Bone, c + new Vector3(i * scale * .15f,
                -scale * .54f, -scale * .53f),
                new Vector3(scale * .09f, scale * .17f, scale * .06f));
        }
    }

    private void BuildCeilingRibs()
    {
        // Pointed tunnel vaults. Only top and sides; the camera's forward view
        // remains clear because there is no ceiling covering the near half.
        for (int i = 0; i < 6; i++)
        {
            float z = 7.5f + i * 1.75f;
            Vector3 apex = new Vector3(Cx, Floor + wallHeight + 2.8f, z);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 basePt = new Vector3(Cx + side * 13.2f,
                    Floor + wallHeight - 1.0f, z);
                Vector3 mid = new Vector3(Cx + side * 7.6f,
                    Floor + wallHeight + 1.4f, z);
                Beam(Surface.WallAlt, basePt, mid, .21f, 9);
                Beam(Surface.StoneLight, mid, apex, .18f, 9);
            }
        }
    }

    private void BuildOccultAccents()
    {
        // Decorative reliefs on the altar wall, not board-specific effects.
        for (int j = 0; j < 10; j++)
        {
            float x = -8.9f + j * 2.66f;
            for (int i = 0; i < 3; i++)
            {
                float y = Floor + 1.65f + i * 1.17f;
                RotBox(Surface.BlackIron,
                    new Vector3(x, y, StoneFront - .20f),
                    new Vector3(.13f, .13f, .07f), Quaternion.Euler(0, 0, 45f));
                if (j % 2 == 0)
                    Sphere(Surface.VioletRune,
                        new Vector3(x, y, StoneFront - .26f),
                        new Vector3(.048f, .06f, .037f), 6, 9);
            }
        }
    }

    private void BuildAlienRitualCircle()
    {
        // Un pentacle discret dessine SUR le sol, derriere l'echiquier.
        // Pas de collider, pas de Light, uniquement des meshes emissifs.
        // Son centre suit le champ Alien Floor Position de l'Inspector.
        float x = alienFloorPosition.x;
        float z = alienFloorPosition.z;
        float y = Floor + .040f;
        float r = alienRitualRadius;
        const int sides = 24;
        for (int ring = 0; ring < 2; ring++)
        {
            float radius = r - ring * .22f;
            for (int i = 0; i < sides; i++)
            {
                float a = 2f * Mathf.PI * i / sides;
                float b = 2f * Mathf.PI * (i + 1) / sides;
                Vector3 p = new Vector3(x + Mathf.Sin(a) * radius, y,
                    z + Mathf.Cos(a) * radius);
                Vector3 q = new Vector3(x + Mathf.Sin(b) * radius, y,
                    z + Mathf.Cos(b) * radius);
                Line(ring == 0 ? Surface.IronBright : Surface.VioletRune,
                    p, q, ring == 0 ? .055f : .028f);
            }
        }
        for (int i = 0; i < 5; i++)
        {
            float a = 2f * Mathf.PI * i / 5f - Mathf.PI * .5f;
            float b = 2f * Mathf.PI * ((i + 2) % 5) / 5f - Mathf.PI * .5f;
            Vector3 p = new Vector3(x + Mathf.Cos(a) * r * .80f, y,
                z + Mathf.Sin(a) * r * .80f);
            Vector3 q = new Vector3(x + Mathf.Cos(b) * r * .80f, y,
                z + Mathf.Sin(b) * r * .80f);
            Line(Surface.VioletRune, p, q, .033f);
            Sphere(Surface.Ember, p + Vector3.up * .015f,
                new Vector3(.065f, .025f, .065f), 6, 9);
        }
    }

    private Material CreateMaterial(Surface s)
    {
        Color color = Hex("#353440");
        float metallic = 0, smooth = .1f, emission = 0;
        switch (s)
        {
            case Surface.FloorDark: color = Hex("#24252D"); break;
            case Surface.FloorMid: color = Hex("#2D2C35"); break;
            case Surface.FloorEdge: color = Hex("#393942"); break;
            case Surface.Wall: color = Hex("#24212E"); break;
            case Surface.WallAlt: color = Hex("#353441"); break;
            case Surface.StoneLight: color = Hex("#51505B"); break;
            case Surface.Basalt: color = Hex("#181821"); break;
            case Surface.BlackIron: color = Hex("#13131B"); metallic = .5f; smooth = .35f; break;
            case Surface.IronBright: color = Hex("#59515F"); metallic = .62f; smooth = .48f; break;
            case Surface.Bone: color = Hex("#C5BDAB"); break;
            case Surface.Rust: color = Hex("#533A35"); break;
            case Surface.VioletGlass: color = Hex("#402455"); emission = glowStainedGlass ? .60f : 0; break;
            case Surface.VioletRune: color = Hex("#9353D8"); emission = 1.70f; break;
            case Surface.VioletDeep: color = Hex("#100B1A"); break;
            case Surface.Ember: color = Hex("#E98144"); emission = 1.0f; break;
            case Surface.Moss: color = Hex("#273B33"); break;
            case Surface.Velvet: color = Hex("#322235"); break;
        }
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError("HalloweenCryptRoomGenerator: no Lit shader found.");
            return null;
        }
        Material m = new Material(shader);
        m.name = "HauntedCrypt_" + s;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (emission > 0 && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * emission);
        }
        createdMaterials.Add(m);
        return m;
    }

    private static Color Hex(string code)
    {
        ColorUtility.TryParseHtmlString(code, out Color c);
        return c;
    }

    private void FlushMeshes()
    {
        int triangles = 0;
        foreach (KeyValuePair<Surface, Bucket> entry in buckets)
        {
            Bucket b = entry.Value;
            if (b.Vertices.Count == 0) continue;
            GameObject obj = new GameObject("Mesh_" + entry.Key);
            obj.transform.SetParent(generatedRoot, false);
            Mesh mesh = new Mesh();
            mesh.name = "Halloween_Crypt_" + entry.Key;
            if (b.Vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(b.Vertices);
            mesh.SetUVs(0, b.UVs);
            mesh.SetTriangles(b.Tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            createdMeshes.Add(mesh);
            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateMaterial(entry.Key);
            renderer.shadowCastingMode =
                (entry.Key == Surface.VioletRune || entry.Key == Surface.Ember ||
                 entry.Key == Surface.VioletGlass) ? ShadowCastingMode.Off : ShadowCastingMode.On;
            renderer.receiveShadows = true;
            triangles += b.Tris.Count / 3;
        }
        Debug.Log("Haunted cathedral ready: " + buckets.Count +
            " merged material groups, " + triangles +
            " triangles, ZERO new lights and ZERO new colliders.", this);
    }

    private sealed class Bucket
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector2> UVs = new List<Vector2>();
        public readonly List<int> Tris = new List<int>();

        private void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int n = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
            UVs.Add(Vector2.zero); UVs.Add(Vector2.right); UVs.Add(Vector2.up);
            Tris.Add(n); Tris.Add(n + 1); Tris.Add(n + 2);
        }
        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int n = Vertices.Count;
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
            UVs.Add(Vector2.zero); UVs.Add(Vector2.right);
            UVs.Add(Vector2.one); UVs.Add(Vector2.up);
            // Double-sided triangle pairs so the ceiling and thin details remain visible.
            Tris.Add(n); Tris.Add(n + 1); Tris.Add(n + 2);
            Tris.Add(n); Tris.Add(n + 2); Tris.Add(n + 3);
        }
        public void AddBox(Vector3 center, Vector3 size, Quaternion rot)
        {
            Vector3 h = size * .5f;
            Vector3[] p = {
                new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),
                new Vector3(h.x,h.y,-h.z), new Vector3(-h.x,h.y,-h.z),
                new Vector3(-h.x,-h.y,h.z),new Vector3(h.x,-h.y,h.z),
                new Vector3(h.x,h.y,h.z), new Vector3(-h.x,h.y,h.z)
            };
            for (int i = 0; i < p.Length; i++) p[i] = center + rot * p[i];
            Quad(p[0], p[3], p[2], p[1]);
            Quad(p[4], p[5], p[6], p[7]);
            Quad(p[0], p[4], p[7], p[3]);
            Quad(p[1], p[2], p[6], p[5]);
            Quad(p[3], p[7], p[6], p[2]);
            Quad(p[0], p[1], p[5], p[4]);
        }
        public void AddCylinder(Vector3 center, float r, float h, int sides, Quaternion rot)
        {
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float b = (i + 1f) * Mathf.PI * 2f / sides;
                Vector3 p0 = new Vector3(Mathf.Cos(a) * r, -h * .5f, Mathf.Sin(a) * r);
                Vector3 p1 = new Vector3(Mathf.Cos(a) * r, h * .5f, Mathf.Sin(a) * r);
                Vector3 p2 = new Vector3(Mathf.Cos(b) * r, h * .5f, Mathf.Sin(b) * r);
                Vector3 p3 = new Vector3(Mathf.Cos(b) * r, -h * .5f, Mathf.Sin(b) * r);
                Quad(center + rot * p0, center + rot * p1, center + rot * p2, center + rot * p3);
                Vector3 top = center + rot * new Vector3(0, h * .5f, 0);
                Vector3 bot = center + rot * new Vector3(0, -h * .5f, 0);
                Tri(top, center + rot * p2, center + rot * p1);
                Tri(bot, center + rot * p0, center + rot * p3);
            }
        }
        public void AddEllipsoid(Vector3 c, Vector3 r, int rings, int slices)
        {
            for (int i = 0; i < rings; i++)
            {
                float a = i * Mathf.PI / rings, b = (i + 1f) * Mathf.PI / rings;
                for (int j = 0; j < slices; j++)
                {
                    float u = j * Mathf.PI * 2f / slices, v = (j + 1f) * Mathf.PI * 2f / slices;
                    Vector3 p0 = P(c, r, a, u), p1 = P(c, r, a, v), p2 = P(c, r, b, v), p3 = P(c, r, b, u);
                    if (i == 0) Tri(p0, p2, p3);
                    else if (i == rings - 1) Tri(p0, p1, p2);
                    else Quad(p0, p1, p2, p3);
                }
            }
        }
        private static Vector3 P(Vector3 c, Vector3 r, float theta, float phi)
        {
            return c + new Vector3(r.x * Mathf.Sin(theta) * Mathf.Cos(phi),
                r.y * Mathf.Cos(theta), r.z * Mathf.Sin(theta) * Mathf.Sin(phi));
        }
    }
}
