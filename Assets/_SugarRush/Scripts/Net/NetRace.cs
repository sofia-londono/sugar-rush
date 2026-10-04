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
    }
}
