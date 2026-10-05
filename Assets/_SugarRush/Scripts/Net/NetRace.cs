using Unity.Netcode;

namespace SugarRush
{
    /// <summary>
    /// Race state decided by the host and mirrored on every machine: countdown, start time
    /// (on the shared server clock), number of laps, and who finished when.
    /// </summary>
    public class NetRace : NetworkBehaviour
    {
        public static NetRace Instance { get; private set; }

        public readonly NetworkVariable<int> Phase = new((int)RaceManager.State.Countdown);
        public readonly NetworkVariable<int> Countdown = new(3);
        public readonly NetworkVariable<double> StartTime = new();
        public readonly NetworkVariable<int> Laps = new(3);

        // Ralph's chaos, decided by the host (see RalphChaos).
        public readonly NetworkVariable<bool> ChaosOn = new();
        public readonly NetworkVariable<byte> BrokenZones = new();
        public readonly NetworkVariable<uint> TakenCoins = new();

        public override void OnNetworkSpawn() => Instance = this;

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Host decides a racer finished; everyone records the same time.</summary>
        [Rpc(SendTo.Everyone)]
        public void RacerFinishedRpc(NetworkObjectReference kart, float time)
        {
            if (kart.TryGet(out var obj) && RaceManager.Instance)
                RaceManager.Instance.OnNetRacerFinished(obj.GetComponent<NetKart>(), time);
        }

        /// <summary>Ralph lands on a zone at this server time (everyone plays the same show).</summary>
        [Rpc(SendTo.Everyone)]
        public void RalphEventRpc(int zone, double landServerTime)
        {
            if (RalphChaos.Instance)
                RalphChaos.Instance.PlayEvent(zone, (float)(landServerTime - NetworkManager.ServerTime.Time));
        }

        /// <summary>A machine says the kart it drives touched a coin; the host checks and counts it.</summary>
        [Rpc(SendTo.Server)]
        public void TakeCoinRpc(NetworkObjectReference kart, int coin)
        {
            if (kart.TryGet(out var obj) && RalphChaos.Instance)
                RalphChaos.Instance.TakeCoin(obj.GetComponent<NetKart>().Progress, coin);
        }

        /// <summary>A kart with a full hammer reached a broken zone; the host checks and repairs.</summary>
        [Rpc(SendTo.Server)]
        public void RepairRpc(NetworkObjectReference kart, int zone)
        {
            if (kart.TryGet(out var obj) && RalphChaos.Instance)
                RalphChaos.Instance.Repair(obj.GetComponent<NetKart>().Progress, zone);
        }

        [Rpc(SendTo.Everyone)]
        public void RepairedRpc(NetworkObjectReference kart, int zone)
        {
            if (kart.TryGet(out var obj) && RalphChaos.Instance)
                RalphChaos.Instance.OnRepaired(obj.GetComponent<NetKart>().Progress, zone);
        }
    }
}
