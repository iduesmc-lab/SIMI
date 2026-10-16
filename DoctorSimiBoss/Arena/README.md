# Arena

- `arena_background_original.webp`: imagen de la arena tal como la aportaste (1671 × 941). Se conserva sin cambios.
- `arena_background.png`: la misma imagen convertida a PNG sin pérdida. Es la que se empaqueta en el mod, porque Unity solo carga PNG/JPG en tiempo de ejecución.

Es un **recurso de juego**, no solo conceptual: se mostrará como fondo detrás de una sala de combate sencilla con suelo y paredes invisibles.

Medidas provisionales, tomadas a ojo sobre la imagen (píxeles, origen arriba a la izquierda). Están en `animation_manifest.json` → `arena`:

| Dato | Valor | Qué es |
|---|---:|---|
| `floorY` | 715 | Línea donde apoyan los pies, en mitad de la superficie de piedra (y ≈ 697–733) |
| `walls.left` / `walls.right` | 200 / 1475 | Bordes de los bloques laterales elevados |
| `bossLandingX` | 1150 | Donde aterriza el Doctor Simi tras su salto de entrada |
| `playerEntryX` | 420 | Donde aparece el jugador |
| `pixelsPerUnit` | 40 | Escala: la arena mide unas 42 × 23,5 unidades de mundo |

Si tienes la imagen original a mayor resolución, conviene usarla: la que llegó aquí puede haberse recomprimido al subirla.
