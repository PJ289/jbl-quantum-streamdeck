# Protocolo IPC de JBL Quantum Engine

## Resumen

Quantum Engine no expone API pública. La comunicación interna usa **UDP** sobre `127.0.0.1:20502`.

| Proceso | Rol |
|---------|-----|
| `QuantumServer.exe` (-s) | Servicio central, escucha en **20502** |
| `QuantumEngine.exe` | UI principal, cliente IPC |
| `QuantumOSC.exe` | Overlay en pantalla, cliente IPC |

## Arquitectura

```
Stream Deck Plugin  ──►  QuantumBridge.exe  ──IPC──►  Quantum Engine (instalado)  ──►  Q810
     (Node.js)         (carga DLL desde Program Files; no las empaqueta)
```

La biblioteca nativa `IPC.dll` (P/Invoke desde `QEIPC.API` en `QuantumServer.dll`) gestiona registro de clientes y envío de mensajes. Esas DLL **deben permanecer en la instalación de Quantum Engine**; este proyecto no las redistribuye.

## Constantes IPC

| Constante | Valor | Uso |
|-----------|-------|-----|
| `APPLICATION_IPC_PATH` | `QE_APPLICATION` | Nombre IPC para clientes de aplicación |
| `QUANTUM_OSC_IPC_PATH` | `QE_OSC_WINDOW` | Overlay (QuantumOSC) |
| Puerto servicio | `20502` | Confirmado en netstat + Process Monitor |

## Formato de mensaje (`QEIPC.IPC_MSG`)

```
Offset  Tipo     Campo
0       uint32   MsgID
4       uint32   Length (payload)
8       bytes    Payload (Length bytes)
```

## Mensajes de gestión (ProductManagement)

| ID | Nombre |
|----|--------|
| 2 | DEVICE_ONLINE_MSG |
| 3 | DEVICE_OFFLINE_MSG |
| 4 | DEVICE_READY_MSG |
| 5 | DEVICE_NOT_READY_MSG |
| 6 | SUBSCRIBE_MSG |
| 7 | UNSUBSCRIBE_MSG |
| 8 | VALID_DEVICE_LIST_MSG |

## Controles útiles — JBL Quantum 810

### ANC (`QECommon.ANCState`)

| Valor | Estado |
|-------|--------|
| 0 | OFF |
| 1 | ANC |
| 2 | TALK_THROUGH |
| 3 | AMBIENT_AWARE |

| Acción | MsgID |
|--------|-------|
| Get | 20506 |
| **Set** | **20507** |
| Notify | 20508 |

### Sidetone software

| Acción | MsgID |
|--------|-------|
| Set | 20501 |

### Sidetone dispositivo (HIDV3)

| Acción | MsgID |
|--------|-------|
| Set | 12627 |

### Volumen micrófono

| Acción | MsgID |
|--------|-------|
| Set | 12675 |

### Balance juego/chat

| Acción | MsgID |
|--------|-------|
| Set | 4173 |

### Modo spatial sound (HIDV3)

| Acción | MsgID |
|--------|-------|
| Set | 12621 |

### Mute micrófono (HIDV3)

| Acción | MsgID |
|--------|-------|
| Set | 12669 |

Lista completa: `docs/ipc-message-ids.json` (1174 entradas extraídas de `QuantumServer.dll`).

## API nativa (`QEIPC.API` → `IPC.dll`)

Funciones clave:

- `QEIRegisterAsClient(port, id, info, ...)`
- `QEIConnectIPC(hClient, ipcName, ...)`
- `QEISendMsgToService(hIPC, IPC_MSG)`
- `QEISendSyncMsgToService(hIPC, msg, expectedMsgID, timeout)`

## Próximo paso para depuración

Si el bridge falla, captura tráfico UDP en Wireshark (`udp.port == 20502`) mientras cambias ANC en Quantum Engine y compara con los MsgID de arriba.
