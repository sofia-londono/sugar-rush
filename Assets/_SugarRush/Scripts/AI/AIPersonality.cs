using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// How one computer racer drives. One asset per character (Data/AI), editable in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Sugar Rush/AI Personality", fileName = "AI_")]
    public class AIPersonality : ScriptableObject
    {
        [Header("Pace")]
        [Tooltip("Multiplier on the kart's top speed on straights.")]
        [Range(0.7f, 1.2f)] public float straightSpeed = 1f;
        [Tooltip("Multiplier on how fast it dares to take corners.")]
        [Range(0.6f, 1.3f)] public float cornerSpeed = 1f;
        [Tooltip("Braking strength used to plan corners. Higher = brakes later (and overshoots more).")]
        [Range(0.5f, 1.6f)] public float lateBraking = 1f;

        [Header("Racing line")]
        [Tooltip("How far (m) it wanders from the centre of the road.")]
        [Range(0f, 3.5f)] public float laneWidth = 1.5f;
        [Tooltip("1 = perfectly steady pace and line, 0 = drifts around a lot.")]
        [Range(0f, 1f)] public float consistency = 0.5f;

        [Header("Aggression")]
        [Tooltip("0 = polite, 1 = rams karts alongside and blocks karts behind.")]
        [Range(0f, 1f)] public float aggression;
        [Tooltip("Distance (m) ahead at which it goes for a kart to bump it.")]
        public float attackRange = 9f;
        [Tooltip("Distance (m) behind at which it moves over to block.")]
        public float blockRange = 12f;

        [Header("Mistakes")]
        [Tooltip("Average number of slip-ups per minute (wobbles, late braking, hesitating).")]
        [Range(0f, 12f)] public float mistakesPerMinute;
        [Tooltip("How long each slip-up lasts (seconds).")]
        public Vector2 mistakeDuration = new(0.4f, 0.9f);

        static AIPersonality neutral;

        /// <summary>Balanced defaults, used when a racer has no personality asset.</summary>
        public static AIPersonality Neutral => neutral ? neutral : neutral = CreateInstance<AIPersonality>();
    }
}
