using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SugarRush
{
    /// <summary>
    /// Local split screen (two players on one machine): a second chase camera, side by side,
    /// plus automatic savings because the track is drawn twice — lower render scale, no track
    /// shadows, shorter draw distance hidden by pastel fog, and a 30 FPS cap.
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

        public KartCamera SecondCamera { get; private set; }

        bool splitActive;
        float savedRenderScale, savedShadowDistance;
        int savedFrameRate, savedVSync;

        void SetupSplitScreen()
        {
            splitActive = true;
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
            foreach (var cam in new[] { cam1, cam2 })
            {
                cam.farClipPlane = splitDrawDistance;
                cam.backgroundColor = splitFogColor;
                cam.clearFlags = CameraClearFlags.SolidColor;
            }
            // Half-width views are tall: open the FOV so each player sees about as much road.
            kartCamera.baseFov = SecondCamera.baseFov = 74f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = splitFogColor;
            RenderSettings.fogStartDistance = splitDrawDistance * 0.45f;
            RenderSettings.fogEndDistance = splitDrawDistance * 0.95f;

            // Track pieces stop casting shadows (karts still do, and the road still receives them).
            var track = GameObject.Find("Track");
            if (track)
                foreach (var r in track.GetComponentsInChildren<MeshRenderer>())
                    r.shadowCastingMode = ShadowCastingMode.Off;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                savedRenderScale = urp.renderScale;
                savedShadowDistance = urp.shadowDistance;
                urp.renderScale = Mathf.Min(urp.renderScale, splitRenderScale);
                urp.shadowDistance = Mathf.Min(urp.shadowDistance, splitShadowDistance);
            }

            savedFrameRate = Application.targetFrameRate;
            savedVSync = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = splitFrameRate;
        }

        /// <summary>Put the global settings back (the URP asset is shared with every scene).</summary>
        void RestoreSplitScreen()
        {
            if (!splitActive) return;
            splitActive = false;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = savedRenderScale;
                urp.shadowDistance = savedShadowDistance;
            }
            Application.targetFrameRate = savedFrameRate;
            QualitySettings.vSyncCount = savedVSync;
            RenderSettings.fog = false;
        }
    }
}
