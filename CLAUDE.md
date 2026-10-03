# Sugar Rush RD

Juego de carreras de karts inspirado en el circuito de Sugar Rush (Wreck-It Ralph / Ralph el demoledor). Proyecto personal de fan sin fines de lucro: el código está en un repo público, pero el juego no se vende ni se distribuye como producto (IP de Disney).

## Stack
- Unity 6000.3.25f1 LTS, URP (Universal 3D), Input System nuevo.
- Unity CLI + paquete `com.unity.pipeline` para conectar Claude Code al Editor (`unity status`, `unity recompile`, `unity command`).
- Plugin de Claude Code: `unity@unity-agent-plugin`.

## Hardware objetivo (portátil de la usuaria)
- Intel i5-10210U, 8 GB RAM, Intel UHD integrada, SSD con poco espacio libre.
- Mantener el juego ligero: URP en calidad Performant/Balanced, iluminación baked para la pista, sombras en tiempo real solo para karts, pocas luces dinámicas, texturas ≤ 2048, sin HDRP.
- Colisiones: la malla simplificada que trae la pista (`raod_tgsd_001`) tiene huecos (p. ej. bajo el arco de hielo), así que todas las piezas visibles de la pista llevan MeshCollider (~100k tris estáticos, aceptable). Solo se excluyen `sky_tgsd` y `mini_map_road`.

## Estructura
Todo el contenido propio va en `Assets/_SugarRush/`:
- `Art/Karts`, `Art/Track` — modelos importados (FBX de Sketchfab); `Art/Generated` — texturas generadas (cuadros de meta, bastón de caramelo)
- `Data/KartRoster.asset` — lista de corredores (prefab, nombre, stats 0..1 para el menú)
- `Materials`, `Prefabs`, `Scenes`, `Audio`
- `Scripts/Core` (GameSettings, Loc, KartRoster), `Scripts/Kart`, `Scripts/Race`, `Scripts/AI`, `Scripts/Camera`, `Scripts/Editor`
- `UI/` — USS, tema, fuente y los scripts de UI (MainMenuUI, RaceUI, KartShowcase, UIKit)

## Assets (CC Attribution — dar crédito, ver CREDITS.md)
- Karts: "Sugar rush karts | Storybook" por RazyBerry — https://sketchfab.com/3d-models/sugar-rush-karts-storybook-05cffff8f28b4d18965b5b7e0e3b05f3 (~9.6k caras).
  - Archivo: `Assets/_SugarRush/Art/Karts/SRstorybookracers.fbx` (los 5 karts en un solo FBX) + `Textures/`.
  - Corredores según texturas: Vanellope, Taffyta, Rancis, Candlehead, Adorabeezle (body, spoiler y wheel por cada uno; texturas `diffusespec` + algunas `normal`).
  - Algunas piezas pueden venir mal ubicadas; las ruedas deben quedar como objetos separados para girar.
- Pista: "Map_tgsd" por amogusstrikesback2 — https://sketchfab.com/3d-models/map-tgsd-6cf96205665d48b681aab3b129aa9a79 (~108k caras).
  - Archivo: `Assets/_SugarRush/Art/Track/map_tgsd.fbx` + `Textures/` (texturas `road_tgsd_*` para la carretera, `tgsd_*` para el entorno, `Sky_tgsd` para el cielo).
- Los ZIP originales están en `~/Downloads` (no se suben al repo).

## Cómo está armado (estado actual)
- Escenas (Build Settings): `Scenes/MainMenu.unity` (0) y `Scenes/SugarRush_Track.unity` (1). Nombres en `SceneNames`.
- Todo se genera con `Scripts/Editor/SugarRushSetup.cs` (menú **Sugar Rush/**, o `BuildAll()`): materiales → prefabs + roster → import de pista → assets de UI → escena de carrera → menú → build settings. Si cambias el builder, vuelve a correr el paso; no edites a mano lo que genera. `BuildMenuScene` lee la ruta desde la escena de carrera, así que va después.
  - Mapeo FBX → personaje: `Base` = Vanellope, `.001` = Taffyta, `.002` = Adorabeezle, `.003` = Rancis, `.004` = Candlehead (sacado de los nombres de Geometry dentro del FBX). Stats por kart en la tabla `Karts` del builder.
  - Pista importada a escala ×40 (`TrackScale`): carretera ~8–10 m de ancho, kart ~2 m. +Z es el frente de los karts.
  - Ruta de carrera (`TrackPath`, 164 puntos cada 4 m, ~668 m por vuelta, índice 0 = línea de meta): trazada a mano sobre una vista cenital (`Route` en el builder), ajustada al centro de `mini_map_road` (esa malla viene rota, no sirve sola) y con alturas por raycast. `RouteOverrides` fija puntos donde la malla del minimapa está corrida (48 = curva cerrada bajo el arco de chocolate).
  - Paredes invisibles (`TrackWalls.asset`) solo en bordes con caída > 3 m, y nunca en bordes que cruzan la ruta (hay un saltito a la salida del anillo).
  - El circuito: recta de salida → eses que suben → anillo elevado (~44 m) → rampa → meseta de ajedrez → salto de 6 m a un puente angosto → curva cerrada bajo el arco → bajada a la meta.
- Carrera: `RaceManager` crea los 5 karts en la parrilla (el jugador sale último), cuenta 3-2-1, ordena posiciones por `RaceProgress.RaceDistance`, detecta la meta y guarda récord por número de vueltas (PlayerPrefs `best_<vueltas>`).
- `RaceProgress` (por kart): vueltas, sentido contrario, fuera de pista (lejos o caído) y atascado (acelera sin avanzar). "Volver a la pista" = `ReturnToTrack()` al punto de la ruta actual; automático a los 6 s (fuera), 1,5 s (caído), 8 s jugador / 4 s IA (atascado). `ReturnLog` registra dónde pasa (diagnóstico).
- `AIKartDriver`: sigue la ruta con look-ahead, carriles solo en tramos rectos, frena antes de curvas (`CornerSpeed`), esquiva obstáculos con "bigotes" (raycasts). Zona que aún cuesta: salida de la meseta/puente (seg ~89–100).
- UI con UI Toolkit, construida por código (`UIKit`), estilos en `UI/SugarRush.uss` vía el tema `SugarRushTheme.tss` (sin el tema por defecto de Unity, por eso el USS estira `.unity-ui-document__root`). Fuente Lilita One (no tiene ★ ◀ ▶; usar ‹ ›). Textos en `Loc` (español/inglés), idioma en Opciones.
- `GameSettings` (PlayerPrefs): idioma, volumen, gráficos (nivel de calidad 0 = Mobile/rendimiento, 1 = PC/calidad; ambos activos en todas las plataformas), vueltas, kart elegido.
- Controles: WASD/flechas, Espacio/Shift drift, R volver a la pista, Esc pausa; gamepad: gatillos/A, stick, RB/X drift, Y volver, Start pausa.
- Pendiente: exportar a WebGL + controles táctiles para celular, música/sonidos, iluminación baked, pulir IA en la meseta.

## Cómo maneja Claude el Editor
- `unity` está en `~/AppData/Local/Unity/bin` (agregarlo al PATH en Bash). Usar `unity command eval_file` con scripts en `AgentScripts/` (fuera de Assets, ignorado por git); el código es el cuerpo de un método, sin `using` (nombres totalmente calificados).
- Llamar `unity command set_autotick` una vez por sesión: si no, el Editor sin foco no procesa comandos.
- `capture_game_view --save_path` siempre guarda dentro de `Assets/`; borrar la imagen después.
- Probar manejo: entrar en Play, agregar `KartTestPilot` al kart vía eval, `wait_for` su `Finished`, leer `Log`. Nunca agregarlo fuera de Play (ensucia la escena).
- Probar una carrera completa: desde Play, `GameSettings.Laps = N` + `SceneManager.LoadScene(SceneNames.Race)` (sin `Save()`), esperar `RaceManager.CurrentState == Racing`, desactivar `PlayerKartInput` del jugador y agregarle `AIKartDriver`, `Time.timeScale = 2`. Al terminar, borrar el récord falso (`PlayerPrefs.DeleteKey("best_N")`).
- Cada comando tarda varios segundos en llegar: no sirve para maniobras cronometradas (usar `KartTestPilot`) ni para capturar momentos breves.
- Con `GetMethod(...).Invoke` se pueden llamar métodos privados de la UI (p. ej. `MainMenuUI.ShowPage`, `RaceUI.SetPaused`) para capturar cada pantalla.
- `UQueryExtensions.Query<T>(root)` en vez de `root.Query<T>()` dentro de eval (no hay `using`).

## Repositorio
- GitHub (público): https://github.com/sofia-londono/sugar-rush — rama `main`.
- Hacer commit después de cada avance funcional.
- No agregar Co-Authored-By ni firmas de Claude en commits ni PRs.

## Convenciones
- Código C# en inglés; textos del juego y comunicación con la usuaria en español.
- Después de cambiar scripts, correr `unity recompile` y revisar errores antes de dar algo por terminado.
