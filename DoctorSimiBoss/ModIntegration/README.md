# Integración — DoctorSimiBoss

## Estado actual (v0.2.0-prototype)

| Parte | Estado |
|---|---|
| Lógica del jefe (`../Core`) | Implementada y con pruebas unitarias (60) en .NET 8. Compila para `net472` y `netstandard2.0` sin dependencias del juego. |
| Lectura y validación del manifiesto | Implementada. Comprueba que existan los 48 cuadros, que sean PNG de 128 × 128, que las fases sean coherentes y que no se use arte conceptual como sprite. |
| Entrada del mod (`DoctorSimiBossMod.cs`) | Carga y valida el manifiesto y lo escribe en el log. **Compila sin errores ni avisos** contra el `Assembly-CSharp.dll` de ModdingAPI v77 publicado (`moddingapi.v77.windows.zip`, misma fecha y tamaño que el instalado por Scarab). Las 250 referencias a `mscorlib` existen en el `mscorlib.dll` del juego. **Probado en el juego** (Hollow Knight 1.5.78.11833, API v77, instalado con Lumafly): carga sin errores, los 48 cuadros dan OK y la arena mide 41.78 × 23.53 unidades según `ModLog.txt`. |
| Arena | Tu imagen está en `../Arena/` con suelo, paredes y puntos de entrada medidos (provisionales). Se valida y se empaqueta. |
| Encuentro en el juego (`Scene/`, v0.3.0) | **Compilado, sin probar en el juego.** Con **F8** en una sala jugable se construye la arena a la derecha de la sala (fuera de sus límites), el Caballero entra, la cámara se bloquea con el `CameraLockArea` del juego y el Doctor Simi entra con Spawn. Recibe golpes del aguijón y hechizos (`IHitResponder`), hace daño por contacto y con el puño (`DamageHero`) y usa la máquina de estados de `Core`. **F8** otra vez cierra la arena y te devuelve a donde estabas. Los especiales usan el golpe Attack con sus tiempos; ondas, cápsulas y pulsos aún no existen. |
| Jugable en Hollow Knight | **Pendiente de la primera prueba.** |

## Estructura

```
DoctorSimiBoss/
├─ Core/                          Lógica independiente del motor (sin Unity ni Modding API)
│  ├─ Json/MiniJson.cs            Lector JSON propio (no se asume Newtonsoft en Managed)
│  ├─ Manifest/                   Modelo, lector y validador de animation_manifest.json
│  ├─ Combat/                     Máquina de estados, ajustes, catálogo de ataques y cerebro (IA)
│  ├─ Arena/ArenaLayout.cs        Convierte las medidas en píxeles de la imagen a unidades de mundo
│  ├─ Animation/                  Elige qué clip reproducir en cada estado (con provisionales)
│  ├─ Arena/FrameGeometry.cs      Cajas de golpe en píxeles del cuadro → unidades alrededor del pivote
│  ├─ Hosting/IBossBody.cs        Lo que la escena debe proporcionar al jefe
│  ├─ Hosting/BossDriver.cs       Conecta la máquina de estados con IBossBody
│  └─ BossDefinition.cs           Manifiesto + validación + ajustes + ataques en un solo objeto
├─ ModIntegration/                Proyecto del mod (compila también los fuentes de Core en una sola DLL)
│  └─ Scene/                      Capa de Unity: sprites, cuerpo del jefe, arena y tecla de acceso
└─ Tests/DoctorSimiBoss.Core.Tests/  Pruebas xUnit de Core contra el manifiesto y los PNG reales
```

La separación es deliberada: todo lo que decide *qué hace* el jefe está en `Core` y se prueba sin el juego.
La futura capa de escena solo tendrá que implementar `IBossBody` (posición del jugador, reproducir clip,
orientación, velocidad, activar/desactivar hitbox, efecto, derrota) y llamar a `BossDriver.Tick` y
`BossDriver.ReceiveHit`.

## Máquina de estados

Estados: `Intro`, `Idle`, `Move`, `Attack`, `Hurt`, `Stun`, `PhaseTransition`, `Defeated`.

- **Intro**: entrada del jefe con la animación Spawn (salta y aterriza). Es invulnerable y no ataca durante `introDuration` (1 s).

- **Idle → decisión**: al acabar la espera, el cerebro elige caminar, golpe básico o especial de la fase.
- **Attack**: cada ataque pasa por `Windup` (anticipación, sin daño) → `Active` (hitbox activa) → `Recovery` (ventana de castigo). La orientación se bloquea durante todo el ataque. Los ataques tienen superarmadura: no se interrumpen por un golpe normal.
- **Hurt**: retroceso breve solo si recibe daño en `Idle` o `Move`.
- **Stun**: tras `stunHitThreshold` golpes, cancela el ataque en curso. *Carrera de pasillo* también lo aturde al frenar.
- **PhaseTransition**: al cruzar 66 % y 33 % de vida. Invulnerable y sin atacar durante `phaseTransitionDuration`.
- **Defeated**: a 0 de vida. Estado final; ignora daño posterior.

Reglas de combate aplicadas por el cerebro por defecto (`DefaultBossBrain`): nunca encadena dos especiales seguidos;
desde lejos solo usa especiales marcados como de alcance (o camina); la fase 3 añade los especiales de las fases anteriores
(`includesPreviousSpecials`).

## Animaciones provisionales

Ninguno de los seis ataques especiales tiene sprites de juego (`assetStatus` = `concept-only` o `design-only`), así que:

- Todos los ataques, especiales incluidos, usan **Attack**. Su velocidad se ajusta para que el cuadro de daño (`suggestedDamageFrame` = 3) coincida con el final de la anticipación de cada ataque.
- `PhaseTransition` usa **Idle** (no hay clip de transición).
- `Move`, `Hurt`, `Stun` y `Defeat` usan sus clips si están completos y si no, **Idle**. Con `PlaceholderPolicy.IdleAndAttackOnly` se fuerza a usar solo Idle y Attack.

Cuando exista, por ejemplo, `Sprites/SpecialPunch/` con sus seis PNG, basta con añadir el clip al manifiesto y poner `"assetStatus": "ready"` en el especial.

## Cambios en `animation_manifest.json`

Todo es aditivo salvo una corrección:

- `PisonSimi` → **`PisotonSimi`** (el id tenía una errata; no lo usaba ningún código).
- Nuevos campos: `spriteFacing` (los sprites miran a la izquierda), `displayName`, `conceptSheet` y `prototypeTiming` en cada especial, `includesPreviousSpecials` en la fase 3, bloque `tuning` y lista `conceptReferences`.
- Los tiempos y ajustes son puntos de partida para probar, no balance final. Las distancias están en unidades de mundo sin calibrar.

## Probar el encuentro (v0.3.0)

1. Instala `DoctorSimiBoss.zip` con Lumafly (**Manually install**), sustituyendo la versión anterior.
2. Carga una partida y, ya controlando al Caballero en cualquier sala, pulsa **F8**.
3. Para salir, pulsa **F8** otra vez. Si mueres, el juego te devuelve al banco como siempre y la arena desaparece.
4. La tecla se cambia en `Assets/animation_manifest.json` → `encounter.debugKey` (nombres de `UnityEngine.KeyCode`, por ejemplo `F7` o `O`).
5. `ModLog.txt` registra al abrir la arena las capas de física elegidas, los cambios de fase, el aturdimiento y la derrota.

Piezas del juego usadas, todas comprobadas en Assembly-CSharp 1.5.78.11833-77 y en las DLL de Unity del jugador: `HitTaker.Hit` → `IHitResponder.Hit(HitInstance)`, `HeroBox.CheckForDamage` → `DamageHero.damageDealt`, `CameraLockArea` (`cameraXMin/XMax/YMin/YMax`), `CameraController.xLimit/yLimit/sceneWidth/SnapTo` (la cámara limita su centro a x ≥ 14,6 e y ≥ 8,3), `HeroController.instance/SoulGain/acceptingInput/cState`, `GameManager.instance/isPaused/IsGameplayScene`, capas de `GlobalEnums.PhysLayers`.

## Compilar y probar

Pruebas de la lógica (no necesitan el juego; requieren .NET SDK 8):

```
dotnet test DoctorSimiBoss/Tests/DoctorSimiBoss.Core.Tests
```

Mod (requiere la Modding API instalada en tu juego y, desde la v0.3.0, estas DLL de `Managed`: `UnityEngine`, `UnityEngine.CoreModule`, `UnityEngine.ImageConversionModule`, `UnityEngine.Physics2DModule`, `UnityEngine.InputLegacyModule` y `PlayMaker`):

1. Pon Hollow Knight en **1.5.78.11833** y activa la API con Lumafly (ver *Versión del juego* más abajo).
2. Compila apuntando a tu carpeta `Managed` (en Windows, desde PowerShell, con el .NET SDK instalado):
   ```
   dotnet build DoctorSimiBoss\ModIntegration\DoctorSimiBoss.csproj -c Release -p:HollowKnightRefs="D:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data\Managed"
   ```
   También puedes copiar `Managed` a `ModIntegration/HollowKnightManaged/` (está en `.gitignore`: las DLL del juego no se suben al repositorio).
3. No existe un `Modding.dll` separado: en la API para 1.5 los tipos `Modding` van dentro del `Assembly-CSharp.dll` parcheado. Si ves *"The type or namespace name 'Modding' could not be found"*, ese `Assembly-CSharp.dll` es el original sin API.
4. El resultado queda en `ModIntegration/bin/Release/` (`DoctorSimiBoss.dll` y `Assets/`). La compilación en Release también genera **`ModIntegration/bin/DoctorSimiBoss.zip`** para Lumafly. Las DLL del juego no se copian a la salida y `References/` nunca se empaqueta.
5. **Instalar con Lumafly:** botón **Manually install** → elige `DoctorSimiBoss.zip`. Lumafly extrae el ZIP tal cual en `Mods\<nombre del zip>\`, así que el archivo debe llamarse exactamente `DoctorSimiBoss.zip` y llevar `DoctorSimiBoss.dll` y `Assets\` en la raíz, sin carpeta intermedia. El resultado debe ser:
   ```
   ...\hollow_knight_Data\Managed\Mods\DoctorSimiBoss\DoctorSimiBoss.dll
   ...\hollow_knight_Data\Managed\Mods\DoctorSimiBoss\Assets\...
   ```
   La API v77 solo busca DLL en `Mods\<subcarpeta>\*.dll`: una DLL suelta en `Mods\` o dos niveles más abajo no se carga, y `Mods\Disabled` se ignora. Sin Lumafly, descomprime el ZIP en `Mods\DoctorSimiBoss\` a mano.
6. Abre el juego y luego revisa `ModLog.txt` en `%USERPROFILE%\AppData\LocalLow\Team Cherry\Hollow Knight\` (la API lo escribe en `Application.persistentDataPath`; los anteriores pasan a `Old ModLogs`). Si el mod cargó, verás una línea por clip y por especial, `Boss definition ready` y las medidas de la arena.

Lo que usa `DoctorSimiBossMod.cs` de la API (`Mod`, `Initialize()`, `GetVersion()`, `Log(string)`) se comprobó contra el código fuente público de ModdingAPI 1.5.78.11833-77, todavía no compilando contra tu DLL.

Si faltan las DLL, la compilación se detiene con un mensaje que indica la ruta buscada.

## Versión del juego

Tu juego estaba en **1.5.12620**; ya está en **1.5.78.11833** con la API v77 funcionando. Las versiones de la Modding API publicadas en [hk-modding/api](https://github.com/hk-modding/api/releases) (hasta la v77) son para **1.5.78.11833**. Los avisos de Scarab y Lumafly sobre el parche 1.5.12459 indican que la API no funciona en los parches 1.5.12xxx, y no he encontrado ninguna versión para 1.5.12620. La API instalada (v77) solo funciona con el juego en 1.5.78.11833.

Pasos:

1. Steam → clic derecho en Hollow Knight → **Propiedades** → **Versiones del juego y betas** → elige **1.5.78.11833 - Previous version**. Espera a que se descargue.
2. Comprueba que el menú principal muestre `1.5.78.11833`.
3. Abre Lumafly y comprueba que la API esté activada (botón **Toggle API**: debe estar en modo con mods). Lumafly descarga la Modding API si falta.
4. Arranca el juego una vez: en el menú debería aparecer el texto de la Modding API.

Si cambias de versión, guarda una copia de tus partidas antes (`%AppData%\..\LocalLow\Team Cherry\Hollow Knight`).

## Decisiones del primer encuentro de prueba

- Plataforma: **Steam en Windows**, mods gestionados con **Lumafly** (antes Scarab; ambos instalan la misma Modding API).
- Acceso: **tecla de prueba** en cualquier sala que lleva a la arena (`encounter.access = "debugKey"`). La tecla aún no está elegida (`debugKey: null`).
- Entrada: el jefe **salta y aterriza** con Spawn en `bossLandingX`.
- Arena: **tu imagen como fondo** de una sala sencilla con suelo y paredes invisibles, sin escena nueva de Unity.

## Datos necesarios antes de insertar el jefe en el juego

1. ~~Plataforma y tienda~~: Windows + Steam (confirmado).
2. ~~Versión de Hollow Knight~~: 1.5.12620, sin API compatible; hay que bajar a 1.5.78.11833 (ver *Versión del juego*).
3. ~~Versión de la Modding API~~: v77 (1.5.78.11833-77), la que trae Scarab/Lumafly.
4. ~~Ruta de `Managed`~~: `D:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data\Managed` (confirmada). No hace falta `Modding.dll`.
5. ~~Carpeta de mods~~: `...\Managed\Mods` (confirmada; ya existe).
6. ~~Dónde probar al jefe~~: arena propia a partir de tu imagen, con tecla de prueba (confirmado). Falta elegir **qué tecla**.
7. **Otros mods instalados** que puedan interferir (por ejemplo, otros mods de jefes o de depuración).
8. Si puedes, el `ModLog.txt` tras arrancar el juego con esta versión, para confirmar que el cargador encuentra la DLL y el manifiesto.

Con eso se comprobarán contra tus DLL los nombres reales de escenas, eventos (hooks) y componentes del jugador antes de usarlos.

## Pendiente

- Compilar contra tus `Managed/*.dll` y confirmar que el cargador ejecuta `Initialize`.
- Capa de escena: crear el objeto del jefe, cargar los PNG como texturas de Unity, implementar `IBossBody` y conectar el daño del jugador y del aguijón.
- Pivote, escala, colisiones e hitboxes en la escena elegida; sincronizar `PunchImpact` con el cuadro 03 de Attack.
- Sprites transparentes para los seis especiales, y efectos de onda y cápsulas.
- Ajustar tiempos y vida jugando.
