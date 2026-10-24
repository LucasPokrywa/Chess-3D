using UnityEngine;

public class OccultSealPulse : MonoBehaviour
{
    private MeshRenderer[] mainRenderers;
    private MeshRenderer[] accentRenderers;

    private MaterialPropertyBlock[] mainBlocks;
    private MaterialPropertyBlock[] accentBlocks;

    private float[] mainPhases;
    private float[] accentPhases;

    private bool animateSeal;
    private float pulseSpeed;
    private float glowIntensity;

    private Color mainColor;
    private Color accentColor;

    public void Configure(
        MeshRenderer[] mains,
        MeshRenderer[] accents,
        bool animate,
        float speed,
        float intensity,
        Color main,
        Color accent)
    {
        mainRenderers = mains;
        accentRenderers = accents;

        animateSeal = animate;
        pulseSpeed = Mathf.Max(0.01f, speed);
        glowIntensity = Mathf.Max(0.5f, intensity);

        mainColor = main;
        accentColor = accent;

        mainBlocks = new MaterialPropertyBlock[mainRenderers.Length];
        accentBlocks = new MaterialPropertyBlock[accentRenderers.Length];

        mainPhases = new float[mainRenderers.Length];
        accentPhases = new float[accentRenderers.Length];

        for (int i = 0; i < mainRenderers.Length; i++)
        {
            mainBlocks[i] = new MaterialPropertyBlock();
            mainPhases[i] = i * 0.23f;
        }

        for (int i = 0; i < accentRenderers.Length; i++)
        {
            accentBlocks[i] = new MaterialPropertyBlock();
            accentPhases[i] = i * 0.31f;
        }

        ApplyStatic();
        enabled = true;
    }

    private void Update()
    {
        if (!animateSeal)
        {
            ApplyStatic();
            return;
        }

        float t = Time.time * pulseSpeed;

        for (int i = 0; i < mainRenderers.Length; i++)
        {
            if (mainRenderers[i] == null)
                continue;

            float wave = 0.5f + 0.5f * Mathf.Sin(
                t * 2.0f + mainPhases[i]
            );

            wave = Mathf.SmoothStep(0f, 1f, wave);

            float power = Mathf.Lerp(
                1.0f,
                2.6f,
                wave
            ) * glowIntensity;

            Color c = mainColor * power;

            mainBlocks[i].SetColor("_BaseColor", c);
            mainBlocks[i].SetColor("_Color", c);

            mainRenderers[i].SetPropertyBlock(mainBlocks[i]);
        }

        for (int i = 0; i < accentRenderers.Length; i++)
        {
            if (accentRenderers[i] == null)
                continue;

            float wave = 0.5f + 0.5f * Mathf.Sin(
                t * 2.5f + accentPhases[i]
            );

            wave = Mathf.SmoothStep(0f, 1f, wave);

            float power = Mathf.Lerp(
                0.9f,
                2.1f,
                wave
            ) * (glowIntensity * 0.95f);

            Color c = accentColor * power;

            accentBlocks[i].SetColor("_BaseColor", c);
            accentBlocks[i].SetColor("_Color", c);

            accentRenderers[i].SetPropertyBlock(accentBlocks[i]);
        }
    }

    private void ApplyStatic()
    {
        for (int i = 0; i < mainRenderers.Length; i++)
        {
            if (mainRenderers[i] == null)
                continue;

            Color c = mainColor * glowIntensity * 1.4f;

            mainBlocks[i].SetColor("_BaseColor", c);
            mainBlocks[i].SetColor("_Color", c);

            mainRenderers[i].SetPropertyBlock(mainBlocks[i]);
        }

        for (int i = 0; i < accentRenderers.Length; i++)
        {
            if (accentRenderers[i] == null)
                continue;

            Color c = accentColor * glowIntensity * 1.2f;

            accentBlocks[i].SetColor("_BaseColor", c);
            accentBlocks[i].SetColor("_Color", c);

            accentRenderers[i].SetPropertyBlock(accentBlocks[i]);
        }
    }
}