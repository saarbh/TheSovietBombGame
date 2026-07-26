using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click wiring for the hold-to-fast-forward feature: builds the screen-space clock
/// HUD and the cigarette smoke under the player camera, and hooks both to the player.
///
/// This exists because the two runtime scripts (<see cref="ClockHudView"/> and
/// <see cref="PlayerSmokeEffect"/>) need scene objects that nobody wants to hand-author,
/// and hand-editing scene YAML is banned in this project. Safe to run more than once -
/// it finds and updates what it already made rather than stacking duplicates.
/// </summary>
public static class TimeSkipSetup
{
    private const string ClockRootName = "ClockHud";
    private const string SmokeObjectName = "CigaretteSmoke";
    private const string SmokeMaterialPath = "Assets/Materials/Placeholders/CigaretteSmoke.mat";

    [MenuItem("Tools/The Hot War/Setup Time Skip (Clock HUD + Smoke)")]
    public static void Setup()
    {
        var clock = SetupClockHud();
        var smoke = SetupSmoke();

        EditorUtility.DisplayDialog(
            "Time Skip Setup",
            $"Clock HUD: {clock}\nCigarette smoke: {smoke}\n\nHold Q (or gamepad north) in play mode to run the clock at 4x.",
            "OK");
    }

    private static string SetupClockHud()
    {
        var existing = GameObject.Find(ClockRootName);

        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        var root = new GameObject(ClockRootName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(root, "Create Clock HUD");

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Above the evidence HUD so a full card list can never cover the clock.
        canvas.sortingOrder = 50;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var label = CreateClockLabel(root.transform);
        var indicator = CreateFastForwardIndicator(root.transform);

        var view = Undo.AddComponent<ClockHudView>(root);

        var so = new SerializedObject(view);
        so.FindProperty("clockLabel").objectReferenceValue = label;
        so.FindProperty("fastForwardIndicator").objectReferenceValue = indicator.gameObject;
        so.FindProperty("playerController").objectReferenceValue = Object.FindAnyObjectByType<PlayerController>();
        so.ApplyModifiedPropertiesWithoutUndo();

        // The indicator starts hidden; ClockHudView turns it on when time accelerates.
        indicator.gameObject.SetActive(false);

        EditorSceneMarkDirty(root);
        return "created";
    }

    private static TMP_Text CreateClockLabel(Transform parent)
    {
        var go = new GameObject("ClockLabel", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();

        // Top-centre, which is the one spot no existing HUD element claims.
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -32f);
        rect.sizeDelta = new Vector2(420f, 96f);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = "07:00";
        text.fontSize = 64f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        return text;
    }

    private static TMP_Text CreateFastForwardIndicator(Transform parent)
    {
        var go = new GameObject("FastForwardIndicator", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -118f);
        rect.sizeDelta = new Vector2(420f, 48f);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = ">> 4x";
        text.fontSize = 28f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.45f, 0.2f);
        text.raycastTarget = false;

        return text;
    }

    private static string SetupSmoke()
    {
        var player = Object.FindAnyObjectByType<PlayerController>();

        if (player == null)
        {
            return "SKIPPED - no PlayerController in the open scene";
        }

        // Parent to the camera: in first person the smoke has to drift up through the
        // lower part of the view, since the player cannot see their own face.
        var camera = player.GetComponentInChildren<Camera>();
        var parent = camera != null ? camera.transform : player.transform;

        var existing = parent.Find(SmokeObjectName);

        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var go = new GameObject(SmokeObjectName);
        Undo.RegisterCreatedObjectUndo(go, "Create Cigarette Smoke");
        go.transform.SetParent(parent, false);

        // Down and forward from the eye: roughly where a cigarette would be held.
        go.transform.localPosition = new Vector3(0.18f, -0.28f, 0.42f);
        go.transform.localRotation = Quaternion.identity;

        var particles = go.AddComponent<ParticleSystem>();
        ConfigureSmoke(particles);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetOrCreateSmokeMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        // Emission is driven entirely by PlayerSmokeEffect; nothing should puff on load.
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var effect = go.AddComponent<PlayerSmokeEffect>();
        var so = new SerializedObject(effect);
        so.FindProperty("playerController").objectReferenceValue = player;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneMarkDirty(go);
        return camera != null ? "created under the player camera" : "created on the player (no camera found)";
    }

    private static void ConfigureSmoke(ParticleSystem particles)
    {
        var main = particles.main;
        main.duration = 4f;
        main.loop = true;
        main.startLifetime = 3.2f;
        main.startSpeed = 0.16f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.82f, 0.82f, 0.8f, 0.28f));
        main.gravityModifier = -0.02f;
        main.maxParticles = 60;
        main.playOnAwake = false;

        // World space, so puffs hang in the air as the player turns instead of
        // riding the camera like a decal.
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 7f;

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.02f;

        // Point the cone up: smoke rises off the cigarette.
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var growth = new AnimationCurve();
        growth.AddKey(0f, 0.35f);
        growth.AddKey(1f, 1f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, growth);

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.25f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.35f;
    }

    /// <summary>
    /// URP will render the stock particle material magenta, so a proper URP unlit
    /// particle material is created once and reused.
    /// </summary>
    private static Material GetOrCreateSmokeMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);

        if (existing != null)
        {
            return existing;
        }

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        var material = new Material(shader) { name = "CigaretteSmoke" };

        var softDot = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");

        if (softDot != null)
        {
            material.mainTexture = softDot;
        }

        // Additive would glow; smoke needs plain alpha blending.
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
        }

        var directory = System.IO.Path.GetDirectoryName(SmokeMaterialPath);

        if (!AssetDatabase.IsValidFolder(directory))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "Placeholders");
        }

        AssetDatabase.CreateAsset(material, SmokeMaterialPath);
        AssetDatabase.SaveAssets();

        return material;
    }

    private static void EditorSceneMarkDirty(GameObject go)
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
    }
}
