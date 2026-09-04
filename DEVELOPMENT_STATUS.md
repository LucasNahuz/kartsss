# DEVELOPMENT STATUS — Vortex Karts VR

Fuente de verdad del estado del proyecto. Actualizado: 2026-09-03.

## Contexto importante

- Unity 6000.0.83f1 instalado en `C:\Program Files\Unity\Hub\Editor\6000.0.83f1` (2026-09-03). El proyecto **compila sin errores** y el setup (URP, OpenXR con 6 perfiles, capas, datos, escenas) corrió correctamente.
- Build Windows de desarrollo verificado: `Builds/Dev/VortexKartsVR.exe` (menú `Vortex Karts > Build`, o `-executeMethod VortexKarts.EditorTools.KartVRBuild.BuildWindowsDev`).
- Verificación automatizada sin visor (modo escritorio): `VortexKartsVR.exe -autorace neon_metro -autopilot -topdown 120 -screenshots 30,100 -quitafter 300`. El piloto automático completó las 3 vueltas de Metro Neón: **2º de 8, 3:44, mejor vuelta 71,1 s**, cero excepciones, récord guardado en `records.json`.
- Hooks de línea de comandos: `-autorace <trackId>`, `-autopilot`, `-topdown [altura]`, `-screenshots s1,s2`, `-quitafter s`. Telemetría `[Telemetry]` cada 5 s en el log con `-autopilot`.
- Todavía **no probado con headset** (esta PC no tiene runtime OpenXR): el juego detecta la ausencia y cae en modo escritorio correctamente.

## COMPLETADO (implementado, pendiente de verificación en Unity)

### Proyecto
- Estructura Unity 6 (`Packages/manifest.json` con URP 17, Input System 1.11, XR Management 4.5, OpenXR 1.13), `ProjectSettings` mínimos (color lineal, Input System nuevo), capas (`TagManager.asset`), 6 escenas y `EditorBuildSettings`.
- `Tools/gen-meta.js` genera `.meta` determinísticos, escenas y build settings.
- Materiales base URP en `Resources/Materials` (Lit, Emissive, Unlit, UnlitTransparent, Particle) para garantizar inclusión de shaders en builds.
- Herramienta de editor `KartVRProjectSetup` con auto-run.

### Core
- `GameManager` persistente, `SceneLoader` async con fundido y pantalla de carga, `SaveManager` JSON (settings + récords), `GameSettings` completo, `GameEvents`, `RaceSetup`, `SceneEntry`, `GraphicsSettingsApplier`.
- `InputManager`: acciones construidas por código (gamepad, teclado, controles XR), deadzone, curva de sensibilidad, auto-acelerar, vibración gamepad + hápticos XR, desconexión/reconexión de gamepad, rebinding interactivo persistido.

### Kart (Prioridad 1)
- `KartController` arcade sobre esfera: aceleración, tope, freno, reversa, dirección dependiente de velocidad, agarre, derrape con deslizamiento real, gravedad, downforce, saltos, off-road, colisiones kart/pared con feedback, rebufo, asistencias.
- `DriftController` 3 niveles + turbo; `BoostController` multi-fuente; `JumpController` hop + landing boost; `RespawnController`; `KartStatusEffects` (trompo, lento, resbaladizo, aturdido, escudo, invulnerable, armadura) con resolución de impactos.
- `KartVisuals` (inclinación, suspensión, ruedas, volante, trompo visual del jugador), `PilotRig` procedural (brazos al volante, cabeza, celebración/derrota), `KartFeedback` (partículas por nivel, boost, impactos, escudo, hápticos), `KartFactory` (4 variantes visuales × 8 pilotos con cascos distintos).

### Pista
- `TrackSpline` Catmull-Rom con muestreo uniforme; `TrackBuilder` genera asfalto, bordes, paredes, túneles, kickers, huecos de salto, kill zones, plano off-road, checkpoints, gatillos de atajo, plataformas de turbo, filas de cajas, peligros (barrera móvil, turbina, zona lenta, zona resbaladiza), portal de salida, sol y decoración por tema.
- 3 circuitos definidos en `DefaultContent`: Metro Neón, Cañón Solar, Sky Lab (cada uno con 1 atajo, saltos, plataformas, peligros y tips).

### Carrera
- `RaceManager`: grilla, cuenta regresiva, salida perfecta/anticipada, tiempo, fin, resultados con estimación para CPU, récords, pausa, autopausa por dispositivo.
- `RaceProgressTracker`: checkpoints ordenados, anti-trampa (marcha atrás sobre la meta, atajos no permitidos), dirección incorrecta, atajos, respawn pose.
- `PositionManager`.

### IA
- `AIKartController` completo: línea de carrera con offset, frenado por nodos, derrape, adelantamiento, defensa, evasión de peligros y paredes, atajos, recuperación, errores por dificultad, rubber banding suave, uso de power-ups por tipo, personalidades y dificultades como ScriptableObjects.

### Power-ups
- 10 power-ups implementados (`PowerUpBase` + subclases), `PowerUpManager` con pools, `PowerUpInventory` (1 slot, preparado para 2), `PowerUpBox` con respawn, `Projectile` (recto con rebotes / guiado siguiendo pista), `MineHazard`, `OilSlickHazard`, EMP con visual, tabla ponderada por posición.

### VR
- `VRManager` (rig persistente, `TrackedPoseDriver`, tracking origin, recenter, detección de tracking, modo escritorio automático), `VRComfortManager` (horizonte estabilizado, movimiento reducido, vignette, shake posicional), `VRFader`, `VRWheelInput` (volante virtual con grip, marcadores de manos).

### UI
- `UIFactory` (canvas world-space, botones, sliders, toggles, selectores, navegación explícita, cancelar), menú principal con kart de exhibición, configuración completa y funcional (todas las opciones tienen efecto real), pausa, HUD de tablero con espejo retrovisor, resultados, pantalla de carga.

### Audio
- `ProceduralAudio` sintetiza todos los sonidos y 4 loops musicales; `AudioManager` con categorías y eventos; `VehicleAudio` motor/derrape por kart.

### Debug
- `DebugRacePanel` (F1, sólo editor/dev build): vueltas, teletransporte a posición, dar power-up, reset, IA on/off, time scale, dibujar waypoints/línea/checkpoints.

## EN DESARROLLO / A VERIFICAR

1. **Prueba con headset real** (Quest Link / SteamVR): recentrado, altura del asiento, confort, volante VR (`CameraOffset` → mundo), rendimiento 72–90 FPS.
2. **Sensación de manejo con un humano al mando**: hasta ahora sólo condujo la IA. Ajustar `KartStats`, grip, drift slide (`0.18`) y tasas de giro en pista vacía (criterio §46).
3. Cañón Solar y Sky Lab: carreras automatizadas en curso; la mina del cañón se rediseñó porque cruzaba la ruta principal.
4. IA en horquillas: todavía roza la pared exterior en la horquilla de Metro Neón (telemetría lat ≈ ±6 con ancho 13).
5. Rendimiento: medir draw calls de decoración, sombras y partículas en VR.
6. Probar menú principal, configuración, pausa y resultados con gamepad real (la lógica corrió, pero la navegación no se ejercitó de forma automatizada).

## PENDIENTE

- Minimapa opcional del HUD.
- Puntero láser VR para menús (hoy los menús se navegan con gamepad/teclado/mouse; el modo recomendado es gamepad).
- Podio visual 3D en resultados (hoy es tabla; el piloto del jugador celebra/lamenta en su kart).
- Segundo slot de power-ups (arquitectura lista).
- Baked lighting / occlusion culling / LODs (geometría procedural, se evaluará tras medir FPS).
- Modelos finales de karts, pilotos y entornos (Fase 10, tras validar diversión).
- Adaptación standalone (Quest): arquitectura compatible, no probada.
- Pruebas del checklist §49 en Unity.

## PROBLEMAS CONOCIDOS

- Nada verificado en tiempo de ejecución todavía (ver Contexto).
- Si `OpenXR Package Settings` no existe al correr el setup, los perfiles de controlador deben activarse a mano una vez en Project Settings > XR Plug-in Management > OpenXR y volver a ejecutar `Vortex Karts > Configure XR`.
- La fuente de UI es la legacy `LegacyRuntime.ttf`; TextMeshPro queda para el polish (mejor nitidez en VR).
- El Sky Lab no tiene plano de suelo: caerse siempre implica respawn (intencional).

## Decisiones tomadas con autonomía

- Física sobre esfera rígida + heading simulado (robusta, sin vuelcos, derrape controlable).
- Trompo del jugador sin girar la cabeza (confort VR).
- Todo generado por código (pistas, karts, UI, audio) para iterar sin depender de assets; las escenas sólo contienen `SceneEntry`.
- Datos en ScriptableObjects generados desde `DefaultContent`, con fallback en memoria.
- Audio procedural original en vez de assets externos.
- Jugador arranca último en la grilla (convención arcade).
