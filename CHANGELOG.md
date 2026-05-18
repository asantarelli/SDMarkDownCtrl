# SDMarkDownCtrl — Changelog

## [1.1.2] - 2026-05-08

### Cambios
- Actualización de dependencia Microsoft.Web.WebView2

---

## [1.1.1] - 2026-04-24

### Fixes

- `SetToolbarVisible(false)` ahora funciona correctamente — se reemplazó manipulación inline de `display` por clase CSS `.toolbar-hidden` con `!important`, robusta contra re-renders de EasyMDE
- Iconos de la toolbar ahora visibles en tema oscuro — EasyMDE v2.20 usa `background-image` en `span[data-img-src]::after`, no `color`; se aplica `filter: invert(1)` en dark mode
- Corregido bug lógico en `setEditable`: el guard `if (!isEditable) return` impedía ocultar la toolbar al pasar a modo no-editable

---

## [1.1.0] - 2026-04-24

### Cambios

#### Eliminado
- Soporte de imágenes (paste desde portapapeles y drag & drop) — removido por inestabilidad

#### Agregado
- `SetPreviewMode(bool)` — activa o desactiva el modo preview visual, independiente de `Editable`

#### Toolbar simplificada
- Eliminados: Create Link, Insert Image, Toggle Side by Side, Toggle Fullscreen
- Botones restantes: Bold, Italic, Heading | Quote, Unordered List, Ordered List | Preview | Guide
- Botón Guide redirige a `https://markdown.es/sintaxis-markdown/` (referencia en español)

---

## [1.0.0] - 2026-04-23

### Primera versión

**Control:** `SDMarkDownCtrl.SDMarkDownCtrlControl`  
**ProgID:** `SDMarkDownCtrl.SDMarkDownCtrlControl`  
**Motor:** WebView2 + EasyMDE

#### Propiedades
- `MarkdownText` (string, get/set) — contenido Markdown
- `Editable` (bool, get/set) — activa/desactiva la edición
- `Theme` (string, get/set) — tema visual: `"light"` o `"dark"`
- `HasChanges` (bool, read-only) — indica si hubo cambios desde el último `SetMarkdownText`
- `WordCount` (int, read-only) — cantidad de palabras
- `LineCount` (int, read-only) — cantidad de líneas
- `IsReady` (bool, read-only) — WebView2 inicializado
- `LastError` (string, read-only) — último error registrado

#### Métodos
- `GetMarkdownText()` — retorna el texto Markdown actual
- `SetMarkdownText(string)` — establece contenido y resetea HasChanges
- `ClearContent()` — limpia todo el contenido
- `ExportHTML()` — retorna el HTML renderizado
- `InsertText(string)` — inserta texto en la posición del cursor
- `SetToolbarVisible(bool)` — muestra/oculta la barra de herramientas
- `SetStatusBarVisible(bool)` — muestra/oculta la barra de estado
- `ScrollToTop()` / `ScrollToBottom()` — navegación
- `FocusEditor()` — pone el foco en el editor
- `About()` — muestra información de versión

#### Eventos
- `ControlReady` — WebView2 listo
- `EditorReady` — EasyMDE cargado y listo
- `MarkdownChanged(string)` — el usuario modificó el contenido
- `ErrorOccurred(string)` — error en el control

#### Características
- Soporte de imágenes: paste desde portapapeles → base64 embebido en Markdown
- Soporte de imágenes: drag & drop de archivos → base64 embebido
- Syntax highlighting en bloques de código (highlight.js)
- Múltiples instancias en la misma ventana (cada instancia usa su propio perfil WebView2)
- Funciona offline (sin dependencias externas en runtime)
