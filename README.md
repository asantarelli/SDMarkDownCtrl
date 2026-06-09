# SDMarkDownCtrl — Markdown Editor COM Control for Clarion

Control COM de edición y visualización de Markdown para aplicaciones Clarion, basado en **WebView2** y **EasyMDE**.

---

## Características

- Editor Markdown completo con toolbar (Negrita, Itálica, Encabezado, Cita, Listas, Preview)
- Vista previa renderizada con syntax highlighting (highlight.js)
- Temas **claro** y **oscuro**
- Modo edición o solo lectura
- Estadísticas de contenido: contador de palabras y líneas
- Detección de cambios (`HasChanges`)
- Exportación a HTML
- Múltiples instancias en la misma ventana (cada una con su propio perfil WebView2)
- Funciona **offline** — sin dependencias externas en runtime
- COM sin registro (RegFree COM via manifest)

---

## Requisitos

| Componente | Versión mínima |
|---|---|
| Windows | 10 / 11 |
| .NET Framework | 4.7.2 |
| Microsoft Edge WebView2 Runtime | 1.0.2535.41+ |
| Clarion | 11+ (ABC) |

> El WebView2 Runtime está incluido en Windows 11 y en versiones recientes de Edge. Si no está presente, se puede descargar desde [microsoft.com/edge/webview2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/).

---

## Instalación

1. Copiar `SDMarkDownCtrl.dll` y `SDMarkDownCtrl.manifest` a la carpeta de recursos de la aplicación Clarion.
2. Copiar la carpeta `wwwroot/` junto al DLL.
3. Referenciar el manifest desde el manifest de la aplicación host (RegFree COM — **no requiere `regsvr32`**).

Para integración con el sistema ClarionCOM, los archivos se despliegan automáticamente via el pipeline de build a `../Clarion/accessory/`.

---

## API

### Propiedades

| Propiedad | Tipo | Acceso | Descripción |
|---|---|---|---|
| `MarkdownText` | `string` | get/set | Contenido Markdown |
| `Editable` | `bool` | get/set | Activa/desactiva la edición |
| `Theme` | `string` | get/set | Tema visual: `"light"` o `"dark"` |
| `FontSize` | `int` | get/set | Tamaño de fuente en px (default: 14) |
| `HasChanges` | `bool` | get | `true` si hubo cambios desde el último `SetMarkdownText` |
| `WordCount` | `int` | get | Cantidad de palabras |
| `LineCount` | `int` | get | Cantidad de líneas |
| `IsReady` | `bool` | get | `true` cuando WebView2 está inicializado |
| `LastError` | `string` | get | Último error registrado |

### Métodos

| Método | Descripción |
|---|---|
| `GetMarkdownText()` | Retorna el texto Markdown actual (incluye ediciones del usuario) |
| `SetMarkdownText(string)` | Establece el contenido y resetea `HasChanges` |
| `ClearContent()` | Limpia todo el contenido del editor |
| `ExportHTML()` | Retorna el HTML renderizado del Markdown actual |
| `InsertText(string)` | Inserta texto en la posición del cursor (solo en modo edición) |
| `SetToolbarVisible(bool)` | Muestra u oculta la barra de herramientas |
| `SetStatusBarVisible(bool)` | Muestra u oculta la barra de estado inferior |
| `SetPreviewMode(bool)` | Activa/desactiva el modo preview (independiente de `Editable`) |
| `ScrollToTop()` | Desplaza el editor al inicio |
| `ScrollToBottom()` | Desplaza el editor al final |
| `FocusEditor()` | Pone el foco en el editor |
| `About()` | Muestra información de versión |

### Eventos

| Evento | Parámetros | Descripción |
|---|---|---|
| `ControlReady` | — | WebView2 inicializado y listo |
| `EditorReady` | — | EasyMDE cargado y contenido disponible |
| `MarkdownChanged` | `markdownText: string` | El usuario modificó el contenido |
| `ErrorOccurred` | `errorMessage: string` | Error en el control |

---

## Ejemplo de uso en Clarion

```clarion
  MAP
    MarkdownChanged(STRING pMarkdown), BOOL
  END

  ! Secuencia de inicialización
  MDCtrl.ControlReady &= ADDRESS(MDCtrl_Ready)
  MDCtrl.EditorReady  &= ADDRESS(MDCtrl_EditorReady)
  MDCtrl.MarkdownChanged &= ADDRESS(MarkdownChanged)

MDCtrl_Ready ROUTINE
  ! El control está listo — establecer configuración inicial
  MDCtrl.Theme    = 'dark'
  MDCtrl.Editable = TRUE
  MDCtrl.SetToolbarVisible(TRUE)

MDCtrl_EditorReady ROUTINE
  ! El editor está listo — cargar contenido
  MDCtrl.SetMarkdownText('# Hola Mundo' & <13,10> & 'Texto de ejemplo.')

MarkdownChanged ROUTINE
  ! Procesar el texto modificado
  IF MDCtrl.HasChanges
    GLO:TextoMarkdown = MDCtrl.GetMarkdownText()
  END
```

---

## Identificadores COM

| Componente | GUID |
|---|---|
| TypeLib | `2C457CBC-67DA-4109-AFE7-765935C21D9C` |
| Interface (`ISDMarkDownCtrlControl`) | `3188F2BB-71E6-4B66-92C0-73ED21781F40` |
| Events (`ISDMarkDownCtrlControlEvents`) | `5F4D3B8B-E27C-4B4B-86C4-C115E6B4E3C2` |
| Class (`SDMarkDownCtrlControl`) | `FE0B7ADF-7C26-4AC8-8A84-552DEE3E1B77` |
| ProgID | `SDMarkDownCtrl.SDMarkDownCtrlControl` |

---

## Dependencias incluidas

| Librería | Versión | Uso |
|---|---|---|
| Microsoft.Web.WebView2 | 1.0.2535.41 | Motor de rendering |
| Newtonsoft.Json | 13.0.3 | Serialización C# ↔ JS |
| EasyMDE | 2.20 | Editor Markdown |
| highlight.js | 11.9.0 | Syntax highlighting |

---

## Historial de versiones

Ver [CHANGELOG.md](CHANGELOG.md).

---

## Licencia

Copyright (c) 2026 SDigitales. Ver [LICENSE](LICENSE) para más detalles.
