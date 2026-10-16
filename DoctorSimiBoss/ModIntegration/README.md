# Esqueleto de integración — DoctorSimiBoss

## Qué contiene

- `DoctorSimiBoss.csproj`: proyecto C# para un mod de Hollow Knight de PC, preparado para .NET Framework 4.7.2. Sigue la estructura del ejemplo Simple Hooks del repositorio de Modding API.
- `DoctorSimiBossMod.cs`: entrada mínima del cargador. Comprueba que el manifiesto se copió y deja un mensaje en el log. No instancia al jefe ni cambia escenas.
- `animation_manifest.json`: mapa de cuadros, velocidad inicial y sincronización sugerida del impacto.
- Al compilar, el proyecto copia los PNG de `../Sprites` y `../Effects` a `Assets/` junto al ensamblado.

## Preparación local

1. Instala Modding API para la versión de Hollow Knight que tengas en PC.
2. Copia la carpeta `Managed` del juego a `ModIntegration/HollowKnightManaged`, o define la propiedad `HollowKnightRefs` para apuntar a esa carpeta.
3. Comprueba que esa carpeta contenga `Assembly-CSharp.dll` y `Modding.dll`.
4. Compila `DoctorSimiBoss.csproj` en configuración Release. El resultado aparecerá en `bin/Release`.
5. Copia el ensamblado y la carpeta `Assets` al directorio de mods de tu instalación para comprobar que el cargador lo reconoce.

La guía de ejemplos de Modding API describe la configuración de referencias del juego y recomienda Simple Hooks como proyecto inicial. El lanzamiento publicado que se toma como referencia aquí es ModdingAPI v77 para Hollow Knight 1.5.78.11833. Verifica que coincida con la versión de tu juego antes de compilar.

## Qué es prototipo

Los PNG, GIF y spritesheets son arte de prueba. El tamaño de 128 × 128, el pivote normalizado (50%, 90%), las velocidades y el cuadro de daño sugerido son valores iniciales; deben calibrarse contra la escala y el terreno del juego. El `animation_manifest.json` es un inventario, todavía no lo consume un reproductor de animación.

## Pendiente para que aparezca un jefe jugable

- Seleccionar una escena o punto de encuentro y comprobarlo en la versión instalada.
- Crear/instanciar el objeto del jefe en esa escena y gestionar su ciclo de vida.
- Cargar las texturas PNG como sprites y conectarlas a un controlador de animación.
- Ajustar pivote, escala, colisiones, hitboxes, invulnerabilidad y daño.
- Implementar IA, transiciones entre estados, efectos de audio y pruebas dentro del juego.
- Sincronizar el daño del golpe con Attack 03 y PunchImpact después de revisar las cajas de colisión.

Este paquete no es todavía un mod jugable ni se ha compilado contra tus DLL locales porque no se ha proporcionado una instalación del juego.

## Diseño de combate por fases

Consulta CombatDesign.md para las tres fases y seis ataques especiales. nimation_manifest.json incluye los umbrales y marca las animaciones especiales como concept-only o design-only: todavía no existen como cuadros PNG de juego.

