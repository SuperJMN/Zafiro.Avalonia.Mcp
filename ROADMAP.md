# AvaloniaMcp — Roadmap: AI-Agent-Friendly Interaction

## Problema

El MCP actual fue diseñado como herramienta de **inspección para desarrolladores**.
Una IA que intenta *usar* una app Avalonia a través de él se encuentra con:

1. **Ceguera textual** — `get_tree` devuelve `{nodeId, type, name, bounds, isVisible}` sin texto ni contenido. La IA necesita screenshots (miles de tokens) o llamadas `get_props` nodo-a-nodo para "ver" la pantalla.
2. **Click roto en controles no-Button** — `RaisePointerEvent` crea un `RoutedEventArgs` genérico, no un `PointerPressedEventArgs`. ListBox, CheckBox, Slider, ComboBox, TabControl… ignoran el evento.
3. **Sin filtro de interactividad** — No hay forma de pedir "dame los controles accionables". La IA recibe árboles de 300+ nodos con Borders, ContentPresenters y demás ruido visual.
4. **Sin modificadores de teclado** — `KeyEventArgs` no incluye `KeyModifiers`. Imposible simular Ctrl+C, Shift+Tab, Alt+F4.
5. **NodeRegistry O(n)** — `GetOrRegister()` itera todo el diccionario por cada nodo. Coste cuadrático en árboles grandes.
6. **Cero tests** — El directorio `test/` está vacío.

---

## Arquitectura actual (referencia)

```
┌──────────────┐     Named Pipe (JSON lines)      ┌──────────────────┐
│ Avalonia App  │◄──────────────────────────────────│  MCP Server      │
│ (AppHost)     │    DiagnosticRequest/Response      │  (stdio MCP)     │
│               │                                    │                  │
│ DiagServer    │                                    │ ConnectionPool   │
│ NodeRegistry  │                                    │ AppConnection    │
│ Handlers[]    │                                    │ Tools[]          │
└──────────────┘                                    └──────────────────┘
       ▲                                                    ▲
       │ .UseMcpDiagnostics()                               │ stdio MCP protocol
       ▼                                                    ▼
   Avalonia App                                       AI Agent
```

**Flujo de un tool call:**
1. AI Agent → (MCP stdio) → `Server/Tools/FooTools.cs` → serializa `DiagnosticRequest`
2. → (Named Pipe) → `AppHost/Handlers/RequestDispatcher.cs` → despacha a `IRequestHandler`
3. Handler opera sobre el árbol visual de Avalonia (UI thread) → `DiagnosticResponse`
4. → (Named Pipe) → Server deserializa → (MCP stdio) → AI Agent

**Archivos clave por área:**

| Área | AppHost (in-process) | Server (MCP) | Protocol (compartido) |
|---|---|---|---|
| Árbol | `Handlers/TreeHandler.cs`, `SearchHandler.cs` | `Tools/TreeTools.cs` | `Models/NodeInfo.cs` |
| Input | `Handlers/InputHandler.cs` (click), `KeyboardHandler`, `TextInputHandler` | `Tools/InputTools.cs` | — |
| Props | `Handlers/PropertyHandler.cs`, `SetPropertyHandler.cs` | `Tools/PropertyTools.cs` | `Models/PropertyInfo.cs` |
| IDs | `Handlers/NodeRegistry.cs` (static dict `int → WeakReference<Visual>`) | — | — |
| Captura | `Handlers/ScreenshotHandler.cs`, `RecordingHandler.cs`, `FrameRecorder.cs`, `GifEncoder.cs` | `Tools/CaptureTools.cs` | — |
| Conexión | `DiagnosticServer.cs`, `Discovery/DiscoveryWriter.cs` | `Connection/ConnectionPool.cs`, `AppConnection.cs` | `Messages/DiagnosticRequest.cs` |

---

## Fase 0 — Fundamentos (bugs y deuda técnica)

> Sin esto, lo demás se construye sobre arena.

### 0.1 Fix `RaisePointerEvent` — construir `PointerEventArgs` reales

**Archivo:** `src/AvaloniaMcp.AppHost/Handlers/InputHandler.cs`

**Problema actual** (línea ~54-58):
```csharp
private static void RaisePointerEvent(Control control, RoutedEvent routedEvent, Point position)
{
    control.RaiseEvent(new RoutedEventArgs(routedEvent)); // ← ignora position, tipo incorrecto
}
```

**Solución:** Construir `PointerPressedEventArgs` / `PointerReleasedEventArgs` con posición, dispositivo y botón. Avalonia requiere un `IPointer` — investigar si se puede usar `MouseDevice` de test o crear un mock `IPointer`. Alternativa pragmática: usar la infraestructura de headless testing de Avalonia (`Avalonia.Headless`) que ya tiene utilidades para simular input.

**Criterio de éxito:** `click(nodeId)` funciona correctamente sobre `ListBoxItem`, `CheckBox`, `ToggleButton`, `RadioButton`, `Slider` (al menos mover al centro), `MenuItem`, `TabItem`, `ComboBoxItem`.

### 0.2 Fix `NodeRegistry` — eliminar búsqueda O(n)

**Archivo:** `src/AvaloniaMcp.AppHost/Handlers/NodeRegistry.cs`

**Problema:** `GetOrRegister()` itera todo el `Dictionary<int, WeakReference<Visual>>` para cada nodo buscando `ReferenceEquals`.

**Solución:** Añadir un `ConditionalWeakTable<Visual, StrongBox<int>>` como mapa inverso `Visual → nodeId`. En `GetOrRegister`: lookup en O(1) vía `ConditionalWeakTable.TryGetValue()`. Si no existe, asignar nuevo ID y registrar en ambas estructuras.

**Criterio de éxito:** `GetOrRegister` pasa de O(n) a O(1). Medir con BenchmarkDotNet sobre un árbol de 1000+ nodos.

### 0.3 Añadir key modifiers al keyboard handler

**Archivo:** `src/AvaloniaMcp.AppHost/Handlers/InputHandler.cs` (clases `KeyboardHandler` / `KeyUpHandler`)

**Problema:** `KeyEventArgs` se crea sin `KeyModifiers`. No se puede hacer Ctrl+A, Ctrl+C, Shift+Tab.

**Solución:** Añadir parámetro opcional `modifiers` (string: `"ctrl"`, `"shift"`, `"alt"`, `"ctrl+shift"`, etc.) al handler. Parsear a `KeyModifiers` enum y pasarlo al constructor de `KeyEventArgs`.

**Cambios en Server:** Actualizar `InputTools.cs` para exponer el parámetro `modifiers` en los tools `key_down` y `key_up`.

### 0.4 Eliminar código muerto

- `InputHandler.RaisePointerEvent`: el parámetro `position` se calcula pero no se usa (se arregla con 0.1).
- `SearchHandler`: el parámetro `searchTemplates` se parsea (línea ~21) pero nunca se usa en `Matches()`. Eliminar o implementar.
- `SetPropertyHandler`: el parámetro `isXamlValue` se recibe pero no se usa. Implementar (parsear XAML markup como valor) o eliminar.

---

## Fase 1 — La IA puede "ver" sin screenshots

> Objetivo: que una IA pueda entender el estado completo de la UI con una sola llamada textual.

### 1.1 Enriquecer `NodeInfo` con texto, estado y semántica

**Archivos:**
- `src/AvaloniaMcp.Protocol/Models/NodeInfo.cs` — añadir campos
- `src/AvaloniaMcp.AppHost/Handlers/TreeHandler.cs` — poblar los campos en `SerializeNode()`

**Campos nuevos en `NodeInfo`:**

```csharp
public string? text { get; set; }          // TextBlock.Text, ContentControl.Content (string), HeaderedContentControl.Header
public bool? isEnabled { get; set; }        // InputElement.IsEnabled
public bool? isFocused { get; set; }        // InputElement.IsFocused
public bool? isInteractive { get; set; }    // IsFocusable && IsHitTestVisible && IsEnabled
public string? automationId { get; set; }   // AutomationProperties.AutomationId
public string? role { get; set; }           // "button", "textbox", "listitem", "checkbox", etc. (derivado del tipo CLR o AutomationPeer)
public string? className { get; set; }      // Classes (CSS classes) como string separado por espacios
public int? parentId { get; set; }          // nodeId del padre (para navegación sin get_ancestors)
```

**Lógica en `SerializeNode()` para `text`:**
1. Si es `TextBlock` → `.Text`
2. Si es `ContentControl` y `.Content` es `string` → eso
3. Si es `HeaderedContentControl` y `.Header` es `string` → eso
4. Si es `TextBox` → `.Text`
5. Si tiene `AutomationProperties.Name` → eso como fallback

**Lógica para `role`:** Mapeo simple de tipo CLR:
- `Button`, `RepeatButton`, `ToggleButton` → `"button"`
- `TextBox`, `MaskedTextBox`, `AutoCompleteBox` → `"textbox"`
- `CheckBox` → `"checkbox"`
- `RadioButton` → `"radio"`
- `ListBoxItem`, `TreeViewItem` → `"listitem"`
- `ComboBox` → `"combobox"`
- `Slider` → `"slider"`
- `TabItem` → `"tab"`
- `MenuItem` → `"menuitem"`
- `TextBlock` → `"text"`
- Default → `null` (no ensuciar con roles inútiles)

### 1.2 Nuevo tool: `get_interactables`

**Concepto:** Devuelve **solo** los controles accionables visibles, con su texto y estado. Esto es lo que una IA necesita el 90% del tiempo — equivalente a "¿qué puedo hacer ahora?".

**Archivos nuevos:**
- `src/AvaloniaMcp.AppHost/Handlers/InteractablesHandler.cs`
- Registrar en `RequestDispatcher.cs`
- Añadir method constant en `ProtocolMethods.cs`
- `src/AvaloniaMcp.Server/Tools/TreeTools.cs` — añadir tool

**Lógica:**
1. Recorrer el logical tree desde la ventana activa (o `nodeId` dado)
2. Filtrar por: `IsVisible && IsEnabled && (IsFocusable || es Button/MenuItem)`
3. Devolver lista plana de `InteractableInfo`:

```csharp
public class InteractableInfo
{
    public int nodeId { get; set; }
    public string type { get; set; }       // "Button", "TextBox", "ListBoxItem"...
    public string role { get; set; }       // "button", "textbox", "listitem"...
    public string? text { get; set; }      // Texto visible
    public string? name { get; set; }      // x:Name
    public string? automationId { get; set; }
    public bool isEnabled { get; set; }
    public bool isFocused { get; set; }
    public string? value { get; set; }     // Para TextBox: Text, CheckBox: IsChecked, Slider: Value, ComboBox: SelectedItem
}
```

**Ejemplo de output para la IA:**
```json
[
  { "nodeId": 12, "role": "button", "text": "¡A Batallar!", "isEnabled": true },
  { "nodeId": 15, "role": "button", "text": "⚡ Batalla Rápida", "isEnabled": true },
  { "nodeId": 23, "role": "listitem", "text": "Mi equipito", "isEnabled": true, "isFocused": false },
  { "nodeId": 30, "role": "textbox", "name": "SearchBox", "value": "", "isEnabled": true }
]
```

Con esto, **cero screenshots necesarios** para decidir qué hacer.

### 1.3 Nuevo tool: `get_screen_text`

**Concepto:** Devuelve todo el texto visible en la pantalla en orden de lectura (top-to-bottom, left-to-right), agrupado por regiones lógicas. Alternativa ultra-barata a un screenshot.

**Archivos nuevos:**
- `src/AvaloniaMcp.AppHost/Handlers/ScreenTextHandler.cs`

**Lógica:**
1. Recorrer el visual tree recolectando todos los `TextBlock` y `ContentPresenter` con texto visible
2. Obtener bounds absolutas (transformando coordenadas locales al espacio de la ventana)
3. Ordenar por Y, luego por X
4. Devolver como string plano con líneas, o como lista de `{text, x, y}`

**Ejemplo:**
```
Mis Equipos
Mi equipito: Pikachu, Pidgey, Machop
¡A Batallar!
⚡ Batalla Rápida
```

---

## Fase 2 — La IA puede interactuar con fiabilidad

> Objetivo: que toda interacción sea fiable, no solo clicks en botones.

### 2.1 Nuevo tool: `select_item`

**Concepto:** Para `SelectingItemsControl` (ListBox, ComboBox, TabControl), seleccionar un item por índice o por texto. Más fiable que simular pointer events.

**Archivo nuevo:** `src/AvaloniaMcp.AppHost/Handlers/SelectionHandler.cs`

**API:**
```
select_item(nodeId, index?: int, text?: string)
```

**Lógica:**
1. Resolver `nodeId` → `SelectingItemsControl`
2. Si `index` dado → `control.SelectedIndex = index`
3. Si `text` dado → iterar `Items`, encontrar el que coincida con su texto/ToString, seleccionarlo
4. Devolver el item seleccionado con su texto y index

### 2.2 Nuevo tool: `toggle`

**Concepto:** Para `ToggleButton`, `CheckBox`, `RadioButton`, `ToggleSwitch` — cambiar estado directamente.

**API:**
```
toggle(nodeId, state?: bool)  // si state omitido, toggle; si dado, set
```

**Lógica:** `control.IsChecked = state ?? !control.IsChecked`

### 2.3 Nuevo tool: `set_value`

**Concepto:** Para `Slider`, `NumericUpDown`, `ProgressBar` — establecer valor numérico.

**API:**
```
set_value(nodeId, value: double)
```

### 2.4 Nuevo tool: `scroll`

**Concepto:** Scroll de contenido visible.

**API:**
```
scroll(nodeId, direction: "up"|"down"|"left"|"right", amount?: double)
```

**Lógica:** Encontrar el `ScrollViewer` padre (o el control mismo si lo es) y ajustar `Offset`.

---

## Fase 3 — La IA puede esperar y reaccionar

> Objetivo: eliminar el polling manual y los race conditions.

### 3.1 Nuevo tool: `wait_for`

**Concepto:** Esperar hasta que una condición se cumpla en la UI, con timeout. Elimina el patrón de "click → screenshot → check → repeat".

**API:**
```
wait_for(
    query: string,           // búsqueda del elemento (como search)
    condition: string,       // "exists", "visible", "enabled", "text_equals", "text_contains", "count_equals"
    value?: string,          // valor para la condición
    timeout_ms?: int = 5000  // máximo de espera
)
```

**Ejemplos de uso por la IA:**
```
wait_for(query: "Button", condition: "text_equals", value: "¡A Batallar!", timeout_ms: 3000)
wait_for(query: "ProgressBar", condition: "visible", timeout_ms: 10000)
wait_for(query: "ErrorMessage", condition: "exists", timeout_ms: 2000)
```

**Implementación:** Polling interno cada 100ms (dentro del UI thread via `DispatcherTimer`) hasta que la condición se cumpla o se agote el timeout. Devuelve el `NodeInfo` del elemento encontrado o error con timeout.

### 3.2 Nuevo tool: `click_and_wait`

**Concepto:** Acción compuesta — click + esperar resultado. Reduce 3 llamadas MCP a 1.

**API:**
```
click_and_wait(
    nodeId: int,
    wait_query: string,
    wait_condition: string,
    wait_value?: string,
    timeout_ms?: int = 5000
)
```

**Ejemplo:**
```
click_and_wait(nodeId: 12, wait_query: "BattleView", wait_condition: "visible")
```

---

## Fase 4 — Calidad y robustez

### 4.1 Tests

**Directorio:** `test/AvaloniaMcp.Tests/`

**Cobertura mínima:**
- **Protocol:** Serialización/deserialización de todos los modelos (NodeInfo, PropertyInfo, DiagnosticRequest/Response)
- **NodeRegistry:** Register, Resolve, GetOrRegister (con el fix O(1)), limpieza de WeakReferences
- **Handlers (unitarios):** Cada handler con un control Avalonia mock. Usar `Avalonia.Headless` para crear controles reales sin ventana.
  - `TreeHandler` — profundidad, modos Visual/Logical/Merged, campos enriquecidos
  - `SearchHandler` — match por tipo, nombre, texto
  - `InputHandler` — click en Button con command, click en Button sin command, click en ListBoxItem
  - `InteractablesHandler` — filtrado correcto, valores extraídos
  - `SelectionHandler` — selección por index y por texto
- **Integración:** Named pipe round-trip completo (Server ↔ AppHost)

### 4.2 Documentación del protocolo

**Archivo:** actualizar `README.md` del repo con:
- Lista completa de tools con ejemplos para IA
- Patrones recomendados ("primero `get_interactables`, luego `click`")
- Anti-patrones ("no uses `get_tree` para entender la UI, usa `get_interactables`")

### 4.3 MCP tool descriptions optimizadas

Revisar las descripciones de cada tool en `Server/Tools/*.cs` para que sean descriptivas para el modelo de lenguaje. Incluir en la descripción:
- Cuándo usar este tool vs alternativas
- Qué devuelve
- Ejemplo de output compacto

---

## Resumen de prioridades

| Fase | Item | Impacto para IA | Esfuerzo |
|---|---|---|---|
| **F0** | 0.1 Fix pointer simulation | 🔴 Crítico — desbloquea clicks en no-Button | Medio |
| **F0** | 0.2 Fix NodeRegistry O(n) | 🟡 Perf — cuadrático en árboles grandes | Pequeño |
| **F0** | 0.3 Key modifiers | 🟡 Desbloquea shortcuts | Pequeño |
| **F0** | 0.4 Limpiar código muerto | 🟢 Higiene | Pequeño |
| **F1** | 1.1 NodeInfo enriquecido | 🔴 Crítico — la IA puede "ver" sin screenshots | Medio |
| **F1** | 1.2 `get_interactables` | 🔴 Crítico — responde "¿qué puedo hacer?" | Medio |
| **F1** | 1.3 `get_screen_text` | 🟡 Alternativa ultra-barata a screenshot | Medio |
| **F2** | 2.1 `select_item` | 🔴 Crítico — selección en listas fiable | Pequeño |
| **F2** | 2.2 `toggle` | 🟡 CheckBox/ToggleButton directo | Pequeño |
| **F2** | 2.3 `set_value` | 🟡 Slider/NumericUpDown directo | Pequeño |
| **F2** | 2.4 `scroll` | 🟡 Navegar contenido largo | Pequeño |
| **F3** | 3.1 `wait_for` | 🔴 Crítico — elimina polling con screenshots | Medio |
| **F3** | 3.2 `click_and_wait` | 🟡 Reduce round-trips | Pequeño |
| **F4** | 4.1 Tests | 🟢 Calidad — previene regresiones | Grande |
| **F4** | 4.2-4.3 Docs + tool descriptions | 🟡 Onboarding IA más rápido | Medio |

---

## Ejemplo: antes vs después

### Antes (estado actual) — 6 llamadas MCP + screenshot

```
1. screenshot()                                    → 50KB imagen, ~5000 tokens
2. (IA interpreta la imagen, deduce que hay un ListBox)
3. search("Mi equipito")                           → [{nodeId: 23, type: "TextBlock"}]
4. get_ancestors(23)                               → [..., {nodeId: 20, type: "ListBoxItem"}]
5. click(20)                                       → ❌ FALLA (pointer simulation roto)
6. (IA busca workaround, hace más screenshots...)
```

### Después (con roadmap implementado) — 2 llamadas MCP, cero screenshots

```
1. get_interactables()  → [
     {nodeId: 12, role: "button", text: "¡A Batallar!"},
     {nodeId: 15, role: "button", text: "⚡ Batalla Rápida"},
     {nodeId: 20, role: "listitem", text: "Mi equipito"},
     ...
   ]
2. click(20)            → ✅ FUNCIONA (pointer simulation arreglado)
```

**Reducción:** De ~5000+ tokens por turno a ~200 tokens. De 6+ llamadas a 2. De fallos silenciosos a éxito fiable.
