# Guía de arte con IA — Doctor Simi al estilo Hollow Knight

Objetivo: sustituir los sprites provisionales (pixel art "chibi" de 128 px) por un Doctor Simi que parezca parte de Hollow Knight. El código ya está listo para recibirlos: `Tools/import_sheet.py` recorta, limpia y alinea las hojas que generes, y el mod las usa en cuanto aparecen en el manifiesto.

## 1. Qué hace que algo "parezca Hollow Knight"

| Rasgo | Hollow Knight | Sprites actuales (a evitar) |
|---|---|---|
| Técnica | Dibujo a mano, pintado suave, alta resolución | Pixel art de 128 px |
| Contorno | Tinta negra limpia, grosor variable | Contorno de píxel de 1 px |
| Color | Apagado y oscuro; uno o dos acentos de color | Rojo y amarillo saturados |
| Proporciones | Siluetas simples y rotundas, cabeza/máscara grande, cuerpo compacto | Personaje "mascota" redondo |
| Cara | Máscara o caparazón claro con ojos negros grandes y vacíos | Cara de peluche con mejillas |
| Ambiente | Gótico, subterráneo, melancólico | Alegre y de cartel |

## 2. Diseño propuesto

Mantener lo reconocible del Doctor Simi y pasarlo por el mundo de Hallownest:

- **Silueta**: escarabajo anciano, robusto y algo encorvado, un poco más alto que ancho.
- **Cara**: máscara/caparazón blanco hueso con dos ojos negros grandes y vacíos. El **bigote blanco** grande y los **mechones de pelo blanco** a los lados siguen siendo la seña principal.
- **Gorra**: roja apagada (rojo vino), con visera; es el acento de color principal.
- **Ropa**: abrigo/caparazón rojo oscuro con **bandas amarillas mates** (reflectantes gastadas), cinturón y botas negras.
- **Detalles**: costuras, desgaste, algún vendaje o frasco colgando para darle tono "médico de Hallownest".

Primero genera una **hoja de referencia** (frente, perfil izquierdo, 3/4) y elige una versión. Úsala después como imagen de referencia en todas las animaciones para que el personaje sea siempre el mismo.

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

## 5. Plantilla de prompt

Los generadores suelen funcionar mejor en inglés. Sustituye `[GRID]` y `[POSES]`:

```
2D side-view game sprite sheet, hand-painted in the style of Hollow Knight by Team Cherry:
dark muted palette, clean black ink outlines of varying thickness, soft painterly shading,
simple bold silhouette, gothic underground mood.
Character: an old sturdy beetle doctor from Hallownest with a bone-white shell mask face,
large hollow black eyes, a big bushy white mustache and white hair tufts on the sides,
a faded wine-red cap with a visor, a dark red coat with dull worn yellow reflective bands,
black belt and black boots.
Full body, facing left, [GRID] grid of evenly spaced poses, same size and scale in every cell,
feet on the same baseline: [POSES].
Flat pure green background #00FF00, no floor, no shadows, no text, no borders.
```

Ejemplo para `Attack`: `[GRID]` = `3x2`, `[POSES]` = `1 guard stance, 2 fist pulled back, 3 punch starting, 4 arm fully extended punch, 5 recovering, 6 back to guard`.

Para la hoja de referencia: cambia la última parte por `character turnaround sheet: front view, left side view, three-quarter view`.

Consejos:

- Adjunta la hoja de referencia elegida como imagen de referencia en cada generación.
- Si una pose sale distinta (otro color o proporciones), regenera solo esa hoja; no mezcles personajes.
- Si la IA dibuja sombras o suelo, añade `no ground, no cast shadow, isolated on green`.

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

- [ ] El personaje es el mismo en todas las poses (colores, bigote, gorra).
- [ ] Mira a la izquierda en todas las poses.
- [ ] Fondo verde plano, sin suelo ni sombras.
- [ ] Las poses no se tocan entre sí ni se salen de su celda.
- [ ] En `Attack` y `SpecialPunch`, la pose 4 tiene el brazo totalmente extendido.
- [ ] Sin texto, logotipos ni marcas reales.
