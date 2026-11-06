// Visor de PDF embebido (pdf.js) que funciona igual en Web y en el WebView de MAUI
// (Android/iOS no muestran PDFs en línea). Dibuja cada página en un <canvas>.
const PDFJS = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.4.168/pdf.min.mjs';
const WORKER = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.4.168/pdf.worker.min.mjs';

export async function mostrar(idContenedor, referencia) {
    const contenedor = document.getElementById(idContenedor);
    if (!contenedor) return 0;
    const datos = new Uint8Array(await referencia.arrayBuffer());
    const pdfjs = await import(PDFJS);
    pdfjs.GlobalWorkerOptions.workerSrc = WORKER;
    const pdf = await pdfjs.getDocument({ data: datos }).promise;
    contenedor.innerHTML = '';
    const ancho = Math.max(contenedor.clientWidth - 8, 280);
    const dpr = window.devicePixelRatio || 1;
    for (let n = 1; n <= pdf.numPages; n++) {
        const pagina = await pdf.getPage(n);
        const base = pagina.getViewport({ scale: 1 });
        const escala = ancho / base.width;
        const vista = pagina.getViewport({ scale: escala * dpr });
        const canvas = document.createElement('canvas');
        canvas.width = vista.width;
        canvas.height = vista.height;
        canvas.style.width = ancho + 'px';
        canvas.style.height = (vista.height / dpr) + 'px';
        canvas.style.display = 'block';
        canvas.style.margin = '0 auto 8px auto';
        canvas.style.boxShadow = '0 1px 4px rgba(0,0,0,.3)';
        contenedor.appendChild(canvas);
        await pagina.render({ canvasContext: canvas.getContext('2d'), viewport: vista }).promise;
    }
    return pdf.numPages;
}

// Web: comparte el archivo con la hoja nativa (WhatsApp incluido) si el navegador lo permite.
// Devuelve false cuando no es posible para que el llamador use el plan B.
export async function compartirArchivo(nombre, referencia, tipoMime, texto) {
    const datos = await referencia.arrayBuffer();
    const archivo = new File([datos], nombre, { type: tipoMime || 'application/pdf' });
    if (navigator.canShare && navigator.canShare({ files: [archivo] })) {
        try {
            await navigator.share({ files: [archivo], title: nombre, text: texto || '' });
            return true;
        } catch (e) {
            if (e && e.name === 'AbortError') return true; // el usuario canceló
        }
    }
    return false;
}
