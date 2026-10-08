using System;
using UnityEngine;

namespace SugarRush
{
    public enum Language { Spanish, English }

    /// <summary>
    /// Player preferences shared by every scene, persisted with PlayerPrefs.
    /// </summary>
    public static class GameSettings
    {
        public const int MinLaps = 1, MaxLaps = 5;

        public static Language Language { get; set; } = Language.Spanish;
        public static float MusicVolume { get; set; } = 0.7f;
        public static float SfxVolume { get; set; } = 0.8f;
        public static Difficulty Difficulty { get; set; } = SugarRush.Difficulty.Normal;
        /// <summary>Index into QualitySettings.names (0 = Mobile/performance, 1 = PC/quality).</summary>
        public static int Quality { get; set; }
        public static int Laps { get; set; } = 3;
        /// <summary>Ralph smashes the road during races; coins and Felix's hammer fix it.</summary>
        public static bool RalphChaos { get; set; } = true;
        /// <summary>Racers sitting in their karts during races (a baked, unanimated mesh each).</summary>
        public static bool DriversInRace { get; set; } = true;
        /// <summary>Touch controls: the kart accelerates by itself (steer, brake and drift only).</summary>
        public static bool AutoAccelerate { get; set; } = true;
        public static int SelectedKart { get; set; }
        /// <summary>Index into <see cref="Tracks.All"/>.</summary>
        public static int Track { get; set; }

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init() => Load();

        public static void Load()
        {
            var defaultLanguage = Application.systemLanguage == SystemLanguage.Spanish ? Language.Spanish : Language.English;
            Language = (Language)PlayerPrefs.GetInt("lang", (int)defaultLanguage);
            float legacyVolume = PlayerPrefs.GetFloat("volume", 0.8f); // single volume slider of older versions
            MusicVolume = PlayerPrefs.GetFloat("musicVolume", legacyVolume * 0.85f);
            SfxVolume = PlayerPrefs.GetFloat("sfxVolume", legacyVolume);
            Difficulty = (SugarRush.Difficulty)Mathf.Clamp(PlayerPrefs.GetInt("difficulty", (int)SugarRush.Difficulty.Normal), 0, 2);
            // "Performance" is the default everywhere (laptops with integrated graphics, web).
            // Settings saved before version 2 had "Quality" as default: switch them once.
            Quality = PlayerPrefs.GetInt("settingsVersion", 1) < 2 ? 0 : PlayerPrefs.GetInt("quality", 0);
            Laps = Mathf.Clamp(PlayerPrefs.GetInt("laps", 3), MinLaps, MaxLaps);
            RalphChaos = PlayerPrefs.GetInt("ralphChaos", 1) != 0;
            DriversInRace = PlayerPrefs.GetInt("drivers", 1) != 0;
            AutoAccelerate = PlayerPrefs.GetInt("autoGas", 1) != 0;
            SelectedKart = PlayerPrefs.GetInt("kart", 0);
            Track = Tracks.Wrap(PlayerPrefs.GetInt("track", 0));
            Apply();
        }

        public static void Save()
        {
            PlayerPrefs.SetInt("lang", (int)Language);
            PlayerPrefs.SetFloat("musicVolume", MusicVolume);
            PlayerPrefs.SetFloat("sfxVolume", SfxVolume);
            PlayerPrefs.SetInt("difficulty", (int)Difficulty);
            PlayerPrefs.SetInt("quality", Quality);
            PlayerPrefs.SetInt("settingsVersion", 2);
            PlayerPrefs.SetInt("laps", Laps);
            PlayerPrefs.SetInt("ralphChaos", RalphChaos ? 1 : 0);
            PlayerPrefs.SetInt("drivers", DriversInRace ? 1 : 0);
            PlayerPrefs.SetInt("autoGas", AutoAccelerate ? 1 : 0);
            PlayerPrefs.SetInt("kart", SelectedKart);
            PlayerPrefs.SetInt("track", Track);
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

        static void Apply()
        {
            AudioListener.volume = 1f; // music and effects are scaled separately by AudioHub / KartAudio
            int level = Mathf.Clamp(Quality, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);
        }

        /// <summary>Record key per track and lap count. The original track keeps its old key ("best_3"), so existing records survive.</summary>
        static string BestKey(int track, int laps) => track == 0 ? $"best_{laps}" : $"best_{Tracks.Get(track).Id}_{laps}";

        public static float GetBestTime(int track, int laps) => PlayerPrefs.GetFloat(BestKey(track, laps), 0f);

        /// <summary>Stores the time if it beats the record; returns true when it is a new record.</summary>
        public static bool TrySetBestTime(int track, int laps, float time)
        {
            float best = GetBestTime(track, laps);
            if (best > 0f && time >= best) return false;
            PlayerPrefs.SetFloat(BestKey(track, laps), time);
            PlayerPrefs.Save();
            return true;
        }
    }
}
