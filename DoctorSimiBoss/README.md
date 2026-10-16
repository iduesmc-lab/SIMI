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

El paquete incluye ya la lógica del jefe y una entrada mínima del mod (ver más abajo), pero todavía no es jugable. Para integrarlo se necesita elegir el cargador y la versión objetivo; cargar las animaciones en Unity, configurar pivotes y colisiones, conectar la máquina de estados a una escena y sincronizar PunchImpact con el golpe. Effects puede ampliarse con partículas de entrada, aterrizaje, daño y derrota.

## Integración para Hollow Knight

`Core/` contiene la lógica del jefe independiente del motor: lectura y validación del manifiesto, máquina de estados (Idle, movimiento, ataque, daño, aturdimiento, transición de fase y derrota), catálogo de ataques por fase y selección de animaciones con Idle y Attack como provisionales. Tiene pruebas en `Tests/`. `ModIntegration/` es el proyecto del mod: carga y valida el manifiesto y lo registra en el log. Aún no inserta al jefe en ninguna escena ni se ha compilado contra las DLL de una instalación real. Consulta `ModIntegration/README.md` para el estado, cómo compilar y los datos de instalación que faltan.

El diseño de combate con tres fases y seis ataques especiales está en ModIntegration/CombatDesign.md; la hoja conceptual está en References/special_attacks_concept_sheet.png. Las animaciones especiales siguen pendientes de producir como sprites transparentes.


La primera referencia visual de ataque cargado está en References/saludo_cargado_concept_sheet.png; aún no es una animación transparente lista para cargar.

