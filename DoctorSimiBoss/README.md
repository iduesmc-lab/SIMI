# Doctor Simi Boss — prototipo de sprites

Arte inicial para un mod fan de jefe de Hollow Knight. El personaje adapta la muñeca de referencia: gorra roja, cara beige de peluche, ojos grandes, bigote blanco, uniforme rojo con bandas reflectantes amarillas, cinturón y botas negras.

## Animaciones disponibles

Spawn, Idle, Walk, Attack, Hurt, Stun y Defeat están en Sprites. Cada animación incluye seis PNG transparentes de 128 × 128 px, un spritesheet 3 × 2 de 384 × 256 px y una vista previa GIF.

## Efectos

- Effects/PunchImpact: destello de impacto de seis cuadros, destinado a sincronizarse con el contacto de Attack.

## Referencias

References/plush_reference_photo.png conserva la foto aportada. Las láminas conceptuales de personajes y del impacto están en References.

## Estado e integración pendiente

Todos los recursos son prototipos visuales. El tamaño de 128 × 128 px es provisional. Falta ajustar escala, punto de apoyo, hitboxes, tiempos y el cuadro activo del ataque dentro del juego.

Este paquete no incluye código ejecutable del mod. Para integrarlo se necesita elegir el cargador y la versión objetivo; configurar animaciones, pivotes y colisiones; implementar estados e IA; y sincronizar PunchImpact con el golpe. Effects puede ampliarse con partículas de entrada, aterrizaje, daño y derrota.

## Integración para Hollow Knight

`ModIntegration/` incluye un proyecto C# inicial, un punto de entrada mínimo para el cargador y un manifiesto que enlaza las animaciones y el efecto de impacto. El proyecto copia los PNG como contenido al compilar. Es un esqueleto: no crea todavía un jefe dentro del juego; faltan la escena, el controlador de animación, la IA y los ajustes de colisión. Consulta `ModIntegration/README.md` antes de compilar.

El diseño de combate con tres fases y seis ataques especiales está en ModIntegration/CombatDesign.md; la hoja conceptual está en References/special_attacks_concept_sheet.png. Las animaciones especiales siguen pendientes de producir como sprites transparentes.


La primera referencia visual de ataque cargado está en References/saludo_cargado_concept_sheet.png; aún no es una animación transparente lista para cargar.

