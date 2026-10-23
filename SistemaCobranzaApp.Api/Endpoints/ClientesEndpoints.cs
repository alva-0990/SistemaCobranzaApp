using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Clientes;
using SistemaCobranzaApp.Infrastructure.Cobranza;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

/// <summary>
/// Endpoints del módulo de clientes, con respuesta estandarizada (<see cref="Respuestas"/>):
///   - Operativos (/api/clientes): búsqueda por texto y consultas por ruta/zona
///     y estado de cuenta (ServicioBusquedaClientes / ServicioEstadoCuenta).
///   - Admin (/api/admin/clientes): CRUD de administración (ServicioClientesAdmin).
/// </summary>
public class ClientesEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // ===================== Operativos (consulta) =====================
        var operativas = app.MapGroup("/api/clientes").RequireAuthorization();

        // Búsqueda de clientes por texto: nombre, apellidos, teléfono, email, dirección,
        // código postal, ruta y zona (todo por texto; ruta/zona por su descripción).
        operativas.MapGet("/buscar", async (
            string? nombre, string? apPaterno, string? apMaterno, string? telefono,
            string? email, string? direccion, string? codigoPostal, string? ruta, string? zona,
            ServicioBusquedaClientes s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Buscar Clientes";
            log.Info(proceso, "Buscando clientes por texto.",
                datos: new { nombre, apPaterno, apMaterno, telefono, email, direccion, codigoPostal, ruta, zona });
            var filtro = new ClienteBusquedaFiltro(nombre, apPaterno, apMaterno, telefono, email, direccion, codigoPostal, ruta, zona);
            var lista = await s.BuscarAsync(filtro, ct);
            log.Info(proceso, $"Clientes encontrados: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Búsqueda de clientes realizada.");
        }).WithName("BuscarClientes");

        // Clientes de una ruta (por Id de ruta).
        operativas.MapGet("/por-ruta/{idRuta:long}", async (
            long idRuta, ServicioBusquedaClientes s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Clientes por Ruta";
            log.Info(proceso, $"Consultando clientes de la ruta #{idRuta}.", datos: new { idRuta });
            var r = await s.PorRutaAsync(idRuta, ct);
            if (r is null)
            {
                log.Warn(proceso, $"Ruta #{idRuta} no encontrada.");
                return Respuestas.NoEncontrado($"No se encontró la ruta #{idRuta}.");
            }
            log.Info(proceso, $"Clientes de la ruta #{idRuta}: {r.Clientes.Count}.", datos: r);
            return Respuestas.Ok(r, "Clientes de la ruta obtenidos.");
        }).WithName("ClientesPorRuta");

        // Clientes de una zona, agrupados por sus rutas (por Id de zona).
        operativas.MapGet("/por-zona/{idZona:long}", async (
            long idZona, ServicioBusquedaClientes s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Clientes por Zona";
            log.Info(proceso, $"Consultando clientes de la zona #{idZona}.", datos: new { idZona });
            var z = await s.PorZonaAsync(idZona, ct);
            if (z is null)
            {
                log.Warn(proceso, $"Zona #{idZona} no encontrada.");
                return Respuestas.NoEncontrado($"No se encontró la zona #{idZona}.");
            }
            log.Info(proceso, $"Rutas con clientes en la zona #{idZona}: {z.Rutas.Count}.", datos: z);
            return Respuestas.Ok(z, "Clientes de la zona obtenidos.");
        }).WithName("ClientesPorZona");

        // Estado de cuenta consolidado del cliente.
        operativas.MapGet("/{id:long}/estado-cuenta", async (
            long id, ServicioEstadoCuenta s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Estado de Cuenta Cliente";
            log.Info(proceso, $"Consultando estado de cuenta del cliente #{id}.", datos: new { id });
            var ec = await s.ObtenerAsync(id, ct);
            if (ec is null)
            {
                log.Warn(proceso, $"Cliente #{id} no encontrado.");
                return Respuestas.NoEncontrado($"No se encontró el cliente #{id}.");
            }
            log.Info(proceso, $"Estado de cuenta del cliente #{id} obtenido.", datos: ec);
            return Respuestas.Ok(ec, "Estado de cuenta obtenido.");
        }).WithName("EstadoCuentaCliente");

        // ===================== Admin (CRUD) =====================
        var admin = app.MapGroup("/api/admin/clientes").RequireAuthorization();

        // Listado paginado con filtros.
        admin.MapGet("/", async (
            string? texto, long? idRuta, bool? incluirInactivos, int? pagina, int? tamanoPagina,
            ServicioClientesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Clientes (Admin)";
            log.Info(proceso, "Consultando clientes paginados.",
                datos: new { texto, idRuta, incluirInactivos, pagina, tamanoPagina });
            var respuesta = await s.ListarAsync(texto, idRuta, incluirInactivos ?? false,
                pagina ?? 1, tamanoPagina ?? 50, ct);
            log.Info(proceso, $"Clientes obtenidos: {respuesta.Items.Count} de {respuesta.TotalItems}.");
            return Respuestas.Ok(respuesta, "Clientes obtenidos.");
        }).WithName("ListarClientes");

        // Obtener por id.
        admin.MapGet("/{id:long}", async (
            long id, ServicioClientesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Cliente";
            log.Info(proceso, $"Buscando cliente #{id}.", datos: new { id });
            var c = await s.ObtenerPorIdAsync(id, ct);
            if (c is null)
            {
                log.Warn(proceso, $"Cliente #{id} no encontrado.");
                return Respuestas.NoEncontrado($"No se encontró el cliente #{id}.");
            }
            log.Info(proceso, $"Cliente #{id} obtenido.", datos: c);
            return Respuestas.Ok(c, "Cliente obtenido.");
        }).WithName("ObtenerCliente");

        // Crear.
        admin.MapPost("/", async (
            CrearClienteRequest req, ServicioClientesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Cliente";
            log.Info(proceso, $"Creando cliente: '{req.Nombre}'.", datos: req);
            var r = await s.CrearAsync(req, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Cliente creado #{r.Cliente!.IdCliente} ('{r.Cliente.NombreCompleto}').", datos: r.Cliente);
                return Respuestas.Creado($"/api/admin/clientes/{r.Cliente.IdCliente}", r.Cliente, "Cliente creado correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear el cliente: {r.Error}", datos: req);
            return Respuestas.SolicitudInvalida("No se pudo crear el cliente.", r.Error ?? "");
        }).WithName("CrearCliente");

        // Actualizar.
        admin.MapPut("/{id:long}", async (
            long id, ActualizarClienteRequest req, ServicioClientesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Cliente";
            log.Info(proceso, $"Actualizando cliente #{id}.", datos: new { id, req });
            var r = await s.ActualizarAsync(id, req, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Cliente #{id} actualizado.", datos: r.Cliente);
                return Respuestas.Ok(r.Cliente!, "Cliente actualizado correctamente.");
            }
            if (r.Error is not null && r.Error.Contains("No se encontró"))
            {
                log.Warn(proceso, $"Cliente #{id} no encontrado.");
                return Respuestas.NoEncontrado(r.Error);
            }
            log.Warn(proceso, $"No se pudo actualizar el cliente #{id}: {r.Error}", datos: new { id });
            return Respuestas.SolicitudInvalida("No se pudo actualizar el cliente.", r.Error ?? "");
        }).WithName("ActualizarCliente");

        // Actualización ligera de la clasificación (usada por el reporte de seguimiento).
        admin.MapPut("/{id:long}/clasificacion", async (
            long id, ActualizarClasificacionClienteRequest req, ServicioClientesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualizar Clasificación Cliente";
            log.Info(proceso, $"Actualizando clasificación del cliente #{id} a #{req.IdClasificacion}.", datos: new { id, req });
            var r = await s.ActualizarClasificacionAsync(id, req.IdClasificacion, req.Observaciones, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Clasificación del cliente #{id} actualizada.", datos: r.Cliente);
                return Respuestas.Ok(r.Cliente!, "Clasificación actualizada correctamente.");
            }
            if (r.Error is not null && r.Error.Contains("No se encontró"))
            {
                log.Warn(proceso, $"Cliente #{id} no encontrado.");
                return Respuestas.NoEncontrado(r.Error);
            }
            log.Warn(proceso, $"No se pudo actualizar la clasificación del cliente #{id}: {r.Error}", datos: new { id });
            return Respuestas.SolicitudInvalida("No se pudo actualizar la clasificación.", r.Error ?? "");
        }).WithName("ActualizarClasificacionCliente");

        // Baja lógica.
        admin.MapDelete("/{id:long}", async (
            long id, ServicioClientesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Cliente";
            log.Info(proceso, $"Dando de baja cliente #{id}.", datos: new { id });
            var r = await s.DarDeBajaAsync(id, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Cliente #{id} dado de baja.", datos: r.Cliente);
                return Respuestas.Ok(r.Cliente!, "Cliente dado de baja correctamente.");
            }
            log.Warn(proceso, $"Cliente #{id} no encontrado.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el cliente #{id}.");
        }).WithName("DarDeBajaCliente");
    }
}
