using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

public class MaterialManager : MonoBehaviour
{
    [Header("URP Materials")]
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material ceilingMaterial;
    [SerializeField] private Material wallMaterial;

    [Header("Material Colors")]
    [SerializeField] private Color floorColor = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color ceilingColor = new Color(0.8f, 0.8f, 0.8f);
    [SerializeField] private Color wallColor = new Color(0.9f, 0.9f, 0.9f);

    [Header("Material Properties")]
    [SerializeField] private float floorMetallic = 0.0f;
    [SerializeField] private float floorSmoothness = 0.3f;
    [SerializeField] private float wallMetallic = 0.0f;
    [SerializeField] private float wallSmoothness = 0.2f;
    [SerializeField] private float ceilingMetallic = 0.0f;
    [SerializeField] private float ceilingSmoothness = 0.3f;

    [Header("Android Optimization")]
    [SerializeField] private bool useSimplifiedMaterials = true;

    private Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

    void Start()
    {
        CreateMaterials();
    }

    private void CreateMaterials()
    {
        // Create floor material
        if (floorMaterial == null)
        {
            floorMaterial = CreateURPMaterial("FloorMaterial", floorColor, floorMetallic, floorSmoothness);
        }

        // Create ceiling material
        if (ceilingMaterial == null)
        {
            ceilingMaterial = CreateURPMaterial("CeilingMaterial", ceilingColor, ceilingMetallic, ceilingSmoothness);
        }

        // Create wall material
        if (wallMaterial == null)
        {
            wallMaterial = CreateURPMaterial("WallMaterial", wallColor, wallMetallic, wallSmoothness);
        }
    }

    private Material CreateURPMaterial(string materialName, Color baseColor, float metallic, float smoothness)
    {
        Material material;

        if (useSimplifiedMaterials && Application.platform == RuntimePlatform.Android)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Smoothness", smoothness);
        }
        else
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        material.name = materialName;
        SetupMaterial(material);

        materialCache[materialName] = material;
        return material;
    }

    private void SetupMaterial(Material material)
    {
        // Set color
        if (!material.HasProperty("_BaseColor")) return;

        // Enable Double Sided for walls
        material.SetFloat("_Cull", (int)CullMode.Off);

        // Set workflow mode to Metallic
        if (material.HasProperty("_WorkflowMode"))
            material.SetFloat("_WorkflowMode", 1.0f);

        // Enable appropriate render queue
        material.renderQueue = (int)RenderQueue.Geometry;

        // Set surface type to opaque
        material.SetFloat("_Surface", 0.0f);
    }

    // Public methods to get materials
    public Material GetFloorMaterial()
    {
        if (materialCache.TryGetValue("FloorMaterial", out Material mat))
            return mat;
        return floorMaterial;
    }

    public Material GetCeilingMaterial()
    {
        if (materialCache.TryGetValue("CeilingMaterial", out Material mat))
            return mat;
        return ceilingMaterial;
    }

    public Material GetWallMaterial()
    {
        if (materialCache.TryGetValue("WallMaterial", out Material mat))
            return mat;
        return wallMaterial;
    }

    // Optional: Create transparent materials for debugging
    public Material CreateTransparentMaterial(Color color, float alpha)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        // Setup for transparency
        material.SetFloat("_Surface", 1.0f); // 1.0f = Transparent
        material.SetFloat("_Blend", 0.0f); // 0.0f = Alpha

        // Set color with alpha
        Color transparentColor = color;
        transparentColor.a = alpha;
        material.SetColor("_BaseColor", transparentColor);

        // Enable transparency
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;

        return material;
    }

    // Optional: Update material colors at runtime
    public void UpdateMaterialColors(Color? newFloorColor = null, Color? newWallColor = null, Color? newCeilingColor = null)
    {
        if (newFloorColor.HasValue && floorMaterial != null)
            floorMaterial.SetColor("_BaseColor", newFloorColor.Value);

        if (newWallColor.HasValue && wallMaterial != null)
            wallMaterial.SetColor("_BaseColor", newWallColor.Value);

        if (newCeilingColor.HasValue && ceilingMaterial != null)
            ceilingMaterial.SetColor("_BaseColor", newCeilingColor.Value);
    }

    // Android-specific material optimization
    public void OptimizeForAndroid()
    {
        if (Application.platform != RuntimePlatform.Android) return;

        foreach (Material mat in materialCache.Values)
        {
            // Switch to Simple Lit shader
            if (mat.shader.name.Contains("Lit") && !mat.shader.name.Contains("Simple"))
            {
                mat.shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }

            // Disable expensive features
            mat.DisableKeyword("_NORMALMAP");
            mat.DisableKeyword("_METALLICSPECGLOSSMAP");
            mat.DisableKeyword("_PARALLAXMAP");
            mat.DisableKeyword("_EMISSION");
        }
    }
}