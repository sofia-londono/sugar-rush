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
    /// </summary>
    public class OnlineTestHooks : MonoBehaviour
    {
        static string joinCode;
        static bool autopilot;
        static int wantedKart = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-sr-join" && i + 1 < args.Length) joinCode = args[i + 1];
                if (args[i] == "-sr-autopilot") autopilot = true;
                if (args[i] == "-sr-kart" && i + 1 < args.Length) int.TryParse(args[i + 1], out wantedKart);
            }
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
            var player = RaceManager.Instance.Player;
            Debug.Log("[SR] autopilot on " + player.racerName);
            var input = player.GetComponent<PlayerKartInput>();
            if (input) Destroy(input);
            var ai = player.gameObject.AddComponent<AIKartDriver>();
            ai.difficulty = new AIDifficulty.Level();
            while (player && !player.Finished) yield return new WaitForSeconds(5f);
            if (player) Debug.Log($"[SR] finished {player.racerName} pos={player.Position} time={player.FinishTime:0.0}");
        }
    }
}
