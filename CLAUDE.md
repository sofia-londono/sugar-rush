# Sugar Rush RD

Juego de carreras de karts inspirado en el circuito de Sugar Rush (Wreck-It Ralph / Ralph el demoledor). Proyecto personal de fan sin fines de lucro: el código está en un repo público, pero el juego no se vende ni se distribuye como producto (IP de Disney).

## Stack
- Unity 6000.3.25f1 LTS, URP (Universal 3D), Input System nuevo.
- Unity CLI + paquete `com.unity.pipeline` para conectar Claude Code al Editor (`unity status`, `unity recompile`, `unity command`).
- Plugin de Claude Code: `unity@unity-agent-plugin`.

## Hardware objetivo (portátil de la usuaria)
- Intel i5-10210U, 8 GB RAM, Intel UHD integrada, SSD con poco espacio libre.
- Mantener el juego ligero: URP en calidad Performant/Balanced, iluminación baked para la pista, sombras en tiempo real solo para karts, pocas luces dinámicas, texturas ≤ 2048, sin HDRP.
- Colisiones de la pista con un MeshCollider simplificado (solo la superficie de la carretera), no el modelo completo.

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

## Repositorio
- GitHub (público): https://github.com/sofia-londono/sugar-rush — rama `main`.
- Hacer commit después de cada avance funcional.

## Convenciones
- Código C# en inglés; textos del juego y comunicación con la usuaria en español.
- Después de cambiar scripts, correr `unity recompile` y revisar errores antes de dar algo por terminado.
