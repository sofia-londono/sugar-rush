using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// All five racers in their karts, lined up under the START arch for the main menu's
    /// background; every few seconds one of them waves.
    /// </summary>
    public class RacerLineup : MonoBehaviour
    {
        public KartRoster roster;
        [Tooltip("One spot per racer, facing the camera.")]
        public Transform[] spots;

        CharacterPuppet[] drivers;
        float nextWave = 1.5f;

        void Start()
        {
            drivers = new CharacterPuppet[Mathf.Min(spots.Length, roster.karts.Length)];
            for (int i = 0; i < drivers.Length; i++)
            {
                var kart = KartShowcase.CreateDisplayKart(roster.karts[i].prefab, spots[i]);
                kart.SetActive(true);
                drivers[i] = CharacterPuppet.CreateDriver(roster.karts[i], kart.transform);
            }
        }

        void Update()
        {
            nextWave -= Time.unscaledDeltaTime;
            if (nextWave > 0f || drivers == null || drivers.Length == 0) return;
            nextWave = Random.Range(1.2f, 2.6f);
            var d = drivers[Random.Range(0, drivers.Length)];
            if (d) d.Hop();
        }
    }
}
