using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;

namespace SDMarkDownCtrl
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class SDMarkDownCtrlHostObject
    {
        private readonly SDMarkDownCtrlControl _control;

        public SDMarkDownCtrlHostObject(SDMarkDownCtrlControl control)
        {
            _control = control;
        }

        public void SendMessage(string message)
        {
            try { _control.HandleHostObjectMessage(message); }
            catch (Exception ex) { _control.SetError($"SendMessage error: {ex.Message}"); }
        }

        public string GetNextMessage()
        {
            try { return _control.DequeueMessage(); }
            catch { return string.Empty; }
        }
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("FE0B7ADF-7C26-4AC8-8A84-552DEE3E1B77")]
    [ComSourceInterfaces(typeof(ISDMarkDownCtrlControlEvents))]
    [ProgId("SDMarkDownCtrl.SDMarkDownCtrlControl")]
    public partial class SDMarkDownCtrlControl : UserControl, ISDMarkDownCtrlControl
    {
        #region Fields

        private WebView2 _webView;
        private SDMarkDownCtrlHostObject _hostObject;
        private bool _isReady;
        private bool _isInitialized;
        private bool _jsReady; // true solo después de recibir 'editorReady' (JS cargado y escuchando)
        private string _lastError;

        // Contenido
        private string _markdownText;
        private string _cachedHtml;
        private bool _hasChanges;
        private int _wordCount;
        private int _lineCount;

        // Apariencia / Comportamiento
        private bool _editable;
        private string _theme;
        private int _fontSize;
        private bool _toolbarVisible;
        private bool _statusBarVisible;
        private string _backgroundColor;

        // Carpeta de datos compartida por todas las instancias del mismo proceso
        private static readonly string _instanceDataFolder = Path.Combine(
            Path.GetTempPath(),
            "SDMarkDownCtrl",
            System.Diagnostics.Process.GetCurrentProcess().Id.ToString());

        // Cola de mensajes pendientes — JS hace polling via host object (solo configs pequeñas)
        private readonly ConcurrentQueue<string> _pendingMessages;

        // Contenido pendiente de enviar via PostWebMessageAsString cuando WebView esté listo
        private string _pendingSetContentJson;

        #endregion

        #region COM Event Delegates

        public delegate void ControlReadyDelegate();
        public delegate void ErrorOccurredDelegate(string errorMessage);
        public delegate void MarkdownChangedDelegate(string markdownText);
        public delegate void EditorReadyDelegate();

        #endregion

        #region COM Events

        public event ControlReadyDelegate ControlReady;
        public event ErrorOccurredDelegate ErrorOccurred;
        public event MarkdownChangedDelegate MarkdownChanged;
        public event EditorReadyDelegate EditorReady;

        #endregion

        #region Constructor

        public SDMarkDownCtrlControl()
        {
            _isReady = false;
            _jsReady = false;
            _isInitialized = false;
            _lastError = string.Empty;
            _markdownText = string.Empty;
            _cachedHtml = string.Empty;
            _hasChanges = false;
            _wordCount = 0;
            _lineCount = 0;
            _editable = true;
            _theme = "light";
            _fontSize = 14;
            _toolbarVisible = true;
            _statusBarVisible = true;
            _backgroundColor = string.Empty;
            _pendingMessages = new ConcurrentQueue<string>();

            // Limpiar carpetas de instancias anteriores (procesos ya finalizados)
            CleanupStaleTempFolders();

            Size = new Size(800, 500);
            DoubleBuffered = true;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode && !_isInitialized)
            {
                BackColor = Color.White;
                InitializeWebView2Async();
            }
        }

        #endregion

        #region WebView2 Initialization

        private async void InitializeWebView2Async()
        {
            try
            {
                if (_isInitialized) return;
                _isInitialized = true;

                _webView = new WebView2 { Dock = DockStyle.Fill };

                var env = await CoreWebView2Environment.CreateAsync(null, _instanceDataFolder, null);
                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.Settings.IsScriptEnabled = true;
                _webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                _webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;

                // Mapear wwwroot al virtual host
                var wwwrootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "localapp.clarioncontrols",
                    wwwrootPath,
                    CoreWebView2HostResourceAccessKind.Allow);

                _hostObject = new SDMarkDownCtrlHostObject(this);
                _webView.CoreWebView2.AddHostObjectToScript("SDMarkDownCtrlHost", _hostObject);

                _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                Controls.Add(_webView);

                _isReady = true;
                RaiseControlReady();

                _webView.CoreWebView2.Navigate(
                    "https://localapp.clarioncontrols/controls/sdmarkdownctrl/index.html");
            }
            catch (Exception ex)
            {
                _lastError = $"WebView2 initialization failed: {ex.Message}";
                RaiseErrorOccurred(_lastError);
            }
        }

        #endregion

        #region Message Handling

        private class WebMessage
        {
            public string type { get; set; }
            public string markdown { get; set; }
            public int wordCount { get; set; }
            public int lineCount { get; set; }
            public string error { get; set; }
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string raw = e.TryGetWebMessageAsString();
                HandleWebMessage(raw);
            }
            catch (Exception ex)
            {
                SetError($"WebMessage error: {ex.Message}");
            }
        }

        internal void HandleWebMessage(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message)) return;

                var msg = JsonConvert.DeserializeObject<WebMessage>(message);
                if (msg == null) return;

                switch (msg.type ?? string.Empty)
                {
                    case "editorReady":
                        _jsReady = true;
                        RaiseEditorReady();
                        // Enviar setContent pendiente (SetMarkdownText llamado antes de que JS cargara)
                        if (_pendingSetContentJson != null)
                        {
                            var pending = _pendingSetContentJson;
                            _pendingSetContentJson = null;
                            try { _webView.CoreWebView2.PostWebMessageAsString(pending); }
                            catch { }
                        }
                        break;

                    case "contentLoaded":
                        _markdownText = msg.markdown ?? string.Empty;
                        _cachedHtml = string.Empty;
                        _wordCount = msg.wordCount;
                        _lineCount = msg.lineCount;
                        _hasChanges = false;
                        break;

                    case "textChanged":
                        _markdownText = msg.markdown ?? string.Empty;
                        _cachedHtml = string.Empty;
                        _wordCount = msg.wordCount;
                        _lineCount = msg.lineCount;
                        _hasChanges = true;
                        RaiseMarkdownChanged(_markdownText);
                        break;

                    case "error":
                        SetError(msg.error ?? "Unknown JS error");
                        break;
                }
            }
            catch (Exception ex)
            {
                SetError($"Message parse error: {ex.Message}");
            }
        }

        internal void HandleHostObjectMessage(string message)
        {
            // Punto de extensión para comunicación via host object
        }

        private void PostToEditor(object payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            _pendingMessages.Enqueue(json);
        }

        // Para payloads grandes (setContent) usa PostWebMessageAsString en lugar del
        // polling via host object, que tiene límite práctico de ~2KB en COM interop.
        private void PostLargeMessageToEditor(object payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            // _jsReady: el JS ya cargó la página y registró el listener 'message'.
            // _isReady (CoreWebView2 listo) NO es suficiente: ControlReady se dispara antes
            // de que el navegador cargue la página, por lo que PostWebMessageAsString se perdería.
            if (_jsReady && _webView?.CoreWebView2 != null)
            {
                try
                {
                    if (_webView.InvokeRequired)
                        _webView.Invoke(new Action(() => _webView.CoreWebView2.PostWebMessageAsString(json)));
                    else
                        _webView.CoreWebView2.PostWebMessageAsString(json);
                    return;
                }
                catch { }
            }
            // JS aún no listo — guardar para enviar cuando llegue 'editorReady'
            _pendingSetContentJson = json;
        }

        internal string DequeueMessage()
        {
            if (_pendingMessages.TryDequeue(out string msg))
                return msg;
            return string.Empty;
        }

        #endregion

        #region ISDMarkDownCtrlControl — Properties

        [ComVisible(true)]
        public bool IsReady => _isReady;

        [ComVisible(true)]
        public string LastError => _lastError ?? string.Empty;

        [ComVisible(true)]
        public string MarkdownText
        {
            get => _markdownText ?? string.Empty;
            set => SetMarkdownText(value);
        }

        [ComVisible(true)]
        public bool Editable
        {
            get => _editable;
            set
            {
                _editable = value;
                PostToEditor(new { action = "setEditable", editable = _editable });
            }
        }

        [ComVisible(true)]
        public string Theme
        {
            get => _theme ?? "light";
            set
            {
                _theme = value ?? "light";
                PostToEditor(new { action = "setTheme", theme = _theme });
            }
        }

        [ComVisible(true)]
        public int FontSize
        {
            get => _fontSize;
            set
            {
                _fontSize = value < 8 ? 8 : value;
                PostToEditor(new { action = "setFontSize", fontSize = _fontSize });
            }
        }

        [ComVisible(true)]
        public string BackgroundColor
        {
            get => _backgroundColor ?? string.Empty;
            set
            {
                _backgroundColor = value ?? string.Empty;
                PostToEditor(new { action = "setBackgroundColor", color = _backgroundColor });
            }
        }

        [ComVisible(true)]
        public bool HasChanges => _hasChanges;

        [ComVisible(true)]
        public int WordCount => _wordCount;

        [ComVisible(true)]
        public int LineCount => _lineCount;

        #endregion

        #region ISDMarkDownCtrlControl — Methods

        [ComVisible(true)]
        public string GetMarkdownText()
        {
            // Si el JS está listo, obtener el valor en vivo del editor via ExecuteScriptAsync.
            // Esto evita depender de la caché (_markdownText) que puede estar desactualizada
            // si hubo problemas de timing en el canal C#→JS.
            if (_jsReady && _webView?.CoreWebView2 != null)
            {
                try
                {
                    var task = _webView.CoreWebView2.ExecuteScriptAsync("window.__editorGetValue ? window.__editorGetValue() : '\"\"'");
                    // Bombear mensajes para evitar deadlock (el STA thread necesita procesar
                    // los callbacks de WebView2 mientras esperamos el resultado).
                    var deadline = Environment.TickCount + 2000;
                    while (!task.IsCompleted && Environment.TickCount < deadline)
                    {
                        Application.DoEvents();
                        System.Threading.Thread.Sleep(1);
                    }
                    if (task.IsCompleted && !task.IsFaulted)
                    {
                        // ExecuteScriptAsync devuelve el valor JSON-encoded: "\"markdown\""
                        var live = JsonConvert.DeserializeObject<string>(task.Result);
                        if (live != null)
                        {
                            _markdownText = live;
                            return live;
                        }
                    }
                }
                catch { }
            }
            return _markdownText ?? string.Empty;
        }

        [ComVisible(true)]
        public void SetMarkdownText(string markdown)
        {
            try
            {
                _markdownText = markdown ?? string.Empty;
                _hasChanges = false;
                PostLargeMessageToEditor(new { action = "setContent", markdown = _markdownText });
            }
            catch (Exception ex)
            {
                SetError($"SetMarkdownText error: {ex.Message}");
            }
        }

        [ComVisible(true)]
        public void ClearContent()
        {
            try
            {
                _markdownText = string.Empty;
                _cachedHtml = string.Empty;
                _wordCount = 0;
                _lineCount = 0;
                _hasChanges = false;
                PostLargeMessageToEditor(new { action = "setContent", markdown = string.Empty });
            }
            catch (Exception ex)
            {
                SetError($"ClearContent error: {ex.Message}");
            }
        }

        [ComVisible(true)]
        public string ExportHTML()
        {
            if (!string.IsNullOrEmpty(_cachedHtml))
                return _cachedHtml;

            if (_webView?.CoreWebView2 == null || string.IsNullOrEmpty(_markdownText))
                return string.Empty;

            try
            {
                // Ejecutar sync en el hilo de UI: bloquear brevemente para obtener el HTML renderizado
                string result = string.Empty;
                bool done = false;

                var task = _webView.CoreWebView2.ExecuteScriptAsync("window.getRenderedHTML()");
                task.ContinueWith(t =>
                {
                    if (!t.IsFaulted && t.Result != null)
                    {
                        // El resultado viene como JSON string (con comillas y escapes)
                        var raw = t.Result;
                        if (raw.Length >= 2 && raw[0] == '"')
                            raw = JsonConvert.DeserializeObject<string>(raw);
                        result = raw ?? string.Empty;
                        _cachedHtml = result;
                    }
                    done = true;
                });

                // Esperar hasta 2 segundos procesando mensajes de UI para no bloquear el pump
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (!done && sw.ElapsedMilliseconds < 2000)
                    Application.DoEvents();

                return _cachedHtml ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        [ComVisible(true)]
        public void InsertText(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return;
                PostToEditor(new { action = "insertText", text });
            }
            catch (Exception ex)
            {
                SetError($"InsertText error: {ex.Message}");
            }
        }

        [ComVisible(true)]
        public void SetToolbarVisible(bool visible)
        {
            try
            {
                _toolbarVisible = visible;
                PostToEditor(new { action = "setToolbarVisible", visible });
            }
            catch (Exception ex)
            {
                SetError($"SetToolbarVisible error: {ex.Message}");
            }
        }

        [ComVisible(true)]
        public void SetStatusBarVisible(bool visible)
        {
            try
            {
                _statusBarVisible = visible;
                PostToEditor(new { action = "setStatusBarVisible", visible });
            }
            catch (Exception ex)
            {
                SetError($"SetStatusBarVisible error: {ex.Message}");
            }
        }

        [ComVisible(true)]
        public void ScrollToTop()
        {
            try { PostToEditor(new { action = "scrollToTop" }); }
            catch (Exception ex) { SetError($"ScrollToTop error: {ex.Message}"); }
        }

        [ComVisible(true)]
        public void ScrollToBottom()
        {
            try { PostToEditor(new { action = "scrollToBottom" }); }
            catch (Exception ex) { SetError($"ScrollToBottom error: {ex.Message}"); }
        }

        [ComVisible(true)]
        public void SetPreviewMode(bool preview)
        {
            try { PostToEditor(new { action = "setPreviewMode", preview }); }
            catch (Exception ex) { SetError($"SetPreviewMode error: {ex.Message}"); }
        }

        [ComVisible(true)]
        public void FocusEditor()
        {
            try { PostToEditor(new { action = "focusEditor" }); }
            catch (Exception ex) { SetError($"FocusEditor error: {ex.Message}"); }
        }

        [ComVisible(true)]
        public void About()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var ver = asm.GetName().Version;
                MessageBox.Show(
                    $"SDMarkDownCtrl\nVersion: {ver.Major}.{ver.Minor}.{ver.Build}\n" +
                    $"WebView2 Runtime: {CoreWebView2Environment.GetAvailableBrowserVersionString()}\n\n" +
                    "Markdown editor control for Clarion.\nUses EasyMDE + WebView2.",
                    "About SDMarkDownCtrl",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch { }
        }

        #endregion

        #region Helper Methods

        internal void SetError(string error)
        {
            _lastError = error ?? string.Empty;
            RaiseErrorOccurred(_lastError);
        }

        #endregion

        #region Event Raising

        private void RaiseControlReady()
        {
            if (ControlReady != null) try { ControlReady(); } catch { }
        }

        private void RaiseErrorOccurred(string msg)
        {
            if (ErrorOccurred != null) try { ErrorOccurred(msg ?? string.Empty); } catch { }
        }

        private void RaiseMarkdownChanged(string markdown)
        {
            if (MarkdownChanged != null) try { MarkdownChanged(markdown ?? string.Empty); } catch { }
        }

        private void RaiseEditorReady()
        {
            if (EditorReady != null) try { EditorReady(); } catch { }
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _webView?.Dispose();
                _webView = null;
                // La carpeta _instanceDataFolder es compartida por todas las instancias del proceso.
                // No se borra aquí — se limpia al próximo arranque vía CleanupStaleTempFolders.
            }
            base.Dispose(disposing);
        }

        private static void CleanupStaleTempFolders()
        {
            try
            {
                var baseDir = Path.Combine(Path.GetTempPath(), "SDMarkDownCtrl");
                if (!Directory.Exists(baseDir)) return;

                var currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
                foreach (var dir in Directory.GetDirectories(baseDir))
                {
                    var name = Path.GetFileName(dir);
                    // Borrar carpetas con nombre numérico (PID) de procesos ya terminados
                    if (int.TryParse(name, out int pid) && pid != currentPid)
                    {
                        try
                        {
                            System.Diagnostics.Process.GetProcessById(pid);
                            // El proceso sigue vivo — no tocar
                        }
                        catch (ArgumentException)
                        {
                            // Proceso ya no existe — borrar su carpeta
                            try { Directory.Delete(dir, true); } catch { }
                        }
                    }
                    else if (!int.TryParse(name, out _))
                    {
                        // Carpeta con GUID (formato viejo) — siempre borrar
                        try { Directory.Delete(dir, true); } catch { }
                    }
                }
            }
            catch { }
        }

        #endregion
    }
}
