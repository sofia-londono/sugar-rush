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
            ["menu.credits"] = ("Juego de fans sin fines de lucro · Modelos 3D: RazyBerry y amogusstrikesback2 (CC BY 4.0) · Fuentes: Luckiest Guy (Apache 2.0) y Fredoka (OFL)",
                                "Non-profit fan game · 3D models: RazyBerry & amogusstrikesback2 (CC BY 4.0) · Fonts: Luckiest Guy (Apache 2.0) & Fredoka (OFL)"),
            ["menu.best"] = ("Récord ({0} vueltas): {1}", "Best ({0} laps): {1}"),

            // Online
            ["menu.online"] = ("Jugar en línea", "Play online"),
            ["online.title"] = ("Jugar en línea", "Play online"),
            ["online.create"] = ("Crear sala", "Create room"),
            ["online.join"] = ("Unirse", "Join"),
            ["online.or"] = ("o únete con un código", "or join with a code"),
            ["online.codeHint"] = ("CÓDIGO", "CODE"),
            ["online.connecting"] = ("Conectando...", "Connecting..."),
            ["online.error"] = ("No se pudo conectar. Revisa el código y tu internet.", "Couldn't connect. Check the code and your internet."),
            ["online.full"] = ("La sala está llena (máximo 5 jugadores)", "The room is full (5 players max)"),
            ["online.notFound"] = ("No hay ninguna sala con ese código", "There's no room with that code"),
            ["online.locked"] = ("Esa sala ya está en carrera. Espera a que vuelvan a la sala", "That room is mid-race. Wait until they're back in the room"),
            ["lobby.yourRacer"] = ("Tu corredor", "Your racer"),
            ["lobby.count"] = ("Jugadores {0}/{1}", "Players {0}/{1}"),
            ["lobby.short"] = ("J{0}", "P{0}"),
            ["online.lost"] = ("Se perdió la conexión con la sala", "Lost connection to the room"),
            ["lobby.title"] = ("Sala de espera", "Waiting room"),
            ["lobby.share"] = ("Comparte este código con tus amigos", "Share this code with your friends"),
            ["lobby.player"] = ("Jugador {0}", "Player {0}"),
            ["lobby.you"] = ("tú", "you"),
            ["lobby.host"] = ("anfitrión", "host"),
            ["lobby.start"] = ("¡Empezar!", "Start!"),
            ["lobby.leave"] = ("Salir de la sala", "Leave room"),
            ["lobby.waiting"] = ("Esperando a que el anfitrión empiece...", "Waiting for the host to start..."),
            ["lobby.aiFill"] = ("Los puestos libres los corre la IA", "Empty spots are raced by the AI"),
            ["lobby.backToRoom"] = ("Volver a la sala", "Back to room"),

            // Local split screen
            ["menu.local"] = ("Local (2 jugadores)", "Local (2 players)"),
            ["local.title"] = ("2 jugadores", "2 players"),
            ["local.join"] = ("Presiona A en tu control\no Enter en el teclado para unirte", "Press A on your gamepad\nor Enter on the keyboard to join"),
            ["local.keyboard"] = ("Teclado", "Keyboard"),
            ["local.gamepad"] = ("Control {0}", "Gamepad {0}"),
            ["local.ready"] = ("¡Listo!", "Ready!"),
            ["local.choose"] = ("Elige y confirma", "Choose and confirm"),
            ["local.hint"] = ("← → corredor  ·  A / Enter: listo  ·  B / Esc: atrás", "← → racer  ·  A / Enter: ready  ·  B / Esc: back"),
            ["local.pressButton"] = ("¿No aparece tu control? Presiona cualquier botón en él", "Gamepad not showing? Press any button on it"),
            ["hud.finished"] = ("¡Meta! {0}", "Finish! {0}"),

            // Characters
            ["char.title"] = ("Elige tu corredor", "Choose your racer"),
            ["char.speed"] = ("Velocidad", "Speed"),
            ["char.accel"] = ("Aceleración", "Acceleration"),
            ["char.handling"] = ("Manejo", "Handling"),

            // Options
            ["opt.title"] = ("Opciones", "Options"),
            ["opt.language"] = ("Idioma", "Language"),
            ["opt.language.value"] = ("Español", "English"),
            ["opt.music"] = ("Música", "Music"),
            ["opt.sfx"] = ("Efectos", "Effects"),
            ["opt.difficulty"] = ("Dificultad", "Difficulty"),
            ["opt.difficulty.0"] = ("Fácil", "Easy"),
            ["opt.difficulty.1"] = ("Normal", "Normal"),
            ["opt.difficulty.2"] = ("Difícil", "Hard"),
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
