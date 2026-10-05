using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Spinning turntable in the main menu that shows one kart at a time, with its driver
    /// standing beside it (hops and waves when picked). On the two-player page it switches to
    /// "duo" mode: player 1's kart and racer on the left, player 2's on the right.
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

        [Header("Two players (left = P1, right = P2)")]
        public Transform[] duoKartSpots = new Transform[2];
        public Transform[] duoCharacterSpots = new Transform[2];
        [Tooltip("Camera pose for the two-player page (straight down the road, both karts centred).")]
        public Transform duoCameraPose;
        Vector3 savedCamPosition;
        Quaternion savedCamRotation;

        GameObject[] karts, characters;
        int current = -1;
        float popTime = 1f;

        // Duo mode: created on first use, one set per player.
        GameObject[,] duoKarts, duoCharacters;
        readonly int[] duoShown = { -1, -1 };
        readonly float[] duoPop = { 1f, 1f };
        bool duo;
        Transform platform;

        void Awake()
        {
            karts = new GameObject[roster.karts.Length];
            for (int i = 0; i < karts.Length; i++) karts[i] = CreateDisplayKart(roster.karts[i].prefab, turntable);

            characters = new GameObject[roster.karts.Length];
            if (characterSpot)
                for (int i = 0; i < characters.Length; i++)
                {
                    characters[i] = CharacterPuppet.Create(roster.karts[i], characterSpot, standeeMaterial);
                    if (characters[i]) characters[i].SetActive(false);
                }
            platform = transform.Find("Platform");
        }

        /// <summary>A kart for display only: no physics or driving.</summary>
        static GameObject CreateDisplayKart(GameObject prefab, Transform parent)
        {
            var go = Instantiate(prefab, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.GetComponent<Rigidbody>().isKinematic = true;
            foreach (var c in go.GetComponents<Collider>()) c.enabled = false;
            go.GetComponent<KartController>().enabled = false;
            go.GetComponent<KartVisuals>().enabled = false;
            go.SetActive(false);
            return go;
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

        // ------------------------------------------------------------ Two players

        /// <summary>Two-player page: hide the turntable and show the players' picks side by side.</summary>
        public void SetDuoMode(bool on)
        {
            if (duo == on) return;
            duo = on;
            turntable.gameObject.SetActive(!on);
            if (characterSpot) characterSpot.gameObject.SetActive(!on);
            if (platform) platform.gameObject.SetActive(!on);
            if (!on) { HideDuo(0); HideDuo(1); }
            var cam = Camera.main;
            if (cam && duoCameraPose)
            {
                if (on)
                {
                    savedCamPosition = cam.transform.position;
                    savedCamRotation = cam.transform.rotation;
                    cam.transform.SetPositionAndRotation(duoCameraPose.position, duoCameraPose.rotation);
                }
                else cam.transform.SetPositionAndRotation(savedCamPosition, savedCamRotation);
            }
        }

        /// <summary>Shows player <paramref name="slot"/>'s racer; a new pick hops, a ready player cheers.</summary>
        public void ShowDuo(int slot, int kart, bool ready)
        {
            if (!duo || slot < 0 || slot > 1 || duoKartSpots[slot] == null) return;
            kart = ((kart % roster.karts.Length) + roster.karts.Length) % roster.karts.Length;
            duoKarts ??= new GameObject[2, roster.karts.Length];
            duoCharacters ??= new GameObject[2, roster.karts.Length];
            if (!duoKarts[slot, kart])
            {
                duoKarts[slot, kart] = CreateDisplayKart(roster.karts[kart].prefab, duoKartSpots[slot]);
                duoCharacters[slot, kart] = CharacterPuppet.Create(roster.karts[kart], duoCharacterSpots[slot], standeeMaterial);
            }

            bool changed = duoShown[slot] != kart;
            if (changed)
            {
                HideDuo(slot);
                duoShown[slot] = kart;
                duoKarts[slot, kart].SetActive(true);
                duoPop[slot] = 0f;
            }
            var character = duoCharacters[slot, kart];
            if (!character) return;
            character.SetActive(true);
            var puppet = character.GetComponent<CharacterPuppet>();
            if (!puppet) return;
            var mood = ready ? CharacterPuppet.Mood.Cheer : CharacterPuppet.Mood.Idle;
            if (changed || puppet.mood != mood) puppet.Hop();
            puppet.mood = mood;
        }

        public void HideDuo(int slot)
        {
            int k = duoShown[slot];
            duoShown[slot] = -1;
            if (k < 0 || duoKarts == null) return;
            if (duoKarts[slot, k]) duoKarts[slot, k].SetActive(false);
            if (duoCharacters[slot, k]) duoCharacters[slot, k].SetActive(false);
        }

        void Update()
        {
            turntable.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);

            for (int s = 0; s < 2; s++)
            {
                if (duoShown[s] < 0 || duoPop[s] >= 1f) continue;
                duoPop[s] = Mathf.Min(1f, duoPop[s] + Time.deltaTime / popDuration);
                duoKarts[s, duoShown[s]].transform.localScale = Vector3.one * (1f + Mathf.Sin(duoPop[s] * Mathf.PI) * 0.15f);
            }

            if (current < 0) return;
            popTime += Time.deltaTime;
            float t = Mathf.Clamp01(popTime / popDuration);
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
            karts[current].transform.localScale = Vector3.one * scale;
        }
    }
}
