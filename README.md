# JBL Quantum — Stream Deck (no oficial)

Plugin de [Elgato Stream Deck](https://www.elgato.com/downloads) para controlar auriculares **JBL Quantum** (probado con **Quantum 810 Wireless**) usando el **JBL Quantum Engine** ya instalado en tu PC.

> **No es un producto oficial de JBL, Harman, Samsung ni Elgato.**  
> No está afiliado, respaldado ni autorizado por ellos.

---

## Aviso legal y descargo de responsabilidad

- Este proyecto es **software de terceros**, no oficial.
- **No redistribuye** las DLL ni el software de Quantum Engine. En tiempo de ejecución carga las bibliotecas desde la instalación local de Quantum Engine (por defecto `C:\Program Files\JBL\QuantumENGINE`). Eso evita empaquetar binarios de terceros; **no** convierte el proyecto en oficial ni garantiza por sí solo el cumplimiento total del EULA de Quantum Engine.
- Usa interfaces internas / no documentadas de Quantum Engine. Pueden cambiar o dejar de funcionar tras una actualización.
- Las marcas **JBL**, **Quantum**, **Harman** y **Stream Deck** pertenecen a sus respectivos dueños.
- Se proporciona **“tal cual”**, sin garantías de ningún tipo (funcionamiento, idoneidad, no infracción, etc.).
- El uso es **bajo tu propia responsabilidad**. El autor no se hace responsable de daños, pérdida de datos, problemas de garantía del hardware/software, ni reclamaciones derivadas del uso de este plugin.
- Antes de redistribuir o publicar builds, asegúrate de **no incluir** archivos de Quantum Engine (`QuantumServer.dll`, `IPC.dll`, `ShareMemory.dll`, etc.).

Esto **no constituye asesoramiento legal**. Si necesitas certeza, consulta a un abogado o a JBL/Harman.

---

## Cómo funciona

```
Stream Deck  →  plugin (Node.js)  →  QuantumBridge.exe  →  Quantum Engine (instalado)  →  auriculares
```

1. Stream Deck ejecuta el plugin.
2. El plugin arranca `QuantumBridge.exe` (código de este repo).
3. El bridge **no lleva DLL de JBL empaquetadas**: las carga desde tu instalación de Quantum Engine y habla con el servicio IPC de Quantum Engine.
4. Quantum Engine aplica el cambio a los auriculares (ANC, perfiles, etc.).

**Requisito clave:** Quantum Engine debe estar instalado y su servicio en ejecución. Sin eso el plugin no puede controlar el headset.

Variable opcional si Quantum Engine está en otra ruta:

```powershell
$env:QUANTUM_ENGINE_PATH = "D:\Ruta\A\QuantumENGINE"
```

---

## Requisitos

| Requisito | Notas |
|-----------|--------|
| Windows 10/11 | Solo Windows |
| [Stream Deck](https://www.elgato.com/downloads) 6.5+ | Software de Elgato |
| [JBL Quantum Engine](https://www.jbl.com/) | Instalado; servicio activo |
| Auriculares Quantum compatibles | Probado con Q810 Wireless |
| [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) | Para ejecutar el bridge |
| Node.js 20+ y .NET SDK 8 | Solo si **compilas** desde el código |

---

## Instalación (desde el código)

En la carpeta del proyecto:

```powershell
npm install
npm run install:plugin
```

Eso:

1. Compila el bridge (usando las DLL de Quantum Engine solo como referencia de compilación).
2. Compila el plugin.
3. Genera iconos si faltan.
4. Copia el plugin a `%APPDATA%\Elgato\StreamDeck\Plugins\com.pj289.jbl-quantum.sdPlugin`.
5. **Elimina** cualquier DLL de JBL que hubiera quedado de builds antiguos.

Luego:

1. **Reinicia Stream Deck.**
2. En el panel de acciones, busca la categoría **JBL Quantum**.
3. Arrastra las acciones a tu perfil.

### Comandos útiles

```powershell
npm run build:bridge   # solo bridge
npm run build          # solo plugin.js
npm run icons          # regenerar iconos
npm run install:plugin # build + instalar
```

Probar el bridge sin Stream Deck:

```powershell
powershell -File tools\test-bridge.ps1 -SkipWrites
```

---

## Uso

### ANC

| Acción | Qué hace |
|--------|----------|
| **Toggle ANC** | Alterna solo OFF ↔ ANC (sin Ambient / Talk Through). Cambia de color según estado. |
| **Cycle ANC** | Recorre OFF → ANC → Talk Through → Ambient. |
| **ANC Off / On / Ambient** | Fija ese modo. Se iluminan cuando ese modo está activo. |

### Perfiles de Quantum Engine

Los perfiles guardan EQ, sonido espacial, etc. en Quantum Engine.

| Acción | Qué hace |
|--------|----------|
| **Cycle Profile** | Pasa al siguiente perfil. Muestra color/nombre del actual. |
| **Set Profile** | Carga un perfil fijo. Al seleccionar el botón, elige el perfil en el panel derecho. El botón usa el **color del perfil** y se ilumina si está activo. |

### Audio

| Acción | Qué hace |
|--------|----------|
| **Mic Volume Up / Down** | Sube o baja el volumen del micrófono. |
| **More Game / More Chat** | Ajusta el balance juego/chat. |

---

## Solución de problemas

| Problema | Qué comprobar |
|----------|----------------|
| El botón muestra alerta / no hace nada | Quantum Engine instalado; servicio en marcha; cascos conectados; reiniciar Stream Deck. |
| “QuantumServer.dll not found” | Instala Quantum Engine o define `QUANTUM_ENGINE_PATH`. |
| Set Profile sin lista | Abre Quantum Engine al menos una vez; pulsa ↻ en el panel del botón. |
| Cambios tras actualizar el plugin | Reinicia Stream Deck. Si un archivo está bloqueado, cierra Stream Deck y vuelve a `npm run install:plugin`. |

---

## Limitaciones

- Integración **no oficial** y basada en IPC interno: puede romperse con actualizaciones de Quantum Engine.
- No hay soporte oficial de JBL/Harman/Elgato para este plugin.
- Compatibilidad validada principalmente con **JBL Quantum 810 Wireless**; otros modelos pueden variar.
- No se incluyen ni se deben publicar binarios de Quantum Engine con este proyecto.

---

## Licencia del código de este repositorio

El código propio de este repositorio (plugin, bridge, scripts) puedes usarlo según la licencia que indiques al publicar (por ejemplo MIT).  
Eso **no** te otorga derechos sobre el software de JBL Quantum Engine ni sobre las marcas comerciales.
