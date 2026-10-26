using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Builds only the newly modelled Sedan01 art asset. Does not edit scenes or vehicle gameplay.</summary>
public static class Sedan01AssetBuilder
{
    private const string AssetRoot = "Assets/Art/Vehicles/Sedan01";
    private const string ReportPath = "Art/Models/Vehicles/Sedan01/unity_import_report.json";

    private readonly struct Finish
    {
        public readonly string Name;
        public readonly Color Color;
        public readonly float Metal, Smooth;
        public Finish(string name, float r, float g, float b, float metal, float smooth, float alpha = 1f)
        { Name = "Sedan01_" + name; Color = new Color(r, g, b, alpha); Metal = metal; Smooth = smooth; }
    }

    [Serializable] private sealed class LevelReport
    {
        public int level, triangles, vertices, renderers, materialSlots;
    }

    [Serializable] private sealed class ImportReport
    {
        public string prefab;
        public string unityVersion;
        public string shader;
        public Vector3 sizeMetres;
        public Vector3 frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel;
        public bool materialAndGeometryChecksPassed;
        public LevelReport[] levels;
    }

    [MenuItem("Tools/Art/Build Sedan01 Reference Model")]
    public static void Build()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("The project's URP/Lit shader is required.");
        Directory.CreateDirectory(AssetRoot + "/Materials");
        Directory.CreateDirectory(AssetRoot + "/Prefabs");
        var finishes = new[]
        {
            new Finish("SilverPaint", .708f, .729f, .735f, .78f, .73f),
            new Finish("Rubber", .123f, .138f, .147f, 0f, .24f),
            new Finish("Graphite", .206f, .229f, .243f, .55f, .68f),
            new Finish("Glass", .381f, .473f, .496f, .05f, .87f, .38f),
            new Finish("Alloy", .575f, .601f, .619f, .88f, .75f),
            new Finish("Chrome", .827f, .854f, .865f, .95f, .84f),
            new Finish("ClearLens", .821f, .870f, .886f, .45f, .84f),
            new Finish("RedLens", .680f, .112f, .143f, .26f, .78f),
            new Finish("AmberLens", .955f, .537f, .172f, .18f, .75f),
            new Finish("Interior", .229f, .250f, .260f, .02f, .17f),
            new Finish("Plate", .881f, .901f, .891f, .2f, .64f),
        };
        var materials = new Dictionary<string, Material>();
        foreach (var finish in finishes)
        {
            string path = AssetRoot + "/Materials/" + finish.Name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = finish.Name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", finish.Color);
            material.SetFloat("_Metallic", finish.Metal);
            material.SetFloat("_Smoothness", finish.Smooth);
            bool transparent = finish.Color.a < 1f;
            material.SetFloat("_Surface", transparent ? 1 : 0);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_AlphaClip", 0);
            material.SetFloat("_Cull", (float)(transparent ? CullMode.Off : CullMode.Back));
            material.SetFloat("_ZWrite", transparent ? 0 : 1);
            material.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            material.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
            if (transparent) material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            else material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (finish.Name == "Sedan01_RedLens")
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", finish.Color.linear * .12f);
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            materials.Add(finish.Name, material);
        }

        var root = new GameObject("Sedan01");
        try
        {
            var lodGroup = root.AddComponent<LODGroup>();
            var lods = new LOD[3];
            var reports = new LevelReport[3];
            float[] thresholds = { .60f, .28f, .065f };
            GameObject near = null;
            for (int level = 0; level < 3; level++)
            {
                string path = AssetRoot + "/Models/Sedan01_LOD" + level + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Missing model: " + path);
                importer.globalScale = 1f;
                importer.useFileScale = true;
                importer.bakeAxisConversion = true;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importAnimation = false;
                importer.importBlendShapes = false;
                importer.isReadable = false;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.generateSecondaryUV = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                // Remap at import time so the FBXs also work when dragged directly into a scene.
                foreach (var entry in materials)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.Key), entry.Value);
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.name = "LOD" + level;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                if (level == 0) near = instance;
                var renderers = instance.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length != 6) throw new InvalidOperationException("Expected body, glass and four wheels.");
                foreach (var renderer in renderers)
                {
                    var mapped = renderer.sharedMaterials.Select(m =>
                    {
                        if (m == null || !materials.TryGetValue(m.name, out var replacement))
                            throw new InvalidOperationException("Unmapped Sedan01 finish: " + (m == null ? "null" : m.name));
                        return replacement;
                    }).ToArray();
                    renderer.sharedMaterials = mapped;
                    renderer.receiveShadows = true;
                    renderer.shadowCastingMode = renderer.name == "Glass" ? ShadowCastingMode.Off : ShadowCastingMode.On;
                }
                var filters = instance.GetComponentsInChildren<MeshFilter>();
                reports[level] = new LevelReport
                {
                    level = level,
                    triangles = filters.Sum(f => (int)(Enumerable.Range(0, f.sharedMesh.subMeshCount).Sum(s => (long)f.sharedMesh.GetIndexCount(s)) / 3)),
                    vertices = filters.Sum(f => f.sharedMesh.vertexCount),
                    renderers = renderers.Length,
                    materialSlots = renderers.Sum(r => r.sharedMaterials.Length),
                };
                lods[level] = new LOD(thresholds[level], renderers);
            }
            lodGroup.SetLODs(lods);
            lodGroup.fadeMode = LODFadeMode.None;
            lodGroup.RecalculateBounds();
            var nearRenderers = near.GetComponentsInChildren<Renderer>();
            Bounds bounds = nearRenderers[0].bounds;
            foreach (var r in nearRenderers.Skip(1)) bounds.Encapsulate(r.bounds);
            Vector3 Wheel(string name)
            {
                var wheel = near.GetComponentsInChildren<Transform>().Single(t => t.name == name);
                return root.transform.InverseTransformPoint(wheel.position);
            }
            var fl = Wheel("Wheel_FL"); var fr = Wheel("Wheel_FR");
            var rl = Wheel("Wheel_RL"); var rr = Wheel("Wheel_RR");
            if (bounds.size.z < 4.3f || bounds.size.z > 4.9f || bounds.size.y < 1.3f || bounds.size.y > 1.6f)
                throw new InvalidOperationException("Unexpected dimensions or model orientation: " + bounds.size);
            if (fl.x > 0 || fr.x < 0 || fl.z < 0 || rl.z > 0 || Mathf.Abs(fl.y - .325f) > .01f)
                throw new InvalidOperationException("Wheel pivots or forward axis require correction: " + fl);
            if (!(reports[0].triangles > reports[1].triangles && reports[1].triangles > reports[2].triangles))
                throw new InvalidOperationException("LOD triangle budgets do not decrease.");
            string prefabPath = AssetRoot + "/Prefabs/Sedan01.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            AssetDatabase.SaveAssets();
            var report = new ImportReport
            {
                prefab = prefabPath, unityVersion = Application.unityVersion, shader = shader.name,
                sizeMetres = bounds.size, frontLeftWheel = fl, frontRightWheel = fr,
                rearLeftWheel = rl, rearRightWheel = rr, levels = reports, materialAndGeometryChecksPassed = true,
            };
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            Debug.Log("SEDAN01_IMPORT_OK " + JsonUtility.ToJson(report));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    public static void BuildAndPreview()
    {
        Build();
        RenderPreviews();
    }

    [MenuItem("Tools/Art/Render Sedan01 URP Previews")]
    public static void RenderPreviews()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            throw new InvalidOperationException("URP previews need a graphics device; omit -nographics.");
        var preview = new PreviewRenderUtility(true);
        var transient = new List<UnityEngine.Object>();
        try
        {
            preview.ambientColor = new Color(.55f, .58f, .63f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetRoot + "/Prefabs/Sedan01.prefab");
            var car = UnityEngine.Object.Instantiate(prefab);
            preview.AddSingleGO(car);
            car.GetComponent<LODGroup>().ForceLOD(0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            preview.AddSingleGO(floor);
            floor.name = "Preview floor";
            floor.transform.localScale = new Vector3(200, .04f, 200);
            floor.transform.position = new Vector3(0, -.035f, 0);
            var floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            transient.Add(floorMaterial);
            floorMaterial.SetColor("_BaseColor", new Color(.25f, .27f, .29f));
            floorMaterial.SetFloat("_Smoothness", .2f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            var key = preview.lights[0];
            key.type = LightType.Directional; key.intensity = 2.6f;
            key.color = new Color(1f, .96f, .89f);key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(42f, -30f, 0);
            var fill = preview.lights[1];
            fill.type = LightType.Directional; fill.intensity = 1.1f;
            fill.color = new Color(.80f, .89f, 1f);fill.transform.rotation = Quaternion.Euler(25f, 145f, 0);
            var camera = preview.camera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.09f, .105f, .12f);
            camera.fieldOfView = 27f;camera.nearClipPlane = .1f;camera.farClipPlane = 100f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            string directory = "Art/Models/Vehicles/Sedan01/Renders";
            void Capture(Vector3 position, string filename)
            {
                camera.transform.position = position;
                camera.transform.LookAt(new Vector3(0, .72f, 0));
                preview.BeginStaticPreview(new Rect(0, 0, 1440, 960));
                preview.Render(true, false);
                var pixels2D = preview.EndStaticPreview();
                File.WriteAllBytes(directory + "/" + filename, pixels2D.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(pixels2D);
            }
            // Prime the preview scene's reflection and ambient state on the first SRP frame.
            camera.transform.position = new Vector3(-5.6f, 3.05f, 6.5f);
            camera.transform.LookAt(new Vector3(0, .72f, 0));
            preview.BeginStaticPreview(new Rect(0, 0, 1440, 960));
            preview.Render(true, false);
            UnityEngine.Object.DestroyImmediate(preview.EndStaticPreview());
            Capture(new Vector3(-5.6f, 3.05f, 6.5f), "Sedan01_UnityURP_Front34.png");
            Capture(new Vector3(-5.4f, 2.7f, -6.3f), "Sedan01_UnityURP_Rear34.png");
            car.GetComponent<LODGroup>().ForceLOD(2);
            Capture(new Vector3(-5.6f, 3.05f, 6.5f), "Sedan01_UnityURP_LOD2.png");
            Debug.Log("SEDAN01_URP_PREVIEWS_OK");
        }
        finally
        {
            preview.Cleanup();
            foreach (var item in transient) UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
