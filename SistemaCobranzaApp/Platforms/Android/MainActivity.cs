using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Webkit;
using AndroidX.Core.View;

namespace SistemaCobranzaApp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // A partir de Android 15 (API 35, SDK de .NET 10) el modo edge-to-edge es
    // obligatorio: la app siempre dibuja debajo de las barras de estado y de
    // navegación. En la web usamos env(safe-area-inset-*), pero el WebView de
    // algunos fabricantes (p. ej. Motorola) NO propaga esos insets y los reporta
    // como 0, por lo que el contenido invade las barras del sistema.
    //
    // Aquí leemos los WindowInsets NATIVOS (que siempre son correctos) y los
    // inyectamos en el WebView como variables CSS --sai-top/-bottom/-left/-right,
    // que app.css usa en lugar de env(). Así el comportamiento es idéntico en
    // todos los dispositivos.
    protected override void OnPostCreate(Bundle? savedInstanceState)
    {
        base.OnPostCreate(savedInstanceState);

        var root = Window?.DecorView?.RootView;
        if (root is null)
            return;

        ViewCompat.SetOnApplyWindowInsetsListener(root, new InsetsListener(this));
        // Forzamos un primer cálculo de insets por si el listener se registra
        // después del primer layout.
        ViewCompat.RequestApplyInsets(root);
    }

    private void AplicarInsetsAlWebView(int top, int bottom, int left, int right)
    {
        // Convertimos de píxeles físicos a píxeles CSS (density-independent).
        var density = Resources?.DisplayMetrics?.Density ?? 1f;
        if (density <= 0f)
            density = 1f;

        var topCss = top / density;
        var bottomCss = bottom / density;
        var leftCss = left / density;
        var rightCss = right / density;

        var webView = BuscarWebView(Window?.DecorView);
        if (webView is null)
            return;

        var js =
            "(function(){var r=document.documentElement.style;" +
            $"r.setProperty('--sai-top','{topCss.ToString(System.Globalization.CultureInfo.InvariantCulture)}px');" +
            $"r.setProperty('--sai-bottom','{bottomCss.ToString(System.Globalization.CultureInfo.InvariantCulture)}px');" +
            $"r.setProperty('--sai-left','{leftCss.ToString(System.Globalization.CultureInfo.InvariantCulture)}px');" +
            $"r.setProperty('--sai-right','{rightCss.ToString(System.Globalization.CultureInfo.InvariantCulture)}px');" +
            "})();";

        // El JS debe correr en el hilo de UI. Reintentamos unas cuantas veces
        // porque el WebView puede no haber cargado el documento todavía la
        // primera vez que recibimos los insets.
        ReintentarInyeccion(webView, js, 20);
    }

    private void ReintentarInyeccion(Android.Webkit.WebView webView, string js, int intentosRestantes)
    {
        webView.Post(() =>
        {
            // Comprobamos que el documento exista antes de fijar las variables;
            // si aún no está, reprogramamos.
            webView.EvaluateJavascript(
                "(function(){return (document && document.documentElement) ? '1' : '0';})();",
                new ValueCallback(resultado =>
                {
                    var listo = resultado == "\"1\"" || resultado == "1";
                    if (listo)
                    {
                        webView.EvaluateJavascript(js, null);
                    }
                    else if (intentosRestantes > 0)
                    {
                        webView.PostDelayed(
                            () => ReintentarInyeccion(webView, js, intentosRestantes - 1),
                            100);
                    }
                }));
        });
    }

    private static Android.Webkit.WebView? BuscarWebView(Android.Views.View? view)
    {
        switch (view)
        {
            case null:
                return null;
            case Android.Webkit.WebView web:
                return web;
            case ViewGroup grupo:
                for (var i = 0; i < grupo.ChildCount; i++)
                {
                    var encontrado = BuscarWebView(grupo.GetChildAt(i));
                    if (encontrado is not null)
                        return encontrado;
                }
                return null;
            default:
                return null;
        }
    }

    private sealed class InsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        private readonly MainActivity _activity;

        public InsetsListener(MainActivity activity) => _activity = activity;

        public WindowInsetsCompat OnApplyWindowInsets(Android.Views.View? v, WindowInsetsCompat? insets)
        {
            if (insets is null)
                return WindowInsetsCompat.Consumed;

            // Tipos que debemos esquivar: barras de sistema (estado + navegación)
            // y, si aplica, el notch/cutout.
            var mask = WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout();
            var barras = insets.GetInsets(mask);

            _activity.AplicarInsetsAlWebView(barras.Top, barras.Bottom, barras.Left, barras.Right);

            // Devolvemos los insets intactos para no romper a otros consumidores.
            return insets;
        }
    }

    private sealed class ValueCallback : Java.Lang.Object, IValueCallback
    {
        private readonly Action<string?> _accion;

        public ValueCallback(Action<string?> accion) => _accion = accion;

        public void OnReceiveValue(Java.Lang.Object? value) => _accion(value?.ToString());
    }
}