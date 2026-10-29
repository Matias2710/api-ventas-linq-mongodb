using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Ventas.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<MongoStore>();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
var app = builder.Build();
if (args.Contains("--seed") || args.Contains("--costos-demo"))
{
    try
    {
        var store = app.Services.GetRequiredService<MongoStore>();
        if (args.Contains("--costos-demo"))
        {
            Console.WriteLine($"Costos demo: {await store.CompletarCostosDemo(CancellationToken.None)} líneas actualizadas. Origen: {store.Origen}.");
            return;
        }
        var nuevos = await store.Sembrar(CancellationToken.None);
        Console.WriteLine($"Carga verificada. Origen: {store.Origen}. Nuevos: {nuevos}. Total: {await store.Ventas.CountDocumentsAsync(FilterDefinition<Venta>.Empty)}.");
    }
    catch { Console.Error.WriteLine("Carga fallida: compruebe configuración, credenciales y red. No se registran secretos."); Environment.ExitCode = 1; }
    return;
}
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (Exception ex) when (ex is ArgumentException or BadHttpRequestException)
    { await Results.Problem(statusCode: 400, title: "Solicitud inválida", detail: ex is ArgumentException ? ex.Message : "Revise los parámetros de consulta.").ExecuteAsync(context); }
    catch (Exception ex) when (ex is MongoException or TimeoutException or BaseNoDisponibleException)
    { await Results.Problem(statusCode: 503, title: "Base de datos no disponible", detail: "Verifique la configuración y el acceso a MongoDB.").ExecuteAsync(context); }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (Exception ex)
    {
        app.Logger.LogError("Operación fallida ({Tipo}); consulte las pruebas y la configuración.", ex.GetType().Name);
        await Results.Problem(statusCode: 500, title: "Error interno").ExecuteAsync(context);
    }
});
app.MapGet("/health/live", () => Results.Ok(new { estado = "Activo" })).WithSummary("Proceso activo; no acredita conexión a MongoDB");
app.MapGet("/health/ready", async (MongoStore s, CancellationToken ct) => { await s.Ping(ct); return Results.Ok(new { estado = "Disponible", origen = s.Origen }); });
if (app.Environment.IsDevelopment()) app.MapOpenApi();
var ventas = app.MapGroup("/api/ventas").WithTags("Ventas");
ventas.MapGet("/", async ([AsParameters] Filtro f, MongoStore s, CancellationToken ct) =>
{
    f.Validar(false);
    var q = Consultas.Filtrar(s.Ventas.AsQueryable(), f, false);
    var total = await q.CountAsync(ct);
    var items = await q.OrderBy(v => v.Fecha).ThenBy(v => v.Id).Skip((f.Pagina - 1) * f.Tamano).Take(f.Tamano).ToListAsync(ct);
    return Results.Ok(Env(new PaginaDto(items, f.Pagina, f.Tamano, total), f, s, "Ventas según filtros; incluye estados no completados"));
}).Produces<Respuesta<PaginaDto>>().ProducesProblem(400).ProducesProblem(503);
ventas.MapGet("/{id}", async (string id, MongoStore s, CancellationToken ct) =>
{
    if (id.Length > 80) return Results.BadRequest(new ProblemDetails { Status = 400, Title = "Identificador inválido" });
    var v = await s.Ventas.AsQueryable().Where(v => v.Id == id).FirstOrDefaultAsync(ct);
    return v is null ? Results.NotFound(new ProblemDetails { Status = 404, Title = "Venta no encontrada" }) :
        Results.Ok(Env(v, new Filtro { Moneda = v.Moneda }, s, "Transacción individual"));
}).Produces<Respuesta<Venta>>().ProducesProblem(404);
var reportes = app.MapGroup("/api/reportes").WithTags("Reportes LINQ");
reportes.MapGet("/resumen", async ([AsParameters] Filtro f, MongoStore s, CancellationToken ct) =>
    Results.Ok(Env(await Consultas.Resumen(Q(f, s)).FirstOrDefaultAsync(ct) ?? new ResumenDto(0, 0, 0, 0, 0), f, s)))
    .Produces<Respuesta<ResumenDto>>().WithSummary("Facturación con impuesto; ingreso neto sin impuesto; ticket con impuesto");
reportes.MapGet("/estados", async ([AsParameters] Filtro f, MongoStore s, CancellationToken ct) =>
{
    f.Validar(false);
    return Results.Ok(Env(await Consultas.Estados(Consultas.Filtrar(s.Ventas.AsQueryable(), f, false)).ToListAsync(ct), f, s, "Todos los estados, salvo filtro Estado"));
}).Produces<Respuesta<List<EstadoDto>>>();
reportes.MapGet("/productos", async ([AsParameters] Filtro f, string? ordenarPor, MongoStore s, CancellationToken ct) =>
{
    if (ordenarPor is not (null or "unidades" or "ingresos")) throw new ArgumentException("ordenarPor: unidades o ingresos.");
    var q = Consultas.Productos(Q(f, s), f.Categoria);
    var orden = ordenarPor == "ingresos" ? q.OrderByDescending(p => p.IngresosNetos).ThenBy(p => p.ProductoId) : q.OrderByDescending(p => p.Unidades).ThenBy(p => p.ProductoId);
    return Results.Ok(Env(await orden.ToListAsync(ct), f, s, "Líneas completadas; categoría restringe líneas; ingresos sin impuesto"));
}).Produces<Respuesta<List<ProductoDto>>>();
reportes.MapGet("/categorias", async ([AsParameters] Filtro f, MongoStore s, CancellationToken ct) =>
    Results.Ok(Env(await Consultas.Categorias(Q(f, s), f.Categoria).ToListAsync(ct), f, s, "Líneas completadas; categoría restringe líneas; ingresos sin impuesto")))
    .Produces<Respuesta<List<CategoriaDto>>>();
reportes.MapGet("/clientes", async ([AsParameters] Filtro f, MongoStore s, CancellationToken ct) => Results.Ok(Env(await Consultas.Clientes(Q(f, s)).ToListAsync(ct), f, s))).Produces<Respuesta<List<GrupoDto>>>();
reportes.MapGet("/ciudades", async ([AsParameters] Filtro f, MongoStore s, CancellationToken ct) => Results.Ok(Env(await Consultas.Ciudades(Q(f, s)).ToListAsync(ct), f, s))).Produces<Respuesta<List<GrupoDto>>>();
reportes.MapGet("/vendedores", async ([AsParameters] Filtro f, MongoStore s, CancellationToken ct) => Results.Ok(Env(await Consultas.Vendedores(Q(f, s)).ToListAsync(ct), f, s))).Produces<Respuesta<List<GrupoDto>>>();
reportes.MapGet("/canales", async ([AsParameters] Filtro f, MongoStore s, CancellationToken ct) => Results.Ok(Env(await Consultas.Canales(Q(f, s)).ToListAsync(ct), f, s))).Produces<Respuesta<List<GrupoDto>>>();
reportes.MapGet("/tendencias", async ([AsParameters] Filtro f, MongoStore s, CancellationToken ct) => Results.Ok(Env(await Consultas.Tendencias(Q(f, s)).ToListAsync(ct), f, s))).Produces<Respuesta<List<TendenciaDto>>>();
reportes.MapGet("/extremos", async ([AsParameters] Filtro f, MongoStore s, CancellationToken ct) => Results.Ok(Env(await Consultas.Extremos(Q(f, s)).FirstOrDefaultAsync(ct), f, s))).Produces<Respuesta<ExtremosDto?>>();
reportes.MapGet("/ciudades-distintas", async ([AsParameters] Filtro f, MongoStore s, CancellationToken ct) => Results.Ok(Env(await Consultas.CiudadesDistintas(Q(f, s)).ToListAsync(ct), f, s))).Produces<Respuesta<List<string>>>();
reportes.MapGet("/rentabilidad", async ([AsParameters] Filtro f, MongoStore s, CancellationToken ct) =>
    Results.Ok(Env(await Consultas.Rentabilidad(Q(f, s), f.Categoria).ToListAsync(ct), f, s,
        "Margen bruto estimado con costos ficticios; solo líneas con costo informado; no utilidad neta")))
    .Produces<Respuesta<List<RentabilidadDto>>>();
app.Run();
static IQueryable<Venta> Q(Filtro f, MongoStore s) { f.Validar(); return Consultas.Filtrar(s.Ventas.AsQueryable(), f); }
static Respuesta<T> Env<T>(T datos, Filtro f, MongoStore s, string alcance = "Ventas completadas; categoría selecciona transacciones completas") =>
    new(datos, new MetaDto(f.Moneda, s.Origen, alcance));
public partial class Program;
