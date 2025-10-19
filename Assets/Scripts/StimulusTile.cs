using UnityEngine;

/// Attach this to each tile. It instantiates a runtime material so changing
/// the normal map only affects this tile. It also enables the _NORMALMAP keyword.
[RequireComponent(typeof(MeshRenderer))]
public class StimulusTile : MonoBehaviour
{
    [Header("Material Setup")]
    public Material baseMaterial;          // optional; if null we clone current renderer material
    public float normalScale = 1f;         // strength
    public Vector2 tiling = new Vector2(1, 1);
    public Vector2 offset = Vector2.zero;

    private MeshRenderer rend;
    private Material runtimeMat;

    private static readonly int BumpMapID   = Shader.PropertyToID("_BumpMap");
    private static readonly int BumpScaleID = Shader.PropertyToID("_BumpScale");
    private static readonly int MainTexSTID = Shader.PropertyToID("_MainTex_ST"); // tiling/offset

    private void Awake()
    {
        rend = GetComponent<MeshRenderer>();

        // create a unique runtime material per tile
        if (baseMaterial != null)
            runtimeMat = new Material(baseMaterial);
        else
            runtimeMat = new Material(rend.sharedMaterial);

        runtimeMat.name = $"{name}_RuntimeMat";
        rend.material = runtimeMat;

        ApplyTiling();
        runtimeMat.EnableKeyword("_NORMALMAP");
        runtimeMat.SetFloat(BumpScaleID, normalScale);
    }

    private void ApplyTiling()
    {
        // For URP/Lit, tiling can be set via _MainTex_ST (xy = tiling, zw = offset)
        runtimeMat.SetVector(MainTexSTID, new Vector4(tiling.x, tiling.y, offset.x, offset.y));
    }

    /// Call this from the manager every trial.
    public void SetNormal(Texture2D normalTex)
    {
        if (normalTex == null) return;

        // Make sure the texture is set up as a Normal Map in import settings.
        runtimeMat.EnableKeyword("_NORMALMAP");
        runtimeMat.SetTexture(BumpMapID, normalTex);
        runtimeMat.SetFloat(BumpScaleID, normalScale);

        // This log helps verify the actual texture name you see is applied.
        Debug.Log($"[StimulusTile:{name}] SetNormal -> prop='_BumpMap', tex='{normalTex.name}', shader='{runtimeMat.shader.name}'");
    }
}
