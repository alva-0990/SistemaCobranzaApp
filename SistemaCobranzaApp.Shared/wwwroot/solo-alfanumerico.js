// Filtro reutilizable para inputs: impide ESCRIBIR caracteres fuera del conjunto
// permitido por un "modo", y avisa cuando bloquea uno. Se engancha DIRECTAMENTE
// al <input> dentro del contenedor cuyo id se pasa. Funciona en web y en el
// WebView de Android/iOS.
//
// Modos (patrón = caracteres PERMITIDOS):
//   'texto'               letras y espacios
//   'textoguionbajo'      letras y _
//   'numeros'             solo dígitos
//   'alfanumerico'        letras, dígitos y espacios
//   'alfanumericoguion'   letras, dígitos, espacios y -
//   'alfanumericoguiones' letras, dígitos, espacios y - _ [ ]
//   'direccion'           letras, dígitos, espacios y , . - _ # ( )
//   'correo'              letras, dígitos y @ . - _
//   'codigo'              letras, dígitos y -
//   'frecuenciatexto'     letras, dígitos, espacios y % . : ; , - _
//   'observaciones'       letras, dígitos, espacios y , . - ; : _ [ ] ( )

const PATRONES = {
    texto:               /[\p{L}\s]/u,
    textoguionbajo:      /[\p{L}_]/u,
    numeros:             /[\d]/u,
    alfanumerico:        /[\p{L}\d\s]/u,
    alfanumericoguion:   /[\p{L}\d\s-]/u,
    alfanumericoguiones: /[\p{L}\d\s_\[\]-]/u,
    direccion:           /[\p{L}\d\s,._#()-]/u,
    correo:              /[\p{L}\d@._-]/u,
    codigo:              /[\p{L}\d-]/u,
    frecuenciatexto:     /[\p{L}\d\s%.:;,_-]/u,
    observaciones:       /[\p{L}\d\s,.;:_()\[\]-]/u,
};

function patronDe(modo) {
    return PATRONES[modo] ?? PATRONES.alfanumerico;
}

function engancharInput(input, modo, ref) {
    if (!input) return false;
    // Si ya está enganchado con el mismo modo, no repetir.
    if (input.dataset.filtroCampoModo === modo) return true;
    // Si cambió el modo (reutilización del input), se re-marca; los listeners
    // leen el modo actual del dataset, así que no se duplican.
    const yaInstalado = input.dataset.filtroCampoModo != null;
    input.dataset.filtroCampoModo = modo;
    if (yaInstalado) return true;

    const leerPatron = () => patronDe(input.dataset.filtroCampoModo);
    const permitido = (ch) => {
        const p = leerPatron();
        return ch != null && ch.length === 1 && p.test(ch);
    };

    let ultimoAviso = 0;
    const avisar = () => {
        const ahora = Date.now();
        if (ref && ahora - ultimoAviso > 600) {
            ultimoAviso = ahora;
            ref.invokeMethodAsync('NotificarBloqueo', input.dataset.filtroCampoModo);
        }
    };

    input.addEventListener('keydown', (e) => {
        if (e.ctrlKey || e.metaKey || e.altKey) return;
        const k = e.key;
        if (k.length === 1 && !permitido(k)) {
            e.preventDefault();
            e.stopPropagation();
            avisar();
        }
    }, true);

    input.addEventListener('beforeinput', (e) => {
        const dato = e.data;
        if (dato == null) return;
        for (const ch of dato) {
            if (!permitido(ch)) {
                e.preventDefault();
                avisar();
                return;
            }
        }
    }, true);

    input.addEventListener('paste', (e) => {
        const texto = (e.clipboardData || window.clipboardData)?.getData('text') ?? '';
        const limpio = Array.from(texto).filter(permitido).join('');
        if (limpio !== texto) {
            e.preventDefault();
            avisar();
            const ini = input.selectionStart ?? input.value.length;
            const fin = input.selectionEnd ?? input.value.length;
            input.value = input.value.slice(0, ini) + limpio + input.value.slice(fin);
            const pos = ini + limpio.length;
            input.setSelectionRange(pos, pos);
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
        }
    }, true);

    // input: última barrera (Android/IME).
    let saneando = false;
    input.addEventListener('input', () => {
        if (saneando) return;
        const valor = input.value;
        const limpio = Array.from(valor).filter(permitido).join('');
        if (limpio !== valor) {
            saneando = true;
            const quitados = valor.length - limpio.length;
            let pos = (input.selectionStart ?? limpio.length) - quitados;
            if (pos < 0) pos = limpio.length;
            input.value = limpio;
            input.setSelectionRange(pos, pos);
            input.dispatchEvent(new Event('input', { bubbles: true }));
            input.dispatchEvent(new Event('change', { bubbles: true }));
            avisar();
            saneando = false;
        }
    }, true);

    return true;
}

// Engancha el campo del contenedor (input o textarea, según Lines del
// MudTextField). Reintenta unos segundos para cubrir el render asíncrono
// (MudTabs, diálogos). engancharInput es idempotente por modo.
export function engancharPorContenedor(idContenedor, modo, ref) {
    let intentos = 0;
    const intentar = () => {
        const cont = document.getElementById(idContenedor);
        const campo = cont ? cont.querySelector('input, textarea') : null;
        if (campo) {
            engancharInput(campo, modo, ref);
            return;
        }
        if (intentos++ < 60) setTimeout(intentar, 100);
    };
    intentar();
}
