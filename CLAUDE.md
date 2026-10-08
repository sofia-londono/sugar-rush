# Sugar Rush RD

Juego de carreras de karts inspirado en el circuito de Sugar Rush (Wreck-It Ralph / Ralph el demoledor). Proyecto personal de fan sin fines de lucro: el código está en un repo público, pero el juego no se vende ni se distribuye como producto (IP de Disney).

## Para retomar (leer primero)
Estado al 2026-10-07 (último commit `32911d6`, rama `main` al día con `origin/main`, árbol limpio):
- Funciona y está en el repo: un jugador, pantalla dividida (2 jugadores), en línea (Relay/Lobby, partida rápida, salas privadas/públicas), IA con personalidades y dificultad, caos de Ralph con dulces envueltos y martillo de Félix, corredores 3D sentados en sus karts (menú, selección, podio y en carrera), menú principal con recorrido de cámara, mini-mapa, controles táctiles y versión web.
- Una sola pista: "Map_tgsd" (FBX de Sketchfab, ruta trazada a mano). Todo el código de carrera asume esa pista (ver "Pistas nuevas" abajo).
- **Versión web publicada** en Vercel: https://sugar-rush-sof12.vercel.app (alias https://sugar-rush-dusky.vercel.app). Pide código de acceso una vez por navegador (ver "Versión web"). **Nunca escribir el código en este archivo, en commits ni en mensajes que se suban**: vive solo en `web-access-code.txt` en el PC de la usuaria.

Próximos pasos, en orden:
1. **Esperar las pruebas de la usuaria de la web** (no verificadas aún por Claude en un navegador real, por falta de RAM):
   - Portátil (Chrome/Edge): carga completa, pantalla de código, una carrera, control Logitech (el navegador lo detecta solo después de apretar un botón), FPS aceptables.
   - Celular en horizontal: botones táctiles, "Acelerar solo" (Opciones), aviso "Gira tu teléfono" en vertical.
   - En línea: crear sala en un dispositivo y unirse con el código desde otro (navegador ↔ navegador y navegador ↔ copia de Windows/Editor; todo usa Relay por WSS).
   - Que el récord y el código aceptado sigan ahí al cerrar y volver a abrir (PlayerPrefs en IndexedDB del navegador).
   - Si algo falla, pedir el mensaje que muestra la pantalla de carga ("No se pudo cargar el juego: ...") o la consola del navegador (F12).
2. **Generador de pistas con Unity Splines + pista piloto "Bosque de gomitas"** (decisión confirmada: opción a, Splines, en vez del Racing Kit de Kenney). La piloto debe tener todo funcionando: IA, dulces, caos de Ralph, mini-mapa, pantalla dividida y en línea.
3. **Selector de pista**: en el menú (un jugador y 2 jugadores) y en la sala en línea (elige el anfitrión).
4. **ALTO: cuando la piloto esté lista, esperar a que la usuaria la pruebe** y diga si le gusta cómo se ve y cómo se maneja. Recién después hacer las demás, una por una: río de chocolate, volcán de soda, montaña de helado (temas que eligió). Ella puede buscar assets de decoración: revisar licencia (CC0 o CC BY, agregar a CREDITS.md), ≤ ~3k triángulos por pieza, GLB/FBX.
5. Opcionales: medir FPS con "Pilotos en carrera" activado (copia de Windows `-sr-sp-test`); repetir la prueba en línea después de los cambios de Ralph (punto 6) y de los pilotos; iluminación baked.

### Pistas nuevas: lo que hay que tener en cuenta (plan acordado)
- Paquete `com.unity.splines` **todavía no instalado** (agregarlo al manifest; versión compatible con Unity 6000.3).
- El generador (Editor, en el estilo de `SugarRushSetup`: todo por código, menú "Sugar Rush/...") debe producir desde el spline lo mismo que hoy sale de la pista FBX: malla de carretera + bordes + MeshCollider, `TrackPath` (puntos cada 4 m, índice 0 = meta, alturas), paredes (`TrackWalls`), parrilla de salida, arco de meta, `ChaosZoneSegments` + `CoinRows` para `RalphChaos`, datos del mini-mapa, puntos de saltos para la IA (`jumpSpeed`/`PlannedSpeed`) y tomas para `MenuCameraTour`. Decoración con instancing, pocas luces, neblina pastel (mismo presupuesto de rendimiento que la pista actual).
- Hoy hay constantes atadas a la pista actual (p. ej. `Route`, `RouteOverrides`, `JumpLips`, `GuideWalls`, `ChaosZoneSegments` en `SugarRushSetup`, `RacerLineup` en el punto 139): pasar esos datos a un asset por pista en vez de constantes.
- Probablemente una escena por pista (`SceneNames`, Build Settings) o una escena con la pista cargada según la elección; decidir al empezar.
- Récords: hoy `best_<vueltas>` (`GameSettings.GetBestTime`); con varias pistas pasar a `best_<pista>_<vueltas>` sin perder los récords existentes de la pista actual.
- En línea: la pista elegida va en `NetLobby`/`NetRace` desde el anfitrión → subir `OnlineSession.ProtocolVersion` (hoy `"sr2"`).
- Después de agregar pistas, volver a hacer el build web y publicarlo (el tamaño crece; vigilar los 28 MB actuales).

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

Scripts clave:
- `Scripts/Core`: `GameSettings` (PlayerPrefs), `Loc` (textos es/en), `KartRoster`, `RaceSetup` (modo un jugador / dividida), `CharacterPuppet` (animación de personajes por código), `AccessGate` (código de acceso web).
- `Scripts/Kart`: `KartController` (física), `PlayerKartInput` (teclado/gamepad/táctil), `TouchDriving` (estado de los botones táctiles), `KartVisuals`, y de prueba `KartTestPilot`, `KartTrailRecorder`.
- `Scripts/Race`: `RaceManager` (+ `.Online.cs`, `.Split.cs`), `RaceProgress`, `TrackPath`, `RalphChaos`, `RalphPuppet`, `ResultsPodium`.
- `Scripts/AI`: `AIKartDriver`, `AIPersonality`, `AIDifficulty`. `Scripts/Audio`: `AudioHub`, `KartAudio`, `SoundLibrary`. `Scripts/Camera`: `KartCamera`.
- `Scripts/Net`: `OnlineSession` (sesiones Relay/Lobby, `ProtocolVersion`), `NetLobby`, `NetRace`, `NetKart`, `OnlineTestHooks`.
- `Scripts/Editor`: `SugarRushSetup` (genera todo el contenido), `GlbImport`, `MeshDecimator`, `WebBuild` (build web).
- `UI/`: `MainMenuUI`, `RaceUI`, `KartShowcase`, `MenuCameraTour`, `RacerLineup`, `TrackMinimap`, `CandyElements`, `UIKit`, `SugarRush.uss`.

Fuera de `Assets/_SugarRush/`:
- `Assets/WebGLTemplates/SugarRush/index.html` — plantilla de la página web (en el repo).
- `Tools/web/vercel.json` y `Tools/web/robots.txt` — se copian al build web (en el repo). `Tools/synth_sfx.py` — genera los efectos sintéticos.
- `AgentScripts/` — scripts de Claude para `unity command eval_file` y pruebas en línea (`net_test.sh`, `quick_test.sh`). Ignorado por git; puede tener basura vieja.
- `Web/` — salida del build web (ignorado por git; se publica desde ahí; contiene `.vercel/` con el enlace al proyecto de Vercel).
- `web-access-code.txt` — el código de acceso en texto plano (ignorado por git, solo en el PC de la usuaria). `Assets/_SugarRush/Resources/Web/AccessHash.txt` — su hash, generado por el build (ignorado).
- `CREDITS.md` — créditos de todos los assets CC BY (actualizar al agregar cualquier modelo/textura/sonido).

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
  - Paredes invisibles (`TrackWalls.asset`) solo en bordes con caída > 3 m, y nunca en bordes que cruzan la ruta. En los labios de salto (`JumpLips` = puntos 74 salida del anillo y 89 bajada de la meseta) se quitan las que miran de frente o están a < 2,5 m de la línea, y en todo el circuito los trocitos < 0,5 m cerca de la línea: la malla se rompe en bordes pequeños justo ahí y frenaban a los karts. `GuideWalls` = paredes guía puestas a mano: al final del puente (seg 94), una diagonal que lleva a los karts que aterrizan abiertos hacia la rampa de arcoíris (antes chocaban de frente con un dulce o se encajaban en un hueco de 1 m). `ReturnLog` guarda `seg:motivo@x,y,z`.
  - El circuito: recta de salida → eses que suben → anillo elevado (~44 m) → rampa → meseta de ajedrez → salto de 6 m a un puente angosto → curva cerrada bajo el arco → bajada a la meta.
- Carrera: `RaceManager` crea los 5 karts en la parrilla (el jugador sale último), cuenta 3-2-1, ordena posiciones por `RaceProgress.RaceDistance`, detecta la meta y guarda récord por número de vueltas (PlayerPrefs `best_<vueltas>`).
- `RaceProgress` (por kart): vueltas, sentido contrario, fuera de pista (lejos o caído) y atascado (acelera sin avanzar). "Volver a la pista" = `ReturnToTrack()` al punto de la ruta actual; automático a los 6 s (fuera), 1,5 s (caído), 8 s jugador / 4 s IA (atascado). `ReturnLog` registra dónde pasa (diagnóstico).
- IA: `AIKartDriver` sigue la ruta con look-ahead, planea la velocidad para curvas y saltos (`PlannedSpeed`, muestrea desde la posición actual cada 3 m), esquiva obstáculos con "bigotes" (raycasts), y se desatasca (reversa → `RaceProgress` la devuelve a la pista).
  - Personalidades: `Data/AI/AI_<Nombre>.asset` (`AIPersonality`, editables en el Inspector; el builder solo las crea si faltan, nunca las sobrescribe). Taffyta agresiva (embiste y bloquea), Rancis rápido en rectas / malo en curvas / frena tarde, Candlehead torpe (errores al azar: bamboleo, frenada tardía, duda), Adorabeezle constante y segura, Vanellope equilibrada. Asignadas en `KartRoster.Entry.personality`.
  - Dificultad: `Data/AI/AIDifficulty.asset` (Fácil/Normal/Difícil: velocidad, curvas, errores, agresividad y rubber banding suave). Se elige en Opciones (`GameSettings.Difficulty`).
  - Saltos: la bajada de la meseta al puente (seg ~89, cae 6 m girando ~50°) exige llegar a ≤ `jumpSpeed` (12 m/s); es un límite de seguridad igual para todos. Saltos < 4 m (salida del anillo, seg ~73) no frenan: ir lento ahí deja el kart encallado en el borde.
  - `ReturnToTrack` busca un hueco libre (0 / ±2,5 m) para no soltar dos karts en el mismo punto.
- Manejo del jugador: el teclado sube el giro en ~0,25 s (`steerRise`) y vuelve rápido (`steerFall`); el stick tiene curva (`stickCurve`); `KartController.highSpeedSteering` reduce el giro a alta velocidad; `airControl` permite girar un poco en el aire.
- Audio (todo liviano): `Resources/SoundLibrary.asset` lista los clips; `AudioHub` (persistente) maneja música con crossfade y pocas voces fijas (3 UI 2D + 5 mundo 3D); `KartAudio` por kart: un solo loop de motor con pitch según velocidad, derrape solo del jugador, rivales con motor 3D más bajo y apagado a > 40 m de la cámara. Proyecto: 24 voces reales, DSP buffer 1024. Música OGG en streaming (calidad 0.45); efectos mono 22 kHz, DecompressOnLoad. Volumen de música y efectos separados en Opciones. Los efectos sintéticos se regeneran con `python Tools/synth_sfx.py`.
- UI con UI Toolkit, construida por código (`UIKit`) con estilo "dulces" inspirado en la película, sin logos ni assets de Disney. Estilos en `UI/SugarRush.uss` vía el tema `SugarRushTheme.tss` (sin el tema por defecto de Unity, por eso el USS estira `.unity-ui-document__root`).
  - Elementos dibujados con Painter2D en `UI/CandyElements.cs` (sin imágenes): `CandyTitle` (letras con borde blanco, sombra 3D y brillo recortado en la mitad superior; siempre en MAYÚSCULAS porque las minúsculas de Luckiest Guy se ven raras), `FrostingPanel` (glaseado que gotea + sprinkles, gotas con semilla fija), `CandyStripes` (rayas de bastón animadas en hover/foco), `PeppermintDisc` + `ChevronGlyph` (flechas de menta), `SprinkleRain`.
  - Rebote de botones = transiciones USS con `ease-out-elastic`/`ease-out-back`. Pantallas: `UIKit.ShowScreen` (clases `screen--entering`/`screen--leaving`), escenas: `UIKit.FadeIn`/`FadeOut` (cortina rosada con sprinkles; funciona con el juego en pausa porque UI Toolkit usa tiempo real).
  - En paneles crema usar títulos `CandyTone.Pink`/`Lavender`; el arcoíris pastel solo sobre la escena 3D (si no, se pierde el contraste).
  - Fuentes: Luckiest Guy (títulos, Apache 2.0) y Fredoka SemiBold/Bold (texto/botones, OFL; instancias fijas generadas con fontTools desde la variable).
  - Los elementos Painter2D deben ignorar tamaños NaN antes del primer layout (`!(w >= 1f)`), si no la animación arranca con posiciones NaN.
  - Textos en `Loc` (español/inglés), idioma en Opciones.
- `GameSettings` (PlayerPrefs): idioma, volumen de música y de efectos, dificultad, gráficos (nivel de calidad 0 = Mobile/rendimiento, 1 = PC/calidad; ambos activos en todas las plataformas), vueltas, kart elegido. "Rendimiento" es el valor por defecto en todas partes; `settingsVersion` < 2 (guardado antes de ese cambio) lo fuerza una vez.
- Gráficos "Rendimiento" en carreras de un jugador (también en línea): `Mobile_RPAsset` con render scale 0,85, sombras de la pista apagadas, distancia de dibujado 420 m con neblina pastel y límite de 60 FPS (`RaceManager.SetupPerformanceMode`, comparte `ApplyLowSpec` con la pantalla dividida). Medido en el portátil (copia de Windows, 1080p): Rendimiento 100–113 FPS sin límite (antes ~30–35), Calidad 26–33 FPS.
- Mini-mapa en el HUD (`TrackMinimap`, abajo a la derecha sobre la velocidad; más chico en pantalla dividida): el circuito dibujado una vez con Painter2D desde `TrackPath`, la meta, los tramos rotos por Ralph y un punto por corredor con el color de su kart (el propio más grande). Solo los puntos se mueven cada cuadro.
- Página "Jugar en línea": Partida rápida arriba y dos tarjetas: "¿Juegas con amigos? Crea una sala" (Privada/Pública + Crear sala) y "¿Te pasaron un código?" (campo con ejemplo "EJ: JT7PKQ" + Unirse). La sala de espera explica dónde escriben el código los amigos.
- Controles: WASD/flechas, Espacio/Shift drift, R volver a la pista, Esc pausa; gamepad: gatillos/A, stick, RB/X drift, Y volver, Start pausa.
- Pendiente: iluminación baked; pistas nuevas con Splines (opción a, confirmada; piloto "Bosque de gomitas", esperar que la usuaria la pruebe antes de hacer las demás: río de chocolate, volcán de soda, montaña de helado).

## Versión web
- Build: "Sugar Rush/Web Build" (`WebBuild.Build`): WebGL con Brotli sin fallback (Vercel manda `Content-Encoding: br` por `vercel.json`), nombres con hash + caché larga, data caching, stripping Minimal (los paquetes en línea usan reflexión), IL2CPP OptimizeSize, música a 22 kHz solo en web (el navegador la decodifica entera en RAM). Salida en `./Web` (ignorada por git) + `vercel.json` y `robots.txt` copiados desde `Tools/web/`.
- Plantilla `Assets/WebGLTemplates/SugarRush/index.html`: pantalla de carga rosada, `noindex`, `devicePixelRatio: 1` (más liviano en pantallas de alta densidad y celulares). Además `X-Robots-Tag: noindex` en todos los archivos y `robots.txt` que bloquea todo.
- Código de acceso (`AccessGate`): primera línea de `web-access-code.txt` en la carpeta del proyecto (ignorado por git, el repo es público); el build guarda solo su SHA-256 con sal en `Resources/Web/AccessHash.txt` (también ignorado). Solo se pide en WebGL, una vez por navegador (PlayerPrefs `accessOk`). Sin mayúsculas/espacios importantes. Es una barrera para curiosos, no seguridad real. Para cambiarlo: editar el archivo, volver a hacer el build y publicar.
- Controles táctiles (`TouchDriving` + `RaceUI.BuildTouchControls`): en celulares/tablets (o `TouchDriving.ForceForTesting`): ‹ › a la izquierda; Derrapar, Frenar y Acelerar a la derecha (Acelerar se oculta con "Acelerar solo", opción `GameSettings.AutoAccelerate`, activada por defecto y visible solo en táctil); pausa arriba al centro; tocar el aviso "¿Perdido?" vuelve a la pista. Con táctil, la velocidad y el mini-mapa suben (`hud--touch`). En vertical aparece "Gira tu teléfono" (`UIKit.RotateHint`).
- Publicación: Vercel CLI desde `./Web`, sin subir el build a GitHub. Proyecto Vercel `sugar-rush` (equipo `sof12`, cuenta sofialp5w-8442): https://sugar-rush-sof12.vercel.app (también https://sugar-rush-dusky.vercel.app; `sugar-rush.vercel.app` es de otra persona). Pasos: "Sugar Rush/Web Build" → `cd Web && npx vercel link --yes --project sugar-rush` (si se borró `.vercel`) → `npx vercel deploy --prod --yes`. Verificar con curl que `Build/*.br` lleguen con `Content-Encoding: br`.
- Tamaño del primer build: 28,4 MB de descarga (datos 20 MB, wasm 9,5 MB, todo Brotli); 22 min de build en el portátil (la segunda vez es más rápido). Después de instalar un módulo de plataforma hay que reiniciar Unity (si no: "Build target 'WebGL' not supported" al final del build).
- Cómo funciona el código de acceso, paso a paso: el build lee `web-access-code.txt` (primera línea, mínimo 4 caracteres) → calcula `SHA-256("sugar-rush-gate:" + código en MAYÚSCULAS sin espacios)` → lo escribe en `Resources/Web/AccessHash.txt`, que va dentro del build. En el navegador, `MainMenuUI` abre la página `Gate` si `AccessGate.Locked`; lo escrito se compara por hash y, si coincide, se guarda `accessOk` y no se vuelve a pedir. En el Editor y en la copia de Windows nunca se pide (`AccessGate.ForceForTesting` para probarlo). `accessOk` guarda el hash aceptado, así que al cambiar el código todos deben escribir el nuevo una vez. OJO: si `AccessHash.txt` no existe o está vacío, `Locked` es falso y la web queda **sin código**; por eso hacer el build siempre con `WebBuild.Build()` (que lo genera y falla si falta `web-access-code.txt`), nunca con "Build Profiles" a mano.
- Reconstruir y publicar (con Unity abierto y conectado):
  1. Cerrar Chrome y programas pesados (8 GB de RAM; el build usa mucha).
  2. Ejecutar `SugarRush.EditorTools.WebBuild.Build()` (menú "Sugar Rush/Web Build" o `unity command eval_file` con un script que lo llame y devuelva el texto). Devuelve "Web build OK: X MB ..." o el motivo del fallo.
  3. `cd Web && npx vercel deploy --prod --yes` (si falta `Web/.vercel`, antes `npx vercel link --yes --project sugar-rush`; si pide sesión, la usuaria corre `! npx vercel login`).
  4. Verificar: `curl -sI https://sugar-rush-sof12.vercel.app/Build/<archivo>.wasm.br` → `Content-Encoding: br`, `Content-Type: application/wasm`, `Cache-Control: ... immutable`, `X-Robots-Tag`; `/vercel.json` y `/.env.local` → 404; el HTML lleva `noindex`.
  5. Antes de cualquier commit, `git status` y `git ls-files | grep -i access` → solo `AccessGate.cs` (+ meta). Nunca agregar `Web/`, `web-access-code.txt` ni `AccessHash.txt`.
- Ya verificado (con curl, 2026-10-07): todos los archivos responden; los `.br` con `Content-Encoding: br` y tipo correcto; caché inmutable en `Build/`; `X-Robots-Tag` en todo; meta `noindex`; archivos privados en 404; el repo no tiene el código ni el hash. **Falta**: probar el juego en navegadores reales (ver "Para retomar").
- Por qué así: Brotli sin fallback = descarga más chica y sin descompresión en JS (requiere los headers de `vercel.json`); nombres con hash = caché "para siempre" sin servir archivos viejos; stripping Minimal porque Netcode/Multiplayer Services usan reflexión y con más stripping se rompe lo en línea; `devicePixelRatio: 1` por la GPU integrada y los celulares; el código de acceso es solo para que el link no lo use cualquiera (el repo es público, por eso solo va el hash y con sal).

## Pantalla dividida local (etapa A)
- Menú "2 jugadores (misma pantalla)" (oculto en celulares: `RaceSetup.SplitScreenAvailable`). Cada asiento se toma con A (control) o Enter (teclado); ← → elige corredor (sin repetir), A/Enter = listo, B/Esc = atrás. Al estar los dos listos arranca la carrera.
- Página de selección: `KartShowcase.SetDuoMode` esconde la plataforma giratoria, mueve la cámara a `DuoCameraPose` (mirando recto por la carretera, inclinada hacia abajo para que los karts queden sobre los paneles) y muestra kart + corredor de cada jugador (`ShowDuo`: P1 izquierda, P2 derecha; cambio = salto, listo = celebra). Paneles compactos abajo; ayuda en una píldora oscura. Los controles se numeran por orden de unión (`LocalSlot.JoinOrder`): el primero que se une es "Control 1".
- `RaceSetup` (estático) guarda el modo (`Single` / `LocalSplit`) y por jugador el kart y sus dispositivos; `PlayerKartInput.devices` limita cada kart a su teclado o control (vacío = cualquiera, como en un jugador). "Jugar" vuelve a `Single`.
- `RaceManager.LocalPlayers`: 1 normalmente, 2 en dividida (la IA llena el resto). La carrera termina para la máquina cuando todos los locales cruzan la meta; el récord solo cuenta en un jugador.
- `RaceManager.Split.cs`: segunda cámara (sin AudioListener: se escucha desde la del J1), vistas lado a lado, FOV 74, y ahorro automático: render scale 0,7, sombras de pista apagadas (los karts sí proyectan), distancia de dibujado 320 m con neblina pastel, sombras a 35 m, límite 30 FPS sin vsync. Todo se restaura en `OnDestroy` (el asset de URP es compartido; verificar renderScale = 1 después de probar).
- `RaceUI`: un HUD por jugador local (`PlayerHud`), cuenta regresiva compartida al centro, separador, resultados con "J1 2º · J2 4º".
- Medir FPS reales: copia de Windows con `-sr-sp-test` / `-sr-local-test` (+ `-sr-uncapped`, `-sr-quality 0|1`), loguea "[SR] fps" cada 10 s durante 60 s y se cierra.

## Caos de Ralph (etapa C)
- Opción "Caos de Ralph: Activado/Desactivado" (`GameSettings.RalphChaos`, activado por defecto). En línea manda la opción del anfitrión (`NetRace.ChaosOn`).
- `RalphChaos` (objeto en la escena de carrera, armado por `BuildChaos` en el builder):
  - 3 tramos fijos (`ChaosZoneSegments` = 13, 103, 126: rectos, planos y anchos). Los escombros cubren el lado más angosto de la carretera (de -1 m de la línea hacia ese borde, máx. 5,5 m) y el otro lado queda como desvío. Escombros = una sola malla combinada por tramo (`Art/Generated/Rubble_N.asset`: cráteres de chocolate, grietas, trozos de dulce), sin colliders: frenan por código (`KartController.SetRough`: 42 % de la velocidad máxima y bamboleo).
  - Eventos: el primero a los 14 s de carrera y luego cada 24–36 s, en un tramo intacto, de preferencia 60–300 m delante del humano que va primero. Aviso "¡Ralph viene!" (banner que tiembla y pasos de Ralph) 2,6 s antes. Ralph cae, da 3 golpes y se va; la cámara tiembla según la distancia (`KartCamera.Shake`).
  - Dulces envueltos en vez de monedas (punto 4 de las mejoras): una malla generada (`BuildWrappedCandyMesh`, ~350 triángulos, cuerpo + envoltura con caras de ambos lados en vértices separados) en 4 colores con instancing; ícono `WrappedCandyIcon` en el HUD y texto "2/5 dulces". En el código siguen llamándose `coins`.
  - 16 dulces (8 pares, a 2,8 m de la línea, `CoinRows`), radio 1,5 m, reaparecen a los 15 s. Con 5 monedas está el "martillo de Félix": al entrar a los escombros el tramo se repara, se gastan las 5 monedas y hay turbo de 2,5 s. Antes eran 24 monedas en la línea y la IA juntaba 5 en una vuelta sin querer.
  - IA (`LaneHint`, desde `AIKartDriver.ChooseLane`): esquiva los escombros por el lado libre; con martillo, más o menos la mitad entra a reparar (por sorteo fijo según el kart y el evento). Solo persigue monedas cerca de su carril y en la mitad de los pares. En pruebas, un tramo queda roto unos 40 s.
  - Ralph (opción "c", elegida por la usuaria: todo por código con el esqueleto que trae el modelo): cae al borde exterior de los escombros (`ChaosZone.ralphSpot`, 0,7 m pasado el borde de la banda bloqueada) mirando hacia la carretera, se agacha (muslos/rodillas apuntados en espacio de mundo y luego baja el cuerpo hasta que los pies tocan el piso), dobla la espalda y golpea 6 veces alternando puños (`RalphPuppet.Hits`, `FistTarget`). Cada golpe: sonido, temblor, 6 trozos de carretera (pool de 14 cubos con los materiales de los escombros, física simple por código) y polvo de azúcar (`SugarDust`, un ParticleSystem con Emit). Los escombros crecen con cada golpe (0→100 %). Tiene cuerpo (cápsula + Rigidbody cinemático, solo mientras está de pie): el kart que lo toca rebota. Solo se dibuja (y solo hay trozos/polvo) si una cámara local está a < 170 m.
- En línea el anfitrión decide todo: eventos (`NetRace.RalphEventRpc` con la hora de aterrizaje en el reloj del servidor), tramos rotos (`BrokenZones`, byte) y monedas tomadas (`TakenCoins`, uint). Cada máquina revisa solo los karts que simula (`RaceProgress.hasAuthority`) y pide al host `TakeCoinRpc` / `RepairRpc`; el contador vive en `NetKart.Coins`. La moneda se esconde al instante en la máquina que la tomó (predicción de 1,5 s). `ProtocolVersion` = "sr2".
- Modelos GLB: `Scripts/Editor/GlbImport.cs` (importador propio, sin paquetes: jerarquía, mallas con skin, texturas, materiales URP; espejo en X de glTF a Unity). "Sugar Rush/Characters (GLB)" importa Ralph desde `~/Downloads/ralph_el_demoledor.glb` solo si falta el prefab (`Art/Characters/Ralph`).
- Costo medido (copia de Windows, 1080p, sin límite): un jugador en Rendimiento 94–105 FPS (antes 101–113), pantalla dividida 66–73.

## Menú principal (punto 3 de las mejoras)
- Botones: "Jugar" grande (`candy-button--hero`), los demás modos en una cuadrícula 2×2 (`menu-tiles`, `candy-button--tile`) y "Salir" pequeño abajo (oculto en web).
- Fondo: `MenuCameraTour` recorre lento 4 tomas (los 5 corredores en sus karts bajo el arco START — `RacerLineup`, punto 139 de la ruta —, el pueblo de la rueda de la fortuna (119), el pastel gigante (63) y los bastones con la carretera arcoíris (84)), con un fundido rosado (`tour-fade`, detrás de toda la UI). Elegidas renderizando vistas cada 7 puntos de la ruta. Las demás páginas devuelven la cámara a la plataforma giratoria (`tour.SetActive(false)` antes de `SetDuoMode`). `BuildMenuScene` copia los puntos de la ruta antes de cerrar la escena de carrera.

## Personajes (etapa D)
- Sentados en su kart (punto 1 de las mejoras): `CharacterPuppet.CreateDriver` (asiento `KartRoster.Entry.seat`, escala `driverScale` 0,7; muslos al frente, canillas abajo, manos al volante y la pelvis clavada al asiento). En menú, selección, sala, pantalla de 2 jugadores y podio están animados (saludan / celebran / aplauden desde el kart). En carrera, si `GameSettings.DriversInRace` (opción "Pilotos en carrera", activada por defecto): `CreateStaticDriver` hornea la pose una vez en mallas normales (sin skinning ni animación por cuadro, sin sombras). OJO: `BakeMesh` deja los vértices en el espacio local SIN escala del renderer: la pieza horneada lleva `localScale = smr.lossyScale` (el FBX de Vanellope tiene escala 0,12 y salía 8 veces más grande).
- Costo estimado en carrera: ~35k triángulos estáticos más (la pista tiene ~108k), ~10 draw calls, sin sombras. Pendiente medirlo en la copia de Windows (no se pudo por falta de RAM).
- `KartRoster.Entry.character` (prefab) y `portrait` (textura, solo si no hay modelo). "Sugar Rush/Characters" (`SetupCharacters`): importa Ralph, Taffyta, Candlehead y Rancis desde los GLB en `~/Downloads` **solo si falta el prefab** (los resultados sí están en el repo, los GLB no), Vanellope desde `Art/Characters/Vanellope/Vanellope.fbx` (en el repo), y Adorabeezle también desde GLB (Kit7207; el ZIP con FBX que bajó la usuaria es el mismo modelo). `BuildPortrait` (retrato dibujado por código) queda solo como respaldo para un corredor sin modelo. Altura 1,35 m (estilo chibi).
- `MeshDecimator` (Editor): simplificación por colapso de aristas con cuádricas acumuladas del modelo original (si se recalculan en cada pasada, piezas chicas como el gorro de Candlehead desaparecen), peso igual por triángulo, respeta costuras UV y bordes, y no da vuelta triángulos. Conserva UV y pesos de huesos de cada vértice. Presupuestos en `GlbCharacters` (Candlehead necesita 8.000: con 7.500 se rompe el cupcake).
- Todos traen esqueleto sin animaciones. `CharacterPuppet` los anima por código apuntando brazos en espacio de mundo (sirve para huesos estilo Blender "Left_Arm/Left_Elbow" y 3ds Max "Bip001_L_UpperArm"): respiración, cabeza, salto + saludo al elegirlos (`Hop`), `Cheer` (brazos arriba) y `Clap` (aplaude). Sin modelo → cartón con el retrato (`CharacterPuppet.Create`, material Unlit con recorte alfa).
- Podio: `ResultsPodium` a 400 m bajo la pista, con su cámara (apagada hasta los resultados) a un RenderTexture de 720×540 que `RaceUI` muestra a la izquierda del panel de resultados; 1º aplaude con los brazos arriba, 2º y 3º aplauden. Se actualiza mientras los demás siguen corriendo. `RaceProgress.kartIndex` dice quién es cada kart.

## Multijugador en línea
- Paquetes: Netcode for GameObjects 2.13.3, Multiplayer Services 2.3.3 (API de "sessions" = Lobby + Relay), Multiplayer Play Mode 2.0.2. Proyecto UGS vinculado: `cd38f69e-af4e-4ba7-85b2-941ae4f89294` (en el dashboard se llama "My project"); Relay y Lobby activos.
- Modelo anfitrión-cliente: quien crea la sala es host. Cada jugador simula SU kart (física local, respuesta inmediata) y lo envía ~20 veces/s (`NetKart.State`, NetworkVariable con escritura del dueño, ~46 bytes). Los demás karts son "proxies" kinemáticos (`KartController.SetProxy`) que interpolan 0,12 s en el pasado y extrapolan hasta 0,25 s. La IA vive en el host (dueño = servidor).
- Choques entre karts de distintas máquinas: el collider del proxy es trigger; el kart que yo simulo recibe un empujón (`NetKart.OnTriggerStay`). Entre karts simulados en la misma máquina, física normal.
- Carrera: el host spawnea la parrilla cuando todos cargaron la escena (`OnLoadEventCompleted`), corre la cuenta regresiva y decide quién terminó (`NetRace`: fase, cuenta, hora de salida en el reloj del servidor, `RacerFinishedRpc`). Vueltas/posiciones se calculan en cada máquina con `RaceProgress` sobre las posiciones sincronizadas. "Terminado" es estado local de cada jugador. Volver a la pista solo lo ejecuta quien simula el kart (`RaceProgress.hasAuthority`).
- Relay siempre por WSS (`NetworkOptions.RelayProtocol = WSS` al crear y al unirse, `UnityTransport.UseWebSockets = true`) para que PC, celular y navegador jueguen juntos.
- Al cambiar mensajes o variables de red, subir `OnlineSession.ProtocolVersion` para que Partida rápida no mezcle versiones.
- Sala: `NetLobby` (NetworkObject persistente entre escenas) con la lista de jugadores; el host asigna personajes sin repetir. Máx. 5 jugadores; la IA llena los puestos libres. Al crear se elige Privada (solo por código, `ISession.Code`) o Pública (código + Partida rápida); por defecto privada.
- Partida rápida (etapa B): `OnlineSession.QuickJoinAsync` = `MatchmakeSessionAsync(QuickJoinOptions{CreateSession, Timeout 0})`: entra a una sala pública abierta (no llena, no bloqueada en carrera) o crea una pública. Solo empareja salas con la misma `ProtocolVersion` (propiedad `v` indexada en String1): subirla cuando cambien los mensajes de red. Si la sala encontrada está muerta (host cerró la pestaña), crea una en vez de mostrar error. En la sala de espera se muestra si es pública/privada y, tras una partida rápida, "¡Encontramos una sala!" o "No había salas abiertas: creamos esta".
- Prueba: `AgentScripts/quick_test.sh [rebuild]` (Editor crea por partida rápida, la copia de Windows con `-sr-quick` entra; sala bloqueada y sala privada no se emparejan).
- Desconexiones: kart con `DontDestroyWithOwner`; el host lo pasa a la IA (`OnClientLeftRace`: primero `Human = false`, después `ChangeOwnership`, si no el host cree por un instante que el kart es suyo). `NetKart.IsMine` exige además `HumanClientId == LocalClientId`. Si se cae el host o el transporte (`OnTransportFailure`), todos vuelven al menú con mensaje.
- Sala de espera (fase 2): cada jugador elige su corredor con flechas (`NetLobby.NextFreeKart` salta los ocupados; el host valida en `RequestKartRpc`). La sala se bloquea al empezar (`OnlineSession.SetRoomLocked`) y se desbloquea al volver; un intento de entrar en plena carrera da "online.locked". Errores de unión traducidos a `online.full` / `online.notFound` / `online.locked`. Quien llegue a la carrera sin kart ve al líder.
- Nombres en carrera: los humanos llevan "(J#)" (número de la sala) también si la IA tomó su kart.
- Transporte: `MaxPacketQueueSize = 512`. En pruebas con editor + 3 copias y ~1,5 GB de RAM libre, Relay llegó a desconectar al host por congelamientos del Editor (swap); con 2 jugadores no pasa. Probar con gente real antes de concluir que es un problema del juego.
- Separación del modo un jugador: el NetworkManager solo existe en línea (`Resources/Net/NetworkManager.prefab`, lo instancia `OnlineSession`). Los karts en red son variantes de prefab (`Prefabs/Net/Kart_<Nombre>_Net`) generadas por el builder (`SetupNetwork`); el código de red de la carrera está aparte en `RaceManager.Online.cs`. En línea la pausa no detiene el tiempo.
- Perfil de autenticación al azar por ejecución, para que varias copias en un PC (Play Mode, pestañas) sean jugadores distintos.
- Pruebas automáticas: `AgentScripts/net_test.sh rebuild` (2 jugadores) y `AgentScripts/net_test2.sh rebuild` (4 jugadores, desconexión a mitad de carrera, intento de unión con la sala bloqueada, volver a la sala) compila una copia de Windows (~2 min, 131 MB, en el scratchpad) que se une con `-sr-join CÓDIGO -sr-autopilot` (`OnlineTestHooks`). OJO: no usar `wait_for` largos mientras se hostea desde el Editor: bloquean el hilo principal y Relay desconecta al host por inactividad (~10 s). Consultar con evals cortos. Con 8 GB de RAM, cerrar Chrome antes de compilar.

## Cómo maneja Claude el Editor
- `unity` está en `~/AppData/Local/Unity/bin` (agregarlo al PATH en Bash). Usar `unity command eval_file` con scripts en `AgentScripts/` (fuera de Assets, ignorado por git); el código es el cuerpo de un método, sin `using` (nombres totalmente calificados).
- Llamar `unity command set_autotick` una vez por sesión: si no, el Editor sin foco no procesa comandos.
- `capture_game_view --save_path` siempre guarda dentro de `Assets/`; borrar la imagen después.
- Probar manejo: entrar en Play, agregar `KartTestPilot` al kart vía eval, `wait_for` su `Finished`, leer `Log`. Nunca agregarlo fuera de Play (ensucia la escena).
- Medir rendimiento: `ProfilerRecorder` sobre los marcadores PlayerLoop / BehaviourUpdate / FixedBehaviourUpdate / FixedUpdate.PhysicsFixedUpdate / AudioManager.Update, guardados entre evals con `AppDomain.CurrentDomain.SetData`. El "Main Thread" total no sirve con el Editor sin foco (queda atado al autotick de 16 ms).
- Diagnosticar a la IA: `KartTrailRecorder` (en Play) graba posición/velocidad/segmento cada 0,1 s; volcar `Log` y graficar sobre la vista cenital.
- Probar una carrera completa: desde Play, `GameSettings.Laps = N` + `SceneManager.LoadScene(SceneNames.Race)` (sin `Save()`), esperar `RaceManager.CurrentState == Racing`, desactivar `PlayerKartInput` del jugador y agregarle `AIKartDriver`, `Time.timeScale = 2`. Al terminar, borrar el récord falso (`PlayerPrefs.DeleteKey("best_N")`).
- Cada comando tarda varios segundos en llegar: no sirve para maniobras cronometradas (usar `KartTestPilot`) ni para capturar momentos breves.
- Con `GetMethod(...).Invoke` se pueden llamar métodos privados de la UI (p. ej. `MainMenuUI.ShowPage`, `RaceUI.SetPaused`) para capturar cada pantalla.
- `UQueryExtensions.Query<T>(root)` en vez de `root.Query<T>()` dentro de eval (no hay `using`).

## Problemas conocidos y cosas que no olvidar
- **RAM (8 GB)**: con el Editor + Chrome + Claude Code el Editor deja de responder o el build falla; Claude Code llegó a matar tareas en segundo plano. Pedirle a la usuaria cerrar programas o reiniciar Unity antes de builds largos, pruebas en línea con varias copias o mediciones de FPS.
- Si Unity está en Play sin que Claude lo haya puesto, preguntar antes de detenerlo.
- Si un USS se edita desde fuera del Editor, correr `AssetDatabase.Refresh()` antes de Play (si no, no se recarga).
- Bash: los heredocs con apóstrofes fallan; para ediciones grandes escribir un script Python en el scratchpad y correrlo.
- `capture_game_view` deja imágenes dentro de `Assets/`: borrarlas (una vez se subió `Assets/seats.png` por error y hubo que quitarla).
- Modelos de personajes: `BakeMesh` + escala del renderer (ver "Personajes"); el decimador necesita cuádricas acumuladas; las caras traseras de mallas generadas necesitan vértices propios (si no, se ven negras).
- La malla `mini_map_road` de la pista FBX está rota; la ruta es manual (`Route` + `RouteOverrides`).
- Sin probar todavía: la web en navegadores reales, FPS con pilotos en carrera, prueba en línea después de los últimos cambios.
- Seguridad: el repo es público. Nada de códigos, contraseñas, tokens ni `.env` en commits; revisar `git status` antes de cada commit.

## Decisiones tomadas (resumen y por qué)
- URP en calidad baja + "Rendimiento" por defecto: el portátil de la usuaria tiene GPU integrada (100+ FPS en Rendimiento contra ~30 en Calidad).
- Todo el contenido se genera por código (`SugarRushSetup`): se puede rehacer desde cero y Claude lo controla sin editar escenas a mano.
- UI Toolkit por código con Painter2D: sin imágenes ni assets de Disney, liviano y fácil de traducir.
- En línea: Netcode for GameObjects + Relay/Lobby por WSS, anfitrión-cliente con cada uno simulando su kart: responde al instante y permite jugar entre PC, celular y navegador.
- Personajes GLB con importador y decimador propios (sin paquetes extra) y animación por código (los modelos no traen animaciones).
- Ralph: opción "c" (animación por código con su esqueleto), elegida por la usuaria.
- Pistas nuevas: Unity Splines (opción a) en vez del Racing Kit de Kenney, porque permite pistas con temas propios de Sugar Rush y las mismas piezas de juego (IA, Ralph, mini-mapa) generadas desde los datos del spline.
- Web en Vercel con código de acceso por hash: el juego es de fan (IP de Disney), no debe quedar público ni indexado.

## Repositorio
- GitHub (público): https://github.com/sofia-londono/sugar-rush — rama `main`.
- Hacer commit después de cada avance funcional.
- No agregar Co-Authored-By ni firmas de Claude en commits ni PRs.

## Convenciones
- Código C# en inglés; textos del juego y comunicación con la usuaria en español.
- Después de cambiar scripts, correr `unity recompile` y revisar errores antes de dar algo por terminado.
