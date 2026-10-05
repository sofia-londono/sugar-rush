using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SugarRush
{
    /// <summary>
    /// Development helper for testing online play without a second person. Only reacts to
    /// command-line arguments, so normal launches are unaffected:
    ///   -sr-join CODE    join that room as soon as the main menu is up
    ///   -sr-autopilot    the computer drives this player's kart in every race
    ///   -sr-local-test   start a split-screen race (both players on autopilot) and log FPS
    ///   -sr-sp-test      start a single-player race on autopilot and log FPS
    ///   -sr-uncapped     with the tests above: no frame cap / vsync, to see the real headroom
    /// </summary>
    public class OnlineTestHooks : MonoBehaviour
    {
        static string joinCode;
        static bool autopilot;
        static int wantedKart = -1;
        static bool localTest, spTest, uncapped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-sr-join" && i + 1 < args.Length) joinCode = args[i + 1];
                if (args[i] == "-sr-autopilot") autopilot = true;
                if (args[i] == "-sr-kart" && i + 1 < args.Length) int.TryParse(args[i + 1], out wantedKart);
                if (args[i] == "-sr-local-test") localTest = true;
                if (args[i] == "-sr-sp-test") spTest = true;
                if (args[i] == "-sr-uncapped") uncapped = true;
            }
            if (localTest || spTest) autopilot = true;
            if (joinCode == null && !autopilot) return;

            var go = new GameObject("OnlineTestHooks");
            DontDestroyOnLoad(go);
            go.AddComponent<OnlineTestHooks>();
            Debug.Log($"[SR] test hooks: join={joinCode} autopilot={autopilot}");
        }

        IEnumerator Start()
        {
            SceneManager.sceneLoaded += (scene, _) =>
            {
                if (autopilot && scene.name == SceneNames.Race) StartCoroutine(Autopilot());
            };

            if (localTest || spTest)
            {
                yield return new WaitForSeconds(2f);
                GameSettings.Laps = 3;
                if (localTest)
                    RaceSetup.SetLocalSplit(new[] { new RaceSetup.LocalPlayer { Kart = 0 }, new RaceSetup.LocalPlayer { Kart = 1 } });
                else RaceSetup.SetSingle();
                SceneManager.LoadScene(SceneNames.Race);
                yield break;
            }

            if (joinCode == null) yield break;
            yield return new WaitForSeconds(2f);
            var join = OnlineSession.JoinRoomAsync(joinCode);
            while (!join.IsCompleted) yield return null;
            Debug.Log($"[SR] join {joinCode}: {join.Result} {OnlineSession.LastErrorKey} {OnlineSession.LastError}");
            if (!join.Result || wantedKart < 0) yield break;
            while (!NetLobby.Instance) yield return null;
            yield return new WaitForSeconds(1f);
            NetLobby.Instance.RequestKartRpc(wantedKart);
            Debug.Log("[SR] requested kart " + wantedKart);
        }

        IEnumerator Autopilot()
        {
            while (!RaceManager.Instance || !RaceManager.Instance.Player) yield return null;
            if (localTest || spTest) StartCoroutine(MeasureFps());
            foreach (var other in RaceManager.Instance.LocalPlayers)
            {
                if (other == RaceManager.Instance.Player) continue;
                var otherInput = other.GetComponent<PlayerKartInput>();
                if (otherInput) Destroy(otherInput);
                other.gameObject.AddComponent<AIKartDriver>().difficulty = new AIDifficulty.Level();
            }
            var player = RaceManager.Instance.Player;
            Debug.Log("[SR] autopilot on " + player.racerName);
            var input = player.GetComponent<PlayerKartInput>();
            if (input) Destroy(input);
            var ai = player.gameObject.AddComponent<AIKartDriver>();
            ai.difficulty = new AIDifficulty.Level();
            while (player && !player.Finished) yield return new WaitForSeconds(5f);
            if (player) Debug.Log($"[SR] finished {player.racerName} pos={player.Position} time={player.FinishTime:0.0}");
        }

        /// <summary>Logs average FPS and the slowest frames every 10 s for 60 s of racing.</summary>
        IEnumerator MeasureFps()
        {
            if (uncapped) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
            while (RaceManager.Instance && RaceManager.Instance.CurrentState == RaceManager.State.Countdown) yield return null;
            var frames = new System.Collections.Generic.List<float>();
            for (int block = 1; block <= 6; block++)
            {
                frames.Clear();
                float end = Time.realtimeSinceStartup + 10f;
                while (Time.realtimeSinceStartup < end) { yield return null; frames.Add(Time.unscaledDeltaTime); }
                frames.Sort();
                float avg = 0f; foreach (var f in frames) avg += f; avg /= frames.Count;
                float p95 = frames[(int)(frames.Count * 0.95f)];
                Debug.Log($"[SR] fps block {block}: avg={1f / avg:0.0} fps ({avg * 1000f:0.0} ms)  slow5%={1f / p95:0.0} fps  " +
                          $"mode={(RaceSetup.IsSplitScreen ? "split" : "single")} res={Screen.width}x{Screen.height} cap={Application.targetFrameRate}");
            }
            Application.Quit();
        }
    }
}
