using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SugarRush
{
    /// <summary>
    /// Online race flow. The host spawns every kart (players own theirs, the host owns the AI),
    /// runs the countdown and decides who finished; every machine tracks laps and positions
    /// locally from the synced kart positions so the HUD stays smooth.
    /// </summary>
    public partial class RaceManager
    {
        const string NetRaceResource = "Net/NetRace";

        public bool IsOnline => OnlineSession.IsOnline;
        bool IsNetServer => IsOnline && NetworkManager.Singleton.IsServer;

        float minTrackY = float.MaxValue;

        void StartOnline()
        {
            foreach (var p in path.points) minTrackY = Mathf.Min(minTrackY, p.y);
            SetState(State.Countdown);
            var nm = NetworkManager.Singleton;
            if (nm.IsServer)
            {
                nm.SceneManager.OnLoadEventCompleted += OnRaceSceneLoadedForAll;
                nm.OnClientDisconnectCallback += OnClientLeftRace;
            }
        }

        void OnDisable()
        {
            var nm = NetworkManager.Singleton;
            if (!nm) return;
            if (nm.SceneManager != null) nm.SceneManager.OnLoadEventCompleted -= OnRaceSceneLoadedForAll;
            nm.OnClientDisconnectCallback -= OnClientLeftRace;
        }

        /// <summary>Host: once every client has the track loaded, spawn the grid and count down.</summary>
        void OnRaceSceneLoadedForAll(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName != SceneNames.Race) return;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnRaceSceneLoadedForAll;

            var raceGo = Instantiate(Resources.Load<GameObject>(NetRaceResource));
            var netRace = raceGo.GetComponent<NetRace>();
            raceGo.GetComponent<NetworkObject>().Spawn();
            netRace.Laps.Value = GameSettings.Laps;

            SpawnOnlineGrid();
            StartCoroutine(OnlineCountdown(netRace));
        }

        void SpawnOnlineGrid()
        {
            // Humans from the waiting room keep their racer; the AI drives the rest.
            var humans = new List<LobbyPlayer>();
            var taken = new HashSet<int>();
            if (NetLobby.Instance)
                foreach (var p in NetLobby.Instance.Players)
                    if (NetworkManager.Singleton.ConnectedClients.ContainsKey(p.ClientId) && taken.Add(p.Kart)) humans.Add(p);

            // AI at the front, players at the back (like single player).
            var grid = new List<(int kart, LobbyPlayer? human)>();
            for (int k = 0; k < roster.karts.Length; k++) if (!taken.Contains(k)) grid.Add((k, null));
            foreach (var h in humans) grid.Add((h.Kart, h));

            for (int slot = 0; slot < grid.Count; slot++)
            {
                var (kartIndex, human) = grid[slot];
                var entry = roster.karts[kartIndex];
                float distance = -(firstSlotBehindLine + slot * slotSpacing);
                Vector3 dir = Vector3.ProjectOnPlane(path.DirectionAtDistance(distance), Vector3.up).normalized;
                Vector3 pos = path.PositionAtDistance(distance) + Vector3.Cross(Vector3.up, dir) * (slot % 2 == 0 ? -slotSideOffset : slotSideOffset);
                pos = GroundAt(pos) + Vector3.up * 0.3f;

                var go = Instantiate(entry.netPrefab, pos, Quaternion.LookRotation(dir, Vector3.up));
                go.GetComponent<NetKart>().Configure(kartIndex, human.HasValue,
                    human?.ClientId ?? NetworkManager.ServerClientId, human?.Number ?? 0);
                var netObj = go.GetComponent<NetworkObject>();
                if (human.HasValue) netObj.SpawnWithOwnership(human.Value.ClientId);
                else netObj.Spawn();
            }
        }

        IEnumerator OnlineCountdown(NetRace netRace)
        {
            yield return new WaitForSeconds(1f); // let every machine receive the spawns
            netRace.Phase.Value = (int)State.Countdown;
            for (int c = 3; c > 0; c--)
            {
                netRace.Countdown.Value = c;
                yield return new WaitForSeconds(1f);
            }
            netRace.Countdown.Value = 0;
            netRace.StartTime.Value = NetworkManager.Singleton.ServerTime.Time;
            netRace.Phase.Value = (int)State.Racing;
        }

        /// <summary>Every machine: mirror the host's race state; "Finished" stays local to this player.</summary>
        void UpdateOnline()
        {
            var netRace = NetRace.Instance;
            if (!netRace) return;
            Laps = netRace.Laps.Value;
            Countdown = netRace.Countdown.Value;

            var phase = (State)netRace.Phase.Value;
            bool racing = phase == State.Racing;
            RaceTime = racing ? (float)(NetworkManager.Singleton.ServerTime.Time - netRace.StartTime.Value) : 0f;

            if (CurrentState == State.Countdown && racing)
            {
                foreach (var r in Racers) r.MarkLapStart(0f);
                SetState(State.Racing);
            }
        }

        // ------------------------------------------------------------ Karts

        /// <summary>Called on every machine when a network kart appears.</summary>
        public void RegisterNetKart(NetKart net)
        {
            var entry = roster.Get(net.Character.Value);
            var go = net.gameObject;
            go.name = "Kart_" + entry.displayName;
            net.Kart.killY = minTrackY == float.MaxValue ? -50f : minTrackY - 30f;

            var progress = go.GetComponent<RaceProgress>();
            if (!progress) progress = go.AddComponent<RaceProgress>();
            progress.path = path;
            progress.racerName = entry.displayName;
            progress.LapCompleted += OnLapCompleted;
            net.Progress = progress;
            Racers.Add(progress);

            if (!go.GetComponent<KartAudio>()) go.AddComponent<KartAudio>();
            RefreshNetKart(net);
        }

        public void UnregisterNetKart(NetKart net)
        {
            if (net.Progress) Racers.Remove(net.Progress);
            if (Player == net.Progress) Player = null;
        }

        /// <summary>Re-evaluates whether a kart is this machine's player (after spawn or ownership changes).</summary>
        public void RefreshNetKart(NetKart net)
        {
            var progress = net.Progress;
            if (!progress) return;
            progress.isHuman = net.Human.Value;
            progress.isPlayer = net.IsMine;
            var audio = net.GetComponent<KartAudio>();
            if (audio) audio.isPlayer = net.IsMine;

            if (net.IsMine && Player != progress)
            {
                Player = progress;
                if (kartCamera)
                {
                    kartCamera.target = net.Kart;
                    kartCamera.SnapToTarget();
                }
            }
        }

        /// <summary>Host: the computer drives this kart (empty slot, or a player who left).</summary>
        public void AddAIDriver(NetKart net)
        {
            var ai = net.gameObject.AddComponent<AIKartDriver>();
            ai.personality = roster.Get(net.Character.Value).personality;
            ai.difficulty = DifficultyLevel;
            ai.laneOffset = Random.Range(-2.2f, 2.2f);
        }

        /// <summary>Host: a player dropped out mid-race, so the AI takes over their kart.</summary>
        void OnClientLeftRace(ulong clientId)
        {
            foreach (var r in Racers)
            {
                var net = r.GetComponent<NetKart>();
                if (!net || !net.Human.Value || net.HumanClientId.Value != clientId) continue;
                if (net.OwnerClientId != NetworkManager.ServerClientId) net.NetworkObject.ChangeOwnership(NetworkManager.ServerClientId);
                net.Human.Value = false;
                net.ApplyAuthority();
            }
        }

        // ------------------------------------------------------------ Laps and finish

        void OnLapCompletedOnline(RaceProgress racer)
        {
            if (CurrentState == State.Countdown || racer.Finished) return;
            RacerLapCompleted?.Invoke(racer);
            if (!IsNetServer || racer.CompletedLaps < Laps) return;
            var net = racer.GetComponent<NetKart>();
            if (net && NetRace.Instance) NetRace.Instance.RacerFinishedRpc(net.NetworkObject, RaceTime);
        }

        /// <summary>Every machine: the host says this kart crossed the line.</summary>
        public void OnNetRacerFinished(NetKart net, float time)
        {
            var racer = net ? net.Progress : null;
            if (!racer || racer.Finished) return;
            racer.Finished = true;
            racer.FinishTime = time;

            if (racer == Player)
            {
                // Victory lap on autopilot, like single player (online times don't count as records).
                var input = racer.GetComponent<PlayerKartInput>();
                if (input) Destroy(input);
                var ai = racer.gameObject.AddComponent<AIKartDriver>();
                ai.difficulty = new AIDifficulty.Level { speedScale = 0.8f, catchUpBoost = 0f, leadSlowdown = 0f };
                SetState(State.Finished);
            }
        }
    }
}
