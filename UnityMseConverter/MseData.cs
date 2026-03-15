using System;
using System.Collections.Generic;

#if UNITY_EDITOR || UNITY_STANDALONE
using UnityEngine;
#endif

namespace UnityMseConverter
{
    /// <summary>
    /// Metin2 .mse efekt dosyasının tüm verilerini tutan ana sınıf.
    /// </summary>
    public class MseEffectData
    {
        public float BoundingSphereRadius { get; set; }
        public MseVector3 BoundingSpherePosition { get; set; } = new MseVector3();
        public List<MseParticleGroup> ParticleGroups { get; set; } = new List<MseParticleGroup>();
    }

    /// <summary>
    /// Tek bir parçacık grubu: pozisyon keyframe'leri, emitter ve particle özellikleri.
    /// </summary>
    public class MseParticleGroup
    {
        public float StartTime { get; set; }
        public List<MsePositionKeyframe> PositionKeyframes { get; set; } = new List<MsePositionKeyframe>();
        public MseEmitterProperty EmitterProperty { get; set; } = new MseEmitterProperty();
        public MseParticleProperty ParticleProperty { get; set; } = new MseParticleProperty();
    }

    public class MsePositionKeyframe
    {
        public float Time { get; set; }
        public string MovingType { get; set; } = "MOVING_TYPE_DIRECT";
        public MseVector3 Position { get; set; } = new MseVector3();
    }

    /// <summary>
    /// Emitter shape, yayılım ve zamana bağlı özellikler.
    /// </summary>
    public class MseEmitterProperty
    {
        public int MaxEmissionCount { get; set; } = 10;
        public float CycleLength { get; set; } = 1.0f;
        public bool CycleLoopEnable { get; set; } = false;
        public int LoopCount { get; set; } = 0;

        // Emitter shape: 0=Point, 1=Ellipse, 2=Square, 3=Sphere
        public int EmitterShape { get; set; } = 0;

        // Advanced type: 0=Free, 1=Outer, 2=Inner
        public int EmitterAdvancedType { get; set; } = 0;

        public MseVector3 EmittingSize { get; set; } = new MseVector3();
        public float EmittingRadius { get; set; } = 0f;

        public MseVector3 EmittingDirection { get; set; } = new MseVector3();
        public bool EmitFromEdgeFlag { get; set; } = false;

        // Time events
        public List<MseTimeEvent> TimeEventEmittingVelocity { get; set; } = new List<MseTimeEvent>();
        public List<MseTimeEvent> TimeEventEmissionCountPerSecond { get; set; } = new List<MseTimeEvent>();
        public List<MseTimeEvent> TimeEventLifeTime { get; set; } = new List<MseTimeEvent>();
        public List<MseTimeEvent> TimeEventSizeX { get; set; } = new List<MseTimeEvent>();
        public List<MseTimeEvent> TimeEventSizeY { get; set; } = new List<MseTimeEvent>();
    }

    /// <summary>
    /// Parçacığın görünüm, hareket ve animasyon özellikleri.
    /// </summary>
    public class MseParticleProperty
    {
        // Blend: D3DBLEND enum değerleri (5=SRCALPHA, 2=ONE, 6=INVSRCALPHA)
        public int SrcBlendType { get; set; } = 5;
        public int DestBlendType { get; set; } = 2;

        // ColorOperationType: D3DTOP enum (4=MODULATE)
        public int ColorOperationType { get; set; } = 4;

        // Billboard: 0=None, 1=All, 2=Y, 3=Lie, 4=2Face, 5=3Face
        public int BillboardType { get; set; } = 1;

        // Rotation: 0=None, 1=TimeEvent, 2=CW, 3=CCW, 4=RandomDirection
        public int RotationType { get; set; } = 0;
        public float RotationSpeed { get; set; } = 0f;
        public float RotationRandomStartBegin { get; set; } = 0f;
        public float RotationRandomStartEnd { get; set; } = 0f;

        public bool AttachEnable { get; set; } = false;
        public bool StretchEnable { get; set; } = false;

        // Texture Animation: 0=None, 1=CW, 2=CCW, 3=RandomFrame, 4=RandomDirection
        public int TexAniType { get; set; } = 0;
        public float TexAniDelay { get; set; } = 0f;
        public bool TexAniRandomStartFrameFlag { get; set; } = false;

        // Time events
        public List<MseTimeEvent> TimeEventGravity { get; set; } = new List<MseTimeEvent>();
        public List<MseTimeEvent> TimeEventAirResistance { get; set; } = new List<MseTimeEvent>();
        public List<MseTimeEvent> TimeEventScaleX { get; set; } = new List<MseTimeEvent>();
        public List<MseTimeEvent> TimeEventScaleY { get; set; } = new List<MseTimeEvent>();
        public List<MseTimeEvent> TimeEventColorRed { get; set; } = new List<MseTimeEvent>();
        public List<MseTimeEvent> TimeEventColorGreen { get; set; } = new List<MseTimeEvent>();
        public List<MseTimeEvent> TimeEventColorBlue { get; set; } = new List<MseTimeEvent>();
        public List<MseTimeEvent> TimeEventAlpha { get; set; } = new List<MseTimeEvent>();
        public List<MseTimeEvent> TimeEventRotation { get; set; } = new List<MseTimeEvent>();

        // Texture dosyaları
        public List<string> TextureFiles { get; set; } = new List<string>();

        // Texture grid boyutları (çoklu frame için)
        public int TexAniFrameCountX { get; set; } = 1;
        public int TexAniFrameCountY { get; set; } = 1;
    }

    /// <summary>
    /// Zamana bağlı tek bir değer çifti (0.0 - 1.0 arası normalize zaman).
    /// </summary>
    public class MseTimeEvent
    {
        public float Time { get; set; }
        public float Value { get; set; }

        public MseTimeEvent() { }

        public MseTimeEvent(float time, float value)
        {
            Time = time;
            Value = value;
        }
    }

    /// <summary>
    /// Unity bağımsız 3D vektör (UnityEngine.Vector3 olmadan da çalışır).
    /// </summary>
    public class MseVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public MseVector3() { }

        public MseVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        public Vector3 ToUnityVector3()
        {
            return new Vector3(X, Y, Z);
        }

        public static MseVector3 FromUnityVector3(Vector3 v)
        {
            return new MseVector3(v.x, v.y, v.z);
        }
#endif
    }
}
