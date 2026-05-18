using System.Runtime.InteropServices;

namespace SDMarkDownCtrl
{
    [ComVisible(true)]
    [Guid("3188F2BB-71E6-4B66-92C0-73ED21781F40")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface ISDMarkDownCtrlControl
    {
        // --- Estado general ---

        [DispId(1)]
        bool IsReady { get; }

        [DispId(2)]
        string LastError { get; }

        // --- Contenido Markdown ---

        /// <summary>Obtiene o establece el texto Markdown del control.</summary>
        [DispId(3)]
        string MarkdownText { get; set; }

        /// <summary>Retorna el texto Markdown actual (incluye ediciones del usuario).</summary>
        [DispId(4)]
        string GetMarkdownText();

        /// <summary>Establece el contenido Markdown y resetea el flag HasChanges.</summary>
        [DispId(5)]
        void SetMarkdownText(string markdown);

        /// <summary>Limpia todo el contenido del editor.</summary>
        [DispId(6)]
        void ClearContent();

        /// <summary>Retorna el HTML renderizado del Markdown actual.</summary>
        [DispId(7)]
        string ExportHTML();

        // --- Modo edición ---

        /// <summary>Activa o desactiva el modo de edición. False = solo vista previa.</summary>
        [DispId(8)]
        bool Editable { get; set; }

        /// <summary>Inserta texto en la posición actual del cursor (solo en modo edición).</summary>
        [DispId(9)]
        void InsertText(string text);

        // --- Estado del contenido ---

        /// <summary>True si el usuario modificó el contenido desde el último SetMarkdownText.</summary>
        [DispId(10)]
        bool HasChanges { get; }

        /// <summary>Cantidad de palabras en el contenido actual.</summary>
        [DispId(11)]
        int WordCount { get; }

        /// <summary>Cantidad de líneas en el contenido actual.</summary>
        [DispId(12)]
        int LineCount { get; }

        // --- Apariencia ---

        /// <summary>Tema visual: "light" o "dark".</summary>
        [DispId(13)]
        string Theme { get; set; }

        /// <summary>Tamaño de fuente del editor en píxeles (default: 14).</summary>
        [DispId(20)]
        int FontSize { get; set; }

        /// <summary>Muestra u oculta la barra de herramientas del editor.</summary>
        [DispId(14)]
        void SetToolbarVisible(bool visible);

        /// <summary>Muestra u oculta la barra de estado inferior.</summary>
        [DispId(15)]
        void SetStatusBarVisible(bool visible);

        // --- Navegación ---

        [DispId(16)]
        void ScrollToTop();

        [DispId(17)]
        void ScrollToBottom();

        // --- Foco ---

        /// <summary>Pone el foco en el editor.</summary>
        [DispId(18)]
        void FocusEditor();

        // --- Modo vista ---

        /// <summary>Activa o desactiva el modo preview (vista renderizada). No afecta Editable.</summary>
        [DispId(21)]
        void SetPreviewMode(bool preview);

        // --- Standard ---

        [DispId(19)]
        void About();
    }
}
