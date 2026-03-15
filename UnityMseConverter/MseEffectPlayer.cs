#if UNITY_EDITOR || UNITY_STANDALONE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UnityMseConverter
{
    /// <summary>
    /// Metin2 .mse efekt dosyasını Unity ParticleSystem'lere dönüştüren MonoBehaviour.
    /// Inspector'dan MseFilePath atayıp Play modunda efekti görüntüleyebilirsiniz.
    /// </summary>
    public class MseEffectPlayer : MonoBehaviour
    {
        [Header("MSE Dosya Yolu (Assets/ altında)")]
        public string MseFilePath;

        [Header("Texture Klasör Yolu (Assets/ altında)")]
        public string TextureBasePath = "Textures/Effects";

        [Header("Ölçek Faktörü (Metin2 birimlerini Unity'ye çevirir)")]
        public float ScaleFactor = 0.01f;

        private MseEffectData effectData;
        private List<ParticleSystem> createdSystems = new List<ParticleSystem>();

        void Start()
        {
            if (!string.IsNullOrEmpty(MseFilePath))
            {
                LoadAndPlay(MseFilePath);
            }
        }

        /// <summary>
        /// MSE dosyasını yükler ve ParticleSystem'leri oluşturur.
        /// </summary>
        public void LoadAndPlay(string filePath)
        {
            ClearExisting();

            effectData = MseParser.ParseFromFile(filePath);

            for (int i = 0; i < effectData.ParticleGroups.Count; i++)
            {
                var group = effectData.ParticleGroups[i];
                CreateParticleSystemFromGroup(group, i);
            }
        }

        /// <summary>
        /// String içerikten MSE verisi yükler.
        /// </summary>
        public void LoadAndPlayFromString(string mseContent)
        {
            ClearExisting();

            effectData = MseParser.ParseFromString(mseContent);

            for (int i = 0; i < effectData.ParticleGroups.Count; i++)
            {
                var group = effectData.ParticleGroups[i];
                CreateParticleSystemFromGroup(group, i);
            }
        }

        private void ClearExisting()
        {
            foreach (var ps in createdSystems)
            {
                if (ps != null)
                    DestroyImmediate(ps.gameObject);
            }
            createdSystems.Clear();
        }

        private void CreateParticleSystemFromGroup(MseParticleGroup group, int groupIndex)
        {
            var childObj = new GameObject($"ParticleGroup_{groupIndex}");
            childObj.transform.SetParent(transform, false);

            var ps = childObj.AddComponent<ParticleSystem>();
            var renderer = childObj.GetComponent<ParticleSystemRenderer>();
            createdSystems.Add(ps);

            var emitter = group.EmitterProperty;
            var particle = group.ParticleProperty;

            // --- Main Module ---
            ConfigureMainModule(ps, group);

            // --- Emission Module ---
            ConfigureEmission(ps, emitter);

            // --- Shape Module ---
            ConfigureShape(ps, emitter);

            // --- Renderer (Billboard, Blend, Material) ---
            ConfigureRenderer(ps, renderer, particle);

            // --- Size Over Lifetime ---
            ConfigureSizeOverLifetime(ps, particle);

            // --- Color Over Lifetime ---
            ConfigureColorOverLifetime(ps, particle);

            // --- Rotation ---
            ConfigureRotation(ps, particle);

            // --- Gravity & Air Resistance ---
            ConfigureForces(ps, particle);

            // --- Texture Sheet Animation ---
            ConfigureTextureAnimation(ps, particle);

            // --- Stretch ---
            if (particle.StretchEnable)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.5f;
                renderer.lengthScale = 1f;
            }

            // --- Pozisyon Animasyonu ---
            if (group.PositionKeyframes.Count > 0)
            {
                StartCoroutine(AnimatePosition(childObj.transform, group.PositionKeyframes));
            }
        }

        #region Main Module

        private void ConfigureMainModule(ParticleSystem ps, MseParticleGroup group)
        {
            var main = ps.main;
            var emitter = group.EmitterProperty;

            main.startDelay = group.StartTime;
            main.duration = emitter.CycleLength;
            main.loop = emitter.CycleLoopEnable;
            main.maxParticles = emitter.MaxEmissionCount;
            main.playOnAwake = true;
            main.simulationSpace = group.ParticleProperty.AttachEnable
                ? ParticleSystemSimulationSpace.Local
                : ParticleSystemSimulationSpace.World;

            // StartLifetime: TimeEventLifeTime'dan ilk değeri al
            if (emitter.TimeEventLifeTime.Count > 0)
            {
                if (emitter.TimeEventLifeTime.Count == 1)
                {
                    main.startLifetime = emitter.TimeEventLifeTime[0].Value;
                }
                else
                {
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1f,
                        TimeEventsToAnimationCurve(emitter.TimeEventLifeTime));
                }
            }

            // StartSpeed: TimeEventEmittingVelocity'den
            if (emitter.TimeEventEmittingVelocity.Count > 0)
            {
                if (emitter.TimeEventEmittingVelocity.Count == 1)
                {
                    main.startSpeed = emitter.TimeEventEmittingVelocity[0].Value * ScaleFactor;
                }
                else
                {
                    main.startSpeed = new ParticleSystem.MinMaxCurve(
                        GetMaxValue(emitter.TimeEventEmittingVelocity) * ScaleFactor,
                        TimeEventsToAnimationCurve(emitter.TimeEventEmittingVelocity, ScaleFactor));
                }
            }

            // StartSize: TimeEventSizeX/Y'den
            if (emitter.TimeEventSizeX.Count > 0 || emitter.TimeEventSizeY.Count > 0)
            {
                main.startSize3D = true;

                if (emitter.TimeEventSizeX.Count > 0)
                {
                    float maxSizeX = GetMaxValue(emitter.TimeEventSizeX) * ScaleFactor;
                    main.startSizeX = emitter.TimeEventSizeX.Count == 1
                        ? new ParticleSystem.MinMaxCurve(emitter.TimeEventSizeX[0].Value * ScaleFactor)
                        : new ParticleSystem.MinMaxCurve(maxSizeX,
                            TimeEventsToAnimationCurve(emitter.TimeEventSizeX, ScaleFactor));
                }

                if (emitter.TimeEventSizeY.Count > 0)
                {
                    float maxSizeY = GetMaxValue(emitter.TimeEventSizeY) * ScaleFactor;
                    main.startSizeY = emitter.TimeEventSizeY.Count == 1
                        ? new ParticleSystem.MinMaxCurve(emitter.TimeEventSizeY[0].Value * ScaleFactor)
                        : new ParticleSystem.MinMaxCurve(maxSizeY,
                            TimeEventsToAnimationCurve(emitter.TimeEventSizeY, ScaleFactor));
                }
            }
        }

        #endregion

        #region Emission

        private void ConfigureEmission(ParticleSystem ps, MseEmitterProperty emitter)
        {
            var emission = ps.emission;
            emission.enabled = true;

            if (emitter.TimeEventEmissionCountPerSecond.Count > 0)
            {
                if (emitter.TimeEventEmissionCountPerSecond.Count == 1)
                {
                    emission.rateOverTime = emitter.TimeEventEmissionCountPerSecond[0].Value;
                }
                else
                {
                    float maxRate = GetMaxValue(emitter.TimeEventEmissionCountPerSecond);
                    emission.rateOverTime = new ParticleSystem.MinMaxCurve(maxRate,
                        TimeEventsToAnimationCurve(emitter.TimeEventEmissionCountPerSecond));
                }
            }
        }

        #endregion

        #region Shape

        private void ConfigureShape(ParticleSystem ps, MseEmitterProperty emitter)
        {
            var shape = ps.shape;
            shape.enabled = true;

            switch (emitter.EmitterShape)
            {
                case 0: // POINT
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.001f;
                    break;

                case 1: // ELLIPSE
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 1f;
                    shape.scale = emitter.EmittingSize.ToUnityVector3() * ScaleFactor;
                    break;

                case 2: // SQUARE
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = emitter.EmittingSize.ToUnityVector3() * ScaleFactor;
                    break;

                case 3: // SPHERE
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = emitter.EmittingRadius * ScaleFactor;
                    break;
            }

            // Emit from edge
            if (emitter.EmitFromEdgeFlag)
            {
                if (emitter.EmitterShape == 3 || emitter.EmitterShape == 1)
                    shape.radiusThickness = 0f; // surface only
            }

            // Emitting direction → shape rotation
            if (emitter.EmittingDirection.X != 0 || emitter.EmittingDirection.Y != 0 || emitter.EmittingDirection.Z != 0)
            {
                shape.rotation = emitter.EmittingDirection.ToUnityVector3();
            }

            // Advanced type: radial velocity
            if (emitter.EmitterAdvancedType != 0)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;

                switch (emitter.EmitterAdvancedType)
                {
                    case 1: // OUTER
                        vel.radial = new ParticleSystem.MinMaxCurve(2f);
                        break;
                    case 2: // INNER
                        vel.radial = new ParticleSystem.MinMaxCurve(-2f);
                        break;
                }
            }
        }

        #endregion

        #region Renderer (Billboard + Blend + Material)

        private void ConfigureRenderer(ParticleSystem ps, ParticleSystemRenderer renderer, MseParticleProperty particle)
        {
            // Billboard type
            if (!particle.StretchEnable) // Stretch overrides billboard
            {
                switch (particle.BillboardType)
                {
                    case 0: // NONE
                        renderer.renderMode = ParticleSystemRenderMode.Mesh;
                        renderer.mesh = CreateQuadMesh();
                        renderer.alignment = ParticleSystemRenderSpace.Local;
                        break;

                    case 1: // ALL (default full billboard)
                        renderer.renderMode = ParticleSystemRenderMode.Billboard;
                        renderer.alignment = ParticleSystemRenderSpace.View;
                        break;

                    case 2: // Y-axis
                        renderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
                        break;

                    case 3: // LIE (horizontal)
                        renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                        break;

                    case 4: // 2FACE
                        renderer.renderMode = ParticleSystemRenderMode.Mesh;
                        renderer.mesh = CreateCrossedQuadMesh();
                        renderer.alignment = ParticleSystemRenderSpace.Local;
                        break;

                    case 5: // 3FACE
                        renderer.renderMode = ParticleSystemRenderMode.Mesh;
                        renderer.mesh = CreateTripleCrossMesh();
                        renderer.alignment = ParticleSystemRenderSpace.Local;
                        break;
                }
            }

            // Material & Blend mode
            Material mat = CreateMaterial(particle);
            renderer.material = mat;

            // Texture yükleme
            if (particle.TextureFiles.Count > 0)
            {
                string textureName = Path.GetFileNameWithoutExtension(particle.TextureFiles[0]);
                string texPath = $"{TextureBasePath}/{textureName}";
                Texture2D tex = Resources.Load<Texture2D>(texPath);
                if (tex != null)
                {
                    mat.mainTexture = tex;
                }
                else
                {
                    Debug.LogWarning($"[MseEffectPlayer] Texture bulunamadı: {texPath}. " +
                        $"Orijinal dosya adı: {particle.TextureFiles[0]}");
                }
            }
        }

        private Material CreateMaterial(MseParticleProperty particle)
        {
            Material mat;

            int src = particle.SrcBlendType;
            int dst = particle.DestBlendType;

            if (src == 5 && dst == 2)
            {
                // SRCALPHA / ONE = Additive with alpha (en yaygın)
                mat = new Material(Shader.Find("Particles/Standard Unlit"));
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetFloat("_Blend", 1f);   // Additive
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.renderQueue = 3000;
            }
            else if (src == 5 && dst == 6)
            {
                // SRCALPHA / INVSRCALPHA = Standard alpha blend
                mat = new Material(Shader.Find("Particles/Standard Unlit"));
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);   // Alpha blend
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.renderQueue = 3000;
            }
            else if (src == 2 && dst == 2)
            {
                // ONE / ONE = Pure additive
                mat = new Material(Shader.Find("Particles/Standard Unlit"));
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 1f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.renderQueue = 3000;
            }
            else
            {
                // Fallback: additive
                mat = new Material(Shader.Find("Particles/Standard Unlit"));
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 1f);
                mat.renderQueue = 3000;
            }

            // ColorOperationType
            // 4 (MODULATE) = default behavior, no extra setup needed
            // 5 (MODULATE2X) = would need HDR or custom shader
            if (particle.ColorOperationType == 3)
            {
                // SELECTARG2 = vertex color only, ignore texture
                mat.mainTexture = Texture2D.whiteTexture;
            }

            return mat;
        }

        #endregion

        #region Size Over Lifetime

        private void ConfigureSizeOverLifetime(ParticleSystem ps, MseParticleProperty particle)
        {
            if (particle.TimeEventScaleX.Count == 0 && particle.TimeEventScaleY.Count == 0)
                return;

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

        #endregion

        #region Color Over Lifetime

        private void ConfigureColorOverLifetime(ParticleSystem ps, MseParticleProperty particle)
        {
            bool hasColor = particle.TimeEventColorRed.Count > 0 ||
                            particle.TimeEventColorGreen.Count > 0 ||
                            particle.TimeEventColorBlue.Count > 0 ||
                            particle.TimeEventAlpha.Count > 0;

            if (!hasColor) return;

            var col = ps.colorOverLifetime;
            col.enabled = true;

            Gradient gradient = BuildGradient(
                particle.TimeEventColorRed,
                particle.TimeEventColorGreen,
                particle.TimeEventColorBlue,
                particle.TimeEventAlpha
            );

            col.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private Gradient BuildGradient(List<MseTimeEvent> red, List<MseTimeEvent> green,
            List<MseTimeEvent> blue, List<MseTimeEvent> alpha)
        {
            var gradient = new Gradient();

            // Tüm benzersiz zaman noktalarını topla
            var allTimes = new SortedSet<float>();
            foreach (var e in red) allTimes.Add(e.Time);
            foreach (var e in green) allTimes.Add(e.Time);
            foreach (var e in blue) allTimes.Add(e.Time);
            foreach (var e in alpha) allTimes.Add(e.Time);

            var times = allTimes.ToList();

            // Unity Gradient max 8 color key, 8 alpha key
            // Gerekirse sample sayısını azalt
            if (times.Count > 8)
            {
                var sampled = new List<float> { times[0] };
                float step = (times[times.Count - 1] - times[0]) / 7f;
                for (int i = 1; i < 7; i++)
                    sampled.Add(times[0] + step * i);
                sampled.Add(times[times.Count - 1]);
                times = sampled;
            }

            var colorKeys = new GradientColorKey[times.Count];
            var alphaKeys = new GradientAlphaKey[times.Count];

            for (int i = 0; i < times.Count; i++)
            {
                float t = times[i];
                float r = SampleTimeEvents(red, t) / 255f;
                float g = SampleTimeEvents(green, t) / 255f;
                float b = SampleTimeEvents(blue, t) / 255f;
                float a = SampleTimeEvents(alpha, t);

                r = Mathf.Clamp01(r);
                g = Mathf.Clamp01(g);
                b = Mathf.Clamp01(b);
                a = Mathf.Clamp01(a);

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

            // Verilen zamandaki değeri lineer interpolasyon ile bul
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

        #endregion

        #region Rotation

        private void ConfigureRotation(ParticleSystem ps, MseParticleProperty particle)
        {
            var main = ps.main;
            var rot = ps.rotationOverLifetime;

            switch (particle.RotationType)
            {
                case 0: // NONE
                    main.startRotation = 0f;
                    rot.enabled = false;
                    break;

                case 1: // TIME_EVENT
                    rot.enabled = true;
                    if (particle.TimeEventRotation.Count > 0)
                    {
                        rot.z = new ParticleSystem.MinMaxCurve(
                            Mathf.Deg2Rad,
                            TimeEventsToAnimationCurve(particle.TimeEventRotation));
                    }
                    break;

                case 2: // CW
                    main.startRotation = new ParticleSystem.MinMaxCurve(
                        particle.RotationRandomStartBegin * Mathf.Deg2Rad,
                        particle.RotationRandomStartEnd * Mathf.Deg2Rad);
                    rot.enabled = true;
                    rot.z = new ParticleSystem.MinMaxCurve(-particle.RotationSpeed * Mathf.Deg2Rad);
                    break;

                case 3: // CCW
                    main.startRotation = new ParticleSystem.MinMaxCurve(
                        particle.RotationRandomStartBegin * Mathf.Deg2Rad,
                        particle.RotationRandomStartEnd * Mathf.Deg2Rad);
                    rot.enabled = true;
                    rot.z = new ParticleSystem.MinMaxCurve(particle.RotationSpeed * Mathf.Deg2Rad);
                    break;

                case 4: // RANDOM_DIRECTION
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

        #endregion

        #region Forces (Gravity & Air Resistance)

        private void ConfigureForces(ParticleSystem ps, MseParticleProperty particle)
        {
            var main = ps.main;

            // Gravity
            if (particle.TimeEventGravity.Count > 0)
            {
                if (particle.TimeEventGravity.Count == 1)
                {
                    main.gravityModifier = particle.TimeEventGravity[0].Value * ScaleFactor;
                }
                else
                {
                    float maxGrav = GetMaxValue(particle.TimeEventGravity) * ScaleFactor;
                    main.gravityModifier = new ParticleSystem.MinMaxCurve(maxGrav,
                        TimeEventsToAnimationCurve(particle.TimeEventGravity, ScaleFactor));
                }
            }

            // Air Resistance → Limit Velocity Over Lifetime dampen
            if (particle.TimeEventAirResistance.Count > 0)
            {
                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.separateAxes = false;

                if (particle.TimeEventAirResistance.Count == 1)
                {
                    limit.dampen = particle.TimeEventAirResistance[0].Value;
                }
                else
                {
                    // Dampen değeri 0-1 arası, en yüksek değeri kullan
                    limit.dampen = GetMaxValue(particle.TimeEventAirResistance);
                }
            }
        }

        #endregion

        #region Texture Sheet Animation

        private void ConfigureTextureAnimation(ParticleSystem ps, MseParticleProperty particle)
        {
            var texSheet = ps.textureSheetAnimation;

            if (particle.TexAniType == 0)
            {
                texSheet.enabled = false;
                return;
            }

            texSheet.enabled = true;
            texSheet.mode = ParticleSystemAnimationMode.Grid;
            texSheet.numTilesX = particle.TexAniFrameCountX > 0 ? particle.TexAniFrameCountX : 1;
            texSheet.numTilesY = particle.TexAniFrameCountY > 0 ? particle.TexAniFrameCountY : 1;

            int totalFrames = texSheet.numTilesX * texSheet.numTilesY;

            switch (particle.TexAniType)
            {
                case 1: // CW (forward sequential)
                    texSheet.animation = ParticleSystemAnimationType.WholeSheet;
                    texSheet.cycleCount = 1;
                    texSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f,
                        AnimationCurve.Linear(0, 0, 1, 1));
                    if (particle.TexAniRandomStartFrameFlag)
                        texSheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);
                    break;

                case 2: // CCW (reverse sequential)
                    texSheet.animation = ParticleSystemAnimationType.WholeSheet;
                    texSheet.cycleCount = 1;
                    texSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f,
                        AnimationCurve.Linear(0, 1, 1, 0));
                    if (particle.TexAniRandomStartFrameFlag)
                        texSheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);
                    break;

                case 3: // RANDOM_FRAME
                    texSheet.animation = ParticleSystemAnimationType.WholeSheet;
                    texSheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                    texSheet.startFrame = new ParticleSystem.MinMaxCurve(0f,
                        (float)(totalFrames - 1) / totalFrames);
                    break;

                case 4: // RANDOM_DIRECTION
                    texSheet.animation = ParticleSystemAnimationType.WholeSheet;
                    texSheet.cycleCount = 1;
                    texSheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f,
                        AnimationCurve.Linear(0, 0, 1, 1),
                        AnimationCurve.Linear(0, 1, 1, 0));
                    break;
            }

            // Frame delay → FPS mode
            if (particle.TexAniDelay > 0.001f)
            {
                texSheet.timeMode = ParticleSystemAnimationTimeMode.FPS;
                texSheet.fps = 1f / particle.TexAniDelay;
            }
        }

        #endregion

        #region Pozisyon Animasyonu

        private IEnumerator AnimatePosition(Transform target, List<MsePositionKeyframe> keyframes)
        {
            if (keyframes.Count == 0) yield break;

            // İlk pozisyonu ayarla
            target.localPosition = keyframes[0].Position.ToUnityVector3() * ScaleFactor;

            for (int i = 0; i < keyframes.Count - 1; i++)
            {
                var from = keyframes[i];
                var to = keyframes[i + 1];
                float duration = to.Time - from.Time;

                if (duration <= 0) continue;

                Vector3 startPos = from.Position.ToUnityVector3() * ScaleFactor;
                Vector3 endPos = to.Position.ToUnityVector3() * ScaleFactor;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    float t = elapsed / duration;
                    target.localPosition = Vector3.Lerp(startPos, endPos, t);
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                target.localPosition = endPos;
            }
        }

        #endregion

        #region Mesh Oluşturma

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh { name = "ParticleQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0)
            };
            mesh.uv = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Mesh CreateCrossedQuadMesh()
        {
            var mesh = new Mesh { name = "CrossedQuad" };
            mesh.vertices = new[]
            {
                // Quad 1: XY plane
                new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0),
                // Quad 2: ZY plane
                new Vector3(0, -0.5f, -0.5f), new Vector3(0, -0.5f, 0.5f),
                new Vector3(0, 0.5f, 0.5f), new Vector3(0, 0.5f, -0.5f)
            };
            mesh.uv = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,   // Quad 1 front
                0, 1, 2, 0, 2, 3,   // Quad 1 back
                4, 6, 5, 4, 7, 6,   // Quad 2 front
                4, 5, 6, 4, 6, 7    // Quad 2 back
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

            // 3 düzlem, 60 derece arayla
            for (int p = 0; p < 3; p++)
            {
                float angle = p * 60f * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                int baseIdx = p * 4;
                verts.Add(new Vector3(-0.5f * cos, -0.5f, -0.5f * sin));
                verts.Add(new Vector3(0.5f * cos, -0.5f, 0.5f * sin));
                verts.Add(new Vector3(0.5f * cos, 0.5f, 0.5f * sin));
                verts.Add(new Vector3(-0.5f * cos, 0.5f, -0.5f * sin));

                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(0, 1));

                // Front & back
                tris.AddRange(new[] { baseIdx, baseIdx + 2, baseIdx + 1, baseIdx, baseIdx + 3, baseIdx + 2 });
                tris.AddRange(new[] { baseIdx, baseIdx + 1, baseIdx + 2, baseIdx, baseIdx + 2, baseIdx + 3 });
            }

            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            return mesh;
        }

        #endregion

        #region Yardımcı Curve Oluşturma

        private AnimationCurve TimeEventsToAnimationCurve(List<MseTimeEvent> events, float scale = 1f)
        {
            var curve = new AnimationCurve();
            foreach (var e in events)
            {
                curve.AddKey(new Keyframe(e.Time, e.Value * scale));
            }
            return curve;
        }

        /// <summary>
        /// TimeEvent değerlerini 0-1 arasına normalize eder (SizeOverLifetime için).
        /// </summary>
        private AnimationCurve TimeEventsToNormalizedCurve(List<MseTimeEvent> events)
        {
            float maxVal = GetMaxValue(events);
            if (maxVal < 0.0001f) maxVal = 1f;

            var curve = new AnimationCurve();
            foreach (var e in events)
            {
                curve.AddKey(new Keyframe(e.Time, e.Value / maxVal));
            }
            return curve;
        }

        private float GetMaxValue(List<MseTimeEvent> events)
        {
            float max = float.MinValue;
            foreach (var e in events)
            {
                if (e.Value > max) max = e.Value;
            }
            return max > float.MinValue ? max : 0f;
        }

        #endregion
    }
}
#endif
