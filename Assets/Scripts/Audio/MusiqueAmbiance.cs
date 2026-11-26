using UnityEngine;

public sealed class MusiqueAmbiance : MonoBehaviour
{
    private const string CleVolume = "Chess3D.VolumeMusique";
    private const string CleMuet = "Chess3D.MusiqueMuette";
    private static MusiqueAmbiance instance;

    [SerializeField] private AudioClip morceau;
    [SerializeField, Range(0f, 1f)] private float volumeParDefaut = 0.07f;

    private AudioSource source;
    private Texture2D iconeSon;
    private Texture2D iconeMuet;
    private string libelleVolume;
    private bool curseurVisible;
    private bool glisseCurseur;

    private float VolumeAffiche => source.mute ? 0f : source.volume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Demarrer()
    {
        var prefab = Resources.Load<GameObject>(nameof(MusiqueAmbiance));
        if (prefab == null)
        {
            Debug.LogError("Le prefab de musique d’ambiance est introuvable.");
            return;
        }

        Instantiate(prefab);
    }

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.clip = morceau;
        source.loop = true;
        source.spatialBlend = 0f;
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(CleVolume, volumeParDefaut));
        source.volume = volume > 0f ? volume : Mathf.Max(0.01f, volumeParDefaut);
        source.mute = volume == 0f || PlayerPrefs.GetInt(CleMuet, 0) != 0;
        ActualiserLibelle();
        iconeSon = CreerIcone(false);
        iconeMuet = CreerIcone(true);
        source.Play();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        if (iconeSon != null) Destroy(iconeSon);
        if (iconeMuet != null) Destroy(iconeMuet);
    }

    private void OnApplicationPause(bool enPause)
    {
        if (enPause)
            PlayerPrefs.Save();
    }

    private void ReglerVolume(float volume)
    {
        if (volume > 0f)
            source.volume = Mathf.Clamp01(volume);

        source.mute = volume <= 0f;
        MemoriserVolume();
    }

    private void BasculerMuet()
    {
        source.mute = !source.mute;
        MemoriserVolume();
    }

    private void MemoriserVolume()
    {
        PlayerPrefs.SetFloat(CleVolume, source.volume);
        PlayerPrefs.SetInt(CleMuet, source.mute ? 1 : 0);
        ActualiserLibelle();
    }

    private void ActualiserLibelle()
    {
        libelleVolume = $"Musique   {Mathf.RoundToInt(VolumeAffiche * 100f)} %";
    }

    private static Rect ZoneVolume => new Rect(Screen.width - 228f, Screen.height - 64f, 212f, 48f);
    private static Rect BoutonVolume => new Rect(ZoneVolume.xMax - 48f, ZoneVolume.y, 48f, ZoneVolume.height);

    private bool SurControle(Vector2 position)
    {
        return BoutonVolume.Contains(position) || (curseurVisible && ZoneVolume.Contains(position));
    }

    internal static bool PointeurSurVolume(Vector2 position)
    {
        return instance != null && instance.SurControle(new Vector2(position.x, Screen.height - position.y));
    }

    private void OnGUI()
    {
        if (source == null)
            return;

        Event evenement = Event.current;
        curseurVisible = glisseCurseur || SurControle(evenement.mousePosition);
        Rect zone = ZoneVolume;
        if (curseurVisible)
            GUI.Box(zone, GUIContent.none);

        if (GUI.Button(BoutonVolume, source.mute ? iconeMuet : iconeSon))
            BasculerMuet();

        if (!curseurVisible)
            return;

        GUI.Label(new Rect(zone.x + 12f, zone.y + 2f, 142f, 21f), libelleVolume);
        var rail = new Rect(zone.x + 12f, zone.y + 27f, 142f, 16f);
        if (evenement.rawType == EventType.MouseDown && evenement.button == 0 && rail.Contains(evenement.mousePosition))
            glisseCurseur = true;

        float volume = GUI.HorizontalSlider(rail, VolumeAffiche, 0f, 1f);
        if (volume != VolumeAffiche)
            ReglerVolume(volume);

        if (evenement.rawType == EventType.MouseUp && evenement.button == 0)
            glisseCurseur = false;
    }

    private static Texture2D CreerIcone(bool muet)
    {
        const int taille = 24;
        var pixels = new Color32[taille * taille];
        var trait = new Color32(240, 225, 202, 255);
        for (int y = 0; y < taille; y++)
        for (int x = 0; x < taille; x++)
        {
            int dy = Mathf.Abs(y - 11);
            int dx = x - 11;
            int rayonCarre = dx * dx + dy * dy;
            bool hautParleur = (x >= 2 && x <= 5 && dy <= 3) || (x >= 6 && x <= 11 && dy <= x - 3);
            bool signal = muet
                ? dy <= 3 && Mathf.Abs(x - 18) == dy
                : dx > 0 && dy <= dx && ((rayonCarre >= 23 && rayonCarre <= 43) || (rayonCarre >= 68 && rayonCarre <= 104));
            if (hautParleur || signal)
                pixels[y * taille + x] = trait;
        }

        var icone = new Texture2D(taille, taille, TextureFormat.RGBA32, false);
        icone.SetPixels32(pixels);
        icone.Apply(false, true);
        return icone;
    }
}
