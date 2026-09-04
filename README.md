# Vortex Karts VR

Kart racer arcade para realidad virtual (PCVR / OpenXR) hecho en Unity 6 LTS + URP + Input System.

- Diseño: `GAME_DESIGN.md`
- Estado real del proyecto: `DEVELOPMENT_STATUS.md`

## Cómo abrirlo

1. Instalar **Unity Hub** y **Unity 6000.0.x LTS** (Windows Build Support). Unity 6.3 LTS también sirve (aceptar la actualización del proyecto).
2. En Unity Hub: *Add project from disk* → esta carpeta.
3. Al abrir, la herramienta `Vortex Karts > Setup Project (all steps)` corre automáticamente: crea el asset URP, asigna OpenXR, capas, escenas de build y los ScriptableObjects de datos en `Assets/Resources/Data`. Si algo falla, correrla desde el menú y leer la consola.
4. Abrir `Assets/Scenes/Bootstrap.unity` y presionar Play. Sin visor conectado funciona en modo escritorio (cámara fija en el asiento, gamepad o teclado).

## Controles rápidos

Gamepad: RT acelerar · LT frenar · stick izq. dirección · A derrape · RB power-up · LB mirar atrás · Start pausa · Select recentrar VR.
Teclado: W/S, A/D, Espacio, Shift, Q, Esc, R.

## Herramientas

- `node Tools/gen-meta.js` — regenera `.meta` faltantes, escenas mínimas y `EditorBuildSettings`.
- `node Tools/check-cs.js` — chequeo estático rápido de los scripts (balance de llaves, namespaces).
- F1 en una carrera (editor / development build) — panel de debug.
