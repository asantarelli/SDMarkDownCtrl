using System.Runtime.InteropServices;

namespace SDMarkDownCtrl
{
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    [Guid("5F4D3B8B-E27C-4B4B-86C4-C115E6B4E3C2")]
    public interface ISDMarkDownCtrlControlEvents
    {
        /// <summary>Fired cuando WebView2 está completamente inicializado.</summary>
        [DispId(1)]
        void ControlReady();

        /// <summary>Fired cuando ocurre un error.</summary>
        [DispId(2)]
        void ErrorOccurred(string errorMessage);

        /// <summary>Fired cuando el usuario modifica el texto en el editor.</summary>
        [DispId(3)]
        void MarkdownChanged(string markdownText);

        /// <summary>Fired cuando EasyMDE está listo y el contenido fue cargado.</summary>
        [DispId(4)]
        void EditorReady();
    }
}
