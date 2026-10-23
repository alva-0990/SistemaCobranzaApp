export async function guardarExcel(nombre, referencia) {
    const datos = await referencia.arrayBuffer();
    const url = URL.createObjectURL(new Blob([datos], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    }));
    const enlace = document.createElement('a');
    enlace.href = url;
    enlace.download = nombre;
    document.body.appendChild(enlace);
    enlace.click();
    enlace.remove();
    setTimeout(() => URL.revokeObjectURL(url), 10000);
}
