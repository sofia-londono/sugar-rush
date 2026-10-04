using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Kart sounds: one engine loop whose pitch follows speed, a drift squeal loop (player only),
    /// boost and crash one-shots. Rival engines are quieter, 3D, and switched off entirely when
    /// they are far from the camera so they don't use a voice.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class KartAudio : MonoBehaviour
    {
        public bool isPlayer;
        [Tooltip("Rival engines beyond this distance (m) from the camera are stopped.")]
        public float cullDistance = 40f;
        public float minPitch = 0.75f, maxPitch = 2.1f;
        public float crashThreshold = 4f;

        KartController kart;
        AudioSource engine, drift;
        Transform listener;
        bool wasBoosting;
        float crashCooldown;

        static SoundLibrary Lib => SoundLibrary.Instance;

        void Start()
        {
            kart = GetComponent<KartController>();
            if (!Lib) { enabled = false; return; }

            engine = CreateLoop("Engine", Lib.engineLoop);
            if (isPlayer) drift = CreateLoop("Drift", Lib.driftLoop);
            if (isPlayer) engine.Play();
        }

        AudioSource CreateLoop(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.dopplerLevel = 0f;
            s.priority = isPlayer ? 8 : 200;
            s.spatialBlend = isPlayer ? 0.2f : 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 3f;
            s.maxDistance = cullDistance;
            return s;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float sfx = GameSettings.SfxVolume;
            float speed01 = Mathf.Clamp01(kart.Speed / Mathf.Max(1f, kart.MaxSpeed));
            crashCooldown -= dt;

            // Rival engines only play near the camera.
            if (!isPlayer && !AudioListener.pause)
            {
                if (!listener && Camera.main) listener = Camera.main.transform;
                bool near = listener && (listener.position - transform.position).sqrMagnitude < cullDistance * cullDistance;
                if (near && !engine.isPlaying) engine.Play();
                else if (!near && engine.isPlaying) engine.Stop();
            }

            if (engine.isPlaying)
            {
                float targetPitch = Mathf.Lerp(minPitch, maxPitch, speed01)
                                    + (kart.IsBoosting ? 0.25f : 0f)
                                    + (kart.IsGrounded ? 0f : 0.15f);
                engine.pitch = Mathf.Lerp(engine.pitch, targetPitch, dt * 8f);
                float load = 0.6f + 0.4f * Mathf.Abs(kart.Throttle);
                engine.volume = (isPlayer ? 0.5f : 0.4f) * load * sfx;
            }

            if (drift)
            {
                bool squeal = kart.IsDrifting && kart.IsGrounded;
                if (squeal && !drift.isPlaying) drift.Play();
                else if (!squeal && drift.isPlaying) drift.Stop();
                drift.pitch = 0.9f + 0.2f * speed01 + 0.08f * kart.DriftLevel;
                drift.volume = 0.35f * sfx;
            }

            if (kart.IsBoosting && !wasBoosting)
            {
                if (isPlayer) AudioHub.PlayUI(Lib.boost, 0.8f);
                else if (engine.isPlaying) AudioHub.PlayAt(Lib.boost, transform.position, 0.6f);
            }
            wasBoosting = kart.IsBoosting;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!enabled || crashCooldown > 0f || Lib.crashes == null || Lib.crashes.Length == 0) return;
            float impact = collision.relativeVelocity.magnitude;
            if (impact < crashThreshold) return;
            if (!isPlayer && !engine.isPlaying) return; // far away: not worth a voice
            crashCooldown = 0.25f;
            var clip = Lib.crashes[Random.Range(0, Lib.crashes.Length)];
            float volume = Mathf.Clamp01(impact / 15f);
            AudioHub.PlayAt(clip, collision.GetContact(0).point, volume, Random.Range(0.9f, 1.1f));
        }
    }
}
