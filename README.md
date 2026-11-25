# videojuego-ultimoGuardabosque

**El Último Guardabosque** (título provisional) es un juego 3D en tercera persona. Eres el último guardabosque de una reserva natural y debes expulsar a las bandas de cazadores furtivos que se la han repartido.

Este documento reúne los conceptos clave del juego, la dirección de arte y el estado actual del proyecto en Unity.

---

## 1. Historia (lore)

### El mundo
La **Reserva de Valmora** fue durante décadas el último gran santuario natural del continente: varios biomas unidos por antiguos corredores de migración. Cuando el gobierno retiró la financiación, el cuerpo de guardabosques se disolvió y la reserva quedó sin protección.

### El enemigo
**El Sindicato** es una red de tráfico de fauna que aprovechó ese vacío y se repartió la reserva entre sus bandas. Cada banda:
- se especializa en un animal y en un negocio (pieles, trofeos, mascotas exóticas, medicina falsa);
- tiene **1 jefe** con personalidad y métodos propios, lugartenientes y **n subalternos** bajo su mando;
- controla un bioma.

### El protagonista
Es el único guardabosque que se negó a irse. Creció en la reserva. Su mentor, el antiguo jefe de guardabosques, desapareció mientras investigaba al Sindicato. Empieza con una cabaña, una radio vieja y un bioma casi perdido.

### El giro
A medida que libera biomas descubre que las bandas reciben ayuda desde dentro: mapas, rutas de patrulla y horarios. El traidor puede ser:
- **el mentor**, que no desapareció sino que se vendió (más personal), o
- **un funcionario** que quiere la reserva vacía de animales para venderla a una minera (villano más "sistémico").

> Decisión pendiente: elegir el traidor.

### El final
Declarar todos los biomas como zona segura no basta: hay que **exponer al Sindicato** para que la reserva recupere su protección legal.

---

## 2. Bucle de juego

1. **Explorar** un bioma ocupado por una banda.
2. **Debilitar a la banda** antes de ir a por el jefe:
   - liberar animales capturados;
   - desactivar trampas y destruir suministros;
   - capturar subalternos e interrogarlos para descubrir la guarida.
3. **Enfrentar al jefe**, con una mecánica propia de su bioma.
4. **Declarar el bioma zona segura.**
5. **Victoria:** todas las bandas eliminadas y todos los biomas declarados zona segura.

### Principios de diseño
- **No es un shooter genérico.** El guardabosque sabotea, desactiva trampas, reúne pruebas, arresta y usa dardos tranquilizantes.
- **Los animales importan.** La población de cada bioma baja mientras la banda siga activa, lo que da urgencia. Los animales rescatados pueden ayudar: un oso que ahuyenta cazadores, aves que delatan posiciones.
- **Progresión por bioma.** Cada zona liberada da algo nuevo: herramientas (dron, cámaras trampa, perro rastreador), aliados (lugareños, veterinaria, exguardabosques) o mejoras de la cabaña.
- **Jefes distintos.** El bioma dicta la pelea: persecución en lancha, duelo de francotiradores, sigilo nocturno.
- **Zonas seguras no del todo permanentes (opcional).** El Sindicato puede contraatacar, y el jugador defiende con las mejoras y aliados que haya conseguido.
- **Empezar pequeño.** Primero un solo bioma, una banda y un jefe. Si ese ciclo es divertido, el resto es contenido sobre una base que funciona.

---

## 3. Biomas y bandas (propuesta)

| Bioma | Animal emblema | Banda | Jefe y estilo |
|---|---|---|---|
| Bosque templado | Oso pardo | Los Peleteros | Cazador veterano: trampas y cepos |
| Selva | Jaguar | Los Sombra | Traficante de mascotas: redes y dardos |
| Sabana | Elefante o rinoceronte | Los Marfil | Exmilitar: vehículos y rifles de largo alcance |
| Humedal | Caimán y aves | Los Pantaneros | Contrabandista: lanchas y emboscadas |
| Montaña nevada | Leopardo de las nieves | Los Cumbre | Cazador de trofeos para millonarios: francotirador |

> Pendiente: el **tigre** ya tiene diseño (sección 5) pero no aparece en esta tabla. Hay que decidir en qué bioma vive (por ejemplo, la selva en lugar del jaguar o junto a él).

---

## 4. Dirección de arte

- **Estilo low poly facetado:** volúmenes hechos de planos grandes con sombreado por faceta y luz desde arriba a la izquierda.
- **Ambientación moderna**, no medieval: ropa táctica y de trabajo, radios, prismáticos, armas de fuego.
- **Cuerpo base robusto y compartido** por todos los humanos: hombros anchos, antebrazos y manos grandes, piernas cortas y botas grandes. Usar las mismas proporciones permite compartir esqueleto y animaciones.
- **Pose T** en las hojas de personaje, con vistas de **frente, perfil y espalda**.
- **Silueta reconocible de lejos:** cada rango y el protagonista deben distinguirse por su forma, no solo por el color.
- **Animales:** low poly en un **estilo más suave**, con esquinas biseladas, menos contraste entre facetas, paleta cálida y rayas en marrón oscuro en lugar de negro puro.

---

## 5. Personajes

### Cazadores: rangos (línea A, la elegida)

| Rango | Diseño | Rasgos y color de identificación |
|---|---|---|
| **Subalterno · Trampero** | Sudadera con capucha bajada, chaleco táctico con portacargadores, radio, pantalón cargo, guantes sin dedos, machete | Pelo negro, ojos verdes, cicatriz; grises y verde oliva |
| **Lugarteniente · Pañuelo rojo** | Chaqueta de cuero abierta, pañuelo rojo grande, vaqueros oscuros, pistolera al muslo, cartuchos en el cinturón | Pelo y barba rojos; **el rojo marca el rango** |
| **Jefe · Gabardina** | Gabardina larga con hombros reforzados, chaleco rojo, correa cruzada, cuchillo | Pelo largo negro, barba, puro, ojos amarillos; gris oscuro y rojo |

### Protagonista: Guardabosque (propuesta B, la elegida)

**Patrulla de campo:** sombrero de ala ancha con cinta y placa, camisa verde remangada, chaleco azul marino abierto, prismáticos al cuello, cantimplora, mochila con esterilla, guantes sin dedos y botas de montaña.

- **Paleta:** verde grisáceo, azul marino y cuero marrón, con acentos **dorados** (placa y parche con un árbol en el hombro).
- **Contraste con los cazadores:** el verde y el dorado son exclusivos del guardabosque, y el sombrero y la mochila le dan una silueta única.

### Animales

| Animal | Estado |
|---|---|
| **Tigre** | Diseño hecho en estilo suave, de pie en pose neutra sobre cuatro patas, con vistas de perfil, frente y espalda. Pendiente: marcar el quiebre del corvejón en la pata trasera. |
| **Oso** | Hay modelo y scripts de animación en la carpeta de personajes (ver sección 6). |

---

## 6. Flujo de producción de assets

Los conceptos y modelos se generan con scripts de Python **fuera de este repositorio**, en la carpeta `~/Desktop/video juego/characters-ia/`:

| Ruta | Contenido |
|---|---|
| `cazadores/`, `guardabosque/`, `animales/` | Hojas de concepto en SVG y PNG |
| `generar_*.py` | Scripts que dibujan cada hoja de concepto |
| `svg_a_png.py` | Convierte los SVG a PNG |
| `modelos3d/` | Modelos 3D por personaje (guardabosque, jefe, lugarteniente, subalterno, oso, tigre, armas) |
| `generar_modelo_*.py`, `animaciones_*.py` | Scripts de modelos y animaciones |

Lo que ya está listo se importa a `Assets/` en Unity.

---

## 7. Estado del proyecto en Unity

- **Versión:** Unity 6 (`6000.3.27f1`) con **URP 17.3** y el **Input System** nuevo.
- **Escenas:** `SampleScene` y `AnimationCharacterScene`.

### Guardabosque importado
| Asset | Ruta |
|---|---|
| Modelo | `Assets/Models/Guardabosques/guardabosque_B.fbx` |
| Material y textura | `Assets/Material/GuardabosquesMaterials/` |
| Prefab | `Assets/Prefabs/guardabosque_B.prefab` |
| Animator Controller | `Assets/Animations/Guardabosques/GuardaBosquesAnimationController.controller` |

**Animaciones:** reposo, caminar, correr, saltar, caída en el aire, caída al suelo, agacharse, caminar agachado, acostarse, giro a la izquierda, giro a la derecha y disparar con rifle.

### Scripts
- **`PlayerController.cs`:**
  - movimiento relativo a la cámara;
  - correr con Shift y agacharse mientras se mantiene Control (la cápsula se reduce y no se levanta si hay techo encima);
  - salto con gravedad propia;
  - muerte y reaparición en un `spawnPoint`.
  - Parámetros del Animator: `Speed` (blend tree: 0 = reposo, 1 = caminar, 2 = correr), `IsGrounded`, `VerticalVelocity`, `Jump` e `IsCrouching`.
- **`CameraController.cs`:** cámara orbital en tercera persona controlada con el ratón, con límites de inclinación y detección de colisiones para no atravesar paredes.

### Controles (acciones del Input System)
`Move` (WASD / joystick), `Jump`, `Run` y `Crouch`.

---

## 8. Próximos pasos sugeridos

- [ ] Elegir el traidor de la historia.
- [ ] Ubicar al tigre en un bioma y cerrar la tabla de biomas y bandas.
- [ ] Construir el primer bioma completo (bosque templado y Los Peleteros) como prueba del bucle de juego.
- [ ] Importar a Unity los cazadores (subalterno, lugarteniente y jefe) reutilizando el esqueleto del guardabosque.
- [ ] Definir las herramientas no letales del guardabosque (dardos, arresto, desactivar trampas).
