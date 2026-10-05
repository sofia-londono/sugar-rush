using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SugarRush
{
    /// <summary>A stretch of road Ralph can smash: rubble covers one side, the other side is the detour.</summary>
    [Serializable]
    public class ChaosZone
    {
        [Tooltip("Centre of the zone, metres along the racing line.")]
        public float distance;
        public float halfLength = 7f;
        [Tooltip("Sideways band covered by rubble (metres from the racing line, + = right).")]
        public float blockedMin, blockedMax;
        [Tooltip("Sideways band of drivable road.")]
        public float roadMin, roadMax;
        [Tooltip("Pre-built rubble, scaled in when the zone breaks.")]
        public Transform rubble;
        public Vector3 ralphSpot;
        [Tooltip("Direction Ralph jumps in from (off the track).")]
        public Vector3 ralphSide;
    }

    /// <summary>
    /// "Ralph's chaos": during the race Ralph leaps onto one of a few fixed stretches of road
    /// ("¡Ralph viene!", shaking cameras) and smashes it — rubble covers part of the road and
    /// slows anyone driving over it. Gold coins lie around the track; a racer carrying 5 has
    /// Felix's hammer, which repairs a broken stretch on contact and gives a boost. The AI
    /// collects coins, avoids rubble and repairs too.
    /// Offline this machine decides everything; online the host does (events, coins, repairs)
    /// and each machine only reports what the karts it drives touched.
    /// </summary>
    public class RalphChaos : MonoBehaviour
    {
        public static RalphChaos Instance { get; private set; }

        public TrackPath path;
        public ChaosZone[] zones;
        public Transform[] coins;
        public RalphPuppet ralph;

        [Header("Rules")]
        public int hammerCost = 5;
        public float coinRadius = 1.5f;
        public float coinRespawn = 15f;
        public float repairBoost = 2.5f;
        public float firstEvent = 14f;
        public Vector2 eventInterval = new(24f, 36f);
        public float warningTime = 2.6f;
        [Tooltip("Ralph is only drawn when a player's camera is this close.")]
        public float visibleDistance = 170f;

        /// <summary>Diagnostics: shows played and repairs seen on this machine.</summary>
        public int EventCount { get; private set; }
        public int RepairCount { get; private set; }
        /// <summary>Diagnostics: seconds of broken road, summed over zones.</summary>
        public float BrokenSeconds { get; private set; }

        public event Action<int> RalphIncoming;
        public event Action<RaceProgress> Repaired;
        public event Action<RaceProgress> CoinCollected;

        RaceManager race;
        bool[] broken;
        float[] rubbleShown, repairCooldown;
        Vector3[] rubbleScale;
        bool[] coinTaken;
        float[] coinRespawnAt, coinPredicted, coinDistance, coinLateral;
        Renderer[] coinRenderers;
        Quaternion[] coinBase;
        bool coinsShown = true;
        readonly Dictionary<RaceProgress, NetKart> netKarts = new();
        readonly Dictionary<RaceProgress, float> roughSoundAt = new();

        float nextEvent = -1f;
        int pendingZone = -1;
        float pendingLandAt;
        int showZone = -1;
        float showLandAt;
        int nextHit;

        // Gold coins bursting out of a repair.
        const int BurstCount = 8;
        Transform[] burst;
        Vector3[] burstVelocity;
        float burstUntil;

        static SoundLibrary Lib => SoundLibrary.Instance;

        public bool Active => race && (race.IsOnline ? NetRace.Instance && NetRace.Instance.ChaosOn.Value : GameSettings.RalphChaos);
        bool Authority => !race.IsOnline || (NetworkManager.Singleton && NetworkManager.Singleton.IsServer);

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            race = RaceManager.Instance;
            broken = new bool[zones.Length];
            rubbleShown = new float[zones.Length];
            repairCooldown = new float[zones.Length];
            rubbleScale = new Vector3[zones.Length];
            for (int z = 0; z < zones.Length; z++)
            {
                rubbleScale[z] = zones[z].rubble.localScale;
                zones[z].rubble.gameObject.SetActive(false);
            }

            int n = coins.Length;
            coinTaken = new bool[n];
            coinRespawnAt = new float[n];
            coinPredicted = new float[n];
            coinDistance = new float[n];
            coinLateral = new float[n];
            coinRenderers = new Renderer[n];
            coinBase = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                coinRenderers[i] = coins[i].GetComponent<Renderer>();
                coinBase[i] = coins[i].localRotation;
                int seg = path.FindClosestSegment(coins[i].position);
                coinDistance[i] = path.ProjectDistance(coins[i].position, seg);
                coinLateral[i] = Lateral(coins[i].position, coinDistance[i]);
            }

            burst = new Transform[BurstCount];
            burstVelocity = new Vector3[BurstCount];
            for (int i = 0; i < BurstCount && n > 0; i++)
            {
                burst[i] = Instantiate(coins[0], transform);
                burst[i].name = "BurstCoin";
                burst[i].localScale *= 0.6f;
                burst[i].gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------ Queries

        public int CoinsOf(RaceProgress racer)
        {
            if (!racer) return 0;
            if (!netKarts.TryGetValue(racer, out var net)) netKarts[racer] = net = racer.GetComponent<NetKart>();
            return net ? net.Coins.Value : racer.Coins;
        }

        public bool HasHammer(RaceProgress racer) => CoinsOf(racer) >= hammerCost;
        public bool IsBroken(int zone) => broken != null && broken[zone];

        float WrapDelta(float d)
        {
            float length = path.Length;
            return Mathf.Repeat(d + length * 0.5f, length) - length * 0.5f;
        }

        Vector3 RightAt(float distance) => Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(path.DirectionAtDistance(distance), Vector3.up)).normalized;

        float Lateral(Vector3 position, float distance) => Vector3.Dot(position - path.PositionAtDistance(distance), RightAt(distance));

        /// <summary>Is the racer on this zone's stretch of road, and is it on the rubble (with a kart's width of margin)?</summary>
        bool InZone(RaceProgress racer, ChaosZone zone, out bool onRubble)
        {
            onRubble = false;
            float along = WrapDelta(racer.PathDistance - zone.distance);
            if (Mathf.Abs(along) > zone.halfLength) return false;
            float lateral = Lateral(racer.transform.position, racer.PathDistance);
            if (lateral < zone.roadMin - 1.5f || lateral > zone.roadMax + 1.5f) return false;
            onRubble = lateral > zone.blockedMin - 0.7f && lateral < zone.blockedMax + 0.7f && Mathf.Abs(along) < zone.halfLength - 0.5f;
            return true;
        }

        /// <summary>
        /// For the AI: the lane to drive in, if chaos matters right now — the free side of a
        /// broken stretch ahead (or straight through the rubble with a hammer, to repair it),
        /// else a coin a little ahead.
        /// </summary>
        public bool LaneHint(RaceProgress racer, float s, out float lane)
        {
            lane = 0f;
            if (!Active || broken == null) return false;
            bool hammer = HasHammer(racer);
            for (int z = 0; z < zones.Length; z++)
            {
                if (!broken[z]) continue;
                var zone = zones[z];
                float ahead = WrapDelta(zone.distance - s);
                if (ahead < -zone.halfLength || ahead > zone.halfLength + 45f) continue;
                // With a hammer, about half the drivers go through to repair (and get the boost);
                // the rest keep their coins and go round, so smashed road lasts a while.
                bool repair = hammer && ((racer.GetInstanceID() * 13 + EventCount * 7 + z * 5) & 0xFF) < 128;
                if (repair) lane = (zone.blockedMin + zone.blockedMax) * 0.5f;
                else
                {
                    float freeRight = zone.roadMax - zone.blockedMax, freeLeft = zone.blockedMin - zone.roadMin;
                    lane = freeRight >= freeLeft ? (zone.blockedMax + zone.roadMax) * 0.5f : (zone.roadMin + zone.blockedMin) * 0.5f;
                }
                return true;
            }
            if (hammer) return false;

            float current = Lateral(racer.transform.position, s);
            float best = float.MaxValue;
            for (int i = 0; i < coins.Length; i++)
            {
                if (!CoinAvailable(i)) continue;
                float ahead = WrapDelta(coinDistance[i] - s);
                if (ahead < 3f || ahead > 28f || ahead >= best || Mathf.Abs(coinLateral[i] - current) > 2.5f) continue;
                // Each driver only bothers with about half of the coin rows, so hammers stay rare.
                if (((racer.GetInstanceID() * 31 + i / 2 * 17) & 0xFF) > 140) continue;
                best = ahead;
                lane = coinLateral[i];
            }
            return best < float.MaxValue;
        }

        bool CoinAvailable(int i) => !coinTaken[i] && Time.time > coinPredicted[i];

        // ------------------------------------------------------------ Frame

        void Update()
        {
            if (!race) return;
            bool active = Active;
            if (active != coinsShown)
            {
                coinsShown = active;
                foreach (var r in coinRenderers) r.enabled = active;
            }
            if (!active) return;
            foreach (bool b in broken) if (b) BrokenSeconds += Time.deltaTime;

            if (Authority) AuthorityUpdate();
            else MirrorNetworkState();
            CheckLocalKarts();
            UpdateShow();
            AnimateVisuals();
        }

        bool Racing => race.IsOnline
            ? NetRace.Instance && NetRace.Instance.Phase.Value == (int)RaceManager.State.Racing
            : race.CurrentState == RaceManager.State.Racing;

        void AuthorityUpdate()
        {
            float now = Time.time;
            bool changed = false;

            if (Racing)
            {
                if (nextEvent < 0f) nextEvent = now + firstEvent;
                if (now >= nextEvent && pendingZone < 0)
                {
                    int zone = ChooseZone();
                    if (zone >= 0)
                    {
                        pendingZone = zone;
                        pendingLandAt = now + warningTime;
                        if (race.IsOnline) NetRace.Instance.RalphEventRpc(zone, NetworkManager.Singleton.ServerTime.Time + warningTime);
                        else PlayEvent(zone, warningTime);
                        nextEvent = now + UnityEngine.Random.Range(eventInterval.x, eventInterval.y);
                    }
                    else nextEvent = now + 5f;
                }
            }
            if (pendingZone >= 0 && now >= pendingLandAt)
            {
                broken[pendingZone] = true;
                pendingZone = -1;
                changed = true;
            }
            for (int i = 0; i < coins.Length; i++)
                if (coinTaken[i] && now >= coinRespawnAt[i]) { coinTaken[i] = false; changed = true; }
            if (changed) Publish();
        }

        /// <summary>An intact zone, preferably one the leading player will reach soon.</summary>
        int ChooseZone()
        {
            var candidates = new List<int>();
            var preferred = new List<int>();
            var lead = race.LeadingHuman;
            for (int z = 0; z < zones.Length; z++)
            {
                if (broken[z]) continue;
                candidates.Add(z);
                if (!lead) continue;
                float ahead = Mathf.Repeat(zones[z].distance - lead.PathDistance, path.Length);
                if (ahead > 60f && ahead < 300f) preferred.Add(z);
            }
            var pool = preferred.Count > 0 ? preferred : candidates;
            return pool.Count == 0 ? -1 : pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        /// <summary>Host: share zones and coins with everyone (two small bit masks).</summary>
        void Publish()
        {
            if (!race.IsOnline || !NetRace.Instance) return;
            byte zonesMask = 0;
            for (int z = 0; z < zones.Length && z < 8; z++) if (broken[z]) zonesMask |= (byte)(1 << z);
            uint coinsMask = 0;
            for (int i = 0; i < coins.Length && i < 32; i++) if (coinTaken[i]) coinsMask |= 1u << i;
            if (NetRace.Instance.BrokenZones.Value != zonesMask) NetRace.Instance.BrokenZones.Value = zonesMask;
            if (NetRace.Instance.TakenCoins.Value != coinsMask) NetRace.Instance.TakenCoins.Value = coinsMask;
        }

        void MirrorNetworkState()
        {
            var net = NetRace.Instance;
            if (!net) return;
            for (int z = 0; z < zones.Length && z < 8; z++) broken[z] = (net.BrokenZones.Value & (1 << z)) != 0;
            for (int i = 0; i < coins.Length && i < 32; i++) coinTaken[i] = (net.TakenCoins.Value & (1u << i)) != 0;
        }

        /// <summary>Karts driven on this machine: coins they touch, rubble under them, repairs.</summary>
        void CheckLocalKarts()
        {
            float r2 = coinRadius * coinRadius;
            foreach (var racer in race.Racers)
            {
                if (!racer || !racer.hasAuthority) continue;
                bool hammer = HasHammer(racer);
                if (!hammer)
                {
                    Vector3 p = racer.transform.position;
                    for (int i = 0; i < coins.Length; i++)
                        if (CoinAvailable(i) && (coins[i].position - p).sqrMagnitude < r2) { RequestCoin(racer, i); break; }
                }

                for (int z = 0; z < zones.Length; z++)
                {
                    if (!broken[z] || !InZone(racer, zones[z], out bool onRubble)) continue;
                    if (!onRubble) continue;
                    // Felix's hammer works when you drive into the rubble.
                    if (hammer) { RequestRepair(racer, z); continue; }
                    if (Time.time < repairCooldown[z]) continue;
                    racer.Kart.SetRough();
                    if (racer.isPlayer && (!roughSoundAt.TryGetValue(racer, out float at) || Time.time > at))
                    {
                        roughSoundAt[racer] = Time.time + 0.9f;
                        if (Lib && Lib.crashes is { Length: > 0 }) AudioHub.PlayAt(Lib.crashes[UnityEngine.Random.Range(0, Lib.crashes.Length)], racer.transform.position, 0.7f);
                        ShakeCameraOf(racer, 0.12f);
                    }
                }
            }
        }

        void RequestCoin(RaceProgress racer, int coin)
        {
            coinPredicted[coin] = Time.time + 1.5f; // hide at once; the host's answer follows
            if (racer.isPlayer)
            {
                if (Lib) AudioHub.PlayUI(Lib.coin, 0.7f, 1f + 0.06f * CoinsOf(racer));
                CoinCollected?.Invoke(racer);
            }
            if (Authority) TakeCoin(racer, coin);
            else if (NetRace.Instance && netKarts.TryGetValue(racer, out var net) && net) NetRace.Instance.TakeCoinRpc(net.NetworkObject, coin);
        }

        /// <summary>Authority: a racer picks up a coin (if it is still there and they have room).</summary>
        public void TakeCoin(RaceProgress racer, int coin)
        {
            if (!racer || coin < 0 || coin >= coins.Length || coinTaken[coin] || HasHammer(racer)) return;
            coinTaken[coin] = true;
            coinRespawnAt[coin] = Time.time + coinRespawn;
            AddCoins(racer, 1);
            Publish();
        }

        void AddCoins(RaceProgress racer, int amount)
        {
            int value = Mathf.Clamp(CoinsOf(racer) + amount, 0, hammerCost);
            if (netKarts.TryGetValue(racer, out var net) && net) net.Coins.Value = value;
            else racer.Coins = value;
        }

        void RequestRepair(RaceProgress racer, int zone)
        {
            if (Time.time < repairCooldown[zone]) return;
            repairCooldown[zone] = Time.time + 1f; // no rubble slowdown while the host confirms
            if (Authority) Repair(racer, zone);
            else if (NetRace.Instance && netKarts.TryGetValue(racer, out var net) && net) NetRace.Instance.RepairRpc(net.NetworkObject, zone);
        }

        /// <summary>Authority: Felix's hammer fixes the zone, costing the racer their coins.</summary>
        public void Repair(RaceProgress racer, int zone)
        {
            if (!racer || zone < 0 || zone >= zones.Length || !broken[zone] || !HasHammer(racer)) return;
            broken[zone] = false;
            AddCoins(racer, -hammerCost);
            Publish();
            if (race.IsOnline && netKarts.TryGetValue(racer, out var net) && net) NetRace.Instance.RepairedRpc(net.NetworkObject, zone);
            else OnRepaired(racer, zone);
        }

        /// <summary>Everyone: the repair happened — sparkle, sound, and a boost for whoever drives that kart.</summary>
        public void OnRepaired(RaceProgress racer, int zone)
        {
            if (zone >= 0 && zone < zones.Length) broken[zone] = false;
            if (!racer) return;
            if (racer.hasAuthority) racer.Kart.Boost(repairBoost);
            if (Lib) AudioHub.PlayAt(Lib.hammerFix, racer.transform.position, 1f);
            if (racer.isPlayer && Lib) AudioHub.PlayUI(Lib.hammerFix, 0.6f);
            RepairCount++;
            StartBurst(racer.transform.position + Vector3.up * 1f);
            Repaired?.Invoke(racer);
        }

        // ------------------------------------------------------------ Ralph's show

        /// <summary>Everyone: Ralph lands on <paramref name="zone"/> in <paramref name="secondsToLanding"/>.</summary>
        public void PlayEvent(int zone, float secondsToLanding)
        {
            if (zone < 0 || zone >= zones.Length) return;
            EventCount++;
            showZone = zone;
            showLandAt = Time.time + secondsToLanding;
            nextHit = 0;
            var z = zones[zone];
            if (ralph) ralph.Play(z.ralphSpot, -path.DirectionAtDistance(z.distance), z.ralphSide, secondsToLanding);
            if (Lib) AudioHub.PlayUI(Lib.ralphWarning, 0.9f);
            RalphIncoming?.Invoke(zone);
        }

        void UpdateShow()
        {
            if (showZone < 0) return;
            float t = Time.time - showLandAt;
            var spot = zones[showZone].ralphSpot;
            if (t < 0f) ShakeCameras(spot, 0.05f, 0.02f); // the ground rumbles as he comes
            while (nextHit < RalphPuppet.Hits.Length && t >= RalphPuppet.Hits[nextHit])
            {
                bool first = nextHit == 0;
                if (Lib) AudioHub.PlayAt(Lib.ralphSmash, spot, first ? 1f : 0.7f, first ? 1f : 1.12f);
                if (first && Lib) AudioHub.PlayUI(Lib.ralphSmash, 0.35f);
                ShakeCameras(spot, first ? 0.55f : 0.3f, first ? 0.08f : 0.03f);
                nextHit++;
            }
            if (ralph)
            {
                bool near = false;
                foreach (var cam in LocalCameras())
                    if ((cam.transform.position - ralph.transform.position).sqrMagnitude < visibleDistance * visibleDistance) near = true;
                ralph.SetVisible(near);
            }
            if (t > RalphPuppet.EndAt) showZone = -1;
        }

        IEnumerable<KartCamera> LocalCameras()
        {
            if (race.kartCamera) yield return race.kartCamera;
            if (race.SecondCamera) yield return race.SecondCamera;
        }

        /// <summary>Shakes every local camera: strong near the spot, a little everywhere.</summary>
        void ShakeCameras(Vector3 spot, float near, float far)
        {
            foreach (var cam in LocalCameras())
                cam.Shake(Mathf.Lerp(near, far, Vector3.Distance(cam.transform.position, spot) / 220f));
        }

        void ShakeCameraOf(RaceProgress racer, float amount)
        {
            foreach (var cam in LocalCameras())
                if (cam.target == racer.Kart) cam.Shake(amount);
        }

        // ------------------------------------------------------------ Visuals

        void AnimateVisuals()
        {
            float dt = Time.deltaTime;
            Quaternion spin = Quaternion.Euler(0f, Time.time * 160f, 0f);
            for (int i = 0; i < coins.Length; i++)
            {
                bool visible = CoinAvailable(i);
                if (coinRenderers[i].enabled != visible) coinRenderers[i].enabled = visible;
                if (visible) coins[i].localRotation = spin * coinBase[i];
            }

            for (int z = 0; z < zones.Length; z++)
            {
                float target = broken[z] ? 1f : 0f;
                float shown = rubbleShown[z];
                if (Mathf.Approximately(shown, target)) continue;
                shown = Mathf.MoveTowards(shown, target, dt * (target > shown ? 3.5f : 2.5f));
                rubbleShown[z] = shown;
                var rubble = zones[z].rubble;
                rubble.gameObject.SetActive(shown > 0f);
                // Pops up with an overshoot, sinks away smoothly.
                float s = target > 0f ? EaseOutBack(shown) : shown * shown;
                rubble.localScale = rubbleScale[z] * Mathf.Max(0.001f, s);
            }

            if (Time.time < burstUntil)
                for (int i = 0; i < BurstCount; i++)
                {
                    if (!burst[i]) continue;
                    burstVelocity[i] += Physics.gravity * dt;
                    burst[i].position += burstVelocity[i] * dt;
                    burst[i].rotation = spin;
                }
            else if (burst != null && burst.Length > 0 && burst[0] && burst[0].gameObject.activeSelf)
                foreach (var b in burst) b.gameObject.SetActive(false);
        }

        void StartBurst(Vector3 at)
        {
            if (burst == null) return;
            burstUntil = Time.time + 0.9f;
            for (int i = 0; i < BurstCount; i++)
            {
                if (!burst[i]) continue;
                burst[i].position = at;
                float angle = i * Mathf.PI * 2f / BurstCount;
                burstVelocity[i] = new Vector3(Mathf.Cos(angle) * 3.5f, 7f + (i % 3), Mathf.Sin(angle) * 3.5f);
                burst[i].gameObject.SetActive(true);
                var r = burst[i].GetComponent<Renderer>();
                if (r) r.enabled = true;
            }
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
