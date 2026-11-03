using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Place les modèles 3D des pièces sur l'échiquier au lancement,
// comme BoardGenerator le fait pour les cases.
// Chaque modèle est automatiquement mis à l'échelle, centré sur sa case
// et posé sur la surface du plateau, quelle que soit son échelle d'origine.
public class ChessPieceSpawner : MonoBehaviour
{
    [System.Serializable]
    public class PieceModelSet
    {
        public GameObject pawn;
        public GameObject knight;
        public GameObject bishop;
        public GameObject rook;
        public GameObject queen;
        public GameObject king;

        public GameObject Get(PieceType type)
        {
            switch (type)
            {
                case PieceType.Pawn: return pawn;
                case PieceType.Knight: return knight;
                case PieceType.Bishop: return bishop;
                case PieceType.Rook: return rook;
                case PieceType.Queen: return queen;
                case PieceType.King: return king;
                default: return null;
            }
        }
    }

    [Header("Echiquier")]
    [SerializeField] private int boardSize = 8;
    [SerializeField] private float tileSize = 1f;

    [Tooltip("Hauteur (locale) de la surface des cases.")]
    [SerializeField] private float boardTopY = 0.1f;

    [Header("Modèles (.blend)")]
    [SerializeField] private PieceModelSet whiteModels = new PieceModelSet();
    [SerializeField] private PieceModelSet blackModels = new PieceModelSet();

    [Header("Matériaux (optionnel)")]
    [Tooltip("Si assigné, remplace les matériaux importés des pièces blanches.")]
    [SerializeField] private Material whiteMaterialOverride;

    [Tooltip("Si assigné, remplace les matériaux importés des pièces noires.")]
    [SerializeField] private Material blackMaterialOverride;

    [Header("Taille des pièces (en cases)")]
    [SerializeField, Range(0.2f, 2f)] private float pawnHeight = 0.55f;
    [SerializeField, Range(0.2f, 2f)] private float knightHeight = 0.70f;
    [SerializeField, Range(0.2f, 2f)] private float bishopHeight = 0.80f;
    [SerializeField, Range(0.2f, 2f)] private float rookHeight = 0.65f;
    [SerializeField, Range(0.2f, 2f)] private float queenHeight = 0.95f;
    [SerializeField, Range(0.2f, 2f)] private float kingHeight = 1.05f;

    [Tooltip("Largeur maximale d'une pièce, en fraction de case.")]
    [SerializeField, Range(0.2f, 1f)] private float maxFootprint = 0.75f;

    [Header("Orientation")]
    [Tooltip("Tourne les pièces noires de 180° pour qu'elles fassent face aux blanches.")]
    [SerializeField] private bool rotateBlackPieces = true;

    [Tooltip("Rotation supplémentaire (en degrés, autour de Y) par type de pièce, " +
        "pour corriger un modèle exporté dans le mauvais sens.")]
    [SerializeField] private float pawnRotationY = 0f;
    [SerializeField] private float knightRotationY = 180f;
    [SerializeField] private float bishopRotationY = 0f;
    [SerializeField] private float rookRotationY = 0f;
    [SerializeField] private float queenRotationY = 0f;
    [SerializeField] private float kingRotationY = 0f;
    private readonly HashSet<string> reportedMissingModels = new HashSet<string>();
    private Material whitePlaceholderMaterial;
    private Material blackPlaceholderMaterial;

    private static readonly PieceType[] BackRank =
    {
        PieceType.Rook,
        PieceType.Knight,
        PieceType.Bishop,
        PieceType.Queen,
        PieceType.King,
        PieceType.Bishop,
        PieceType.Knight,
        PieceType.Rook
    };

    public int BoardSize => boardSize;
    public float TileSize => tileSize;
    public float BoardTopY => boardTopY;

    public Vector3 SquareToLocal(int x, int z)
    {
        return new Vector3(x * tileSize, boardTopY, z * tileSize);
    }

    // Crée toutes les pièces en position de départ.
    // Les modèles absents sont remplacés par une forme provisoire.
    public List<ChessPiece> SpawnStartingPosition()
    {
#if UNITY_EDITOR
        AutoAssignModels();
#endif

        List<ChessPiece> pieces = new List<ChessPiece>();

        for (int x = 0; x < boardSize && x < BackRank.Length; x++)
        {
            AddIfNotNull(pieces, SpawnPiece(BackRank[x], true, x, 0));
            AddIfNotNull(pieces, SpawnPiece(PieceType.Pawn, true, x, 1));

            AddIfNotNull(pieces, SpawnPiece(PieceType.Pawn, false, x, boardSize - 2));
            AddIfNotNull(pieces, SpawnPiece(BackRank[x], false, x, boardSize - 1));
        }

        return pieces;
    }

    public ChessPiece SpawnPiece(PieceType type, bool isWhite, int x, int z)
    {
        GameObject model = (isWhite ? whiteModels : blackModels).Get(type);

        string color = isWhite ? "Blanc" : "Noir";
        char file = (char)('a' + x);

        GameObject root = new GameObject($"{type}_{color}_{file}{z + 1}");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = SquareToLocal(x, z);

        GameObject instance;
        if (model != null)
        {
            instance = Instantiate(model, root.transform);
            instance.name = model.name;
            instance.transform.localPosition = model.transform.localPosition;
            instance.transform.localRotation = model.transform.localRotation;
            instance.transform.localScale = model.transform.localScale;
        }
        else
        {
            instance = CreatePlaceholder(root.transform, type, isWhite);
            if (reportedMissingModels.Add($"{type}_{color}"))
                Debug.LogWarning($"Modèle {type} {color} absent : forme provisoire utilisée. " +
                    "Assigne son modèle dans ChessPieceSpawner pour la remplacer.", this);
        }

        RemoveImportedExtras(instance);
        ApplyMaterialOverride(instance, isWhite ? whiteMaterialOverride : blackMaterialOverride);

        FitToSquare(root.transform, instance.transform, GetHeight(type));

        float yaw = GetRotationY(type);

        if (rotateBlackPieces && !isWhite)
            yaw += 180f;

        root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        ChessPiece piece = root.AddComponent<ChessPiece>();
        piece.Initialize(type, isWhite, x, z);

        return piece;
    }

    private GameObject CreatePlaceholder(Transform parent, PieceType type, bool isWhite)
    {
        Material material = isWhite ? whitePlaceholderMaterial : blackPlaceholderMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader) { name = isWhite ? "Provisoire_Blanc" : "Provisoire_Noir" };
            material.color = isWhite ? new Color(0.85f, 0.8f, 0.65f) : new Color(0.22f, 0.12f, 0.3f);
            if (isWhite) whitePlaceholderMaterial = material;
            else blackPlaceholderMaterial = material;
        }
        GameObject model = new GameObject($"{type}_Provisoire");
        model.transform.SetParent(parent, false);
        AddPlaceholderPart(model.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f),
            new Vector3(0.6f, 0.08f, 0.6f), material);
        AddPlaceholderPart(model.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.33f, 0f),
            new Vector3(0.25f, 0.2f, 0.25f), material);
        if (type == PieceType.Knight)
        {
            // Silhouette simple de tête de cheval, en attendant le vrai modèle.
            AddPlaceholderPart(model.transform, PrimitiveType.Cube, new Vector3(0f, 0.62f, 0.08f),
                new Vector3(0.25f, 0.3f, 0.34f), material);
            AddPlaceholderPart(model.transform, PrimitiveType.Cube, new Vector3(0f, 0.56f, 0.28f),
                new Vector3(0.25f, 0.15f, 0.22f), material);
            AddPlaceholderPart(model.transform, PrimitiveType.Cube, new Vector3(0f, 0.82f, 0f),
                new Vector3(0.12f, 0.15f, 0.1f), material);
        }
        else
        {
            AddPlaceholderPart(model.transform, PrimitiveType.Sphere, new Vector3(0f, 0.62f, 0f),
                new Vector3(0.35f, 0.35f, 0.35f), material);
            var label = new GameObject("Type");
            label.transform.SetParent(model.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.88f, 0f);
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = type.ToString();
            text.anchor = TextAnchor.MiddleCenter;
            text.characterSize = 0.06f;
            text.fontSize = 40;
        }
        return model;
    }

    private static void AddPlaceholderPart(Transform parent, PrimitiveType type,
        Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    private void OnDestroy()
    {
        if (whitePlaceholderMaterial != null) Destroy(whitePlaceholderMaterial);
        if (blackPlaceholderMaterial != null) Destroy(blackPlaceholderMaterial);
    }

    // Prépare une vue complète avant de remplacer celle de la partie en cours.
    public List<ChessPiece> SpawnPosition(PositionEchecs position)
    {
#if UNITY_EDITOR
        AutoAssignModels();
#endif
        var pieces = new List<ChessPiece>();
        for (int x = 0; x < 8; x++)
            for (int z = 0; z < 8; z++)
            {
                PieceEtat etat = position.LirePiece(x, z);
                if (etat == null) continue;
                ChessPiece piece = SpawnPiece(etat.Type, etat.IsWhite, x, z);
                if (piece == null)
                {
                    foreach (ChessPiece created in pieces)
                    {
                        created.gameObject.SetActive(false);
                        Destroy(created.gameObject);
                    }
                    return null;
                }
                piece.HasMoved = etat.HasMoved;
                pieces.Add(piece);
            }
        return pieces;
    }

    private float GetHeight(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn: return pawnHeight;
            case PieceType.Knight: return knightHeight;
            case PieceType.Bishop: return bishopHeight;
            case PieceType.Rook: return rookHeight;
            case PieceType.Queen: return queenHeight;
            case PieceType.King: return kingHeight;
            default: return 1f;
        }
    }

    private float GetRotationY(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn: return pawnRotationY;
            case PieceType.Knight: return knightRotationY;
            case PieceType.Bishop: return bishopRotationY;
            case PieceType.Rook: return rookRotationY;
            case PieceType.Queen: return queenRotationY;
            case PieceType.King: return kingRotationY;
            default: return 0f;
        }
    }

    // Les fichiers .blend peuvent contenir la caméra et la lumière
    // de la scène Blender : on les retire pour ne pas perturber la scène.
    private static void RemoveImportedExtras(GameObject instance)
    {
        foreach (Camera cam in instance.GetComponentsInChildren<Camera>(true))
            Destroy(cam.gameObject);

        foreach (Light light in instance.GetComponentsInChildren<Light>(true))
            Destroy(light.gameObject);

        foreach (AudioListener listener in instance.GetComponentsInChildren<AudioListener>(true))
            Destroy(listener);

        foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
            Destroy(c);
    }

    private static void ApplyMaterialOverride(GameObject instance, Material material)
    {
        if (material == null)
            return;

        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = new Material[r.sharedMaterials.Length];

            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;

            r.sharedMaterials = materials;
        }
    }

    // Met le modèle à la bonne taille, le centre sur la case,
    // pose sa base sur le plateau et ajoute un collider cliquable.
    private void FitToSquare(Transform root, Transform model, float heightInTiles)
    {
        if (!TryGetRendererBounds(model, out Bounds bounds))
            return;

        Vector3 rootScale = root.lossyScale;
        float height = bounds.size.y / Mathf.Max(rootScale.y, 0.0001f);
        float footprint = Mathf.Max(bounds.size.x, bounds.size.z) /
            Mathf.Max(rootScale.x, 0.0001f);

        if (height <= 0.0001f)
            return;

        float scale = heightInTiles * tileSize / height;

        if (footprint * scale > maxFootprint * tileSize)
            scale = maxFootprint * tileSize / footprint;

        model.localScale *= scale;

        TryGetRendererBounds(model, out bounds);

        Vector3 anchor = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        model.position += root.position - anchor;

        TryGetRendererBounds(model, out bounds);

        BoxCollider box = root.gameObject.AddComponent<BoxCollider>();
        box.center = root.InverseTransformPoint(bounds.center);
        box.size = new Vector3(
            bounds.size.x / rootScale.x,
            bounds.size.y / rootScale.y,
            bounds.size.z / rootScale.z
        );
    }

    private static bool TryGetRendererBounds(Transform target, out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))
                continue;

            if (!found)
            {
                bounds = r.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return found;
    }

    private static void AddIfNotNull(List<ChessPiece> list, ChessPiece piece)
    {
        if (piece != null)
            list.Add(piece);
    }

#if UNITY_EDITOR
    // Remplit automatiquement les champs vides avec les fichiers du projet
    // (pion_blanc, fou_noir, ...). Quand tu ajouteras cavalier_blanc / cavalier_noir,
    // ils seront détectés tout seuls.
    private void OnValidate()
    {
        if (!Application.isPlaying && AutoAssignModels())
            EditorUtility.SetDirty(this);
    }

    private bool AutoAssignModels()
    {
        bool changed = false;

        changed |= Assign(ref whiteModels.pawn, "pion_blanc", "pion_blanche");
        changed |= Assign(ref whiteModels.knight, "cavalier_blanc", "cavalier_blanche");
        changed |= Assign(ref whiteModels.bishop, "fou_blanc", "fou_blanche");
        changed |= Assign(ref whiteModels.rook, "tour_blanche", "tour_blanc");
        changed |= Assign(ref whiteModels.queen, "dame_blanche", "dame_blanc");
        changed |= Assign(ref whiteModels.king, "roi_blanc", "roi_blanche");

        changed |= Assign(ref blackModels.pawn, "pion_noir", "pion_noire");
        changed |= Assign(ref blackModels.knight, "cavalier_noir", "cavalier_noire");
        changed |= Assign(ref blackModels.bishop, "fou_noir", "fou_noire");
        changed |= Assign(ref blackModels.rook, "tour_noir", "tour_noire");
        changed |= Assign(ref blackModels.queen, "dame_noir", "dame_noire");
        changed |= Assign(ref blackModels.king, "roi_noir", "roi_noire");

        return changed;
    }

    private static bool Assign(ref GameObject field, params string[] names)
    {
        if (field != null)
            return false;

        // Les .fbx sont prioritaires sur les .blend portant le même nom.
        GameObject fallback = null;

        foreach (string name in names)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (System.IO.Path.GetFileNameWithoutExtension(path) != name)
                    continue;

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (asset == null)
                    continue;

                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    field = asset;
                    return true;
                }

                if (fallback == null)
                    fallback = asset;
            }
        }

        if (fallback == null)
            return false;

        field = fallback;
        return true;
    }
#endif
}
