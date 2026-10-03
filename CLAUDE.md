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
- `Art/Karts`, `Art/Track` — modelos importados (FBX de Sketchfab)
- `Materials`, `Prefabs`, `Scenes`, `Audio`, `UI`
- `Scripts/Kart` (controlador arcade, drift, turbo), `Scripts/Race` (vueltas, checkpoints, posiciones), `Scripts/Camera`, `Scripts/AI` (rivales), `Scripts/UI` (HUD, menús)

## Assets (CC Attribution — dar crédito, ver CREDITS.md)
- Karts: "Sugar rush karts | Storybook" por RazyBerry — https://sketchfab.com/3d-models/sugar-rush-karts-storybook-05cffff8f28b4d18965b5b7e0e3b05f3 (~9.6k caras).
  - Archivo: `Assets/_SugarRush/Art/Karts/SRstorybookracers.fbx` (los 5 karts en un solo FBX) + `Textures/`.
  - Corredores según texturas: Vanellope, Taffyta, Rancis, Candlehead, Adorabeezle (body, spoiler y wheel por cada uno; texturas `diffusespec` + algunas `normal`).
  - Algunas piezas pueden venir mal ubicadas; las ruedas deben quedar como objetos separados para girar.
- Pista: "Map_tgsd" por amogusstrikesback2 — https://sketchfab.com/3d-models/map-tgsd-6cf96205665d48b681aab3b129aa9a79 (~108k caras).
  - Archivo: `Assets/_SugarRush/Art/Track/map_tgsd.fbx` + `Textures/` (texturas `road_tgsd_*` para la carretera, `tgsd_*` para el entorno, `Sky_tgsd` para el cielo).
- Los ZIP originales están en `~/Downloads` (no se suben al repo).

## Cómo está armado (estado actual)
- Escena principal: `Assets/_SugarRush/Scenes/SugarRush_Track.unity` (única en Build Settings).
- Todo se genera con `Assets/_SugarRush/Scripts/Editor/SugarRushSetup.cs` (menú **Sugar Rush/**): materiales de karts → prefabs → import de pista → escena. Si cambias el builder, vuelve a correr el paso; no edites a mano lo que genera.
  - Mapeo FBX → personaje: `Base` = Vanellope, `.001` = Taffyta, `.002` = Adorabeezle, `.003` = Rancis, `.004` = Candlehead (sacado de los nombres de Geometry dentro del FBX).
  - Pista importada a escala ×40 (`TrackScale`): carretera ~10 m de ancho, kart ~2 m. +Z es el frente de los karts.
  - Paredes invisibles (`TrackWalls.asset`) solo en bordes de la carretera con caída > 3 m.
  - El sentido de carrera en la salida va hacia donde apuntan las flechas del piso (por eso `FindStart` invierte la dirección).
- Prefabs: `Prefabs/Kart_<Nombre>.prefab` — raíz en layer "Ignore Raycast" (la suspensión usa raycasts y no debe pegarle al propio kart), Rigidbody + BoxCollider sin fricción + `KartController` + `KartVisuals`.
- Scripts de juego (namespace `SugarRush`): `KartController` (física arcade, drift con 2 niveles de turbo, respawn a un punto seguro de ~3 s atrás), `PlayerKartInput` (WASD/flechas, Espacio/Shift drift, R reaparecer, gamepad), `KartVisuals`, `KartCamera`, `DebugSpeedometer` (HUD temporal), `KartTestPilot` (secuencia de inputs para pruebas automáticas).
- Pendiente: vueltas/checkpoints, línea de meta, HUD real, rivales con IA (se puede sacar la ruta de `mini_map_road`), selección de personaje, música, iluminación baked.

## Cómo maneja Claude el Editor
- `unity` está en `~/AppData/Local/Unity/bin` (agregarlo al PATH en Bash). Usar `unity command eval_file` con scripts en `AgentScripts/` (fuera de Assets, ignorado por git); el código es el cuerpo de un método, sin `using` (nombres totalmente calificados).
- Llamar `unity command set_autotick` una vez por sesión: si no, el Editor sin foco no procesa comandos.
- `capture_game_view --save_path` siempre guarda dentro de `Assets/`; borrar la imagen después.
- Probar manejo: entrar en Play, agregar `KartTestPilot` al kart vía eval, `wait_for` su `Finished`, leer `Log`. Nunca agregarlo fuera de Play (ensucia la escena).

## Repositorio
- GitHub (público): https://github.com/sofia-londono/sugar-rush — rama `main`.
- Hacer commit después de cada avance funcional.
- No agregar Co-Authored-By ni firmas de Claude en commits ni PRs.

## Convenciones
- Código C# en inglés; textos del juego y comunicación con la usuaria en español.
- Después de cambiar scripts, correr `unity recompile` y revisar errores antes de dar algo por terminado.
