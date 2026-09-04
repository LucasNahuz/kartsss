# VORTEX KARTS VR — Game Design

Kart racer arcade para realidad virtual (PCVR / OpenXR). Identidad propia: pilotos, circuitos, power-ups, sonidos y assets son originales y generados por código en esta fase.

## Concepto

- Rápido, fácil de aprender, difícil de dominar.
- 8 participantes: jugador + 7 CPU con personalidades distintas.
- Derrape con carga de 3 niveles → turbo al soltar.
- Power-ups con probabilidades según posición.
- 3 circuitos con atajos, saltos, plataformas de turbo y peligros.
- Prioridad: GAMEPLAY > ESTABILIDAD > COMODIDAD VR > GRÁFICOS.

## Controles

### Gamepad (modo recomendado, también dentro de VR)

| Acción | Xbox | DualShock | Teclado (debug) |
|---|---|---|---|
| Acelerar | RT | R2 | W / ↑ |
| Frenar / marcha atrás | LT | L2 | S / ↓ |
| Dirección | Stick izq. | Stick izq. | A / D, ← / → |
| Derrape / salto | A | Cruz | Espacio |
| Usar power-up | RB | R1 | Shift izq. / E |
| Mirar atrás | LB o B | L1 o Círculo | Q |
| Pausa | Start | Options | Esc |
| Recentrar VR | Select/View | Share | R |

Deadzone 0.12, curva de sensibilidad configurable (0.5–1.5), vibración con intensidad configurable. Rebind de los botones de gamepad desde CONFIGURACIÓN → CONTROLES.

### VR Motion Controllers (opcional)

Modo `CONTROLES VR (VOLANTE)` en configuración: agarrá (grip) el volante virtual con una o dos manos y girá. Gatillo derecho acelera, gatillo izquierdo frena, A = power-up, B = derrape, X = mirar atrás, Y = recentrar, Menú = pausa. Si no se sostiene el volante, se usa el thumbstick izquierdo.

## Perspectiva y confort VR

- Cámara en el asiento del piloto; la pose de cabeza viene sólo del headset (`TrackedPoseDriver`).
- El rig sigue la posición del asiento y el yaw del kart; pitch/roll sólo parcialmente (horizonte estabilizado, intensidad configurable).
- Vignette de confort en giros/aceleraciones laterales fuertes, intensidad configurable.
- Camera shake OFF/LOW/MEDIUM: sólo desplazamiento milimétrico del asiento, nunca rotación.
- Trompo del jugador: la cabeza sólo bambolea ±; el chasis gira visualmente.
- Respawn y cambios de escena con fundido a negro.
- Recentrar en cualquier momento; ajustes de altura y avance del asiento.
- Pausa no congela el tracking.
- Pérdida de tracking o desconexión del gamepad → pausa automática.

## Sistema de kart (`Scripts/Kart`)

Rigidbody esfera (r = 0.6) para colisiones + heading simulado aplicado a un hijo `Orientation`. Cada paso de física la velocidad se reexpresa en el nuevo frame: la parte longitudinal la controla el acelerador/freno y la lateral decae con el agarre. Poco agarre (derrape, slime) = deslizamiento.

`KartStats` (ScriptableObject): MaxSpeed, Acceleration, Handling, DriftControl, Weight, BoostPower, OffRoadPenalty + tuning fino (freno, reversa, tasas de giro, grip, drift grip).

Variantes: Vector (equilibrado), Rayo (velocidad), Ágil (manejo), Titán (pesado).

### Derrape

Mantener DERRAPE + dirección a >42 % de velocidad. Carga → niveles 1/2/3 (0.9 s / 1.9 s / 3.0 s escalados por DriftControl). Soltar → turbo ×1.16 / ×1.24 / ×1.32. Girar hacia adentro aprieta, hacia afuera abre; la dirección nunca se invierte. Partículas por nivel (humo → cian → naranja → magenta), sonido y vibración progresiva.

### Boost (`BoostController`)

Fuentes: derrape, power-up, plataforma, aterrizaje, salida perfecta, rebufo (slipstream), Mega Boost. El más fuerte manda, +2 % por fuente extra encadenada. Ignora la penalización de terreno.

### Saltos

Puntos `JumpGap` en la pista quitan el asfalto y añaden un kicker. Aterrizar tras ≥0.45 s en el aire da un turbo corto. Hop al iniciar el derrape.

### Recuperación (`RespawnController`)

Caída bajo `killY`, zonas de muerte bajo los saltos, atascado 4 s, volcado 2 s o caída libre 4.5 s → respawn en el último checkpoint válido, 2 s de invulnerabilidad, fundido para el jugador.

## Power-ups (`Scripts/PowerUps`)

| Nombre | Efecto |
|---|---|
| Turbo | ×1.28 durante 2.2 s |
| Misil Rastreador | persigue al rival de adelante, sigue la pista; se evita con paredes, escudo, EMP o esquivando |
| Bólido | proyectil recto, rebota 3 veces |
| Mina Magnética | queda atrás 25 s; trompo + lentitud |
| Escudo Prisma | bloquea un impacto, 7 s |
| Pulso EMP | aturde rivales en 22 m (aceleración −60 %, sin ítems), destruye proyectiles |
| Mancha Slime | charco resbaladizo 15 s |
| Triple Bólido | 3 cargas |
| Triple Turbo | 3 turbos cortos |
| Mega Boost | ×1.45 durante 5 s + armadura contra pérdida de control |

`PositionWeightedPowerUpTable`: cada `PowerUpData` tiene pesos Líder / Adelante / Medio / Atrás interpolados por posición normalizada. El líder recibe defensivos, el fondo ofensivos y recuperación. Sin rubber banding absurdo.

Cajas: prismas flotantes, desaparecen 5 s tras recogerse. 1 slot (arquitectura preparada para 2).

## Circuitos (`TrackData` → `TrackBuilder`)

Definidos como puntos de control (posición, ancho, flags) → spline Catmull-Rom → nodos cada 4 m (línea de carrera, curvatura, velocidad recomendada, flags) → malla de asfalto, bordes emisivos, paredes, túneles, kill zones, checkpoints (cada ~110 m), gatillos de atajo, plataformas de turbo, filas de cajas, peligros y decoración por tema.

1. **Metro Neón** — fácil/media, ~70 s. Ciudad nocturna, autopista elevada, túnel, salto, callejón como atajo, barrera móvil en el túnel, tráfico decorativo.
2. **Cañón Solar** — media, ~88 s. Desierto, puentes sin barandas, salto sobre barranco, mina abandonada (atajo túnel), arena off-road, zona lenta.
3. **Sky Lab** — media/alta, ~78 s. Plataformas sin bordes (caídas), tubo de gravedad con anillos giratorios, turbina que empuja, pasarela de servicio como atajo, gran salto sobre el vacío.

Atajos: un gatillo de entrada permite saltar los checkpoints entre entrada y salida; sin pasar por el gatillo, saltear checkpoints no cuenta.

## IA (`Scripts/AI`)

`AIKartController`: sigue la línea con offset lateral personal, frena por velocidad recomendada de los nodos, derrapa en zonas de derrape, adelanta (elige el lado con más lugar), defiende (bloquea), evita minas/slime/paredes, toma atajos, se recupera (reversa y realineación, respawn si no puede) y usa power-ups con intención según el tipo.

Personalidades: Aggressive, Balanced, Technical, Defensive, Chaotic (agresividad, precisión, uso de ítems, adelantamiento, riesgo, drift, defensa, variación de línea, reacción).

Dificultad (Easy/Normal/Hard) mejora comportamiento: precisión, errores, uso de derrape, atajos, power-ups y mirada adelante. Factores de velocidad 0.86/0.96/1.0. Rubber banding ±2–3.5 % de velocidad máxima según distancia al jugador.

## Carrera (`Scripts/Race`)

`RaceManager`: grilla 2 columnas (jugador último), cuenta regresiva 3-2-1-GO, salida perfecta (acelerar ≤0.55 s antes de GO → turbo) / anticipada (>1.1 s → 0.9 s bloqueado), tiempo, fin, resultados, récords, pausa y autopausa.

`RaceProgressTracker`: checkpoints ordenados, vueltas validadas, progreso continuo, dirección incorrecta, atajos, respawn pose. `PositionManager` ordena por progreso; los que terminan conservan su orden.

## UI (`Scripts/UI`)

Todo uGUI world-space generado por código, navegable con gamepad (navegación explícita) y mouse.

- Menú principal: JUGAR / CONFIGURACIÓN / SALIR. Flujo JUGAR → circuito → dificultad → kart + piloto → carrera. El jugador está sentado en un kart de exhibición.
- Configuración: Juego, VR, Audio, Gráficos (presets LOW/MEDIUM/HIGH + render scale, sombras, efectos, AA), Controles (rebind).
- HUD en el tablero: posición, vuelta, velocidad, power-up, barras de derrape y turbo. Aviso flotante para cuenta regresiva, vueltas, dirección incorrecta y mensajes. Espejo retrovisor (render texture) al mantener MIRAR ATRÁS.
- Pausa: CONTINUAR / REINICIAR / CONFIGURACIÓN / SALIR AL MENÚ.
- Resultados: tabla, mejor vuelta, récords, CONTINUAR / REPETIR / MENÚ.
- Carga: nombre del mapa + tip.

## Audio (`Scripts/Audio`)

100 % procedural (`ProceduralAudio`): motor (pitch por RPM simulada), derrape, boost, impactos, explosión, pickup, uso de ítem, escudo, EMP, vuelta, cuenta regresiva, UI, fanfarria y loops musicales chiptune por tema. Categorías Master / Música / SFX. Rivales espacializados.

## Arquitectura

```
Assets/Scripts/
  Core/    GameManager, SceneLoader, SaveManager, GameSettings, InputManager, GameEvents, SceneEntry, RaceSetup, GraphicsSettingsApplier
  Data/    KartStats, PowerUpData, TrackData, AIPersonality, DifficultySettings, PilotData, DefaultContent, GameDatabase
  Kart/    KartController, DriftController, BoostController, JumpController, RespawnController, KartStatusEffects, KartVisuals, KartFeedback, KartFactory, PilotRig, PlayerKartDriver
  Track/   TrackSpline, TrackNode, TrackBuilder, TrackDecorator, TrackRuntime, TrackSurface, Checkpoint, BoostPad, KillZone, ShortcutTrigger, TrackHazards
  Race/    RaceManager, RaceProgressTracker, PositionManager, TrackSceneController
  AI/      AIKartController
  PowerUps/ PowerUpManager, PowerUpBase (+10 implementaciones), PowerUpInventory, PowerUpBox, Projectile, TrackHazardObjects, PositionWeightedPowerUpTable
  VR/      VRManager, VRComfortManager, VRFader, VRWheelInput
  UI/      UIFactory, MainMenuController, SettingsMenu, PauseMenu, RaceHUD, ResultsScreen, LoadingScreen
  Audio/   AudioManager, ProceduralAudio, VehicleAudio
  Utils/   ObjectPoolManager, MaterialLibrary, PrimitiveFactory, MeshBuilder, VfxFactory, MathUtil, Layers
  Debug/   DebugRacePanel
Assets/Editor/KartVR/KartVRProjectSetup.cs  (URP, OpenXR, capas, escenas, assets de datos)
```

Escenas: Bootstrap, MainMenu, Loading, Track_NeonCity, Track_SolarCanyon, Track_SkyLab. Cada escena contiene sólo un `SceneEntry`; todo lo demás se genera al cargar.

Datos: ScriptableObjects en `Assets/Resources/Data/*` generados desde `DefaultContent` por la herramienta del editor. Si faltan, el juego usa los mismos valores por defecto en memoria.

Persistencia: JSON en `Application.persistentDataPath` (`settings.json`, `records.json`).

## Estado actual

Ver `DEVELOPMENT_STATUS.md`.
