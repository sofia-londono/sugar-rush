using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Spinning turntable in the main menu that shows one kart at a time with its racer sitting
    /// in it (waves when picked). On the two-player page it switches to "duo" mode: player 1's
    /// kart on the left, player 2's on the right, each with their racer.
    /// </summary>
    public class KartShowcase : MonoBehaviour
    {
        public KartRoster roster;
        public Transform turntable;
        public float spinSpeed = 30f;
        public float popDuration = 0.25f;
        [Tooltip("Where a racer without a 3D model stands (as a cut-out), facing the camera.")]
        public Transform characterSpot;
        public Material standeeMaterial;

        [Header("Two players (left = P1, right = P2)")]
        public Transform[] duoKartSpots = new Transform[2];
        [Tooltip("Camera pose for the two-player page (straight down the road, both karts centred).")]
        public Transform duoCameraPose;
        Vector3 savedCamPosition;
        Quaternion savedCamRotation;

        GameObject[] karts, standees;
        CharacterPuppet[] drivers;
        int current = -1;
        float popTime = 1f;

        // Duo mode: created on first use, one set per player.
        GameObject[,] duoKarts;
        CharacterPuppet[,] duoDrivers;
        readonly int[] duoShown = { -1, -1 };
        readonly float[] duoPop = { 1f, 1f };
        bool duo;
        Transform platform;

        void Awake()
        {
            int n = roster.karts.Length;
            karts = new GameObject[n];
            drivers = new CharacterPuppet[n];
            standees = new GameObject[n];
            for (int i = 0; i < n; i++)
            {
                karts[i] = CreateDisplayKart(roster.karts[i].prefab, turntable);
                drivers[i] = CharacterPuppet.CreateDriver(roster.karts[i], karts[i].transform);
                if (!drivers[i] && characterSpot)
                {
                    standees[i] = CharacterPuppet.Create(roster.karts[i], characterSpot, standeeMaterial);
                    if (standees[i]) standees[i].SetActive(false);
                }
            }
            platform = transform.Find("Platform");
        }

        /// <summary>A kart for display only: no physics or driving.</summary>
        public static GameObject CreateDisplayKart(GameObject prefab, Transform parent)
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
                if (standees[current]) standees[current].SetActive(false);
            }
            current = index;
            karts[current].SetActive(true);
            popTime = 0f;
            if (drivers[current]) drivers[current].Hop();
            if (standees[current]) standees[current].SetActive(true);
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

        /// <summary>Shows player <paramref name="slot"/>'s kart and racer; a new pick waves, a ready player cheers.</summary>
        public void ShowDuo(int slot, int kart, bool ready)
        {
            if (!duo || slot < 0 || slot > 1 || duoKartSpots[slot] == null) return;
            kart = ((kart % roster.karts.Length) + roster.karts.Length) % roster.karts.Length;
            duoKarts ??= new GameObject[2, roster.karts.Length];
            duoDrivers ??= new CharacterPuppet[2, roster.karts.Length];
            if (!duoKarts[slot, kart])
            {
                duoKarts[slot, kart] = CreateDisplayKart(roster.karts[kart].prefab, duoKartSpots[slot]);
                duoDrivers[slot, kart] = CharacterPuppet.CreateDriver(roster.karts[kart], duoKarts[slot, kart].transform);
            }

            bool changed = duoShown[slot] != kart;
            if (changed)
            {
                HideDuo(slot);
                duoShown[slot] = kart;
                duoKarts[slot, kart].SetActive(true);
                duoPop[slot] = 0f;
            }
            var puppet = duoDrivers[slot, kart];
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
