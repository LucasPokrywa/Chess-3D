
using UnityEngine;

public class RunePulse : MonoBehaviour
{
    private MeshRenderer auraRenderer;
    private MeshRenderer coreRenderer;

    private MaterialPropertyBlock auraBlock;
    private MaterialPropertyBlock coreBlock;

    private bool animated;
    private float speed;
    private float intensity;
    private float phase;

    private float magicBoost;

    public void Configure(
        MeshRenderer aura,
        MeshRenderer core,
        bool animate,
        float pulseSpeed,
        float glowIntensity,
        float phaseOffset)
    {
        auraRenderer = aura;
        coreRenderer = core;

        animated = animate;

        speed = Mathf.Max(
            0.01f,
            pulseSpeed
        );

        intensity = Mathf.Max(
            0.5f,
            glowIntensity
        );

        phase = phaseOffset;
        magicBoost = 0f;

        auraBlock = new MaterialPropertyBlock();
        coreBlock = new MaterialPropertyBlock();

        ApplyColors(0.65f);

        enabled = animated;
    }

    // Appelee quand une case est selectionnee.
    public void TriggerMagicPulse(float strength)
    {
        magicBoost = Mathf.Max(
            magicBoost,
            Mathf.Clamp(strength, 0f, 3f)
        );

        enabled = true;
    }

    private void Update()
    {
        if (auraRenderer == null ||
            coreRenderer == null)
        {
            return;
        }

        float wave = 0.65f;

        if (animated)
        {
            wave = 0.5f + 0.5f * Mathf.Sin(
                Time.time *
                speed *
                Mathf.PI * 2f +
                phase
            );

            wave = Mathf.SmoothStep(
                0f,
                1f,
                wave
            );
        }

        // L'intensite supplementaire disparait
        // progressivement.
        magicBoost = Mathf.MoveTowards(
            magicBoost,
            0f,
            Time.deltaTime * 1.35f
        );

        ApplyColors(wave);

        if (!animated && magicBoost <= 0.001f)
            enabled = false;
    }

    private void ApplyColors(float wave)
    {
        if (auraRenderer == null ||
            coreRenderer == null)
        {
            return;
        }

        float boost = 1f + magicBoost * 2f;

        float auraPower = Mathf.Lerp(
            0.8f,
            1.35f,
            wave
        ) * intensity * boost;

        float corePower = Mathf.Lerp(
            0.95f,
            1.65f,
            wave
        ) * intensity * boost;

        Color auraColor =
            new Color(0.35f, 0.06f, 0.85f) *
            auraPower;

        Color coreColor =
            new Color(0.87f, 0.70f, 1f) *
            corePower;

        auraBlock.SetColor(
            "_BaseColor",
            auraColor
        );

        auraBlock.SetColor(
            "_Color",
            auraColor
        );

        auraRenderer.SetPropertyBlock(auraBlock);

        coreBlock.SetColor(
            "_BaseColor",
            coreColor
        );

        coreBlock.SetColor(
            "_Color",
            coreColor
        );

        coreRenderer.SetPropertyBlock(coreBlock);
    }
}
