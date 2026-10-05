using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SugarRush
{
    public enum RaceMode { Single, LocalSplit }

    /// <summary>
    /// How the next race is set up, chosen in the menu and read by RaceManager. Online races are
    /// configured by the room instead (see OnlineSession / NetLobby).
    /// </summary>
    public static class RaceSetup
    {
        /// <summary>One person on this machine: their racer and the devices only they control.</summary>
        public struct LocalPlayer
        {
            public int Kart;
            public InputDevice[] Devices;
        }

        public static RaceMode Mode { get; private set; } = RaceMode.Single;
        public static readonly List<LocalPlayer> LocalPlayers = new();

        public static bool IsSplitScreen => Mode == RaceMode.LocalSplit && LocalPlayers.Count >= 2;

        /// <summary>Split screen needs a big screen and two input devices: not offered on phones.</summary>
        public static bool SplitScreenAvailable => !Application.isMobilePlatform;

        public static void SetSingle()
        {
            Mode = RaceMode.Single;
            LocalPlayers.Clear();
        }

        public static void SetLocalSplit(IEnumerable<LocalPlayer> players)
        {
            Mode = RaceMode.LocalSplit;
            LocalPlayers.Clear();
            LocalPlayers.AddRange(players);
        }
    }
}
