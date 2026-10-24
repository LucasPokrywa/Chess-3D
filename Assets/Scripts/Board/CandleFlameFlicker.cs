using UnityEngine;

public class CandleFlameFlicker : MonoBehaviour
{
    private Transform flamePivot;
    private MeshRenderer outerRenderer;
    private MeshRenderer coreRenderer;
    private Light candleLight;
    private Color outerColor;
    private Color coreColor;
    private float speed;
    private float baseIntensity;
    private float phase;
    private MaterialPropertyBlock outerBlock;
    private MaterialPropertyBlock coreBlock;

    public void Configure(
        Transform pivot,
        MeshRenderer outer,
        MeshRenderer core,
        Light pointLight,
        Color outside,
        Color inside,
        float flickerSpeed,
        float intensity,
        float phaseOffset)
    {
        flamePivot = pivot;
        outerRenderer = outer;
        coreRenderer = core;
        candleLight = pointLight;
        outerColor = outside;
        coreColor = inside;
        speed = Mathf.Max(0.1f, flickerSpeed);
        baseIntensity = Mathf.Max(0f, intensity);
        phase = phaseOffset;
        outerBlock = new MaterialPropertyBlock();
        coreBlock = new MaterialPropertyBlock();
        ApplyGlow(1f);
    }

    private void Update()
    {
        if (flamePivot == null || candleLight == null)
            return;

        float t = Time.time * speed;
        float noise = Mathf.PerlinNoise(phase + 4f, t * 1.6f);
        float wave = 0.5f + 0.5f * Mathf.Sin(t * 5.1f + phase);
        float flicker = Mathf.Clamp01(0.78f * noise + 0.22f * wave);

        // Petites oscillations naturelles, sans extinction de la flamme.
        float tiltX = Mathf.Sin(t * 2.8f + phase) * 4f;
        float tiltZ = Mathf.Cos(t * 3.5f + phase) * 5f;
        flamePivot.localRotation = Quaternion.Euler(tiltX, 0f, tiltZ);
        flamePivot.localScale = new Vector3(
            Mathf.Lerp(0.95f, 1.07f, flicker),
            Mathf.Lerp(0.91f, 1.13f, flicker),
            Mathf.Lerp(0.95f, 1.07f, flicker));

        candleLight.intensity = baseIntensity * Mathf.Lerp(0.75f, 1.15f, flicker);
        ApplyGlow(Mathf.Lerp(0.85f, 1.2f, flicker));
    }

    private void ApplyGlow(float amount)
    {
        SetColor(outerRenderer, outerBlock, outerColor * amount);
        SetColor(coreRenderer, coreBlock, coreColor * Mathf.Lerp(1f, 1.25f, amount));
    }

    private static void SetColor(MeshRenderer renderer, MaterialPropertyBlock block, Color color)
    {
        if (renderer == null || block == null)
            return;
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }
}
