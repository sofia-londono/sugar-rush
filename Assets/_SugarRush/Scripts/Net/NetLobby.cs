using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SugarRush
{
    /// <summary>One connected player in the room: who they are and which racer they drive.</summary>
    public struct LobbyPlayer : INetworkSerializable, IEquatable<LobbyPlayer>
    {
        public ulong ClientId;
        public int Kart;
        public int Number;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Kart);
            serializer.SerializeValue(ref Number);
        }

        public bool Equals(LobbyPlayer other) => ClientId == other.ClientId && Kart == other.Kart && Number == other.Number;
    }

    /// <summary>
    /// The waiting room, shared by everyone in the session and kept alive between scenes.
    /// The host owns the player list and makes sure no two players drive the same character.
    /// </summary>
    public class NetLobby : NetworkBehaviour
    {
        const string Resource = "Net/NetLobby";

        public static NetLobby Instance { get; private set; }

        public KartRoster roster;
        public NetworkList<LobbyPlayer> Players;

        int KartCount => roster ? roster.karts.Length : 5;

        /// <summary>Bumped whenever the list changes, so UIs can refresh cheaply.</summary>
        public int Version { get; private set; }

        void Awake() => Players = new NetworkList<LobbyPlayer>();

        public static void SpawnForHost()
        {
            var go = Instantiate(Resources.Load<GameObject>(Resource));
            go.GetComponent<NetworkObject>().Spawn(destroyWithScene: false);
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Players.OnListChanged += _ => Version++;

            if (IsServer)
            {
                AddPlayer(NetworkManager.LocalClientId, GameSettings.SelectedKart);
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
            else
            {
                RequestKartRpc(GameSettings.SelectedKart);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            if (Instance == this) Instance = null;
        }

        public bool TryGetPlayer(ulong clientId, out LobbyPlayer player)
        {
            foreach (var p in Players)
                if (p.ClientId == clientId) { player = p; return true; }
            player = default;
            return false;
        }

        public bool IsKartTaken(int kart, ulong exceptClient)
        {
            foreach (var p in Players)
                if (p.Kart == kart && p.ClientId != exceptClient) return true;
            return false;
        }

        // ------------------------------------------------------------ Server

        void OnClientConnected(ulong clientId)
        {
            if (!TryGetPlayer(clientId, out _)) AddPlayer(clientId, -1);
        }

        void OnClientDisconnected(ulong clientId)
        {
            for (int i = Players.Count - 1; i >= 0; i--)
                if (Players[i].ClientId == clientId) Players.RemoveAt(i);
        }

        void AddPlayer(ulong clientId, int wantedKart)
        {
            Players.Add(new LobbyPlayer { ClientId = clientId, Kart = FreeKart(wantedKart, clientId), Number = FreeNumber() });
        }

        /// <summary>The wanted racer if nobody else has it, otherwise the first free one.</summary>
        int FreeKart(int wanted, ulong clientId)
        {
            int count = KartCount;
            if (wanted >= 0 && wanted < count && !IsKartTaken(wanted, clientId)) return wanted;
            for (int k = 0; k < count; k++)
                if (!IsKartTaken(k, clientId)) return k;
            return 0;
        }

        int FreeNumber()
        {
            for (int n = 1; ; n++)
            {
                bool used = false;
                foreach (var p in Players) used |= p.Number == n;
                if (!used) return n;
            }
        }

        [Rpc(SendTo.Server)]
        public void RequestKartRpc(int kart, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].ClientId != sender) continue;
                var p = Players[i];
                if (kart >= 0 && kart < KartCount && !IsKartTaken(kart, sender)) p.Kart = kart;
                Players[i] = p;
                return;
            }
            AddPlayer(sender, kart);
        }

        /// <summary>Next racer in the given direction that nobody else in the room is driving.</summary>
        public int NextFreeKart(int current, int direction, ulong clientId)
        {
            int count = KartCount;
            for (int step = 1; step <= count; step++)
            {
                int k = ((current + direction * step) % count + count) % count;
                if (!IsKartTaken(k, clientId)) return k;
            }
            return current;
        }

        /// <summary>Host only: everyone goes to the race track (the room is locked meanwhile).</summary>
        public void StartRace()
        {
            if (!IsServer) return;
            OnlineSession.SetRoomLocked(true);
            NetworkManager.SceneManager.LoadScene(SceneNames.Race, LoadSceneMode.Single);
        }

        /// <summary>Host only: everyone goes back to the waiting room (main menu scene).</summary>
        public void BackToRoom()
        {
            if (!IsServer) return;
            OnlineSession.SetRoomLocked(false);
            NetworkManager.SceneManager.LoadScene(SceneNames.MainMenu, LoadSceneMode.Single);
        }
    }
}
