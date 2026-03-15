#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMseConverter
{
    /// <summary>
    /// Assets icindeki tum .mse dosyalarini otomatik bulur ve
    /// tek tikla Prefab + Material olarak kaydeder.
    ///
    /// Kullanim: Tools -> MSE Toplu Donustur
    /// </summary>
    public class MseImporterWindow : EditorWindow
    {
        private float scaleFactor = 0.01f;
        private Vector2 scrollPos;
        private List<string> mseFiles = new List<string>();
        private string statusMessage = "";

        [MenuItem("Tools/MSE Toplu Donustur")]
        public static void ShowWindow()
        {
            var window = GetWindow<MseImporterWindow>("MSE Donusturucu");
            window.minSize = new Vector2(400, 300);
            window.ScanForMseFiles();
        }

        private void OnEnable()
        {
            ScanForMseFiles();
        }

        private void ScanForMseFiles()
        {
            mseFiles.Clear();
            string[] guids = AssetDatabase.FindAssets("", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".mse", System.StringComparison.OrdinalIgnoreCase))
                    mseFiles.Add(path);
            }
            mseFiles.Sort();
        }

        private void OnGUI()
        {
            GUILayout.Label("MSE -> Unity ParticleSystem", EditorStyles.boldLabel);
            GUILayout.Space(5);

            scaleFactor = EditorGUILayout.FloatField("Olcek Faktoru", scaleFactor);
            GUILayout.Space(5);

            // Yenile butonu
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Bulunan .mse dosyalari: {mseFiles.Count}", EditorStyles.miniLabel);
            if (GUILayout.Button("Yenile", GUILayout.Width(60)))
                ScanForMseFiles();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Dosya listesi
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            foreach (string path in mseFiles)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
                if (GUILayout.Button("Donustur", GUILayout.Width(70)))
                {
                    ConvertSingleMse(path);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(10);

            // Tumunu donustur
            GUI.enabled = mseFiles.Count > 0;
            if (GUILayout.Button("Tumunu Donustur", GUILayout.Height(35)))
            {
                int success = 0;
                for (int i = 0; i < mseFiles.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("MSE Donusturuluyor",
                        mseFiles[i], (float)i / mseFiles.Count);
                    if (ConvertSingleMse(mseFiles[i]))
                        success++;
                }
                EditorUtility.ClearProgressBar();
                statusMessage = $"{success}/{mseFiles.Count} dosya donusturuldu.";
            }
            GUI.enabled = true;

            // Durum mesaji
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(5);
                EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
            }
        }

        /// <summary>
        /// Tek bir .mse dosyasini ayni dizinde prefab + materyal olarak kaydeder.
        /// </summary>
        private bool ConvertSingleMse(string msePath)
        {
            string fullPath = Path.GetFullPath(msePath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[MSE] Dosya bulunamadi: {msePath}");
                return false;
            }

            MseEffectData data = MseParser.ParseFromFile(fullPath);
            if (data.ParticleGroups.Count == 0)
            {
                Debug.LogWarning($"[MSE] Parcacik grubu yok: {msePath}");
                return false;
            }

            string effectName = Path.GetFileNameWithoutExtension(msePath);
            string mseDir = Path.GetDirectoryName(msePath).Replace("\\", "/");
            string materialsFolder = $"{mseDir}/{effectName}_Materials";
            EnsureFolderExists(materialsFolder);
            string meshesFolder = $"{mseDir}/{effectName}_Meshes";

            // Ana GameObject
            var rootObj = new GameObject(effectName);
            bool needsMeshFolder = false;

            for (int i = 0; i < data.ParticleGroups.Count; i++)
            {
                var group = data.ParticleGroups[i];
                var emitter = group.EmitterProperty;
                var particle = group.ParticleProperty;

                var childObj = new GameObject($"ParticleGroup_{i}");
                childObj.transform.SetParent(rootObj.transform, false);

                var ps = childObj.AddComponent<ParticleSystem>();
                var psRenderer = childObj.GetComponent<ParticleSystemRenderer>();

                // --- Main Module ---
                var main = ps.main;
                main.startDelay = group.StartTime;
                main.duration = emitter.CycleLength > 0 ? emitter.CycleLength : 1f;
                main.loop = emitter.CycleLoopEnable;
                main.maxParticles = emitter.MaxEmissionCount > 0 ? emitter.MaxEmissionCount : 100;
                main.playOnAwake = true;
                main.simulationSpace = particle.AttachEnable
                    ? ParticleSystemSimulationSpace.Local
                    : ParticleSystemSimulationSpace.World;
                main.startLifetime = 2f;
                main.startSpeed = 1f;
                main.startSize = 0.5f;

                // Lifetime
                if (emitter.TimeEventLifeTime.Count == 1)
                    main.startLifetime = emitter.TimeEventLifeTime[0].Value;
                else if (emitter.TimeEventLifeTime.Count > 1)
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1f,
                        TimeEventsToAnimationCurve(emitter.TimeEventLifeTime));

                // Speed
                if (emitter.TimeEventEmittingVelocity.Count == 1)
                    main.startSpeed = emitter.TimeEventEmittingVelocity[0].Value * scaleFactor;
                else if (emitter.TimeEventEmittingVelocity.Count > 1)
                    main.startSpeed = new ParticleSystem.MinMaxCurve(
                        GetMaxValue(emitter.TimeEventEmittingVelocity) * scaleFactor,
                        TimeEventsToAnimationCurve(emitter.TimeEventEmittingVelocity, scaleFactor));

                // Size
                if (emitter.TimeEventSizeX.Count > 0 || emitter.TimeEventSizeY.Count > 0)
                {
                    main.startSize3D = true;
                    if (emitter.TimeEventSizeX.Count == 1)
                        main.startSizeX = emitter.TimeEventSizeX[0].Value * scaleFactor;
                    else if (emitter.TimeEventSizeX.Count > 1)
                        main.startSizeX = new ParticleSystem.MinMaxCurve(
                            GetMaxValue(emitter.TimeEventSizeX) * scaleFactor,
                            TimeEventsToAnimationCurve(emitter.TimeEventSizeX, scaleFactor));

                    if (emitter.TimeEventSizeY.Count == 1)
                        main.startSizeY = emitter.TimeEventSizeY[0].Value * scaleFactor;
                    else if (emitter.TimeEventSizeY.Count > 1)
                        main.startSizeY = new ParticleSystem.MinMaxCurve(
                            GetMaxValue(emitter.TimeEventSizeY) * scaleFactor,
                            TimeEventsToAnimationCurve(emitter.TimeEventSizeY, scaleFactor));
                }

                // --- Emission ---
                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 10f;
                if (emitter.TimeEventEmissionCountPerSecond.Count == 1)
                    emission.rateOverTime = emitter.TimeEventEmissionCountPerSecond[0].Value;
                else if (emitter.TimeEventEmissionCountPerSecond.Count > 1)
                    emission.rateOverTime = new ParticleSystem.MinMaxCurve(
                        GetMaxValue(emitter.TimeEventEmissionCountPerSecond),
                        TimeEventsToAnimationCurve(emitter.TimeEventEmissionCountPerSecond));

                // --- Shape ---
                var shape = ps.shape;
                shape.enabled = true;
                switch (emitter.EmitterShape)
                {
                    case 0:
                        shape.shapeType = ParticleSystemShapeType.Sphere;
                        shape.radius = 0.001f;
                        break;
                    case 1:
                        shape.shapeType = ParticleSystemShapeType.Sphere;
                        shape.radius = 1f;
                        shape.scale = emitter.EmittingSize.ToUnityVector3() * scaleFactor;
                        break;
                    case 2:
                        shape.shapeType = ParticleSystemShapeType.Box;
                        shape.scale = emitter.EmittingSize.ToUnityVector3() * scaleFactor;
                        break;
                    case 3:
                        shape.shapeType = ParticleSystemShapeType.Sphere;
                        shape.radius = emitter.EmittingRadius * scaleFactor;
                        break;
                }
                if (emitter.EmitFromEdgeFlag && (emitter.EmitterShape == 1 || emitter.EmitterShape == 3))
                    shape.radiusThickness = 0f;
                if (emitter.EmittingDirection.X != 0 || emitter.EmittingDirection.Y != 0 || emitter.EmittingDirection.Z != 0)
                    shape.rotation = emitter.EmittingDirection.ToUnityVector3();
                if (emitter.EmitterAdvancedType != 0)
                {
                    var vel = ps.velocityOverLifetime;
                    vel.enabled = true;
                    vel.radial = new ParticleSystem.MinMaxCurve(emitter.EmitterAdvancedType == 1 ? 2f : -2f);
                }

                // --- Billboard & Renderer ---
                Mesh customMesh = null;
                if (!particle.StretchEnable)
                {
                    switch (particle.BillboardType)
                    {
                        case 0:
                            psRenderer.renderMode = ParticleSystemRenderMode.Mesh;
                            customMesh = CreateQuadMesh();
                            psRenderer.alignment = ParticleSystemRenderSpace.Local;
                            break;
                        case 1:
                            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                            psRenderer.alignment = ParticleSystemRenderSpace.View;
                            break;
                        case 2:
                            psRenderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
                            break;
                        case 3:
                            psRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                            break;
                        case 4:
                            psRenderer.renderMode = ParticleSystemRenderMode.Mesh;
                            customMesh = CreateCrossedQuadMesh();
                            psRenderer.alignment = ParticleSystemRenderSpace.Local;
                            break;
                        case 5:
                            psRenderer.renderMode = ParticleSystemRenderMode.Mesh;
                            customMesh = CreateTripleCrossMesh();
                            psRenderer.alignment = ParticleSystemRenderSpace.Local;
                            break;
                    }
                }
                else
                {
                    psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                    psRenderer.velocityScale = 0.5f;
                    psRenderer.lengthScale = 1f;
                }

                // Mesh'i diske kaydet
                if (customMesh != null)
                {
                    if (!needsMeshFolder)
                    {
                        EnsureFolderExists(meshesFolder);
                        needsMeshFolder = true;
                    }
                    string meshPath = $"{meshesFolder}/Mesh_Group{i}.asset";
                    AssetDatabase.CreateAsset(customMesh, meshPath);
                    psRenderer.mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                }

                // --- Material ---
                Material mat = CreateMaterial(particle);

                // Texture yukle (.png olarak ara)
                if (particle.TextureFiles.Count > 0)
                {
                    string texFileName = particle.TextureFiles[0].Replace("\\", "/");
                    string texName = Path.GetFileNameWithoutExtension(texFileName);

                    // Tum Assets icinde .png olarak ara
                    Texture2D tex = FindTexture(texName);
                    if (tex != null)
                        mat.mainTexture = tex;
                    else
                        Debug.LogWarning($"[MSE] Texture bulunamadi: '{texName}.png' (Assets icinde arandı)");
                }

                // Material'i diske kaydet
                string matPath = $"{materialsFolder}/Mat_Group{i}.mat";
                AssetDatabase.CreateAsset(mat, matPath);
                psRenderer.material = AssetDatabase.LoadAssetAtPath<Material>(matPath);

                // --- Color Over Lifetime ---
                bool hasColor = particle.TimeEventColorRed.Count > 0 ||
                                particle.TimeEventColorGreen.Count > 0 ||
                                particle.TimeEventColorBlue.Count > 0 ||
                                particle.TimeEventAlpha.Count > 0;
                if (hasColor)
                {
                    var col = ps.colorOverLifetime;
                    col.enabled = true;
                    col.color = new ParticleSystem.MinMaxGradient(BuildGradient(
                        particle.TimeEventColorRed, particle.TimeEventColorGreen,
                        particle.TimeEventColorBlue, particle.TimeEventAlpha));
                }

                // --- Size Over Lifetime ---
                if (particle.TimeEventScaleX.Count > 0 || particle.TimeEventScaleY.Count > 0)
                {
                    var sol = ps.sizeOverLifetime;
                    sol.enabled = true;
                    if (particle.TimeEventScaleX.Count > 0 && particle.TimeEventScaleY.Count > 0)
                    {
                        sol.separateAxes = true;
                        sol.x = new ParticleSystem.MinMaxCurve(1f,
                            TimeEventsToNormalizedCurve(particle.TimeEventScaleX));
                        sol.y = new ParticleSystem.MinMaxCurve(1f,
                            TimeEventsToNormalizedCurve(particle.TimeEventScaleY));
                    }
                    else if (particle.TimeEventScaleX.Count > 0)
                    {
                        sol.size = new ParticleSystem.MinMaxCurve(1f,
                            TimeEventsToNormalizedCurve(particle.TimeEventScaleX));
                    }
                }

                // --- Rotation ---
                ConfigureRotation(ps, particle);

                // --- Gravity ---
                if (particle.TimeEventGravity.Count == 1)
                    main.gravityModifier = particle.TimeEventGravity[0].Value * scaleFactor;
                else if (particle.TimeEventGravity.Count > 1)
                    main.gravityModifier = new ParticleSystem.MinMaxCurve(
                        GetMaxValue(particle.TimeEventGravity) * scaleFactor,
                        TimeEventsToAnimationCurve(particle.TimeEventGravity, scaleFactor));

                // --- Air Resistance ---
                if (particle.TimeEventAirResistance.Count > 0)
                {
                    var limit = ps.limitVelocityOverLifetime;
                    limit.enabled = true;
                    limit.dampen = particle.TimeEventAirResistance.Count == 1
                        ? particle.TimeEventAirResistance[0].Value
                        : GetMaxValue(particle.TimeEventAirResistance);
                }

                // --- Texture Sheet Animation ---
                ConfigureTextureAnimation(ps, particle);
            }

            // Prefab olarak kaydet (.mse'nin yanina)
            string prefabPath = $"{mseDir}/{effectName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(rootObj, prefabPath);
            DestroyImmediate(rootObj);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            statusMessage = $"Donusturuldu: {prefabPath} ({data.ParticleGroups.Count} grup)";
            Debug.Log($"[MSE] {statusMessage}");
            return true;
        }

        #region Texture Arama

        /// <summary>
        /// Assets icinde verilen isimle .png texture arar.
        /// </summary>
        private Texture2D FindTexture(string textureName)
        {
            // Oncelikle tam isimle ara
            string[] guids = AssetDatabase.FindAssets($"{textureName} t:Texture2D");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName.Equals(textureName, System.StringComparison.OrdinalIgnoreCase)
                    && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            // .png bulunamazsa herhangi bir formatta dene
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName.Equals(textureName, System.StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            return null;
        }

        #endregion

        #region Yardimci Metodlar

        private void EnsureFolderExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private Material CreateMaterial(MseParticleProperty particle)
        {
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Mobile/Particles/Additive");

            Material mat = new Material(shader);
            int src = particle.SrcBlendType;
            int dst = particle.DestBlendType;

            if (src == 5 && dst == 2)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 1f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.renderQueue = 3000;
            }
            else if (src == 5 && dst == 6)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.renderQueue = 3000;
            }
            else if (src == 2 && dst == 2)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 1f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.renderQueue = 3000;
            }
            else
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 1f);
                mat.renderQueue = 3000;
            }

            if (particle.ColorOperationType == 3)
                mat.mainTexture = Texture2D.whiteTexture;

            return mat;
        }

        private void ConfigureRotation(ParticleSystem ps, MseParticleProperty particle)
        {
            var main = ps.main;
            var rot = ps.rotationOverLifetime;

            switch (particle.RotationType)
            {
                case 0:
                    rot.enabled = false;
                    break;
                case 1:
                    rot.enabled = true;
                    if (particle.TimeEventRotation.Count > 0)
                        rot.z = new ParticleSystem.MinMaxCurve(Mathf.Deg2Rad,
                            TimeEventsToAnimationCurve(particle.TimeEventRotation));
                    break;
                case 2:
                    main.startRotation = new ParticleSystem.MinMaxCurve(
                        particle.RotationRandomStartBegin * Mathf.Deg2Rad,
                        particle.RotationRandomStartEnd * Mathf.Deg2Rad);
                    rot.enabled = true;
                    rot.z = new ParticleSystem.MinMaxCurve(-particle.RotationSpeed * Mathf.Deg2Rad);
                    break;
                case 3:
                    main.startRotation = new ParticleSystem.MinMaxCurve(
                        particle.RotationRandomStartBegin * Mathf.Deg2Rad,
                        particle.RotationRandomStartEnd * Mathf.Deg2Rad);
                    rot.enabled = true;
                    rot.z = new ParticleSystem.MinMaxCurve(particle.RotationSpeed * Mathf.Deg2Rad);
                    break;
                case 4:
                    main.startRotation = new ParticleSystem.MinMaxCurve(
                        particle.RotationRandomStartBegin * Mathf.Deg2Rad,
                        particle.RotationRandomStartEnd * Mathf.Deg2Rad);
                    rot.enabled = true;
                    rot.z = new ParticleSystem.MinMaxCurve(
                        -particle.RotationSpeed * Mathf.Deg2Rad,
                        particle.RotationSpeed * Mathf.Deg2Rad);
                    break;
            }
        }

        private void ConfigureTextureAnimation(ParticleSystem ps, MseParticleProperty particle)
        {
            var texSheet = ps.textureSheetAnimation;
            if (particle.TexAniType == 0) { texSheet.enabled = false; return; }

            texSheet.enabled = true;
            texSheet.mode = ParticleSystemAnimationMode.Grid;
            texSheet.numTilesX = particle.TexAniFrameCountX > 0 ? particle.TexAniFrameCountX : 1;
            texSheet.numTilesY = particle.TexAniFrameCountY > 0 ? particle.TexAniFrameCountY : 1;
            int totalFrames = texSheet.numTilesX * texSheet.numTilesY;

            switch (particle.TexAniType)
            {
                case 1:
                    texSheet.animation = ParticleSystemAnimationType.WholeSheet;
                    texSheet.cycleCount = 1;
                    texSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f,
                        AnimationCurve.Linear(0, 0, 1, 1));
                    if (particle.TexAniRandomStartFrameFlag)
                        texSheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);
                    break;
                case 2:
                    texSheet.animation = ParticleSystemAnimationType.WholeSheet;
                    texSheet.cycleCount = 1;
                    texSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f,
                        AnimationCurve.Linear(0, 1, 1, 0));
                    if (particle.TexAniRandomStartFrameFlag)
                        texSheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);
                    break;
                case 3:
                    texSheet.animation = ParticleSystemAnimationType.WholeSheet;
                    texSheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                    texSheet.startFrame = new ParticleSystem.MinMaxCurve(0f,
                        (float)(totalFrames - 1) / totalFrames);
                    break;
                case 4:
                    texSheet.animation = ParticleSystemAnimationType.WholeSheet;
                    texSheet.cycleCount = 1;
                    texSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f,
                        AnimationCurve.Linear(0, 0, 1, 1),
                        AnimationCurve.Linear(0, 1, 1, 0));
                    break;
            }

            if (particle.TexAniDelay > 0.001f)
            {
                texSheet.timeMode = ParticleSystemAnimationTimeMode.FPS;
                texSheet.fps = 1f / particle.TexAniDelay;
            }
        }

        private Gradient BuildGradient(List<MseTimeEvent> red, List<MseTimeEvent> green,
            List<MseTimeEvent> blue, List<MseTimeEvent> alpha)
        {
            var gradient = new Gradient();
            var allTimes = new SortedSet<float>();
            foreach (var e in red) allTimes.Add(e.Time);
            foreach (var e in green) allTimes.Add(e.Time);
            foreach (var e in blue) allTimes.Add(e.Time);
            foreach (var e in alpha) allTimes.Add(e.Time);

            var times = allTimes.ToList();
            if (times.Count == 0)
            {
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f) },
                    new[] { new GradientAlphaKey(1f, 0f) });
                return gradient;
            }

            if (times.Count > 8)
            {
                var sampled = new List<float> { times[0] };
                float step = (times[times.Count - 1] - times[0]) / 7f;
                for (int i = 1; i < 7; i++) sampled.Add(times[0] + step * i);
                sampled.Add(times[times.Count - 1]);
                times = sampled;
            }

            var colorKeys = new GradientColorKey[times.Count];
            var alphaKeys = new GradientAlphaKey[times.Count];
            for (int i = 0; i < times.Count; i++)
            {
                float t = times[i];
                float r = Mathf.Clamp01(SampleTimeEvents(red, t) / 255f);
                float g = Mathf.Clamp01(SampleTimeEvents(green, t) / 255f);
                float b = Mathf.Clamp01(SampleTimeEvents(blue, t) / 255f);
                float a = Mathf.Clamp01(SampleTimeEvents(alpha, t));
                colorKeys[i] = new GradientColorKey(new Color(r, g, b), t);
                alphaKeys[i] = new GradientAlphaKey(a, t);
            }
            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }

        private float SampleTimeEvents(List<MseTimeEvent> events, float time)
        {
            if (events.Count == 0) return 1f;
            if (events.Count == 1) return events[0].Value;
            if (time <= events[0].Time) return events[0].Value;
            if (time >= events[events.Count - 1].Time) return events[events.Count - 1].Value;
            for (int i = 0; i < events.Count - 1; i++)
            {
                if (time >= events[i].Time && time <= events[i + 1].Time)
                {
                    float t = (time - events[i].Time) / (events[i + 1].Time - events[i].Time);
                    return Mathf.Lerp(events[i].Value, events[i + 1].Value, t);
                }
            }
            return events[events.Count - 1].Value;
        }

        private AnimationCurve TimeEventsToAnimationCurve(List<MseTimeEvent> events, float scale = 1f)
        {
            var curve = new AnimationCurve();
            foreach (var e in events) curve.AddKey(new Keyframe(e.Time, e.Value * scale));
            return curve;
        }

        private AnimationCurve TimeEventsToNormalizedCurve(List<MseTimeEvent> events)
        {
            float maxVal = GetMaxValue(events);
            if (maxVal < 0.0001f) maxVal = 1f;
            var curve = new AnimationCurve();
            foreach (var e in events) curve.AddKey(new Keyframe(e.Time, e.Value / maxVal));
            return curve;
        }

        private float GetMaxValue(List<MseTimeEvent> events)
        {
            float max = float.MinValue;
            foreach (var e in events) if (e.Value > max) max = e.Value;
            return max > float.MinValue ? max : 0f;
        }

        #endregion

        #region Mesh Olusturma

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh { name = "ParticleQuad" };
            mesh.vertices = new[] {
                new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0)
            };
            mesh.uv = new[] {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh CreateCrossedQuadMesh()
        {
            var mesh = new Mesh { name = "CrossedQuad" };
            mesh.vertices = new[] {
                new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0),
                new Vector3(0, -0.5f, -0.5f), new Vector3(0, -0.5f, 0.5f),
                new Vector3(0, 0.5f, 0.5f), new Vector3(0, 0.5f, -0.5f)
            };
            mesh.uv = new[] {
                new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
                new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1)
            };
            mesh.triangles = new[] {
                0,2,1, 0,3,2, 0,1,2, 0,2,3,
                4,6,5, 4,7,6, 4,5,6, 4,6,7
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh CreateTripleCrossMesh()
        {
            var mesh = new Mesh { name = "TripleCross" };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int p = 0; p < 3; p++)
            {
                float angle = p * 60f * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                int b = p * 4;
                verts.Add(new Vector3(-0.5f * cos, -0.5f, -0.5f * sin));
                verts.Add(new Vector3(0.5f * cos, -0.5f, 0.5f * sin));
                verts.Add(new Vector3(0.5f * cos, 0.5f, 0.5f * sin));
                verts.Add(new Vector3(-0.5f * cos, 0.5f, -0.5f * sin));
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                tris.AddRange(new[] { b, b+2, b+1, b, b+3, b+2, b, b+1, b+2, b, b+2, b+3 });
            }
            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            return mesh;
        }

        #endregion
    }
}
#endif
