# Guía de arte con IA — Doctor Simi al estilo Hollow Knight

Objetivo: **el mismo Doctor Simi de los sprites actuales** (mismo personaje, mismas proporciones y poses), redibujado con el **acabado visual** de Hollow Knight. No se convierte en insecto ni se rediseña; solo cambia la técnica de dibujo, el color y la luz. El código ya está listo para recibirlos: `Tools/import_sheet.py` recorta, limpia y alinea las hojas que generes, y el mod las usa en cuanto aparecen en el manifiesto.

## 1. Qué cambia y qué se mantiene

| Se mantiene (diseño) | Cambia (acabado al estilo Hollow Knight) |
|---|---|
| Abuelo con gorra roja con visera | Dibujo a mano en alta resolución en vez de pixel art de 128 px |
| Pelo blanco a los lados y bigote blanco grande | Contorno de tinta negra limpia de grosor variable, no de 1 px |
| Cara beige, ojos grandes | Sombreado pintado suave, con luz fría y ambiental de cueva |
| Uniforme rojo con bandas amarillas, cinturón y botas negras | Colores apagados: rojo vino en vez de rojo puro, amarillo ocre mate en vez de amarillo brillante |
| Proporciones y siluetas de cada pose | Sombras más profundas y un poco de textura, menos brillo "de plástico" |
| Las poses y el orden de los cuadros de cada animación | Formas algo más limpias y simplificadas, como los personajes del juego |

## 2. Método: redibujar las hojas que ya tienes

Cada animación existente es ya una hoja de **3 × 2 poses** en `Sprites/<Animación>/<animación>_spritesheet_128.png` (por ejemplo `Sprites/Attack/attack_spritesheet_128.png`). Para cada una:

1. Sube esa hoja a tu herramienta de IA como **imagen de referencia** (función *image to image*, *reference* o *edit*). Mejor usa la versión ampliada sobre verde (`<Animación>_reference.png`, 1536 × 1024), que genera `Tools/make_reference_sheets.py`: la original de 384 × 256 con fondo transparente es demasiado pequeña para muchas herramientas.
2. Usa el prompt de la sección 5 para que la **redibuje** con el estilo de Hollow Knight, manteniendo personaje, poses y orden.
3. Si la herramienta lo permite, ajusta la fuerza de la referencia alta (≈ 0,6–0,8) para que no invente otro personaje ni cambie las poses.
4. Haz primero `Idle`; cuando te guste el resultado, úsalo además como **referencia de estilo** en todas las demás, para que todas tengan el mismo acabado.

Los ataques especiales no tienen hoja todavía. Para ellos usa como referencia de poses las láminas de `References/` (por ejemplo `special_attacks_concept_sheet.png` o `saludo_cargado_concept_sheet.png`) junto con tu `Idle` ya redibujado como referencia de personaje y estilo.

## 3. Reglas técnicas para cada hoja (importante)

1. **Una animación por imagen**, con las poses en **cuadrícula regular** (por ejemplo 3 × 2 o 4 × 2) y separadas entre sí.
2. **Fondo verde puro plano `#00FF00`**: sin degradados, suelo, sombras ni viñeta. Si tu herramienta da PNG transparente, mejor aún.
3. **Vista lateral, mirando a la izquierda**, cuerpo entero visible en todas las poses.
4. **Mismo tamaño y escala en todas las celdas**. El personaje no debe cambiar de altura entre poses salvo por la acción (agacharse o saltar).
5. **Sin texto, marcos, números ni marcas de agua.**
6. Resolución alta: al menos **1536 px de ancho** por hoja (cada celda ≥ 384 px).
7. Formato PNG preferiblemente; JPG también vale (la herramienta tolera sus artefactos).
8. Las cápsulas son **de fantasía**: sin letras, logotipos ni marcas de medicamentos reales.

## 4. Animaciones a generar

Seis poses por animación para que la IA mantenga la coherencia. Orden de las poses: de izquierda a derecha y de arriba abajo.

### Personaje

| Nombre (carpeta) | Cuadrícula | Poses | Bucle |
|---|---|---|---|
| `Idle` | 3 × 2 | De pie en guardia, respiración: cuerpo sube y baja levemente, bigote se mueve | sí |
| `Walk` | 3 × 2 | Ciclo de pasos pesados: contacto, recogida, paso, contacto con la otra pierna, recogida, paso | sí |
| `Attack` | 3 × 2 | 1 guardia, 2 anticipación (puño atrás), 3 inicio del golpe, **4 brazo totalmente extendido (impacto)**, 5 recuperación, 6 vuelta a guardia | no |
| `Hurt` | 3 × 2 | Sobresalto hacia atrás, tambaleo, recuperación | no |
| `Stun` | 3 × 2 | Aturdido: balanceo, mano en la cabeza, ojos en espiral o estrellas | sí |
| `Defeat` | 3 × 2 | Tambaleo, rodillas ceden, cae de lado, inmóvil | no |
| `Spawn` | 3 × 2 | Agachado, impulso, en el aire subiendo, punto alto, aterrizaje, guardia | no |

### Ataques especiales

| Nombre (carpeta) | Ataque | Poses |
|---|---|---|
| `SpecialPunch` | Saludo cargado | 1–2 carga el puño con el codo alto (brillo), 3 arranca hacia delante, **4 puñetazo largo extendido**, 5–6 recuperación |
| `SpecialSlam` | Pisotón Simi | 1 agachado, 2 salta con ambos puños arriba, 3 en lo alto, 4 cayendo, **5 aterriza golpeando el suelo**, 6 se incorpora |
| `SpecialDash` | Carrera de pasillo | 1 se agacha (bandas amarillas brillando), 2 arranque, 3–4 carrera inclinada muy rápida, 5 frenazo derrapando, 6 mareado |
| `SpecialSpin` | Giro de botarga | 1 agachado, 2–5 giro en el aire con los brazos extendidos (4 ángulos), 6 aterrizaje |
| `SpecialCapsules` | Lluvia de cápsulas | 1–2 levanta un frasco grande, 3–4 lo agita hacia el techo, 5–6 brazos arriba |
| `SpecialWave` | Aplauso de salida | 1 brazos arriba, 2 manos juntas sobre la cabeza, 3 golpe al suelo, 4 brazos arriba, 5 golpe al suelo, 6 pose final |

### Efectos (sobre verde, sin el personaje)

| Nombre | Cuadrícula | Contenido |
|---|---|---|
| `Shockwave` | 3 × 2 | Onda baja de energía dorada/blanca que avanza por el suelo (vista lateral) |
| `Capsule` | 2 × 1 | Cápsula de fantasía roja y blanca vista de lado, entera y con brillo |
| `CapsuleBurst` | 3 × 2 | Cápsula reventando en polvo y chispas al tocar el suelo |
| `PunchImpact` | 3 × 2 | Destello de impacto blanco/dorado al estilo Hollow Knight |

## 5. Plantillas de prompt

Los generadores suelen funcionar mejor en inglés.

**Animaciones existentes** (con la hoja actual adjunta como referencia):

```
Redraw the attached sprite sheet in the visual style of Hollow Knight by Team Cherry.
Keep the SAME character, design, proportions, poses, pose order and 3x2 layout:
an old man mascot with a red cap with a visor, white hair on the sides, a big white mustache,
a beige face with large eyes, a red work uniform with yellow reflective bands,
a black belt and black boots. Do not turn him into a bug or change his design.
Change only the rendering: hand-drawn high-resolution 2D game art, clean black ink outlines
of varying thickness, soft painterly shading, muted colors (wine red instead of pure red,
dull ochre instead of bright yellow), cool dim cave lighting, subtle texture, gothic mood.
Full body, facing left, same size and scale in every cell, feet on the same baseline.
Flat pure green background #00FF00, no floor, no shadows, no text, no borders.
```

**Ataques especiales nuevos** (con tu `Idle` redibujado y la lámina de poses adjuntos):

```
Using the attached character (same design and Hollow Knight style), draw a 3x2 sprite sheet
of these poses: [POSES].
Same character, proportions and rendering as the reference, full body, facing left,
same size and scale in every cell, feet on the same baseline.
Flat pure green background #00FF00, no floor, no shadows, no text, no borders.
```

Consejos:

- Si la IA cambia el diseño (otro color de gorra, sin bigote, aspecto de insecto), baja la creatividad o sube la fuerza de la referencia y repite.
- Si una sola pose sale mal, regenera la hoja entera; no mezcles poses de generaciones distintas.
- Si dibuja sombras o suelo, añade `no ground, no cast shadow, isolated on green`.

## 6. Cómo me lo entregas

Súbeme cada hoja con su nombre (por ejemplo `Attack.png`, `SpecialSlam.png`) e indica la cuadrícula si no es 3 × 2.

Yo la paso por la herramienta:

```
python Tools/import_sheet.py Attack.png --anim Attack --cols 3 --rows 2 --out Sprites
```

La herramienta:

1. Recorta cada celda y quita el fondo verde, incluido el reflejo verde en los bordes.
2. Escala toda la animación con un solo factor (la pose más alta mide 224 px), así el personaje no cambia de tamaño entre cuadros.
3. Coloca cada pose en un cuadro transparente de **256 × 256** con los pies en la fila 248 (pivote 0,97) y centrada.
4. Genera la hoja y un GIF de vista previa para revisarla.

Después actualizo el manifiesto: rutas, tamaño de cuadro 256, 64 px por unidad (el personaje sigue midiendo unas 3,5 unidades) y cajas de golpe medidas sobre los cuadros nuevos. A partir de ahí, cada especial con sprites propios pasa de `concept-only` a `ready` y deja de usar el provisional.

## 7. Revisión antes de enviarla

- [ ] Es el mismo Doctor Simi de los sprites actuales (gorra, pelo y bigote blancos, uniforme con bandas), no un personaje nuevo.
- [ ] Las poses y su orden coinciden con la hoja original.
- [ ] Mira a la izquierda en todas las poses.
- [ ] Fondo verde plano, sin suelo ni sombras.
- [ ] Las poses no se tocan entre sí ni se salen de su celda.
- [ ] En `Attack` y `SpecialPunch`, la pose 4 tiene el brazo totalmente extendido.
- [ ] Sin texto, logotipos ni marcas reales.
