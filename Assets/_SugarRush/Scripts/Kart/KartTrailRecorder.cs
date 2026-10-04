using System.Text;
using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Development helper: samples a kart's position, speed and track segment at a fixed rate so
    /// a problem spot can be plotted afterwards. Add it in Play mode only.
    /// </summary>
    public class KartTrailRecorder : MonoBehaviour
    {
        public float interval = 0.1f;
        public int maxSamples = 600;

        readonly StringBuilder log = new();
        int samples;
        float timer;
        KartController kart;
        RaceProgress progress;

        public string Log => log.ToString();
        public int Samples => samples;

        void Awake()
        {
            kart = GetComponent<KartController>();
            progress = GetComponent<RaceProgress>();
        }

        void FixedUpdate()
        {
            if (samples >= maxSamples) return;
            timer += Time.fixedDeltaTime;
            if (timer < interval) return;
            timer = 0f;
            samples++;
            var p = transform.position;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            log.Append(p.x.ToString("F1", ci)).Append(',').Append(p.z.ToString("F1", ci)).Append(',').Append(p.y.ToString("F1", ci))
               .Append(',').Append((kart.Speed * 3.6f).ToString("F0", ci))
               .Append(',').Append(progress ? progress.Segment : -1)
               .Append(',').Append(kart.IsGrounded ? 1 : 0)
               .Append(',').Append(progress ? progress.TrackReturns : 0)
               .Append(' ');
        }
    }
}
