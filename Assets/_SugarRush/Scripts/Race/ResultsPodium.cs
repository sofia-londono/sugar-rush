using System.Collections.Generic;
using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// A little candy podium far below the track, filmed by its own camera into a texture that
    /// the results screen shows: the top three karts stand on it with their racers sitting in
    /// them (the winner cheers, the others clap). Nothing here exists or renders until the
    /// results appear.
    /// </summary>
    public class ResultsPodium : MonoBehaviour
    {
        public KartRoster roster;
        public Camera podiumCamera;
        [Tooltip("1st, 2nd and 3rd place spots.")]
        public Transform[] spots = new Transform[3];
        public Material standeeMaterial;
        public Vector2Int resolution = new(720, 540);

        GameObject[] characters;
        RenderTexture texture;
        readonly int[] shown = { -1, -1, -1 };

        void OnDestroy()
        {
            if (texture) texture.Release();
        }

        /// <summary>Puts these karts (roster indices, best first) on the podium and returns the view.</summary>
        public RenderTexture Show(IList<int> kartIndices)
        {
            if (characters == null)
            {
                characters = new GameObject[roster.karts.Length];
                for (int i = 0; i < characters.Length; i++)
                {
                    // Display kart with its racer sitting in it (or the portrait cut-out on top if there's no model).
                    characters[i] = KartShowcase.CreateDisplayKart(roster.karts[i].prefab, transform);
                    if (!CharacterPuppet.CreateDriver(roster.karts[i], characters[i].transform))
                    {
                        var standee = CharacterPuppet.Create(roster.karts[i], characters[i].transform, standeeMaterial);
                        if (standee) standee.transform.localPosition = Vector3.up * 0.5f;
                    }
                }
                texture = new RenderTexture(resolution.x, resolution.y, 24) { name = "PodiumView", antiAliasing = 2 };
                podiumCamera.targetTexture = texture;
            }
            podiumCamera.gameObject.SetActive(true);

            var wanted = new int[spots.Length];
            for (int place = 0; place < spots.Length; place++)
                wanted[place] = place < kartIndices.Count ? kartIndices[place] : -1;
            for (int i = 0; i < characters.Length; i++)
                if (characters[i]) characters[i].SetActive(System.Array.IndexOf(wanted, i) >= 0);

            for (int place = 0; place < spots.Length; place++)
            {
                int kart = wanted[place];
                if (kart < 0 || kart >= characters.Length || !characters[kart]) { shown[place] = kart; continue; }
                var go = characters[kart];
                go.transform.SetParent(spots[place], false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                var puppet = go.GetComponentInChildren<CharacterPuppet>();
                if (puppet)
                {
                    puppet.mood = place == 0 ? CharacterPuppet.Mood.Cheer : CharacterPuppet.Mood.Clap;
                    if (shown[place] != kart) puppet.Hop();
                }
                shown[place] = kart;
            }
            return texture;
        }
    }
}
