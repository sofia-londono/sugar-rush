using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SugarRush
{
    /// <summary>
    /// Local split screen (two players on one machine): a second chase camera, side by side,
    /// plus automatic savings because the track is drawn twice — lower render scale, no track
    /// shadows, shorter draw distance hidden by pastel fog, and a 30 FPS cap.
    /// One player on "Performance" graphics gets the same savings, milder: 85% render scale,
    /// no track shadows, a shorter draw distance and a 60 FPS cap.
    /// Everything is restored when the race scene closes.
    /// </summary>
    public partial class RaceManager
    {
        [Header("Split screen")]
        public float splitRenderScale = 0.7f;
        public float splitDrawDistance = 320f;
        public float splitShadowDistance = 35f;
        public int splitFrameRate = 30;
        public Color splitFogColor = new(0.86f, 0.9f, 1f);

        [Header("One player, Performance graphics")]
        public float performanceRenderScale = 0.85f;
        public float performanceDrawDistance = 420f;
        [Tooltip("Steady 60 instead of 70-110 FPS: keeps a laptop cooler, so it doesn't throttle mid-race.")]
        public int performanceFrameRate = 60;

        public KartCamera SecondCamera { get; private set; }

        bool lowSpecActive;
        // A track can bring its own fog (spline tracks: pastel haze in its colour); it is kept, only shortened.
        bool sceneFog;
        Color sceneFogColor;
        float sceneFogStart, sceneFogEnd;

        void SaveSceneFog()
        {
            sceneFog = RenderSettings.fog;
            sceneFogColor = RenderSettings.fogColor;
            sceneFogStart = RenderSettings.fogStartDistance;
            sceneFogEnd = RenderSettings.fogEndDistance;
        }
        float savedRenderScale, savedShadowDistance;
        int savedFrameRate, savedVSync;

        /// <summary>Graphics option 0 ("Rendimiento"); called for one-player races, offline or online.</summary>
        void SetupPerformanceMode()
        {
            if (GameSettings.Quality != 0 || !kartCamera) return;
            var cam = kartCamera.GetComponent<Camera>();
            ApplyLowSpec(new[] { cam }, performanceDrawDistance, performanceRenderScale, float.MaxValue, performanceFrameRate);
        }

        void SetupSplitScreen()
        {
            var cam1 = kartCamera.GetComponent<Camera>();

            var secondGo = Instantiate(kartCamera.gameObject);
            secondGo.name = "Main Camera P2";
            secondGo.tag = "Untagged";
            Destroy(secondGo.GetComponent<AudioListener>()); // one listener: player 1's camera
            SecondCamera = secondGo.GetComponent<KartCamera>();
            SecondCamera.target = LocalPlayers[1].Kart;
            SecondCamera.SnapToTarget();

            var cam2 = secondGo.GetComponent<Camera>();
            cam1.rect = new Rect(0f, 0f, 0.5f, 1f);
            cam2.rect = new Rect(0.5f, 0f, 0.5f, 1f);
            // Half-width views are tall: open the FOV so each player sees about as much road.
            kartCamera.baseFov = SecondCamera.baseFov = 74f;

            ApplyLowSpec(new[] { cam1, cam2 }, splitDrawDistance, splitRenderScale, splitShadowDistance, splitFrameRate);
        }

        /// <summary>Shorter draw distance hidden by fog, no track shadows, lower render scale and an optional frame cap.</summary>
        void ApplyLowSpec(Camera[] cams, float drawDistance, float renderScale, float shadowDistance, int frameRate)
        {
            lowSpecActive = true;
            Color fogColor = sceneFog ? sceneFogColor : splitFogColor;
            float fogEnd = sceneFog ? Mathf.Min(sceneFogEnd, drawDistance * 0.95f) : drawDistance * 0.95f;
            float fogStart = sceneFog ? Mathf.Min(sceneFogStart, fogEnd * 0.5f) : drawDistance * 0.45f;
            foreach (var cam in cams)
            {
                cam.farClipPlane = Mathf.Min(drawDistance, fogEnd + 20f);
                cam.backgroundColor = fogColor;
                cam.clearFlags = CameraClearFlags.SolidColor;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            // Track pieces stop casting shadows (karts still do, and the road still receives them).
            var track = GameObject.Find("Track");
            if (track)
                foreach (var r in track.GetComponentsInChildren<MeshRenderer>())
                    r.shadowCastingMode = ShadowCastingMode.Off;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                savedRenderScale = urp.renderScale;
                savedShadowDistance = urp.shadowDistance;
                urp.renderScale = renderScale;
                urp.shadowDistance = Mathf.Min(urp.shadowDistance, shadowDistance);
            }

            savedFrameRate = Application.targetFrameRate;
            savedVSync = QualitySettings.vSyncCount;
            if (frameRate > 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = frameRate;
            }
        }

        /// <summary>Put the global settings back (the URP asset is shared with every scene).</summary>
        void RestoreSplitScreen()
        {
            if (!lowSpecActive) return;
            lowSpecActive = false;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = savedRenderScale;
                urp.shadowDistance = savedShadowDistance;
            }
            Application.targetFrameRate = savedFrameRate;
            QualitySettings.vSyncCount = savedVSync;
            RenderSettings.fog = sceneFog;
            RenderSettings.fogColor = sceneFogColor;
            RenderSettings.fogStartDistance = sceneFogStart;
            RenderSettings.fogEndDistance = sceneFogEnd;
        }
    }
}
