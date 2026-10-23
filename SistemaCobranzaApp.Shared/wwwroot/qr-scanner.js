// Escáner de QR basado en la API BarcodeDetector del WebView (sin dependencias externas).
// Expone funciones para iniciar/detener la cámara y notificar al componente Blazor
// cuando se detecta un código. El valor leído (folio/código de venta) se envía de vuelta
// mediante un DotNetObjectReference.

let _stream = null;      // MediaStream activo de la cámara.
let _video = null;       // Elemento <video> donde se muestra la cámara.
let _detector = null;    // Instancia de BarcodeDetector.
let _loop = null;        // Handle del requestAnimationFrame / timeout del bucle de detección.
let _dotnet = null;      // Referencia al componente Blazor para los callbacks.
let _activo = false;

// Indica si el navegador/WebView soporta la detección nativa de códigos.
export function soportado() {
    return typeof window.BarcodeDetector !== 'undefined';
}

// Inicia la cámara trasera y comienza a buscar códigos QR.
// videoId: id del elemento <video>. dotnetRef: referencia al componente Blazor.
export async function iniciar(videoId, dotnetRef) {
    if (!soportado()) {
        return { ok: false, error: 'Este dispositivo no soporta la lectura de códigos en el navegador.' };
    }

    try {
        _dotnet = dotnetRef;
        _video = document.getElementById(videoId);
        if (!_video) {
            return { ok: false, error: 'No se encontró el visor de la cámara.' };
        }

        _detector = new window.BarcodeDetector({ formats: ['qr_code'] });

        // Preferimos la cámara trasera (environment) para escanear.
        _stream = await navigator.mediaDevices.getUserMedia({
            video: { facingMode: { ideal: 'environment' } },
            audio: false
        });

        _video.srcObject = _stream;
        _video.setAttribute('playsinline', 'true'); // Evita pantalla completa forzada en móvil.
        await _video.play();

        _activo = true;
        _detectarEnBucle();

        return { ok: true, error: null };
    } catch (e) {
        detener();
        return { ok: false, error: 'No se pudo acceder a la cámara: ' + (e && e.message ? e.message : e) };
    }
}

// Bucle de detección: analiza fotogramas y, al encontrar un QR, notifica a Blazor una sola vez.
async function _detectarEnBucle() {
    if (!_activo || !_detector || !_video) return;

    try {
        const codigos = await _detector.detect(_video);
        if (codigos && codigos.length > 0) {
            const valor = (codigos[0].rawValue || '').trim();
            if (valor && _dotnet) {
                // Detenemos antes de notificar para no disparar múltiples lecturas del mismo código.
                _activo = false;
                await _dotnet.invokeMethodAsync('OnQrDetectado', valor);
                return;
            }
        }
    } catch {
        // Un fotograma puede fallar al decodificar; se ignora y se reintenta.
    }

    // ~5 lecturas por segundo son suficientes y ahorran batería.
    _loop = setTimeout(() => _detectarEnBucle(), 200);
}

// Detiene la cámara y libera los recursos.
export function detener() {
    _activo = false;

    if (_loop) {
        clearTimeout(_loop);
        _loop = null;
    }

    if (_stream) {
        _stream.getTracks().forEach(t => t.stop());
        _stream = null;
    }

    if (_video) {
        _video.srcObject = null;
    }

    _detector = null;
    _dotnet = null;
}
