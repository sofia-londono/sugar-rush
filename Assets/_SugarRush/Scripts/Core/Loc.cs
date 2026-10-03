using System.Collections.Generic;

namespace SugarRush
{
    /// <summary>
    /// Tiny Spanish/English string table. Use Loc.T("key") or Loc.T("key", args) for formatted text.
    /// </summary>
    public static class Loc
    {
        static readonly Dictionary<string, (string es, string en)> Table = new()
        {
            // Main menu
            ["menu.subtitle"] = ("¡La carrera más dulce!", "The sweetest race!"),
            ["menu.play"] = ("Jugar", "Play"),
            ["menu.characters"] = ("Elegir personaje", "Choose racer"),
            ["menu.options"] = ("Opciones", "Options"),
            ["menu.quit"] = ("Salir", "Quit"),
            ["menu.back"] = ("Volver", "Back"),
            ["menu.select"] = ("¡Elegir!", "Pick!"),
            ["menu.credits"] = ("Juego de fans sin fines de lucro · Modelos 3D: RazyBerry y amogusstrikesback2 (CC BY 4.0) · Fuente: Lilita One (OFL)",
                                "Non-profit fan game · 3D models: RazyBerry & amogusstrikesback2 (CC BY 4.0) · Font: Lilita One (OFL)"),
            ["menu.best"] = ("Récord ({0} vueltas): {1}", "Best ({0} laps): {1}"),

            // Characters
            ["char.title"] = ("Elige tu corredor", "Choose your racer"),
            ["char.speed"] = ("Velocidad", "Speed"),
            ["char.accel"] = ("Aceleración", "Acceleration"),
            ["char.handling"] = ("Manejo", "Handling"),

            // Options
            ["opt.title"] = ("Opciones", "Options"),
            ["opt.language"] = ("Idioma", "Language"),
            ["opt.language.value"] = ("Español", "English"),
            ["opt.volume"] = ("Volumen", "Volume"),
            ["opt.quality"] = ("Gráficos", "Graphics"),
            ["opt.quality.0"] = ("Rendimiento", "Performance"),
            ["opt.quality.1"] = ("Calidad", "Quality"),
            ["opt.laps"] = ("Vueltas", "Laps"),

            // HUD
            ["hud.lap"] = ("Vuelta {0}/{1}", "Lap {0}/{1}"),
            ["hud.go"] = ("¡YA!", "GO!"),
            ["hud.finalLap"] = ("¡Última vuelta!", "Final lap!"),
            ["hud.wrongWay"] = ("¡Sentido contrario!", "Wrong way!"),
            ["hud.lost"] = ("¿Perdido? Presiona R (o Y en el control) para volver a la pista",
                            "Lost? Press R (or Y on a gamepad) to get back on track"),
            ["hud.autoReturn"] = ("Volviendo a la pista en {0}...", "Returning to track in {0}..."),
            ["hud.pauseHint"] = ("Esc: pausa", "Esc: pause"),
            ["hud.lapTime"] = ("Vuelta {0}: {1}", "Lap {0}: {1}"),

            // Pause
            ["pause.title"] = ("Pausa", "Paused"),
            ["pause.resume"] = ("Reanudar", "Resume"),
            ["pause.restart"] = ("Reiniciar carrera", "Restart race"),
            ["pause.checkpoint"] = ("Volver al último checkpoint", "Back to last checkpoint"),
            ["pause.mainMenu"] = ("Menú principal", "Main menu"),

            // Results
            ["results.title"] = ("¡Carrera terminada!", "Race complete!"),
            ["results.retry"] = ("Correr otra vez", "Race again"),
            ["results.newRecord"] = ("¡Nuevo récord!", "New record!"),
            ["results.bestLap"] = ("Mejor vuelta: {0}", "Best lap: {0}"),
            ["results.racing"] = ("corriendo...", "racing..."),
        };

        public static string T(string key)
        {
            if (!Table.TryGetValue(key, out var entry)) return key;
            return GameSettings.Language == Language.Spanish ? entry.es : entry.en;
        }

        public static string T(string key, params object[] args) => string.Format(T(key), args);

        /// <summary>1 -> "1º" in Spanish, "1st" in English.</summary>
        public static string Ordinal(int n)
        {
            if (GameSettings.Language == Language.Spanish) return n + "º";
            int mod100 = n % 100;
            string suffix = mod100 is >= 11 and <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return n + suffix;
        }

        public static string Time(float seconds)
        {
            if (seconds <= 0f) return "--:--.--";
            int minutes = (int)(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return $"{minutes}:{rest.ToString("00.00", System.Globalization.CultureInfo.InvariantCulture)}";
        }
    }
}
