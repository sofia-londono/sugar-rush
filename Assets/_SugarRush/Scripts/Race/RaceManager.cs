using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SugarRush
{
    /// <summary>
    /// Runs a race: spawns the player and AI karts on the grid, counts down, ranks racers,
    /// detects the finish, and handles pause / restart / quit to menu.
    /// Online races are handled in RaceManager.Online.cs; everything here is the single player flow.
    /// </summary>
    public partial class RaceManager : MonoBehaviour
    {
        public enum State { Countdown, Racing, Finished }

        public static RaceManager Instance { get; private set; }

        public KartRoster roster;
        public TrackPath path;
        public KartCamera kartCamera;

        [Header("Grid")]
        public float firstSlotBehindLine = 8f;
        public float slotSpacing = 7f;
        public float slotSideOffset = 2.6f;

        [Header("AI")]
        public AIDifficulty aiDifficulty;

        public State CurrentState { get; private set; } = State.Countdown;
        public int Countdown { get; private set; } = 3;
        public float RaceTime { get; private set; }
        public int Laps { get; private set; }
        public bool IsPaused { get; private set; }
        public bool NewRecord { get; private set; }
        public RaceProgress Player { get; private set; }
        public readonly List<RaceProgress> Racers = new();

        public event System.Action<State> StateChanged;
        public event System.Action<RaceProgress> RacerLapCompleted;

        int finishedCount;

        public AIDifficulty.Level DifficultyLevel => aiDifficulty ? aiDifficulty.Get(GameSettings.Difficulty) : new AIDifficulty.Level();

        public bool CanDrive(RaceProgress racer) =>
            CurrentState != State.Countdown && !(racer && racer.isPlayer && racer.Finished);

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            Laps = GameSettings.Laps;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void Start()
        {
            if (IsOnline) { StartOnline(); return; }
            SpawnRacers();
            StartCoroutine(CountdownRoutine());
        }

        /// <summary>The human furthest along the track (rubber banding reference for the AI).</summary>
        public RaceProgress LeadingHuman
        {
            get
            {
                RaceProgress best = null;
                foreach (var r in Racers)
                    if (r.isHuman && !r.Finished && (!best || r.RaceDistance > best.RaceDistance)) best = r;
                return best;
            }
        }

        void SpawnRacers()
        {
            int playerIndex = Mathf.Clamp(GameSettings.SelectedKart, 0, roster.karts.Length - 1);
            // The player starts at the back of the grid; rivals fill the slots ahead.
            var order = new List<int>();
            for (int i = 0; i < roster.karts.Length; i++) if (i != playerIndex) order.Add(i);
            order.Add(playerIndex);

            float minY = float.MaxValue;
            foreach (var p in path.points) minY = Mathf.Min(minY, p.y);

            for (int slot = 0; slot < order.Count; slot++)
            {
                var entry = roster.karts[order[slot]];
                float distance = -(firstSlotBehindLine + slot * slotSpacing);
                Vector3 pos = path.PositionAtDistance(distance);
                Vector3 dir = Vector3.ProjectOnPlane(path.DirectionAtDistance(distance), Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir);
                pos += right * (slot % 2 == 0 ? -slotSideOffset : slotSideOffset);
                pos = GroundAt(pos) + Vector3.up * 0.3f;

                var go = Instantiate(entry.prefab, pos, Quaternion.LookRotation(dir, Vector3.up));
                go.name = "Kart_" + entry.displayName;
                var kart = go.GetComponent<KartController>();
                kart.killY = minY - 30f;

                var progress = go.AddComponent<RaceProgress>();
                progress.path = path;
                progress.racerName = entry.displayName;
                progress.isPlayer = order[slot] == playerIndex;
                progress.isHuman = progress.isPlayer;
                progress.LapCompleted += OnLapCompleted;
                Racers.Add(progress);

                if (progress.isPlayer)
                {
                    go.AddComponent<PlayerKartInput>();
                    Player = progress;
                }
                else
                {
                    var ai = go.AddComponent<AIKartDriver>();
                    ai.personality = entry.personality;
                    ai.difficulty = DifficultyLevel;
                    ai.laneOffset = (slot % 2 == 0 ? -1f : 1f) * Random.Range(0.5f, 2.2f);
                }
                go.AddComponent<KartAudio>().isPlayer = progress.isPlayer;
            }

            if (kartCamera)
            {
                kartCamera.target = Player.Kart;
                kartCamera.SnapToTarget();
            }
            UpdatePositions();
        }

        Vector3 GroundAt(Vector3 point)
        {
            float bestDy = float.MaxValue;
            Vector3 result = point;
            foreach (var h in Physics.RaycastAll(point + Vector3.up * 3f, Vector3.down, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (h.normal.y < 0.5f) continue;
                float dy = Mathf.Abs(h.point.y - point.y);
                if (dy < bestDy) { bestDy = dy; result = h.point; }
            }
            return result;
        }

        IEnumerator CountdownRoutine()
        {
            SetState(State.Countdown);
            for (Countdown = 3; Countdown > 0; Countdown--)
                yield return new WaitForSeconds(1f);
            Countdown = 0;
            RaceTime = 0f;
            foreach (var r in Racers) r.MarkLapStart(0f);
            SetState(State.Racing);
        }

        void Update()
        {
            if (IsOnline) UpdateOnline();
            else if (CurrentState != State.Countdown) RaceTime += Time.deltaTime;
            UpdatePositions();
        }

        void UpdatePositions()
        {
            Racers.Sort((a, b) =>
            {
                if (a.Finished != b.Finished) return a.Finished ? -1 : 1;
                if (a.Finished) return a.FinishTime.CompareTo(b.FinishTime);
                return b.RaceDistance.CompareTo(a.RaceDistance);
            });
            for (int i = 0; i < Racers.Count; i++) Racers[i].Position = i + 1;
        }

        void OnLapCompleted(RaceProgress racer)
        {
            if (IsOnline) { OnLapCompletedOnline(racer); return; }
            if (CurrentState == State.Countdown || racer.Finished) return;
            RacerLapCompleted?.Invoke(racer);
            if (racer.CompletedLaps < Laps) return;

            racer.Finished = true;
            racer.FinishTime = RaceTime;
            finishedCount++;

            if (racer.isPlayer)
            {
                NewRecord = GameSettings.TrySetBestTime(Laps, RaceTime);
                // Let the computer drive the player's kart for the victory lap.
                racer.GetComponent<PlayerKartInput>().enabled = false;
                var ai = racer.gameObject.AddComponent<AIKartDriver>();
                ai.difficulty = new AIDifficulty.Level { speedScale = 0.8f, catchUpBoost = 0f, leadSlowdown = 0f };
                SetState(State.Finished);
            }
        }

        void SetState(State state)
        {
            CurrentState = state;
            StateChanged?.Invoke(state);
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            if (IsOnline)
            {
                // Online the race can't stop for one player: just show the menu and coast.
                AudioHub.Instance.Duck(paused);
                return;
            }
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused; // pauses engines; music and menu sounds ignore it
            AudioHub.Instance.Duck(paused);
        }

        public void PlayerBackToTrack()
        {
            SetPaused(false);
            if (Player && !Player.Finished) Player.ReturnToTrack();
        }

        public void Restart()
        {
            SetPaused(false);
            if (IsOnline) { if (NetLobby.Instance) NetLobby.Instance.StartRace(); return; }
            SceneManager.LoadScene(SceneNames.Race);
        }

        public void QuitToMenu()
        {
            SetPaused(false);
            if (IsOnline)
            {
                // Host takes everyone back to the room; a guest leaves the room.
                if (OnlineSession.IsHost && NetLobby.Instance) NetLobby.Instance.BackToRoom();
                else OnlineSession.LeaveToMenu();
                return;
            }
            SceneManager.LoadScene(SceneNames.MainMenu);
        }
    }
}
