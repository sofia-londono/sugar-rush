using System.Text;
using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Development helper: plays back a fixed input timeline on a kart and records telemetry,
    /// so handling can be verified without a human at the keyboard.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class KartTestPilot : MonoBehaviour
    {
        [System.Serializable]
        public struct Step
        {
            public float duration, throttle, steer;
            public bool drift;
            public Step(float duration, float throttle, float steer, bool drift = false)
            { this.duration = duration; this.throttle = throttle; this.steer = steer; this.drift = drift; }
        }

        public Step[] steps =
        {
            new Step(1.3f, 1f, 0f),
            new Step(1.4f, 1f, 1f, true),
            new Step(1.5f, 1f, 0f),
        };
        public float sampleInterval = 0.2f;

        public bool Finished { get; private set; }
        public string Log => log.ToString();

        KartController kart;
        readonly StringBuilder log = new();
        int stepIndex;
        float stepTime, sampleTime, totalTime;
        bool sawDrift, sawBoost;
        int maxDriftLevel;

        void Awake()
        {
            kart = GetComponent<KartController>();
            var player = GetComponent<PlayerKartInput>();
            if (player) player.enabled = false;
        }

        void FixedUpdate()
        {
            if (Finished) return;
            float dt = Time.fixedDeltaTime;

            var step = steps[stepIndex];
            kart.Throttle = step.throttle;
            kart.Steer = step.steer;
            kart.DriftHeld = step.drift;

            sawDrift |= kart.IsDrifting;
            sawBoost |= kart.IsBoosting;
            maxDriftLevel = Mathf.Max(maxDriftLevel, kart.DriftLevel);

            sampleTime += dt;
            if (sampleTime >= sampleInterval)
            {
                sampleTime = 0f;
                log.AppendLine($"t={totalTime:0.0} step={stepIndex} speed={kart.Speed * 3.6f:0} yaw={transform.eulerAngles.y:0} " +
                               $"grounded={kart.IsGrounded} drift={kart.IsDrifting}/{kart.DriftLevel} boost={kart.IsBoosting} pos={transform.position}");
            }

            stepTime += dt;
            totalTime += dt;
            if (stepTime >= step.duration)
            {
                stepTime = 0f;
                if (++stepIndex >= steps.Length)
                {
                    Finished = true;
                    kart.Throttle = 0f; kart.Steer = 0f; kart.DriftHeld = false;
                    log.AppendLine($"DONE sawDrift={sawDrift} maxDriftLevel={maxDriftLevel} sawBoost={sawBoost}");
                }
            }
        }
    }
}
