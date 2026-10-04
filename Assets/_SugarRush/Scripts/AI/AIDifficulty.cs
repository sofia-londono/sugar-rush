using UnityEngine;

namespace SugarRush
{
    public enum Difficulty { Easy, Normal, Hard }

    /// <summary>
    /// Global AI tuning per difficulty level, applied on top of each racer's personality.
    /// Editable in the Inspector (Data/AI/AIDifficulty.asset).
    /// </summary>
    [CreateAssetMenu(menuName = "Sugar Rush/AI Difficulty", fileName = "AIDifficulty")]
    public class AIDifficulty : ScriptableObject
    {
        [System.Serializable]
        public class Level
        {
            [Range(0.7f, 1.2f)] public float speedScale = 1f;
            [Range(0.6f, 1.3f)] public float cornerScale = 1f;
            [Range(0f, 3f)] public float mistakeScale = 1f;
            [Range(0f, 1.5f)] public float aggressionScale = 1f;

            [Header("Rubber banding (gentle)")]
            [Tooltip("Extra top speed for a racer far behind the player (0.06 = +6%).")]
            [Range(0f, 0.2f)] public float catchUpBoost = 0.06f;
            [Tooltip("Top speed taken away from a racer far ahead of the player.")]
            [Range(0f, 0.2f)] public float leadSlowdown = 0.04f;
            [Tooltip("Gap (m) at which the full boost / slowdown applies.")]
            public float rubberBandDistance = 120f;
        }

        public Level easy = new()
        {
            speedScale = 0.86f, cornerScale = 0.92f, mistakeScale = 1.6f, aggressionScale = 0.5f,
            catchUpBoost = 0.03f, leadSlowdown = 0.08f,
        };

        public Level normal = new()
        {
            speedScale = 0.94f, cornerScale = 0.98f, mistakeScale = 1f, aggressionScale = 1f,
            catchUpBoost = 0.06f, leadSlowdown = 0.05f,
        };

        public Level hard = new()
        {
            speedScale = 1f, cornerScale = 1.05f, mistakeScale = 0.5f, aggressionScale = 1.2f,
            catchUpBoost = 0.09f, leadSlowdown = 0.02f,
        };

        public Level Get(Difficulty d) => d switch { Difficulty.Easy => easy, Difficulty.Hard => hard, _ => normal };
    }
}
