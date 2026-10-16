# Diseño de combate — Doctor Simi

Este documento añade un encuentro en tres fases. Las cifras son puntos de partida para implementar y probar, no balance final.

## Fases

| Fase | Vida restante | Ritmo | Ataques especiales |
|---|---:|---|---|
| 1. Turno de bienvenida | 100–66 % | Pausado; enseña señales y ventanas de castigo | Saludo cargado, Pisotón Simi |
| 2. Ronda express | 65–33 % | Más presión y cambios de lado | Carrera de pasillo, Giro de botarga |
| 3. Última visita | 32–0 % | Mezcla patrones anteriores con proyectiles y pulsos | Lluvia de cápsulas, Aplauso de salida |

Al cruzar 66 % y 33 %, el jefe hace una transición breve y claramente señalada. No ataca durante ella. Los umbrales se deben ajustar con pruebas; no son aún valores implementados.

## Ataques especiales

| Ataque | Fase | Señal y patrón | Cómo responder | Arte |
|---|---:|---|---|---|
| **Saludo cargado** | 1 | Retrae el puño, levanta el codo y lanza un golpe horizontal hacia la posición del jugador. | Esquivar hacia el jefe o saltar; deja recuperación tras fallar. | Conceptos 1–2; animación `SpecialPunch` pendiente. Se puede reutilizar `Attack` para prototipo. |
| **Pisotón Simi** | 1 | Salto corto con ambos puños arriba; al caer produce una onda por el suelo. | Saltar la onda; castigar después del aterrizaje. | Concepto 3; animación `SpecialSlam` pendiente. |
| **Carrera de pasillo** | 2 | Se agacha, las bandas amarillas se encienden y cruza la arena en línea recta. | Saltar o atravesar con dash; queda aturdido brevemente al frenar. | Concepto 4; animación `SpecialDash` pendiente. |
| **Giro de botarga** | 2 | Salto giratorio con brazos extendidos; al aterrizar lanza una onda circular baja. | Mantener distancia o saltar la onda; recuperación al terminar. | Concepto 5; animación `SpecialSpin` pendiente. |
| **Lluvia de cápsulas** | 3 | Marca tres carriles y lanza cápsulas fantásticas en una secuencia con un hueco seguro visible. | Leer las marcas y moverse al hueco; nunca llenar todos los carriles a la vez. | Diseño descrito; falta concepto y animación `SpecialCapsules`. |
| **Aplauso de salida** | 3 | Alza ambos brazos y, tras la señal, manda tres pulsos por el suelo con pausas regulares. | Saltar cada pulso; tras el tercero hay una ventana larga de castigo. | Concepto 6; animación `SpecialWave` pendiente. |

Las cápsulas son proyectiles de fantasía, sin marcas ni referencias a medicamentos reales.

## Reglas de lectura y balance

- Cada ataque debe tener una anticipación visible antes de que su hitbox pueda dañar.
- La señal, la zona peligrosa y el instante del impacto deben coincidir con los cuadros de animación.
- No encadenar dos ataques especiales si la primera evasión deja al jugador dentro de una segunda hitbox inevitable.
- Conservar una ventana de recuperación tras cada especial, más larga al final de `Aplauso de salida`.
- Ajustar alturas y hitboxes al tamaño real del personaje en juego; las láminas son referencias, no colisiones.

## Lámina conceptual

`References/special_attacks_concept_sheet.png` reúne seis poses de acción. Es una lámina conceptual con fondo oscuro: **no** es un spritesheet transparente ni cuadros listos para cargar. Los sprites finales deben dibujarse en celdas 128 × 128 consistentes, con fondo transparente, pivote común y secuencias de anticipación, impacto y recuperación.

## Próximo trabajo de arte

1. Crear seis cuadros para cada animación especial pendiente.
2. Separar anticipación, cuadro activo y recuperación; validar el cuadro de daño contra el efecto.
3. Crear las cápsulas y ondas como efectos transparentes aparte.
4. Probar escala, pivote y legibilidad dentro de la escena elegida.

### Primera animación especial en producción

Se añadió References/saludo_cargado_concept_sheet.png para fijar las poses de Saludo cargado. Conserva un fondo oscuro generado y no tiene transparencia ni celdas listas para el juego; la secuencia final de PNG aún queda pendiente.

## Cambio de enfoque (v0.7.0): sin golpes cuerpo a cuerpo

El Caballero pelea a distancia corta y esquivando, así que los puñetazos directos no funcionan bien en este juego. El jefe ataca ahora con proyectiles y ondas, al estilo de Cuphead o de los jefes a distancia de Hollow Knight:

- **Lanzamiento de cápsula** (ataque básico): lanza una cápsula en parábola que cae donde está el Caballero y revienta al tocar el suelo. Encadena de 1 a 3 lanzamientos según la fase y se usa a cualquier distancia.
- **Saludo cargado**: tras cargar, dispara un abanico de tres cápsulas rectas dirigidas al Caballero. Ya no embiste con el puño.
- **Carrera de pasillo** (embestida), Pisotón, Giro, Lluvia de cápsulas y Aplauso no cambian. El daño por contacto con su cuerpo se mantiene.
- No hay caja de daño en el puño: `hitboxes.punch` solo marca desde dónde salen los proyectiles.

## Fases y ataques nuevos (v0.8.0)

| Fase | Especiales disponibles |
|---|---|
| 1. Turno de bienvenida | Saludo cargado, Pisotón Simi |
| 2. Ronda express | Lo anterior + Carrera de pasillo, Giro de botarga, **Jeringas guiadas** |
| 3. Última visita | Todo lo anterior + Lluvia de cápsulas, Aplauso de salida, **Espiral de vendas** |

- **Jeringas guiadas:** tres jeringas que salen hacia arriba, siguen al Caballero alrededor de un segundo y después vuelan en línea recta. Se esquivan cambiando de dirección en el último momento.
- **Espiral de vendas:** tres brazos de vendas que giran desde el jefe durante dos segundos. Hay que colocarse entre los brazos y acompañar el giro.

## Presentación (v0.9.0)

- **Título:** solo "DOCTOR SIMI", abajo en pantalla, aparece al terminar la entrada y se desvanece (~4,5 s).
- **Avisos:** cada especial empieza su carga con el brillo pulsante y un campanazo corto ascendente.
- **Música:** una pista del propio juego (cue de `Room_Colosseum_Bronze`); vuelve la de la sala al salir o al ganar.
- **Escenario:** azotea de noche con lluvia animada delante y detrás del jefe.
