using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Temporary on-screen speed / drift readout until the real HUD exists.
    /// </summary>
    public class DebugSpeedometer : MonoBehaviour
    {
        public KartController kart;
        GUIStyle style;

        void OnGUI()
        {
            if (!kart) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };

            string drift = kart.IsDrifting ? $"  DRIFT {new string('★', kart.DriftLevel)}" : "";
            string boost = kart.IsBoosting ? "  ¡TURBO!" : "";
            GUI.Label(new Rect(20, Screen.height - 70, 600, 40), $"{kart.Speed * 3.6f:0} km/h{drift}{boost}", style);
            GUI.Label(new Rect(20, 16, 900, 30), "WASD / flechas: manejar · Espacio: drift (suelta para turbo) · R: reaparecer", new GUIStyle(style) { fontSize = 14 });
        }
    }
}
