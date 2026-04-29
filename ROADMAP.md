# Zafiro.Avalonia.Mcp — Roadmap: AI-Agent-Friendly Interaction

> **Community project** — This project is independently maintained and is not officially affiliated with, endorsed by, or maintained by Avalonia UI (AvaloniaUI OÜ). "Avalonia" is a trademark of AvaloniaUI OÜ.

## Estado general

| Fase | Alcance | Estado |
|---|---|---|
| **Fase 0** | Fundamentos (bugs y deuda técnica) | ✅ Completada |
| **Fase 1** | La IA puede "ver" sin screenshots | ✅ Completada |
| **Fase 2** | La IA puede interactuar con fiabilidad | ✅ Completada |
| **Fase 3** | La IA puede esperar y reaccionar | ✅ Completada |
| **Fase 4** | Calidad y robustez | ⬜ Pendiente |
| **Fase 5** | Inteligencia MVVM y puente XAML↔Runtime | ✅ Completada |
| **Fase 6** | Selectors universales, errores estructurados, diagnósticos avanzados, eventos | ✅ Completada (v2.0.0) |

### Desglose por item

| Fase | Item | Descripción | Estado |
|---|---|---|---|
| F0 | 0.1 | Fix `RaisePointerEvent` — `PointerEventArgs` reales | ✅ Implementado |
| F0 | 0.2 | Fix `NodeRegistry` O(n) → O(1) | ✅ Implementado |
| F0 | 0.3 | Key modifiers en keyboard handler | ✅ Implementado |
| F0 | 0.4 | Eliminar código muerto | ✅ Implementado |
| F1 | 1.1 | Enriquecer `NodeInfo` (texto, role, estado) | ✅ Implementado |
| F1 | 1.2 | `get_interactables` | ✅ Implementado |
| F1 | 1.3 | `get_screen_text` | ✅ Implementado |
| F2 | 2.1 | `select_item` | ✅ Implementado |
| F2 | 2.2 | `toggle` | ✅ Implementado |
| F2 | 2.3 | `set_value` | ✅ Implementado |
| F2 | 2.4 | `scroll` | ✅ Implementado |
| F3 | 3.1 | `wait_for` | ✅ Implementado |
| F3 | 3.2 | `click_and_wait` | ✅ Implementado |
| F4 | 4.1 | Tests (Protocol, NodeRegistry, Handlers, Integración) | ⬜ Pendiente |
| F4 | 4.2 | Documentación del protocolo en README | ⬜ Pendiente |
| F4 | 4.3 | Optimizar tool descriptions para LLMs | ✅ Completado |
| F5 | B1 | Fix `click_by_query` — filtro de interactividad | ✅ Implementado (v1.3.0) |
| F5 | B2 | Fix `pseudo_class` — `IPseudoClasses.Set()` | ✅ Implementado (v1.3.0) |
| F5 | B3 | Fix `capture_animation` — LZW, GCE, off-UI-thread | ⚠️ Parcial (v1.3.0) — PNG round-trip por mejorar |
| F5 | 5.1 | `get_datacontext` — inspeccionar ViewModel | ✅ Implementado (v1.3.0) |
| F5 | 5.2 | `get_bindings` — diagnosticar bindings | ✅ Implementado (v1.3.0) |
| F5 | 5.3 | `find_view_source` — mapear control → AXAML | ✅ Implementado (v1.3.0) |
| F5 | 5.4 | `get_xaml` — ver XAML fuente | ✅ Implementado (v1.3.0) |
| F5 | 5.5 | `get_screen_text` `visibleOnly` — filtro viewport | ✅ Implementado (v1.3.0) |
| F5 | 5.6 | `diff_tree` — snapshot/diff de cambios | ✅ Implementado (v1.3.0) |
| F6 | 6.1 | Gramática de selectors CSS-like + parser | ✅ Implementado (v2.0.0) |
| F6 | 6.2 | `SelectorEngine.Default` (resolver completo) | ✅ Implementado (v2.0.0) |
| F6 | 6.3 | `DiagnosticError` con 8 códigos estables | ✅ Implementado (v2.0.0) |
| F6 | 6.4 | Migrar handlers de acción a `selector` | ✅ Implementado (v2.0.0) |
| F6 | 6.5 | Migrar handlers de lectura a `selector` | ✅ Implementado (v2.0.0) |
| F6 | 6.6 | `get_focus`, `get_active_window`, `get_open_dialogs` | ✅ Implementado (v2.0.0) |
| F6 | 6.7 | `get_command_info` — estado `ICommand.CanExecute` | ✅ Implementado (v2.0.0) |
| F6 | 6.8 | `get_validation_errors` — `INotifyDataErrorInfo` walk | ✅ Implementado (v2.0.0) |
| F6 | 6.9 | `get_layout_info` — caja de layout, márgenes, clipping | ✅ Implementado (v2.0.0) |
| F6 | 6.10 | `find_by_datacontext` + `get_item` (virtualización) | ✅ Implementado (v2.0.0) |
| F6 | 6.11 | `fill_form` — múltiples campos + submit + redacción | ✅ Implementado (v2.0.0) |
| F6 | 6.12 | `subscribe` / `poll_events` / `unsubscribe` | ✅ Implementado (v2.0.0) |
| F6 | 6.13 | Documentación v2 (README, ROADMAP, AGENTS) | ✅ Completado (v2.0.0) |
| F6 | 6.14 | Release v2.0.0 + `MIGRATION-v2.md` | ✅ Completado (v2.0.0) |

---

## Problema original

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

## Fase 0 — Fundamentos (bugs y deuda técnica) ✅

> Sin esto, lo demás se construye sobre arena.

### 0.1 Fix `RaisePointerEvent` — construir `PointerEventArgs` reales ✅

**Archivo:** `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/InputHandler.cs`

**Problema actual** (línea ~54-58):
```csharp
private static void RaisePointerEvent(Control control, RoutedEvent routedEvent, Point position)
{
    control.RaiseEvent(new RoutedEventArgs(routedEvent)); // ← ignora position, tipo incorrecto
}
```

**Solución:** Construir `PointerPressedEventArgs` / `PointerReleasedEventArgs` con posición, dispositivo y botón. Avalonia requiere un `IPointer` — investigar si se puede usar `MouseDevice` de test o crear un mock `IPointer`. Alternativa pragmática: usar la infraestructura de headless testing de Avalonia (`Avalonia.Headless`) que ya tiene utilidades para simular input.

**Criterio de éxito:** `click(nodeId)` funciona correctamente sobre `ListBoxItem`, `CheckBox`, `ToggleButton`, `RadioButton`, `Slider` (al menos mover al centro), `MenuItem`, `TabItem`, `ComboBoxItem`.

### 0.2 Fix `NodeRegistry` — eliminar búsqueda O(n) ✅

**Archivo:** `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/NodeRegistry.cs`

**Problema:** `GetOrRegister()` itera todo el `Dictionary<int, WeakReference<Visual>>` para cada nodo buscando `ReferenceEquals`.

**Solución:** Añadir un `ConditionalWeakTable<Visual, StrongBox<int>>` como mapa inverso `Visual → nodeId`. En `GetOrRegister`: lookup en O(1) vía `ConditionalWeakTable.TryGetValue()`. Si no existe, asignar nuevo ID y registrar en ambas estructuras.

**Criterio de éxito:** `GetOrRegister` pasa de O(n) a O(1). Medir con BenchmarkDotNet sobre un árbol de 1000+ nodos.

### 0.3 Añadir key modifiers al keyboard handler ✅

**Archivo:** `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/InputHandler.cs` (clases `KeyboardHandler` / `KeyUpHandler`)

**Problema:** `KeyEventArgs` se crea sin `KeyModifiers`. No se puede hacer Ctrl+A, Ctrl+C, Shift+Tab.

**Solución:** Añadir parámetro opcional `modifiers` (string: `"ctrl"`, `"shift"`, `"alt"`, `"ctrl+shift"`, etc.) al handler. Parsear a `KeyModifiers` enum y pasarlo al constructor de `KeyEventArgs`.

**Cambios en Server:** Actualizar `InputTools.cs` para exponer el parámetro `modifiers` en los tools `key_down` y `key_up`.

### 0.4 Eliminar código muerto ✅

- `InputHandler.RaisePointerEvent`: el parámetro `position` se calcula pero no se usa (se arregla con 0.1).
- `SearchHandler`: el parámetro `searchTemplates` se parsea (línea ~21) pero nunca se usa en `Matches()`. Eliminar o implementar.
- `SetPropertyHandler`: el parámetro `isXamlValue` se recibe pero no se usa. Implementar (parsear XAML markup como valor) o eliminar.

---

## Fase 1 — La IA puede "ver" sin screenshots ✅

> Objetivo: que una IA pueda entender el estado completo de la UI con una sola llamada textual.

### 1.1 Enriquecer `NodeInfo` con texto, estado y semántica ✅

**Archivos:**
- `src/Zafiro.Avalonia.Mcp.Protocol/Models/NodeInfo.cs` — añadir campos
- `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/TreeHandler.cs` — poblar los campos en `SerializeNode()`

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

### 1.2 Nuevo tool: `get_interactables` ✅

**Concepto:** Devuelve **solo** los controles accionables visibles, con su texto y estado. Esto es lo que una IA necesita el 90% del tiempo — equivalente a "¿qué puedo hacer ahora?".

**Archivos nuevos:**
- `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/InteractablesHandler.cs`
- Registrar en `RequestDispatcher.cs`
- Añadir method constant en `ProtocolMethods.cs`
- `src/Zafiro.Avalonia.Mcp.Tool/Tools/TreeTools.cs` — añadir tool

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

### 1.3 Nuevo tool: `get_screen_text` ✅

**Concepto:** Devuelve todo el texto visible en la pantalla en orden de lectura (top-to-bottom, left-to-right), agrupado por regiones lógicas. Alternativa ultra-barata a un screenshot.

**Archivos nuevos:**
- `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/ScreenTextHandler.cs`

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

## Fase 2 — La IA puede interactuar con fiabilidad ✅

> Objetivo: que toda interacción sea fiable, no solo clicks en botones.

### 2.1 Nuevo tool: `select_item` ✅

**Concepto:** Para `SelectingItemsControl` (ListBox, ComboBox, TabControl), seleccionar un item por índice o por texto. Más fiable que simular pointer events.

**Archivo nuevo:** `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/SelectionHandler.cs`

**API:**
```
select_item(nodeId, index?: int, text?: string)
```

**Lógica:**
1. Resolver `nodeId` → `SelectingItemsControl`
2. Si `index` dado → `control.SelectedIndex = index`
3. Si `text` dado → iterar `Items`, encontrar el que coincida con su texto/ToString, seleccionarlo
4. Devolver el item seleccionado con su texto y index

### 2.2 Nuevo tool: `toggle` ✅

**Concepto:** Para `ToggleButton`, `CheckBox`, `RadioButton`, `ToggleSwitch` — cambiar estado directamente.

**API:**
```
toggle(nodeId, state?: bool)  // si state omitido, toggle; si dado, set
```

**Lógica:** `control.IsChecked = state ?? !control.IsChecked`

### 2.3 Nuevo tool: `set_value` ✅

**Concepto:** Para `Slider`, `NumericUpDown`, `ProgressBar` — establecer valor numérico.

**API:**
```
set_value(nodeId, value: double)
```

### 2.4 Nuevo tool: `scroll` ✅

**Concepto:** Scroll de contenido visible.

**API:**
```
scroll(nodeId, direction: "up"|"down"|"left"|"right", amount?: double)
```

**Lógica:** Encontrar el `ScrollViewer` padre (o el control mismo si lo es) y ajustar `Offset`.

---

## Fase 3 — La IA puede esperar y reaccionar ✅

> Objetivo: eliminar el polling manual y los race conditions.

### 3.1 Nuevo tool: `wait_for` ✅

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

### 3.2 Nuevo tool: `click_and_wait` ✅

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

## Fase 4 — Calidad y robustez ⬜

### 4.1 Tests ⬜

**Directorio:** `test/Zafiro.Avalonia.Mcp.Tests/`

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

### 4.2 Documentación del protocolo ⬜

**Archivo:** actualizar `README.md` del repo con:
- Lista completa de tools con ejemplos para IA
- Patrones recomendados ("primero `get_interactables`, luego `click`")
- Anti-patrones ("no uses `get_tree` para entender la UI, usa `get_interactables`")

### 4.3 MCP tool descriptions optimizadas ✅

Todas las descripciones de los tools en `src/Zafiro.Avalonia.Mcp.Tool/Tools/*.cs` se reescribieron con un formato consistente de tres bloques:
- Propósito + cuándo usar vs. alternativas
- `Returns:` describiendo la forma del resultado
- `Example:` con un output compacto (JSON o texto)

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

---

## Estado tras Fase 0–3 — Validación con app real (Angor, Abril 2026)

> Fases 0–3 implementadas. Se evaluó contra **Angor** (app real de Bitcoin, Avalonia 11.3.12, ~50 vistas, custom controls, ScrollViewers con cientos de items).

### ✅ Lo que funciona bien (valoración 8.5/10 para comfort IA)

| Tool | Rating | Notas |
|---|---|---|
| `get_screen_text` | ⭐⭐⭐⭐⭐ | Texto visible en orden de lectura con nodeIds. ~200 tokens vs ~5000 de screenshot. |
| `get_interactables` | ⭐⭐⭐⭐⭐ | JSON plano `{nodeId, role, text, automationId}`. Decisión inmediata. |
| `search` | ⭐⭐⭐⭐⭐ | Encuentra por tipo, nombre o texto. Bounds + parentId incluidos. |
| `click` | ⭐⭐⭐⭐⭐ | Navegación real — cambió de sección, UI se actualizó correctamente. |
| `set_prop` | ⭐⭐⭐⭐⭐ | Modificó `Text` y `Foreground` en vivo. Confirmado con screenshot. |
| `get_tree` (Logical) | ⭐⭐⭐⭐ | Estructura `MainWindow > ShellView > DockPanel > [TopBar, Nav, Content]` — orientación inmediata. |
| `get_props` / `get_styles` | ⭐⭐⭐⭐ | Valores actuales con prioridad (Style/StyleTrigger/LocalValue). Clases CSS activas. |
| `get_ancestors` | ⭐⭐⭐⭐ | Cadena completa hasta raíz — 25 nodos en un click. |
| `screenshot` | ⭐⭐⭐ | Funciona, pero alto coste en tokens. Último recurso. |
| `list_assets` / `get_resources` | ⭐⭐⭐ | Funcional pero ruidoso (miles de assets de icon packs). |

### 🐛 Bugs encontrados

#### B1. `click_by_query` no encuentra controles custom ✅ (v1.3.0)

**Síntoma:** `click_by_query("Funds", role="listitem")` → `"No interactive element found"`, pero `get_interactables` SÍ devuelve el `SectionStripItem` con ese texto.

**Causa probable:** `click_by_query` usa un motor de búsqueda diferente (más restrictivo) que `get_interactables`. Debería compartir la misma lógica de matching.

**Archivos:** `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/` — comparar búsqueda de `ClickByQueryHandler` vs `InteractablesHandler`.

#### B2. `pseudo_class` falla al activar pseudo-clases ✅ (v1.3.0)

**Síntoma:** `pseudo_class(nodeId, "pointerover", isActive=true)` → error de servidor. Listar pseudo-clases funciona (`pseudo_class(nodeId)` → `[]`).

**Archivos:** `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/PseudoClassHandler.cs`

#### B3. `capture_animation` inestable ⚠️ Parcial (v1.3.0)

**Síntoma:** Devuelve "Recorded 25 frames" pero el proceso se interrumpe. GIF resultante corrupto o no entregado.

**Archivos:** `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/RecordingHandler.cs`, `GifEncoder.cs`

---

## Fase 5 — Inteligencia MVVM y puente XAML↔Runtime ✅

> Objetivo: la IA no solo "ve" la UI, sino que entiende *por qué* se ve así y *cómo modificarla permanentemente*.

### 5.1 `get_datacontext(nodeId)` — Ver el ViewModel ✅

**Problema:** `get_props` devuelve `"DataContext": "Zafiro.UI.Navigation.Sections.Section"` (solo el tipo). La IA no puede inspeccionar las propiedades del ViewModel.

**API:**
```
get_datacontext(nodeId, depth?: int = 1)
```

**Output:**
```json
{
  "type": "FindProjectsSectionViewModel",
  "properties": [
    { "name": "Projects", "type": "ObservableCollection<ProjectViewModel>", "count": 42 },
    { "name": "IsLoading", "type": "Boolean", "value": "False" },
    { "name": "SearchQuery", "type": "String", "value": "" },
    { "name": "RefreshCommand", "type": "ReactiveCommand", "canExecute": true }
  ]
}
```

**Implementación:** Reflexión sobre el DataContext del control. Serializar propiedades públicas a 1 nivel (2 con `depth=2`). Para colecciones, solo `count`. Para commands (ReactiveCommand, ICommand), incluir `canExecute`.

**Archivos nuevos:**
- `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/DataContextHandler.cs`
- `src/Zafiro.Avalonia.Mcp.Tool/Tools/DataContextTools.cs`

### 5.2 `get_bindings(nodeId)` — Diagnosticar bindings ✅

**Problema:** Un TextBlock muestra "" vacío. ¿Es porque el binding está roto, el valor es null, o no hay binding? Ahora no hay forma de saberlo sin leer el XAML.

**API:**
```
get_bindings(nodeId)
```

**Output:**
```json
[
  {
    "property": "Text",
    "path": "Name",
    "mode": "OneWay",
    "source": "DataContext",
    "status": "Bound",
    "actualValue": "Pay Flow 168ec533b028"
  },
  {
    "property": "IsVisible",
    "path": "HasFunders",
    "mode": "OneWay",
    "source": "DataContext",
    "status": "Error",
    "error": "Property 'HasFunders' not found on 'ProjectCardViewModel'"
  }
]
```

**Implementación:** Iterar `AvaloniaObject.GetSetValues()` o reflection sobre binding expressions. Para cada `BindingExpression` activa, extraer path, modo, estado y valor actual.

**Archivos nuevos:**
- `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/BindingHandler.cs`

### 5.3 `find_view_source(nodeId)` — Localizar el XAML ✅

**Problema:** La IA sabe que hay un `ShellView` en el árbol, pero no sabe que está definido en `UI/Shell/ShellView.axaml`.

**API:**
```
find_view_source(nodeId)
```

**Output:**
```json
{
  "type": "ShellView",
  "assembly": "AngorApp",
  "xamlAsset": "avares://AngorApp/UI/Shell/ShellView.axaml",
  "codeBehind": "UI/Shell/ShellView.axaml.cs"
}
```

**Implementación:** 
1. Obtener el tipo CLR del control (o su View ancestro más cercano)
2. Buscar en `list_assets` un `.axaml` cuyo path coincida con el namespace/nombre del tipo
3. `codeBehind` = derivado por convención (`.axaml` → `.axaml.cs`)

### 5.4 `get_xaml(nodeId)` — Ver el XAML fuente ✅

**Problema:** El árbol visual dice QUÉ hay. El XAML dice POR QUÉ y CÓMO cambiarlo de forma duradera.

**API:**
```
get_xaml(nodeId)
```

**Output:** El contenido del `.axaml` asset asociado al View que contiene el control.

**Implementación:** 
1. Usar `find_view_source` para localizar el `avares://` URL
2. Leer el asset embebido (ya existe `open_asset`)
3. Devolver el contenido XAML

**Dependencia:** Requiere `find_view_source` (5.3).

### 5.5 `get_screen_text` con filtro `visibleOnly` ✅

**Problema:** En la vista "Find Projects" de Angor, `get_screen_text` devolvió **260+ líneas** incluyendo todos los items del ScrollViewer que están fuera de pantalla. Desperdicio masivo de tokens.

**API:** Añadir parámetro opcional:
```
get_screen_text(nodeId?, visibleOnly?: bool = false)
```

**Implementación:** Cuando `visibleOnly=true`, filtrar TextBlocks cuyas bounds absolutas no intersecten con el viewport de su ScrollViewer ancestro (o la ventana).

### 5.6 `diff_tree` — Comparar estados antes/después ✅

**Problema:** Patrón actual de la IA: `get_screen_text` → `click` → `get_screen_text` → diff mental (costoso en tokens y propenso a errores).

**API:**
```
diff_tree(before_snapshot_id, after_snapshot_id)
```

O versión autocontenida:
```
take_snapshot() → snapshot_id
// ... interact ...
diff_snapshot(snapshot_id) → { added: [...], removed: [...], changed: [...] }
```

**Implementación:** Guardar snapshots del texto visible con nodeIds. Al hacer diff, comparar por nodeId y texto.

---

## Resumen de prioridades (actualizado Abril 2026)

| Item | Impacto | Esfuerzo |
|---|---|---|
| **B1** Fix `click_by_query` matching | 🔴 Crítico — herramienta de conveniencia inutilizable | Pequeño |
| **B2** Fix `pseudo_class` activation | 🟡 Medio — afecta testing visual de estados | Pequeño |
| **B3** Fix `capture_animation` stability | 🟡 Medio — GIF recording no fiable | Medio |
| **5.5** `get_screen_text` visibleOnly | 🔴 Crítico — ScrollViews con 100+ items queman tokens | Pequeño |
| **5.1** `get_datacontext` | 🔴 Crítico — transforma debugging MVVM | Medio |
| **5.2** `get_bindings` | 🔴 Crítico — diagnosticar bindings rotos sin leer código | Medio |
| **5.3** `find_view_source` | 🟡 Medio — puente runtime→código fuente | Pequeño |
| **5.4** `get_xaml` | 🟡 Medio — ver definición XAML en contexto | Pequeño (depende de 5.3) |
| **5.6** `diff_tree` | 🟡 Medio — reduce tokens en flujos interactivos | Medio |

---

## Estado de implementación (Actualizado)

| Item | Estado | Notas |
|---|---|---|
| **B1** `click_by_query` | ✅ Implementado | Reescrito `FindMatchingVisuals` con filtro de interactividad (misma lógica que `InteractablesHandler.IsInteractive`), matching de texto por hijos visuales, AutomationId y AutomationName |
| **B2** `pseudo_class` | ✅ Implementado | Cambiado `styled.Classes.Set()` → `((IPseudoClasses)styled.Classes).Set()` para pseudo-classes gestionadas por el framework |
| **B3** `capture_animation` | ✅ Parcialmente implementado | Corregido LZW off-by-one, añadido GCE disposal method (restore-to-background), encoding GIF movido fuera del UI thread, error handling en FrameRecorder. PNG round-trip sigue como área de mejora potencial |
| **5.1** `get_datacontext` | ✅ Implementado | `DataContextHandler` — refleja propiedades públicas del DataContext con valores truncados |
| **5.2** `get_bindings` | ✅ Implementado | `BindingsHandler` — usa `GetDiagnostic()` de Avalonia.Diagnostics para mostrar propiedades con binding activo, prioridad, valor y diagnóstico |
| **5.3** `find_view_source` | ✅ Implementado | `FindViewSourceHandler` — mapea tipo runtime → avares:// URL probando convenciones de path comunes |
| **5.4** `get_xaml` | ✅ Implementado | `GetXamlHandler` — combina find_view_source + open_asset en una sola llamada, devuelve el XAML fuente |
| **5.5** `visibleOnly` | ✅ Implementado | Parámetro `visibleOnly` en `ScreenTextHandler` — filtra por intersección con viewport de ventana y ScrollViewers ancestros |
| **5.6** `diff_tree` | ✅ Implementado | `DiffTreeHandler` — snapshot/diff de texto visible, devuelve solo líneas añadidas/eliminadas |

### Archivos nuevos
- `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/DataContextHandler.cs`
- `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/BindingsHandler.cs`
- `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/FindViewSourceHandler.cs`
- `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/GetXamlHandler.cs`
- `src/Zafiro.Avalonia.Mcp.AppHost/Handlers/DiffTreeHandler.cs`
- `src/Zafiro.Avalonia.Mcp.Tool/Tools/DataTools.cs`

### Archivos modificados
- `ClickByQueryHandler.cs` — reescrito completamente
- `PseudoClassHandler.cs` — fix IPseudoClasses cast
- `GifEncoder.cs` — LZW fix + GCE disposal
- `FrameRecorder.cs` — error logging
- `RecordingHandler.cs` — GIF encoding off UI thread
- `ScreenTextHandler.cs` — visibleOnly + viewport clipping
- `RequestDispatcher.cs` — 5 nuevos handlers registrados
- `ProtocolMethods.cs` — 5 nuevas constantes
- `TreeTools.cs` — parámetro visibleOnly en get_screen_text
- `DataTools.cs` — 5 nuevas herramientas MCP

---

## Fase 6 — Selectors universales, errores estructurados, diagnósticos avanzados ✅

Cinco grandes cambios, todos rompedores, agrupados en la release **v2.0.0**.

### 6.1 / 6.2 Selectors CSS-like ✅

Reemplazan el `nodeId` numérico (que forzaba 3 round-trips: snapshot → resolve → llamada) por una sola string declarativa que resuelve el árbol vivo.

Gramática (ver `src/Zafiro.Avalonia.Mcp.Protocol/Selectors/`):

- `#42` → nodeId existente
- `#Name` → match por `x:Name`
- `Type[Property=Value]`, `*=`, `^=`, `$=`, `~=` (case-insensitive)
- `[dc.Path=Value]` → property path sobre el DataContext
- `[dc:'predicate']` → predicado C# evaluado por Roslyn (200 ms timeout, sandbox)
- `:nth(N)`, `:visible`, `:focused`, `:enabled`, `:checked` (pseudo-classes)
- `>>` descendiente, `>` hijo directo, `,` alternativas

Motor: `SelectorEngine.Default.Resolve(string|ParsedSelector, scope?) → IReadOnlyList<Visual>`.

### 6.3 Errores estructurados ✅

`DiagnosticError { code, message, suggested?, details? }`. Códigos estables: `NO_MATCH`, `AMBIGUOUS_SELECTOR`, `STALE_NODE`, `INVALID_PARAM`, `INVALID_SELECTOR`, `UNSUPPORTED_OPERATION`, `TIMEOUT`, `INTERNAL`. Cada error sugiere la recuperación (`Add :nth(N)…`, `Re-call get_snapshot…`).

### 6.4 / 6.5 Migración a `selector` ✅

- **Action handlers** (commit `2dbb35b`): `ActionHandler`, `InputHandler` (click/tap/key_down/key_up/text_input), `PseudoClassHandler`, `ScrollHandler`, `SelectionHandler`, `SetPropertyHandler`, `SetValueHandler`, `ToggleHandler`. Helper compartido `SelectorRequestHelper.ResolveSingle`.
- **Read handlers** (commit `bf87161`): `AncestorsHandler`, `BindingsHandler`, `DataContextHandler`, `FindViewSourceHandler`, `GetXamlHandler`, `PropertyHandler`, `ScreenshotHandler` (selector opcional), `StylesHandler`.

### 6.6–6.10 Nuevos diagnósticos ✅

- `get_focus` — elemento con foco actual.
- `get_active_window` + `get_open_dialogs` — estado de ventanas y diálogos.
- `get_command_info` — `ICommand.CanExecute` para botones/menús.
- `get_validation_errors` — recorre `INotifyDataErrorInfo` de todo el árbol.
- `get_layout_info` — caja de layout, márgenes, alignment, clipping.
- `find_by_datacontext` — equivalente a `[dc:'…']` como tool top-level.
- `get_item` — resuelve hijos virtualizados por índice/text/dc.

### 6.11 `fill_form` ✅

Tool composite que aplica una lista de campos (`{ selector, value, secret? }`) y opcionalmente hace click en un selector `submit` final. Devuelve resultados por campo; si `secret:true`, el valor registrado se serializa como `"<value> (redacted)"`.

### 6.12 Suscripciones a eventos ✅

Long-poll: `subscribe`, `poll_events`, `unsubscribe`. Tipos: `property_changed`, `window_opened`, `window_closed`, `focus_changed`. Cola por suscripción acotada a 1000 eventos, poll por defecto 30 s (máx 60 s), TTL 5 min, máximo 32 suscripciones simultáneas (`SUBSCRIPTION_LIMIT`).

### 6.13 Documentación v2 ✅

`README.md` reescrito con sección "v2.0 highlights", cheat-sheet de selectors, ejemplo de `DiagnosticError`, convención de nombres y ejemplos basados en `selector` en lugar de `nodeId`. `AGENTS.md` añade un apartado "v2 Tool API". `ROADMAP.md` añade esta sección con todos los items en ✅.

### 6.14 Release v2.0.0 ✅

Documento `MIGRATION-v2.md` en la raíz del repo: rationale, tabla de renombres (`take_screenshot → screenshot`), tabla de migración por handler (`v1 nodeId:N` ↔ `v2 selector:"…"`), nuevos tools, nueva forma del error, gramática, y eliminados/deprecados (ninguno). El versionado lo dirige el tag `v2.0.0` vía GitVersion (estrategia Mainline + ConfiguredNextVersion); no hay `<Version>` en `.csproj` ni en `Directory.Build.props`, así que el bump se materializa con el tag — ver `MIGRATION-v2.md` para el comando exacto.

### Higiene de naming

`take_screenshot → screenshot`. La página `instructions(page='tools')` (51 tools) lista la superficie canónica + cheat-sheet de selectors + tabla de error codes + workflows recomendados. Esto resuelve el bug original (un agente IA alucinando `take_screenshot` que no existía).

**Phase 6 shipped in v2.0.0.**

## Fase 7 — Transporte remoto: Android (ADB) y otros devices 🟡 (MVP en `feature/android-adb-mvp`)

**Objetivo:** que un agente IA pueda probar end-to-end una app Avalonia corriendo en un dispositivo **Android** (físico o emulador) conectado al PC del agente vía **ADB**, usando exactamente la misma superficie de tools MCP que ya usa en desktop. Más adelante, generalizar el transporte para iOS (`idevice` / Mac), WSA, y conexiones remotas (SSH).

**MVP entregado (v2.1.0):** transporte TCP loopback + tool `connect_adb` manual. El agente forwardea el puerto con `adb forward tcp:HOSTPORT tcp:DEVICEPORT` y llama `connect_adb port=HOSTPORT`. Auto-discovery, limpieza de forwards y `AdbDeviceProbe` quedan para fases siguientes.

### Estado actual (limitaciones de v2)

- `AppHost` apunta a `net8.0;net10.0` puros — **no compila** para `net8.0-android` / `net9.0-android`.
- Discovery: `DiscoveryWriter` escribe `{PID}.json` en `Path.GetTempPath()` y `ConnectionPool` lo lee del temp **local del PC**. En Android, `Path.GetTempPath()` apunta al cache privado del paquete (`/data/data/<pkg>/cache`) — inaccesible desde el host sin `adb`.
- Transporte: `DiagnosticServer` usa `NamedPipeServerStream` (sockets Unix de filesystem en `/tmp/CoreFxPipe_<name>` en Linux/Android) y `AppConnection` usa `NamedPipeClientStream`. `adb forward` **no** puede mapear directamente un socket de filesystem dentro del sandbox del paquete a un puerto del host: solo puede `localabstract:`, `localreserved:`, `localfilesystem:` y rutas accesibles por `adb`, pero los CoreFxPipe del paquete viven dentro de `/data/data/<pkg>` que solo es accesible vía `run-as`.
- No hay forma de listar qué apps Avalonia están corriendo en el device desde el PC.

### 7.1 Multi-target Android para `AppHost` ✅ (no requerido)

- ~~Añadir `net8.0-android;net9.0-android`~~. **No necesario en MVP**: `AppHost` sigue en `net8.0;net10.0` puros. Las apps `net10.0-android` lo consumen vía .NET Standard fallback. `AndroidPaths` usa reflection (`Type.GetType("Android.App.Application, Mono.Android")`) para acceder a `ExternalCacheDir` sin dependencia de compilación.
- `samples/SampleApp.Android` (`net10.0-android`) verifica el camino end-to-end: referencia el `AppHost` directamente, llama `UseMcpDiagnostics()` desde `AvaloniaAndroidApplication.CustomizeAppBuilder`, y compila a APK.

### 7.2 Transporte abstracto + TCP loopback ✅

Introducir una abstracción `IDiagnosticTransport` con dos implementaciones:

- `NamedPipeTransport` (actual, default desktop).
- `TcpLoopbackTransport`: escucha en `127.0.0.1:0` (puerto efímero) y publica el puerto real en el discovery JSON. En Android es la única opción viable para que `adb forward tcp:HOSTPORT tcp:DEVICEPORT` funcione sin permisos elevados.

`UseMcpDiagnostics(options => { options.Transport = TransportKind.Auto; })` con `Auto` = TCP en Android, NamedPipe en desktop. Permitir `TransportKind.Tcp` explícito en cualquier plataforma para casos remotos.

**Implementado:** `IDiagnosticTransport` + `NamedPipeTransport` + `TcpLoopbackTransport`. `Endpoint` se publica en formato `tcp:127.0.0.1:NNN`. `OperatingSystem.IsAndroid()` resuelve `Auto`. Cubierto por `TcpLoopbackTransportTests`.

### 7.3 Discovery cross-process ✅ (parcial)

Extender `DiscoveryInfo` con `TransportKind`, `Endpoint` (string: `pipe:<name>` o `tcp:<port>`), `PackageId` (en Android), `Abi`, `OsVersion`. En Android, escribir el JSON en una ruta accesible por `adb`:

- Opción A (preferida): cache externo del paquete (`Context.ExternalCacheDir` → `/sdcard/Android/data/<pkg>/cache/`), legible vía `adb shell ls/cat` sin `run-as`.
- Opción B: el AppHost expone un endpoint TCP de discovery muy simple (ASCII, una línea JSON) que el tool consulta con `adb forward`.

**Implementado en MVP:** `DiscoveryInfo` lleva los campos opcionales `transport`, `endpoint`, `packageId` (retro-compatibles, `WhenWritingNull`). `DiscoveryWriter` usa `AndroidPaths.ExternalCacheDir` cuando está disponible. **Pendiente:** `Abi`, `OsVersion`, y la auto-detección desde el host (cubre 7.4).

### 7.4 `AdbDeviceProbe` en el tool ⬜ (post-MVP)

Nuevo módulo en `Zafiro.Avalonia.Mcp.Tool` (o `…Tool.Adb`) que envuelve el binario `adb`:

- `adb devices -l` → lista de devices.
- Por cada device: `adb shell pm list packages -3 -f` + heurística (paquetes con assets `*.axaml`, dependencia de `Avalonia.*.dll`) o, mejor, leer los discovery JSON publicados en `/sdcard/Android/data/*/cache/zafiro-avalonia-mcp/`.
- Para cada app encontrada: hacer `adb forward tcp:0 tcp:<devicePort>`, leer el puerto host asignado, y construir un `AppConnection` apuntado a `127.0.0.1:<hostPort>`.
- `list_apps` integra los devices Android sin distinción para el agente (campo opcional `transport: "adb:<serial>"` en la respuesta).

**MVP shortcut:** se proporciona el tool `connect_adb port=<HOSTPORT> [host=127.0.0.1] [label=...]` que crea un `DiscoveryInfo` sintético TCP y se conecta. El usuario corre `adb forward tcp:9999 tcp:<devicePortFromJson>` manualmente.

### 7.5 Limpieza de forwards y reconexión ⬜ (post-MVP)

`ConnectionPool` debe registrar los `adb forward` creados y liberarlos (`adb forward --remove tcp:HOSTPORT`) cuando la conexión se cierra o el device se desconecta. Reintento exponencial si el device se reconecta a USB/WiFi.

### 7.6 Input táctil end-to-end en Android ⬜

Auditar que toda la superficie de tools de input (`click`, `tap`, `text_input`, `key_down`, `scroll`, `select_item`, `toggle`, `fill_form`) funciona contra el dispatcher real de Android (Avalonia.Android usa `SingleTopLevelImpl`). Si algún tool depende de Win32/X11, sustituir por la API portable equivalente de Avalonia (`InputManager.ProcessInput`, `RawInputEventArgs`).

### 7.7 Screenshots y recordings en Android ⬜

`screenshot` ya usa `Visual.RenderTo(Bitmap)` que es portable, debería funcionar tal cual. Validar que `capture_animation` / `start_recording` no asumen un timer del thread UI desktop.

### 7.8 Sample y CI 🟡

- ✅ `samples/SampleApp.Android` mínimo, single-activity, con TextBox/Button/CheckBox/Slider/ListBox cubriendo los tools de input típicos.
- ✅ Documentado en README la sección "Android via ADB (preview)" con `adb forward` + `connect_adb`.
- ⬜ (Opcional) Job de CI con emulador Android headless que ejecute un smoke test end-to-end vía MCP.

### 7.9 Documentación ✅ (MVP)

- ✅ Sección "Android via ADB (preview)" en `README.md` con prerequisitos, pasos, y referencia al endpoint `tcp:` en el discovery JSON.
- ⬜ Apartado en `AGENTS.md` explicando que el agente no necesita saber si la app corre en desktop o Android — la API es idéntica.

### Out of scope (futuras fases)

- iOS vía `idevice` / Mac (similar pero requiere infra Apple).
- Conexiones SSH a un PC remoto que ejecuta la app Avalonia.
- Autenticación / firmado del transporte TCP (hoy solo loopback, OK para dev local; remoto necesitaría TLS + token).
