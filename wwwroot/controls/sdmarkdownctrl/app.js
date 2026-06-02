// SDMarkDownCtrl — EasyMDE initialization and C# bridge
(function () {
    'use strict';

    var editor = null;
    var isEditable = true;
    var toolbarVisible = true;
    var statusBarVisible = true;
    var currentTheme = 'light';
    var currentFontSize = 14;
    var customBgColor = '';
    var changeDebounceTimer = null;

    // -------------------------------------------------------------------------
    // Inicialización
    // -------------------------------------------------------------------------

    function initEditor() {
        var options = {
            element: document.getElementById('md-editor'),
            autofocus: false,
            spellChecker: false,
            autosave: { enabled: false },
            status: ['lines', 'words', 'cursor'],
            renderingConfig: {
                codeSyntaxHighlighting: true
            },
            toolbar: [
                'bold', 'italic', 'heading', '|',
                'quote', 'unordered-list', 'ordered-list', '|',
                'preview', '|',
                {
                    name: 'guide',
                    action: function () { window.open('https://markdown.es/sintaxis-markdown/', '_blank'); },
                    className: 'fa fa-question-circle',
                    title: 'Guía de Markdown'
                }
            ]
        };

        // Activar highlight.js si está disponible
        if (typeof hljs !== 'undefined') {
            options.renderingConfig.hljs = hljs;
        }

        editor = new EasyMDE(options);

        // Escuchar cambios con debounce para no saturar C# en cada tecla
        editor.codemirror.on('change', function () {
            clearTimeout(changeDebounceTimer);
            changeDebounceTimer = setTimeout(notifyTextChanged, 150);
        });

        // Iniciar polling de mensajes desde C#
        startPolling();

        // Avisar que el editor está listo
        postToCSharp({ type: 'editorReady' });
    }

    // -------------------------------------------------------------------------
    // Polling de mensajes C# → JS via host object
    // -------------------------------------------------------------------------

    function startPolling() {
        function poll() {
            chrome.webview.hostObjects.SDMarkDownCtrlHost.GetNextMessage().then(function (msg) {
                if (msg && msg.length > 0) {
                    try { handleCSharpMessage(JSON.parse(msg)); } catch (e) { }
                    setTimeout(poll, 0); // hay más mensajes, continuar sin espera
                } else {
                    setTimeout(poll, 100); // cola vacía, esperar 100ms
                }
            }).catch(function () {
                setTimeout(poll, 200);
            });
        }
        poll();
    }

    // -------------------------------------------------------------------------
    // Comunicación hacia C#
    // -------------------------------------------------------------------------

    function notifyTextChanged() {
        if (!editor) return;
        var markdown = editor.value();
        var words = countWords(markdown);
        var lines = markdown.split('\n').length;

        postToCSharp({
            type: 'textChanged',
            markdown: markdown,
            wordCount: words,
            lineCount: lines
        });
    }

    function notifyContentLoaded() {
        if (!editor) return;
        var markdown = editor.value();
        var words = countWords(markdown);
        var lines = markdown.split('\n').length;

        postToCSharp({
            type: 'contentLoaded',
            markdown: markdown,
            wordCount: words,
            lineCount: lines
        });
    }

    // Expuesto para que C# lo llame via ExecuteScriptAsync cuando necesite el HTML
    window.getRenderedHTML = function () {
        if (!editor) return '';
        var markdown = editor.value();
        return editor.options.previewRender
            ? editor.options.previewRender(markdown)
            : '';
    };

    function countWords(text) {
        var trimmed = text.trim().replace(/```[\s\S]*?```/g, '') // quitar bloques de código
                          .replace(/`[^`]*`/g, '')
                          .replace(/[#*_~\[\]()>!\-]/g, ' ');
        if (!trimmed) return 0;
        return trimmed.split(/\s+/).filter(function (w) { return w.length > 0; }).length;
    }

    // -------------------------------------------------------------------------
    // Recibir mensajes desde C#
    // -------------------------------------------------------------------------

    window.handleCSharpMessage = function (msg) {
        if (!editor) return;

        switch (msg.action) {
            case 'setContent':
                var wasPreview = editor.isPreviewActive();
                if (wasPreview) editor.togglePreview();   // salir temporalmente para poder setear el valor
                editor.value(msg.markdown || '');
                clearTimeout(changeDebounceTimer); // cancelar el change que dispara setValue
                if (wasPreview) editor.togglePreview();   // restaurar modo preview
                notifyContentLoaded();
                break;

            case 'setEditable':
                setEditable(msg.editable !== false);
                break;

            case 'setTheme':
                setTheme(msg.theme || 'light');
                break;

            case 'insertText':
                if (msg.text && isEditable) {
                    editor.codemirror.replaceSelection(msg.text);
                }
                break;

            case 'setFontSize':
                setFontSize(msg.fontSize || 14);
                break;

            case 'setToolbarVisible':
                setToolbarVisible(msg.visible !== false);
                break;

            case 'setStatusBarVisible':
                setStatusBarVisible(msg.visible !== false);
                break;

            case 'scrollToTop':
                editor.codemirror.scrollTo(0, 0);
                break;

            case 'scrollToBottom':
                var lastLine = editor.codemirror.lineCount() - 1;
                editor.codemirror.scrollIntoView({ line: lastLine, ch: 0 });
                break;

            case 'setPreviewMode':
                setPreviewMode(msg.preview !== false);
                break;

            case 'focusEditor':
                if (isEditable) editor.codemirror.focus();
                break;

            case 'setBackgroundColor':
                customBgColor = msg.color || '';
                applyBackgroundColor();
                break;
        }
    };

    // -------------------------------------------------------------------------
    // Modo preview (independiente del modo editable)
    // -------------------------------------------------------------------------

    function setPreviewMode(preview) {
        if (!editor) return;
        var inPreview = editor.isPreviewActive();
        if (preview && !inPreview) editor.togglePreview();
        else if (!preview && inPreview) editor.togglePreview();
    }

    // -------------------------------------------------------------------------
    // Control de modo editable / preview
    // -------------------------------------------------------------------------

    function setEditable(editable) {
        isEditable = editable;

        var inPreview = editor.isPreviewActive();

        if (!editable) {
            // Forzar modo preview (solo lectura visual)
            if (!inPreview) editor.togglePreview();
            editor.codemirror.setOption('readOnly', 'nocursor');
        } else {
            // Volver a modo edición
            if (inPreview) editor.togglePreview();
            editor.codemirror.setOption('readOnly', false);
        }

        // Reaplicar visibilidad de toolbar según preferencia y nuevo estado editable
        applyToolbarVisibility();
    }

    // -------------------------------------------------------------------------
    // Tamaño de fuente
    // -------------------------------------------------------------------------

    function setFontSize(size) {
        currentFontSize = size;
        document.documentElement.style.setProperty('--editor-font-size', size + 'px');
        if (editor) editor.codemirror.refresh();
    }

    // -------------------------------------------------------------------------
    // Toolbar / Statusbar
    // -------------------------------------------------------------------------

    function setToolbarVisible(visible) {
        toolbarVisible = visible;
        applyToolbarVisibility();
    }

    function applyToolbarVisibility() {
        // En modo no-editable la toolbar siempre se oculta, independiente de la preferencia
        var show = toolbarVisible && isEditable;
        document.body.classList.toggle('toolbar-hidden', !show);
    }

    function setStatusBarVisible(visible) {
        statusBarVisible = visible;
        var sb = document.querySelector('.editor-statusbar');
        if (sb) sb.style.display = visible ? '' : 'none';
    }

    // -------------------------------------------------------------------------
    // Color de fondo personalizado
    // -------------------------------------------------------------------------

    function hexToRgb(hex) {
        var clean = hex.replace(/^#/, '');
        if (clean.length === 3) {
            clean = clean[0]+clean[0]+clean[1]+clean[1]+clean[2]+clean[2];
        }
        if (clean.length !== 6) return null;
        return {
            r: parseInt(clean.slice(0,2), 16),
            g: parseInt(clean.slice(2,4), 16),
            b: parseInt(clean.slice(4,6), 16)
        };
    }

    function darkenHex(hex, factor) {
        // factor: 0=negro, 1=color original. Usa 0.2 para dark mode.
        var rgb = hexToRgb(hex);
        if (!rgb) return hex;
        var r = Math.round(rgb.r * factor);
        var g = Math.round(rgb.g * factor);
        var b = Math.round(rgb.b * factor);
        return '#' + [r,g,b].map(function(v){ return v.toString(16).padStart(2,'0'); }).join('');
    }

    function applyBackgroundColor() {
        var color = customBgColor
            ? (currentTheme === 'dark' ? darkenHex(customBgColor, 0.2) : customBgColor)
            : '';

        // body: visible en márgenes y status bar
        document.body.style.backgroundColor = color;

        // editor-preview: el área renderizada que se ve en modo Editable=false / preview
        var preview = document.querySelector('.editor-preview');
        if (preview) preview.style.backgroundColor = color;

        // CodeMirror outer wrapper: solo el div contenedor, sin tocar internos
        var cm = editor ? editor.codemirror.getWrapperElement() : null;
        if (cm) cm.style.backgroundColor = color;
    }

    // -------------------------------------------------------------------------
    // Temas
    // -------------------------------------------------------------------------

    function setTheme(theme) {
        currentTheme = theme;
        document.body.classList.remove('theme-light', 'theme-dark');
        document.body.classList.add('theme-' + theme);

        // EasyMDE usa CodeMirror; cambiar tema de CM
        if (theme === 'dark') {
            editor.codemirror.setOption('theme', 'base16-dark');
        } else {
            editor.codemirror.setOption('theme', 'default');
        }

        applyBackgroundColor();
    }

    // -------------------------------------------------------------------------
    // Bootstrap
    // -------------------------------------------------------------------------

    // Esperar a que el DOM esté listo
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initEditor);
    } else {
        initEditor();
    }

})();
