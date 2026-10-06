using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Access code for the web version: the game asks for it once per browser before the menu.
    /// Only a salted SHA-256 of the code ships in the build (Resources/Web/AccessHash, written by
    /// the web build script from web-access-code.txt, which never goes to the public repo).
    /// It keeps casual visitors out; it is not real security (everything runs in the browser).
    /// </summary>
    public static class AccessGate
    {
        const string Salt = "sugar-rush-gate:";
        const string PrefsKey = "accessOk";

        /// <summary>Ask for the code in the editor too (tests / screenshots).</summary>
        public static bool ForceForTesting;

        static string expected;

        static string Expected
        {
            get
            {
                if (expected == null)
                {
                    var asset = Resources.Load<TextAsset>("Web/AccessHash");
                    expected = asset ? asset.text.Trim() : "";
                }
                return expected;
            }
        }

        public static bool Locked
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                bool web = true;
#else
                bool web = false;
#endif
                if (!(web || ForceForTesting) || string.IsNullOrEmpty(Expected)) return false;
                return PlayerPrefs.GetString(PrefsKey, "") != Expected;
            }
        }

        public static string Hash(string code)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Salt + (code ?? "").Trim().ToUpperInvariant()));
            var sb = new StringBuilder();
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>True (and remembered in this browser) if the code is right.</summary>
        public static bool TryUnlock(string code)
        {
            if (Hash(code) != Expected) return false;
            PlayerPrefs.SetString(PrefsKey, Expected);
            PlayerPrefs.Save();
            return true;
        }
    }
}
