using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Persistent audio service: music with cross-fades and a small fixed pool of voices for menu
    /// sounds (2D) and world one-shots such as crashes (3D). Pools are tiny on purpose so the game
    /// never plays more than a handful of effects at once.
    /// </summary>
    public class AudioHub : MonoBehaviour
    {
        const int UIVoices = 3, WorldVoices = 5;
        const float MusicLevel = 0.6f, FadeSpeed = 1.6f;

        static AudioHub instance;

        AudioSource music;
        AudioSource[] ui, world;
        int nextUI, nextWorld;
        AudioClip queuedMusic;
        float musicFade = 1f;
        bool ducked;

        public static AudioHub Instance
        {
            get
            {
                if (!instance)
                {
                    var go = new GameObject("AudioHub");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<AudioHub>();
                    instance.Setup();
                }
                return instance;
            }
        }

        static SoundLibrary Lib => SoundLibrary.Instance;

        void Setup()
        {
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.priority = 0;
            music.spatialBlend = 0f;
            music.ignoreListenerPause = true;
            music.playOnAwake = false;

            ui = new AudioSource[UIVoices];
            for (int i = 0; i < UIVoices; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.priority = 16;
                s.spatialBlend = 0f;
                s.ignoreListenerPause = true; // menu sounds still work while paused
                s.playOnAwake = false;
                ui[i] = s;
            }

            world = new AudioSource[WorldVoices];
            for (int i = 0; i < WorldVoices; i++)
            {
                var child = new GameObject("WorldVoice" + i);
                child.transform.SetParent(transform, false);
                var s = child.AddComponent<AudioSource>();
                s.priority = 160;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 4f;
                s.maxDistance = 50f;
                s.dopplerLevel = 0f;
                s.playOnAwake = false;
                world[i] = s;
            }
        }

        void Update()
        {
            // Cross-fade: fade the old track out, swap, fade the new one in (unscaled, works in pause).
            float step = Time.unscaledDeltaTime * FadeSpeed;
            if (queuedMusic)
            {
                musicFade = Mathf.MoveTowards(musicFade, 0f, step);
                if (musicFade <= 0f || !music.isPlaying)
                {
                    music.clip = queuedMusic;
                    queuedMusic = null;
                    music.Play();
                }
            }
            else musicFade = Mathf.MoveTowards(musicFade, 1f, step);

            music.volume = MusicLevel * GameSettings.MusicVolume * musicFade * (ducked ? 0.45f : 1f);
        }

        public static void PlayMusic(AudioClip clip)
        {
            var hub = Instance;
            if (!clip || (hub.music.clip == clip && hub.music.isPlaying && !hub.queuedMusic)) return;
            hub.queuedMusic = clip;
        }

        public void Duck(bool duck) => ducked = duck;

        /// <summary>Plays a 2D one-shot (menus, countdown, player boost) on the next free UI voice.</summary>
        public static void PlayUI(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (!clip || GameSettings.SfxVolume <= 0f) return;
            var hub = Instance;
            var s = hub.ui[hub.nextUI++ % UIVoices];
            s.pitch = pitch;
            s.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }

        /// <summary>Plays a 3D one-shot at a point (crashes, rival boosts).</summary>
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (!clip || GameSettings.SfxVolume <= 0f) return;
            var hub = Instance;
            var s = hub.world[hub.nextWorld++ % WorldVoices];
            s.transform.position = position;
            s.pitch = pitch;
            s.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }

        // Menu shortcuts
        public static void UIMove() => PlayUI(Lib ? Lib.uiMove : null, 0.5f);
        public static void UIClick() => PlayUI(Lib ? Lib.uiClick : null, 0.8f);
        public static void UIBack() => PlayUI(Lib ? Lib.uiBack : null, 0.8f);
        public static void UIConfirm() => PlayUI(Lib ? Lib.uiConfirm : null, 0.8f);
    }
}
