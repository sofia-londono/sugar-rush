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
        public static float Volume { get; set; } = 0.8f;
        /// <summary>Index into QualitySettings.names (0 = Mobile/performance, 1 = PC/quality).</summary>
        public static int Quality { get; set; } = 1;
        public static int Laps { get; set; } = 3;
        public static int SelectedKart { get; set; }

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init() => Load();

        public static void Load()
        {
            var defaultLanguage = Application.systemLanguage == SystemLanguage.Spanish ? Language.Spanish : Language.English;
            Language = (Language)PlayerPrefs.GetInt("lang", (int)defaultLanguage);
            Volume = PlayerPrefs.GetFloat("volume", 0.8f);
            Quality = PlayerPrefs.GetInt("quality", Application.isMobilePlatform ? 0 : QualitySettings.names.Length - 1);
            Laps = Mathf.Clamp(PlayerPrefs.GetInt("laps", 3), MinLaps, MaxLaps);
            SelectedKart = PlayerPrefs.GetInt("kart", 0);
            Apply();
        }

        public static void Save()
        {
            PlayerPrefs.SetInt("lang", (int)Language);
            PlayerPrefs.SetFloat("volume", Volume);
            PlayerPrefs.SetInt("quality", Quality);
            PlayerPrefs.SetInt("laps", Laps);
            PlayerPrefs.SetInt("kart", SelectedKart);
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

        static void Apply()
        {
            AudioListener.volume = Volume;
            int level = Mathf.Clamp(Quality, 0, QualitySettings.names.Length - 1);
            if (QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);
        }

        public static float GetBestTime(int laps) => PlayerPrefs.GetFloat($"best_{laps}", 0f);

        /// <summary>Stores the time if it beats the record; returns true when it is a new record.</summary>
        public static bool TrySetBestTime(int laps, float time)
        {
            float best = GetBestTime(laps);
            if (best > 0f && time >= best) return false;
            PlayerPrefs.SetFloat($"best_{laps}", time);
            PlayerPrefs.Save();
            return true;
        }
    }
}
