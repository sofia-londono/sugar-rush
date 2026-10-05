using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Main menu background: the camera glides slowly through a few of the prettiest spots of
    /// the track (the racers lined up under the START arch, the ferris-wheel town, the giant
    /// cake, the candy canes by the rainbow road), with a soft pink fade between shots. Other
    /// menu pages put the camera back on the kart turntable.
    /// </summary>
    public class MenuCameraTour : MonoBehaviour
    {
        [System.Serializable]
        public class Shot
        {
            public Vector3 fromPosition, toPosition, fromLook, toLook;
            public float duration = 9f;
        }

        public Camera cam;
        public Shot[] shots;
        public float fadeTime = 0.7f;

        /// <summary>0..1 opacity of the pink curtain between shots (MainMenuUI draws it).</summary>
        public float Fade { get; private set; }

        bool active;
        int shot;
        float time;
        Vector3 restPosition;
        Quaternion restRotation;
        bool restSaved;

        public void SetActive(bool on)
        {
            if (!cam || shots == null || shots.Length == 0) return;
            if (!restSaved)
            {
                restPosition = cam.transform.position;
                restRotation = cam.transform.rotation;
                restSaved = true;
            }
            if (on == active) return;
            active = on;
            if (on)
            {
                time = 0f;
                Apply();
            }
            else
            {
                Fade = 0f;
                cam.transform.SetPositionAndRotation(restPosition, restRotation);
            }
        }

        void Update()
        {
            if (!active) return;
            time += Time.unscaledDeltaTime;
            if (time >= shots[shot].duration)
            {
                time = 0f;
                shot = (shot + 1) % shots.Length;
            }
            Apply();
        }

        void Apply()
        {
            var s = shots[shot];
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / s.duration));
            Vector3 position = Vector3.Lerp(s.fromPosition, s.toPosition, k);
            Vector3 look = Vector3.Lerp(s.fromLook, s.toLook, k);
            cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position, Vector3.up));
            // Fade in at the start of each shot and out at its end.
            Fade = Mathf.Max(1f - time / fadeTime, 1f - (s.duration - time) / fadeTime, 0f);
        }
    }
}
