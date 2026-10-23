namespace SistemaCobranzaApp;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

#if ANDROID
        // Permite que el contenido del WebView use la cámara (getUserMedia) para el escaneo de QR.
        // Android deniega por defecto las solicitudes de permiso del WebView; asignamos un
        // WebChromeClient que las concede (el permiso de sistema CAMERA se pide aparte en runtime).
        blazorWebView.BlazorWebViewInitialized += (sender, e) =>
        {
            e.WebView.SetWebChromeClient(new SistemaCobranzaApp.Servicios.PermisivoWebChromeClient());
        };
#endif
    }
}