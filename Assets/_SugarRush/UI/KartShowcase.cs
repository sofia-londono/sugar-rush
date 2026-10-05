using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Spinning turntable in the main menu that shows one kart at a time, with its driver
    /// standing beside it (hops and waves when picked).
    /// </summary>
    public class KartShowcase : MonoBehaviour
    {
        public KartRoster roster;
        public Transform turntable;
        public float spinSpeed = 30f;
        public float popDuration = 0.25f;
        [Tooltip("Where the racer stands, facing the camera.")]
        public Transform characterSpot;
        public Material standeeMaterial;

        GameObject[] karts, characters;
        int current = -1;
        float popTime = 1f;

        void Awake()
        {
            karts = new GameObject[roster.karts.Length];
            for (int i = 0; i < karts.Length; i++)
            {
                var go = Instantiate(roster.karts[i].prefab, turntable);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                // Display only: no physics or driving.
                go.GetComponent<Rigidbody>().isKinematic = true;
                foreach (var c in go.GetComponents<Collider>()) c.enabled = false;
                go.GetComponent<KartController>().enabled = false;
                go.GetComponent<KartVisuals>().enabled = false;
                go.SetActive(false);
                karts[i] = go;
            }

            characters = new GameObject[roster.karts.Length];
            if (characterSpot)
                for (int i = 0; i < characters.Length; i++)
                {
                    characters[i] = CharacterPuppet.Create(roster.karts[i], characterSpot, standeeMaterial);
                    if (characters[i]) characters[i].SetActive(false);
                }
        }

        public void Show(int index)
        {
            index = ((index % karts.Length) + karts.Length) % karts.Length;
            if (index == current) return;
            if (current >= 0)
            {
                karts[current].SetActive(false);
                if (characters[current]) characters[current].SetActive(false);
            }
            current = index;
            karts[current].SetActive(true);
            popTime = 0f;
            if (characters[current])
            {
                characters[current].SetActive(true);
                characters[current].GetComponent<CharacterPuppet>()?.Hop();
            }
        }

        void Update()
        {
            turntable.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);

            if (current < 0) return;
            popTime += Time.deltaTime;
            float t = Mathf.Clamp01(popTime / popDuration);
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
            karts[current].transform.localScale = Vector3.one * scale;
        }
    }
}
