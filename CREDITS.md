# Créditos

Proyecto de fan sin fines de lucro. Sugar Rush y sus personajes son propiedad de Disney.

## Modelos 3D (licencia CC Attribution 4.0)
- **Sugar rush karts | Storybook** — RazyBerry
  https://sketchfab.com/3d-models/sugar-rush-karts-storybook-05cffff8f28b4d18965b5b7e0e3b05f3
- **Map_tgsd** — amogusstrikesback2
  https://sketchfab.com/3d-models/map-tgsd-6cf96205665d48b681aab3b129aa9a79
- **Ralph El Demoledor** — danigamer495channel (https://sketchfab.com/danigamer495channel)
  https://sketchfab.com/3d-models/ralph-el-demoledor-08338217221848249fe65d0dad6c17f1
  (importado del GLB con `GlbImport`; textura del cuerpo reducida a 1024 px)
- **Vanellope von Schweetz** — guinavarro.al
  https://sketchfab.com/3d-models/vanellope-von-schweetz-c7e57d22abd345fc879617884fbb3d46
- **Taffyta Muttonfudge**, **Candlehead**, **Rancis Fluggerbutter** y **Adorabeezle Winterpop** — Kit7207 (https://sketchfab.com/pmino7207)
  https://sketchfab.com/3d-models/taffyta-muttonfudge-4574790598c5432ea45098329d050271
  https://sketchfab.com/3d-models/candlehead-f54da0d8cd864f80be2b2e14b5c4d685
  https://sketchfab.com/3d-models/rancis-fluggerbutter-2d1e73a23d734dceaa8133c695ce3be4
  https://sketchfab.com/3d-models/adorabeezle-winterpop-e1a81ecb21e740a5ae55e91e7f95dbdc
  (simplificados con `MeshDecimator`: Taffyta 24.784 → 7.500 triángulos, Candlehead 31.008 → 8.000, Rancis 22.540 → 7.500,
  Adorabeezle 29.648 → 7.500)

### Decoración del Bosque de gomitas
Importados con `PropImport` (Editor): mallas simplificadas, pintadas con la paleta pastel del juego y dibujadas con GPU instancing.
- **Japanese bridge** — Aoerchemix (https://sketchfab.com/Aoerchemix), CC BY 4.0
  https://sketchfab.com/3d-models/japanese-bridge-715951f5c3f74862a1ae74b899a2b51f (42.710 → 6.000 triángulos, colores pastel)
- **Sugar Rush Tree** — ofihombre (https://sketchfab.com/ofihombre), CC BY 4.0
  https://sketchfab.com/3d-models/sugar-rush-tree-1a8da3e6e3fb49d1846ad56d59a090e7 (con tintes de color)
- **Simple Candy Canes** — Blender3D (https://sketchfab.com/Blender3D), CC BY 4.0
  https://sketchfab.com/3d-models/simple-candy-canes-5063e5ccff8949918f221202df3a3977 (1.884 → 1.100 y 360 triángulos)
- **CHOCOLATE EASTER BUNNY** — l o u i s (https://sketchfab.com/louis), CC BY 4.0
  https://sketchfab.com/3d-models/chocolate-easter-bunny-99a623a4cbd84b8488050ac8162705b5 (24.464 → 4.000 triángulos)
- **Cinnamon Delight** — dcm.3designer (https://sketchfab.com/dcm.3designer), CC BY 4.0
  https://sketchfab.com/3d-models/cinnamon-delight-300aa2d66e4240538191f7632eb717e3 (9.998 → 900 triángulos)
- Ositos de goma, medias donas, arcos de dona, malvaviscos y aros de goma del lago: hechos por código para este proyecto (`SugarRushSetup.CandyShapes.cs`).

## Tipografías
- **Luckiest Guy** — Astigmatic (licencia Apache 2.0) — títulos
  https://fonts.google.com/specimen/Luckiest+Guy (licencia en `Assets/_SugarRush/UI/Fonts/LuckiestGuy-LICENSE.txt`)
- **Fredoka** — The Fredoka Project Authors (SIL Open Font License 1.1) — botones y textos; instancias fijas SemiBold/Bold generadas de la fuente variable
  https://fonts.google.com/specimen/Fredoka (licencia en `Assets/_SugarRush/UI/Fonts/Fredoka-OFL.txt`)

## Música (CC0 / dominio público)
- **Chiptune Adventures** — Juhani Junkala (SubspaceAudio), CC0
  https://opengameart.org/content/4-chiptunes-adventure
  - Menú: "Stage Select" → `Assets/_SugarRush/Audio/Music/menu_music.ogg`
  - Carrera: "Stage 2" → `Assets/_SugarRush/Audio/Music/race_music.ogg`

## Efectos de sonido
- **Impact Sounds** — Kenney (www.kenney.nl), CC0 — choques
  https://kenney.nl/assets/impact-sounds
- **Interface Sounds** — Kenney (www.kenney.nl), CC0 — sonidos de menú (clic, mover, atrás, confirmar)
  https://kenney.nl/assets/interface-sounds
- Motor, derrape, turbo, cuenta regresiva, campana de vuelta, fanfarria de meta, moneda, pasos y golpe de
  Ralph y martillo de Félix: sintetizados para este proyecto con `Tools/synth_sfx.py` (código propio, CC0).
