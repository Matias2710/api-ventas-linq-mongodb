using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Ventas.Api;

namespace Ventas.Tests;

public sealed class MongoFactAttribute : FactAttribute
{
    public MongoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VENTAS_TEST_URI")))
            Skip = "Requiere MongoDB real en loopback y VENTAS_TEST_URI. No es una prueba aprobada.";
    }
}
public class IntegracionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    [MongoFact]
    public async Task Mongo_real_carga_idempotente_endpoints_filtros_y_traduccion()
    {
        var uri = Environment.GetEnvironmentVariable("VENTAS_TEST_URI")!;
        var url = new MongoUrl(uri);
        Assert.All(url.Servers, s => Assert.Contains(s.Host, new[] { "127.0.0.1", "localhost", "::1" }));
        var db = "VentasTests_" + Guid.NewGuid().ToString("N");
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development"); b.ConfigureLogging(l => l.ClearProviders());
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
                ["MongoDB:ConnectionString"] = uri, ["MongoDB:Database"] = db,
                ["MongoDB:LocalTest"] = "true", ["MongoDB:CaptureDiagnostics"] = "true" }));
        });
        var store = factory.Services.GetRequiredService<MongoStore>();
        var client = factory.CreateClient();
        var evidence = new List<object>();
        try
        {
            Assert.Equal(18, await store.Sembrar(default)); Assert.Equal(0, await store.Sembrar(default));
            Assert.Equal(18, await store.Ventas.CountDocumentsAsync(FilterDefinition<Venta>.Empty));
            await store.Ventas.UpdateOneAsync(v => v.Id == "DEMO-V001", Builders<Venta>.Update
                .Unset("Productos.0.CostoUnitario").Unset("Productos.0.CostoTotal"));
            Assert.Equal(1, await store.CompletarCostosDemo(default));
            Assert.Equal(0, await store.CompletarCostosDemo(default));
            var reference = DatosDemo.Crear()[0];
            var zero = Calculos.Crear("TEST-ZERO", reference.Fecha, "Completada", "Tarjeta", "CRC", "Web",
                reference.Cliente, reference.Vendedor, [Calculos.Linea("PZERO", "Prueba cero", "Prueba", 1, 100m, 100m, 40m)]);
            await store.Ventas.InsertOneAsync(zero);
            var zeroMargin = await Consultas.Rentabilidad(store.Ventas.AsQueryable().Where(v => v.Id == "TEST-ZERO"), null).SingleAsync();
            Assert.Equal(-40m, zeroMargin.MargenBruto); Assert.Null(zeroMargin.MargenPorcentaje);
            await store.Ventas.DeleteOneAsync(v => v.Id == "TEST-ZERO");
            // Centinela USD para demostrar que los importes nunca se mezclan entre monedas.
            await store.Ventas.InsertOneAsync(DatosDemo.Crear()[0] with { Id = "TEST-USD", Moneda = "USD" });
            async Task<JsonElement> Get(string path, HttpStatusCode status = HttpStatusCode.OK)
            {
                using var response = await client.GetAsync(path);
                var body = await response.Content.ReadAsStringAsync();
                evidence.Add(new { ruta = path, codigo = (int)response.StatusCode, respuesta = JsonDocument.Parse(body).RootElement.Clone() });
                Assert.Equal(status, response.StatusCode);
                Assert.Contains("json", response.Content.Headers.ContentType!.MediaType!);
                return JsonDocument.Parse(body).RootElement.Clone();
            }
            async Task<T> Data<T>(string path) => (await Get(path)).GetProperty("datos").Deserialize<T>(Json)!;
            await Get("/health/live"); var health = await Get("/health/ready");
            Assert.Contains("local", health.GetProperty("origen").GetString());
            var all = await Data<PaginaDto>("/api/ventas"); Assert.Equal(18, all.Total);
            var first = await Data<Venta>("/api/ventas/DEMO-V001"); Assert.Equal(1006830m, first.Total);
            var page = await Data<PaginaDto>("/api/ventas?Pagina=2&Tamano=5"); Assert.Equal(5, page.Ventas.Count);
            Assert.Empty(page.Ventas.Select(v => v.Id).Intersect(all.Ventas.Take(5).Select(v => v.Id)));
            var completed = DatosDemo.Crear().Where(v => v.Estado == "Completada").ToArray();
            var summary = await Data<ResumenDto>("/api/reportes/resumen");
            Assert.Equal(12, summary.Ventas); Assert.Equal(completed.Sum(v => v.Total), summary.Facturacion);
            Assert.Equal(completed.Sum(v => v.IngresoNeto), summary.IngresosNetos);
            Assert.Equal(summary.Facturacion - summary.IngresosNetos, summary.Impuestos);
            Assert.Equal(Math.Round(summary.Facturacion / 12, 2), summary.TicketPromedio);
            var states = await Data<List<EstadoDto>>("/api/reportes/estados");
            Assert.Equal(new[] { new EstadoDto("Cancelada", 3), new EstadoDto("Completada", 12), new EstadoDto("Pendiente", 3) }, states);
            var products = await Data<List<ProductoDto>>("/api/reportes/productos");
            foreach (var p in products)
            {
                var units = 0; decimal revenue = 0;
                foreach (var sale in completed) foreach (var line in sale.Productos)
                    if (line.ProductoId == p.ProductoId) { units += line.Cantidad; revenue += line.IngresoNeto; }
                Assert.Equal(units, p.Unidades); Assert.Equal(revenue, p.IngresosNetos);
            }
            Assert.Equal(summary.IngresosNetos, products.Sum(p => p.IngresosNetos));
            var margins = await Data<List<RentabilidadDto>>("/api/reportes/rentabilidad");
            foreach (var p in margins)
            {
                var lines = completed.SelectMany(v => v.Productos).Where(l => l.ProductoId == p.ProductoId);
                var cost = lines.Sum(l => l.CostoTotal ?? 0m); var revenue = lines.Sum(l => l.IngresoNeto);
                Assert.Equal(cost, p.Costos); Assert.Equal(revenue - cost, p.MargenBruto);
                Assert.Equal(Math.Round((revenue - cost) / revenue * 100, 2), p.MargenPorcentaje);
            }
            Assert.Equal("P03", margins[0].ProductoId);
            Assert.Equal(1329750m, margins.Sum(p => p.MargenBruto));
            Assert.All(await Data<List<RentabilidadDto>>("/api/reportes/rentabilidad?Categoria=Audio"), p => Assert.Equal("P05", p.ProductoId));
            var byRevenue = await Data<List<ProductoDto>>("/api/reportes/productos?ordenarPor=ingresos");
            Assert.Equal(products.Max(p => p.IngresosNetos), byRevenue[0].IngresosNetos);
            var categories = await Data<List<CategoriaDto>>("/api/reportes/categorias");
            Assert.Equal(summary.IngresosNetos, categories.Sum(c => c.IngresosNetos));
            foreach (var path in new[] { "clientes", "ciudades", "vendedores", "canales" })
            {
                var groups = await Data<List<GrupoDto>>("/api/reportes/" + path);
                Assert.Equal(12, groups.Sum(g => g.Ventas)); Assert.Equal(summary.Facturacion, groups.Sum(g => g.Facturacion));
                foreach (var g in groups)
                {
                    var members = completed.Where(v => (path switch { "clientes" => v.Cliente.Identificacion,
                        "ciudades" => v.Cliente.Ciudad, "vendedores" => v.Vendedor.Identificacion, _ => v.Canal }) == g.Id);
                    Assert.Equal(members.Count(), g.Ventas); Assert.Equal(members.Sum(v => v.Total), g.Facturacion);
                }
            }
            var trends = await Data<List<TendenciaDto>>("/api/reportes/tendencias");
            Assert.Equal(new[] { 7, 8, 9 }, trends.Select(t => t.Mes));
            foreach (var t in trends) Assert.Equal(completed.Where(v => v.Fecha.Month == t.Mes).Sum(v => v.Total), t.Facturacion);
            var extremes = await Data<ExtremosDto>("/api/reportes/extremos");
            Assert.Equal(completed.Max(v => v.Total), extremes.Mayor); Assert.Equal(completed.Min(v => v.Total), extremes.Menor);
            var cities = await Data<List<string>>("/api/reportes/ciudades-distintas");
            Assert.Equal(completed.Select(v => v.Cliente.Ciudad).Distinct().OrderBy(x => x, StringComparer.Ordinal), cities);
            var dated = await Data<ResumenDto>("/api/reportes/resumen?Desde=2026-07-02&Hasta=2026-07-02");
            Assert.Equal(1, dated.Ventas); Assert.Equal(first.Total, dated.Facturacion);
            var city = await Data<ResumenDto>("/api/reportes/resumen?Ciudad=San%20Jos%C3%A9");
            Assert.Equal(completed.Where(v => v.Cliente.Ciudad == "San José").Sum(v => v.Total), city.Facturacion);
            var category = await Data<List<ProductoDto>>("/api/reportes/productos?Categoria=Audio");
            Assert.All(category, p => Assert.Equal("Audio", p.Categoria));
            Assert.Equal(completed.SelectMany(v => v.Productos).Where(p => p.Categoria == "Audio").Sum(p => p.IngresoNeto), category.Sum(p => p.IngresosNetos));
            var usd = await Data<ResumenDto>("/api/reportes/resumen?Moneda=USD"); Assert.Equal(1, usd.Ventas);
            Assert.Equal(3, (await Data<PaginaDto>("/api/ventas?Estado=Cancelada")).Total);
            var empty = await Data<ResumenDto>("/api/reportes/resumen?Ciudad=Inexistente"); Assert.Equal(new ResumenDto(0, 0, 0, 0, 0), empty);
            Assert.Empty(await Data<List<ProductoDto>>("/api/reportes/productos?Desde=2020-01-01&Hasta=2020-01-31"));
            Assert.Equal(JsonValueKind.Null, (await Get("/api/reportes/extremos?Ciudad=Inexistente")).GetProperty("datos").ValueKind);
            await Get("/api/ventas/no-existe", HttpStatusCode.NotFound);
            foreach (var path in new[] { "resumen?Desde=mal", "resumen?Desde=2026-09-01&Hasta=2026-08-01", "resumen?Estado=Cancelada", "resumen?Moneda=EUR", "productos?ordenarPor=rentabilidad" })
                await Get("/api/reportes/" + path, HttpStatusCode.BadRequest);
            await Get("/api/ventas?Tamano=101", HttpStatusCode.BadRequest);
            await Get("/api/ventas?Pagina=0", HttpStatusCode.BadRequest);
            var spec = await Get("/openapi/v1.json");
            foreach (var path in new[] { "/health/live", "/health/ready", "/api/ventas", "/api/ventas/{id}",
                "/api/reportes/resumen", "/api/reportes/estados", "/api/reportes/productos", "/api/reportes/categorias",
                "/api/reportes/clientes", "/api/reportes/ciudades", "/api/reportes/vendedores", "/api/reportes/canales",
                "/api/reportes/tendencias", "/api/reportes/extremos", "/api/reportes/ciudades-distintas", "/api/reportes/rentabilidad" })
                Assert.True(spec.GetProperty("paths").TryGetProperty(path, out _), "Ruta OpenAPI faltante: " + path);
            var pipelines = store.Pipelines.ToArray();
            Assert.NotEmpty(pipelines);
            foreach (var op in new[] { "$match", "$group", "$sum", "$avg", "$max", "$min", "$unwind", "$sort" })
                Assert.Contains(pipelines, p => p.Contains(op));
            var output = Environment.GetEnvironmentVariable("VENTAS_EVIDENCE_DIR");
            if (!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, "integracion-http.json"), JsonSerializer.Serialize(new {
                    ejecutadoUtc = DateTime.UtcNow, origen = store.Origen, carga = new { primera = 18, segunda = 0 },
                    resultado = "Todas las aserciones anteriores aprobadas", solicitudes = evidence }, Json));
                await File.WriteAllTextAsync(Path.Combine(output, "pipelines-ejecutados.json"), JsonSerializer.Serialize(pipelines, Json));
                await File.WriteAllTextAsync(Path.Combine(output, "ventas-demo.json"), JsonSerializer.Serialize(DatosDemo.Crear(), Json));
            }
        }
        finally
        {
            // Solo esta base temporal, creada por esta prueba, en loopback.
            await new MongoClient(uri).DropDatabaseAsync(db);
        }
    }
    [Fact]
    public async Task Sin_configuracion_devuelve_503_sin_secretos()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
            b.ConfigureLogging(l => l.ClearProviders()); b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                new Dictionary<string, string?> { ["MongoDB:ConnectionString"] = "" })); });
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/reportes/resumen");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("mongodb://", body);
        Assert.DoesNotContain("StackTrace", body);
    }
}
