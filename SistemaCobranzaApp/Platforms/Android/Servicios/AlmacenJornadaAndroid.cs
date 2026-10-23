using Android.Content;
using Android.Database.Sqlite;
using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Servicios;

/// <summary>
/// Una instantánea y su cola se guardan juntas en una transacción SQLite.
/// No usa SecureStorage para carteras grandes y no requiere paquetes externos.
/// </summary>
public sealed class AlmacenJornadaAndroid : IAlmacenJornada
{
    private readonly BaseJornada _base = new(Android.App.Application.Context);
    public bool Disponible => true;
    public bool ConInternet => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    public event Action? ConexionRecuperada;

    public AlmacenJornadaAndroid()
    {
        Connectivity.Current.ConnectivityChanged += (_, e) =>
        {
            // Notificamos en CUALQUIER cambio de conectividad, no solo al recuperar internet.
            // Así la UI (p. ej. el aviso "sin conexión" del layout) repinta de inmediato
            // tanto al perder como al recuperar la señal. Las acciones que requieren red
            // (refresh de token, sincronización) ya están protegidas por guardas `ConInternet`
            // en ServicioJornadaLocal, por lo que una notificación sin internet es inocua.
            ConexionRecuperada?.Invoke();
        };
    }

    // Tamaño de fragmento de lectura. El CursorWindow de Android tiene un tope (~2 MB) por fila;
    // una jornada grande (muchos clientes/ventas/pagos) supera ese límite y GetString(0) lanza
    // "Row too big to fit into CursorWindow". Leemos el TEXT en trozos con SUBSTR para evitarlo.
    private const int TamanoFragmento = 512 * 1024; // 512 KB por lectura, muy por debajo del tope.

    public Task<string?> LeerAsync(long idUsuario) => Task.Run(() =>
    {
        var db = _base.ReadableDatabase!;

        // 1. Longitud total del contenido (un entero cabe siempre en el CursorWindow).
        long longitud;
        using (var cursorLongitud = db.RawQuery(
                   "SELECT length(contenido) FROM jornadas WHERE usuario = ?", [idUsuario.ToString()]))
        {
            if (!cursorLongitud!.MoveToFirst() || cursorLongitud.IsNull(0))
                return null;
            longitud = cursorLongitud.GetLong(0);
        }
        if (longitud == 0)
            return string.Empty;

        // 2. Leer el contenido por fragmentos con SUBSTR (índice SQL es 1-based) y concatenar.
        //    Así ninguna fila del cursor supera el límite del CursorWindow.
        var sb = new System.Text.StringBuilder((int)Math.Min(longitud, int.MaxValue));
        for (long inicio = 1; inicio <= longitud; inicio += TamanoFragmento)
        {
            using var cursor = db.RawQuery(
                "SELECT substr(contenido, ?, ?) FROM jornadas WHERE usuario = ?",
                [inicio.ToString(), TamanoFragmento.ToString(), idUsuario.ToString()]);
            if (!cursor!.MoveToFirst())
                return null;
            sb.Append(cursor.GetString(0));
        }
        return sb.ToString();
    });

    public Task GuardarAsync(long idUsuario, string json) => Task.Run(() =>
    {
        var db = _base.WritableDatabase!;
        db.BeginTransaction();
        try
        {
            using var valores = new ContentValues();
            valores.Put("usuario", idUsuario);
            valores.Put("contenido", json);
            db.Delete("jornadas", "usuario = ?", [idUsuario.ToString()]);
            if (db.Insert("jornadas", null, valores) < 0)
                throw new IOException("No fue posible guardar la jornada en el dispositivo.");
            db.SetTransactionSuccessful();
        }
        finally { db.EndTransaction(); }
    });

    private sealed class BaseJornada(Context context) : SQLiteOpenHelper(context, "jornadas.db", null, 1)
    {
        public override void OnCreate(SQLiteDatabase? db)
            => db!.ExecSQL("CREATE TABLE jornadas (usuario INTEGER PRIMARY KEY, contenido TEXT NOT NULL)");
        public override void OnUpgrade(SQLiteDatabase? db, int oldVersion, int newVersion) { }
    }
}
