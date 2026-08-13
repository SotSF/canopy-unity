using UnityEngine;

/// <summary>
/// Base component for prefab-rooted effects that render in isolation: a dedicated camera
/// under the prefab root renders the effect into a per-instance RenderTexture, so multiple
/// instances of the same effect can run with independent parameters. VFX graphs use
/// VFXInstance; a ShaderGraph-based equivalent can extend this the same way.
/// </summary>
public abstract class CameraEffectInstance : MonoBehaviour
{
    [Tooltip("Camera rendering this effect. Auto-discovered in children if left unset.")]
    public Camera cam;

    [Tooltip("Render texture size allocated per instance unless the creator overrides it.")]
    public Vector2Int defaultTextureSize = new Vector2Int(256, 256);

    public RenderTexture OutputTexture { get; private set; }

    /// <summary>
    /// Binds components and allocates this instance's render target.
    /// Called by VFXRegistry after instantiation.
    /// </summary>
    public void Initialize(Vector2Int? textureSize = null)
    {
        if (cam == null)
        {
            cam = GetComponentInChildren<Camera>(true);
        }
        if (cam == null)
        {
            Debug.LogError($"CameraEffectInstance '{name}' has no Camera in its hierarchy.");
            return;
        }
        AllocateRenderTexture(textureSize ?? defaultTextureSize);
        cam.gameObject.SetActive(true);
        OnInitialized();
    }

    public RenderTexture AllocateRenderTexture(Vector2Int size)
    {
        ReleaseRenderTexture();
        OutputTexture = new RenderTexture(size.x, size.y, 24);
        OutputTexture.Create();
        if (cam != null)
        {
            cam.targetTexture = OutputTexture;
        }
        return OutputTexture;
    }

    protected void ReleaseRenderTexture()
    {
        if (OutputTexture == null) return;
        if (cam != null && cam.targetTexture == OutputTexture)
        {
            cam.targetTexture = null;
        }
        OutputTexture.Release();
        Destroy(OutputTexture);
        OutputTexture = null;
    }

    /// <summary>Effect-type-specific setup, called once camera and render target are ready.</summary>
    protected virtual void OnInitialized() { }

    protected virtual void OnDestroy()
    {
        ReleaseRenderTexture();
    }
}
