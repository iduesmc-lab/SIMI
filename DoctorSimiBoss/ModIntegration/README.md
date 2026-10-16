# Integración — DoctorSimiBoss

## Estado actual (v0.2.0-prototype)

| Parte | Estado |
|---|---|
| Lógica del jefe (`../Core`) | Implementada y con pruebas unitarias (51) en .NET 8. Compila para `net472` y `netstandard2.0` sin dependencias del juego. |
| Lectura y validación del manifiesto | Implementada. Comprueba que existan los 48 cuadros, que sean PNG de 128 × 128, que las fases sean coherentes y que no se use arte conceptual como sprite. |
| Entrada del mod (`DoctorSimiBossMod.cs`) | Carga y valida el manifiesto y lo escribe en el log. **No se ha compilado contra tus DLL reales** (`Modding.dll`, `Assembly-CSharp.dll`). Solo se comprobó su sintaxis y el empaquetado con stubs falsos. |
| Inserción en una escena | **No implementada.** Depende de los datos de tu instalación (ver abajo). |
| Jugable en Hollow Knight | **No.** Todavía no hay objeto del jefe en el juego, ni sprites cargados en Unity, ni colisiones. |

## Estructura

```
DoctorSimiBoss/
├─ Core/                          Lógica independiente del motor (sin Unity ni Modding API)
│  ├─ Json/MiniJson.cs            Lector JSON propio (no se asume Newtonsoft en Managed)
│  ├─ Manifest/                   Modelo, lector y validador de animation_manifest.json
│  ├─ Combat/                     Máquina de estados, ajustes, catálogo de ataques y cerebro (IA)
│  ├─ Animation/                  Elige qué clip reproducir en cada estado (con provisionales)
│  ├─ Hosting/IBossBody.cs        Lo que la escena debe proporcionar al jefe
│  ├─ Hosting/BossDriver.cs       Conecta la máquina de estados con IBossBody
│  └─ BossDefinition.cs           Manifiesto + validación + ajustes + ataques en un solo objeto
├─ ModIntegration/                Proyecto del mod (compila también los fuentes de Core en una sola DLL)
└─ Tests/DoctorSimiBoss.Core.Tests/  Pruebas xUnit de Core contra el manifiesto y los PNG reales
```

La separación es deliberada: todo lo que decide *qué hace* el jefe está en `Core` y se prueba sin el juego.
La futura capa de escena solo tendrá que implementar `IBossBody` (posición del jugador, reproducir clip,
orientación, velocidad, activar/desactivar hitbox, efecto, derrota) y llamar a `BossDriver.Tick` y
`BossDriver.ReceiveHit`.

## Máquina de estados

Estados: `Idle`, `Move`, `Attack`, `Hurt`, `Stun`, `PhaseTransition`, `Defeated`.

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

## Compilar y probar

Pruebas de la lógica (no necesitan el juego; requieren .NET SDK 8):

```
dotnet test DoctorSimiBoss/Tests/DoctorSimiBoss.Core.Tests
```

Mod (requiere tus DLL):

1. Instala Modding API para tu versión de Hollow Knight.
2. Copia la carpeta `Managed` del juego a `ModIntegration/HollowKnightManaged/`, o pasa `-p:HollowKnightRefs=<ruta a Managed>`.
   Esa carpeta está en `.gitignore`: las DLL del juego no se suben al repositorio.
3. `dotnet build DoctorSimiBoss/ModIntegration/DoctorSimiBoss.csproj -c Release`
4. El resultado queda en `ModIntegration/bin/Release/`: `DoctorSimiBoss.dll` y `Assets/` (manifiesto, `Sprites/`, `Effects/`).
   Las DLL del juego no se copian a la salida y `References/` nunca se empaqueta.
5. Copia esos archivos a la carpeta de mods de tu instalación. Si el cargador lo reconoce, el log debería mostrar una línea por clip y por especial, y `Boss definition ready`.

Si faltan las DLL, la compilación se detiene con un mensaje que indica la ruta buscada.

> La referencia previa a *ModdingAPI v77 para Hollow Knight 1.5.78.11833* y al ejemplo *Simple Hooks* viene del paquete original y **no se ha verificado** contra tu instalación.

## Datos necesarios antes de insertar el jefe en el juego

1. **Plataforma y tienda**: Windows / macOS / Linux; Steam / GOG / otra.
2. **Versión exacta de Hollow Knight** (menú principal, esquina inferior; por ejemplo 1.5.78.11833 o 1.4.3.2).
3. **Cargador de mods y su versión**: Modding API (y versión), instalado a mano o con Scarab/Lumafly, u otro.
4. **Ruta completa de `Managed`** donde están `Assembly-CSharp.dll` y `Modding.dll` (en Steam/Windows suele ser `...\Hollow Knight\hollow_knight_Data\Managed`, pero debe confirmarse en tu equipo).
5. **Ruta de la carpeta de mods** (normalmente `Managed/Mods`, también por confirmar).
6. **Dónde probar al jefe**: una arena existente que quieras reutilizar (nombre exacto de la escena si lo conoces, o la zona/jefe del juego), una sala vacía, o aparecer mediante una tecla de depuración en cualquier sala.
7. **Otros mods instalados** que puedan interferir (por ejemplo, otros mods de jefes o de depuración).
8. Si puedes, el `ModLog.txt` tras arrancar el juego con esta versión, para confirmar que el cargador encuentra la DLL y el manifiesto.

Con eso se comprobarán contra tus DLL los nombres reales de escenas, eventos (hooks) y componentes del jugador antes de usarlos.

## Pendiente

- Compilar contra tus `Managed/*.dll` y confirmar que el cargador ejecuta `Initialize`.
- Capa de escena: crear el objeto del jefe, cargar los PNG como texturas de Unity, implementar `IBossBody` y conectar el daño del jugador y del aguijón.
- Pivote, escala, colisiones e hitboxes en la escena elegida; sincronizar `PunchImpact` con el cuadro 03 de Attack.
- Sprites transparentes para los seis especiales, y efectos de onda y cápsulas.
- Ajustar tiempos y vida jugando.
