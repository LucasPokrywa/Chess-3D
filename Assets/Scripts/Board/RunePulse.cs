
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
        speed = Mathf.Max(0.01f, pulseSpeed);
        intensity = Mathf.Max(0.5f, glowIntensity);
        phase = phaseOffset;

        auraBlock = new MaterialPropertyBlock();
        coreBlock = new MaterialPropertyBlock();

        // La rune reste visible même sans animation.
        ApplyColors(0.65f);

        enabled = animated;
    }

    private void Update()
    {
        if (auraRenderer == null ||
            coreRenderer == null)
            return;

        float wave = 0.5f + 0.5f * Mathf.Sin(
            Time.time * speed * Mathf.PI * 2f
            + phase
        );

        wave = Mathf.SmoothStep(0f, 1f, wave);

        ApplyColors(wave);
    }

    private void ApplyColors(float wave)
    {
        // L'aura ne s'éteint jamais.
        float auraPower = Mathf.Lerp(
            0.8f,
            1.35f,
            wave
        ) * intensity;

        // Le coeur lumineux reste constamment lisible.
        float corePower = Mathf.Lerp(
            0.95f,
            1.65f,
            wave
        ) * intensity;

        Color auraColor = new Color(
            0.35f, 0.06f, 0.85f
        ) * auraPower;

        Color coreColor = new Color(
            0.87f, 0.70f, 1f
        ) * corePower;

        auraBlock.SetColor(
            "_BaseColor", auraColor
        );

        auraBlock.SetColor(
            "_Color", auraColor
        );

        auraRenderer.SetPropertyBlock(auraBlock);

        coreBlock.SetColor(
            "_BaseColor", coreColor
        );

        coreBlock.SetColor(
            "_Color", coreColor
        );

        coreRenderer.SetPropertyBlock(coreBlock);
    }
}
