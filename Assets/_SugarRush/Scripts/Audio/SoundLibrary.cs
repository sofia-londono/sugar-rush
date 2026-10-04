using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Every sound the game uses, in one asset (Resources/SoundLibrary) so any script can reach it.
    /// Filled in by the Sugar Rush builder; credits for each file are in CREDITS.md.
    /// </summary>
    public class SoundLibrary : ScriptableObject
    {
        [Header("Music (OGG, streamed)")]
        public AudioClip menuMusic;
        public AudioClip raceMusic;

        [Header("Kart")]
        [Tooltip("Single engine loop; KartAudio changes its pitch with speed.")]
        public AudioClip engineLoop;
        public AudioClip driftLoop;
        public AudioClip boost;
        public AudioClip[] crashes;

        [Header("Race")]
        public AudioClip countdownBeep;
        public AudioClip countdownGo;
        public AudioClip lapChime;
        public AudioClip finishFanfare;

        [Header("Menus")]
        public AudioClip uiMove;
        public AudioClip uiClick;
        public AudioClip uiBack;
        public AudioClip uiConfirm;

        static SoundLibrary instance;
        public static SoundLibrary Instance => instance ? instance : instance = Resources.Load<SoundLibrary>("SoundLibrary");
    }
}
