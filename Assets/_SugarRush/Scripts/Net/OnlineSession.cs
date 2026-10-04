using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SugarRush
{
    /// <summary>
    /// Online play entry point: Unity Services sign-in, creating / joining a room by code
    /// (Lobby + Relay through the Multiplayer Services "sessions" API) and leaving.
    /// The NetworkManager only exists while online, so single player never touches networking.
    /// Relay always uses secure WebSockets so desktop, phone and browser players can mix.
    /// </summary>
    public static class OnlineSession
    {
        public const int MaxPlayers = 5;
        const string NetworkManagerResource = "Net/NetworkManager";

        public static ISession Session { get; private set; }
        public static bool IsOnline => Session != null && NetworkManager.Singleton && NetworkManager.Singleton.IsListening;
        public static bool IsHost => IsOnline && NetworkManager.Singleton.IsHost;
        public static string Code => Session?.Code;
        public static string LastError { get; private set; }

        /// <summary>Loc key of a message for the main menu to show (e.g. connection lost).</summary>
        public static string PendingMessageKey { get; set; }

        static bool leaving;

        static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                // A fresh profile per launch, so several copies on one machine (Multiplayer Play
                // Mode, two browser tabs) sign in as different players.
                var options = new InitializationOptions().SetProfile("p" + UnityEngine.Random.Range(0, 99999999));
                await UnityServices.InitializeAsync(options);
            }
            while (UnityServices.State == ServicesInitializationState.Initializing) await Task.Yield();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        static void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton) return;
            var go = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(NetworkManagerResource));
            go.name = "NetworkManager";
            go.GetComponent<UnityTransport>().UseWebSockets = true; // WSS relay on every platform
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        }

        static NetworkOptions SecureWebSockets => new() { RelayProtocol = RelayProtocol.WSS };

        /// <summary>Creates a private room and starts hosting it. Returns false (see LastError) on failure.</summary>
        public static async Task<bool> CreateRoomAsync()
        {
            try
            {
                LastError = null;
                await EnsureSignedInAsync();
                EnsureNetworkManager();
                var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = true }
                    .WithNetworkOptions(SecureWebSockets)
                    .WithRelayNetwork();
                Session = await MultiplayerService.Instance.CreateSessionAsync(options);
                NetLobby.SpawnForHost();
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogException(e);
                await CleanupAsync();
                return false;
            }
        }

        /// <summary>Joins a room by its short code. Returns false (see LastError) on failure.</summary>
        public static async Task<bool> JoinRoomAsync(string code)
        {
            try
            {
                LastError = null;
                await EnsureSignedInAsync();
                EnsureNetworkManager();
                var options = new JoinSessionOptions().WithNetworkOptions(SecureWebSockets);
                Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant(), options);
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogException(e);
                await CleanupAsync();
                return false;
            }
        }

        public static async Task LeaveAsync()
        {
            leaving = true;
            try
            {
                if (Session != null) await Session.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Leaving the session failed: " + e.Message);
            }
            await CleanupAsync();
            leaving = false;
        }

        /// <summary>Leaves the room and goes back to the main menu, optionally showing a message there.</summary>
        public static async void LeaveToMenu(string messageKey = null)
        {
            PendingMessageKey = messageKey;
            await LeaveAsync();
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneNames.MainMenu);
        }

        static Task CleanupAsync()
        {
            Session = null;
            var nm = NetworkManager.Singleton;
            if (nm)
            {
                nm.OnClientDisconnectCallback -= OnClientDisconnect;
                if (nm.IsListening) nm.Shutdown();
                UnityEngine.Object.Destroy(nm.gameObject);
            }
            return Task.CompletedTask;
        }

        static void OnClientDisconnect(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (!nm || nm.IsServer || leaving || clientId != nm.LocalClientId) return;
            // The host closed the room or the connection dropped.
            LeaveToMenu("online.lost");
        }
    }
}
