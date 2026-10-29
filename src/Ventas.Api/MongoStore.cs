using MongoDB.Bson;
using MongoDB.Driver;

namespace Ventas.Api;

public sealed class BaseNoDisponibleException : Exception;
public sealed class MongoStore
{
    public System.Collections.Concurrent.ConcurrentQueue<string> Pipelines { get; } = new();
    private readonly IMongoDatabase? database;
    public IMongoCollection<Venta> Ventas => database?.GetCollection<Venta>("Ventas") ?? throw new BaseNoDisponibleException();
    public string Origen { get; } = "Sin configurar";
    public MongoStore(IConfiguration config)
    {
        var uri = config["MongoDB:ConnectionString"];
        if (string.IsNullOrWhiteSpace(uri)) return;
        try
        {
            var url = new MongoUrl(uri);
            var local = config.GetValue<bool>("MongoDB:LocalTest");
            if (local && url.Servers.Any(s => s.Host is not ("localhost" or "127.0.0.1" or "::1"))) throw new ArgumentException();
            if (!local && (!uri.StartsWith("mongodb+srv://", StringComparison.Ordinal) ||
                url.Servers.Any(s => !s.Host.EndsWith(".mongodb.net", StringComparison.OrdinalIgnoreCase)))) throw new ArgumentException();
            if (url.AllowInsecureTls == true || url.UseTls == false && !local) throw new ArgumentException();
            var settings = MongoClientSettings.FromUrl(url);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
            settings.ApplicationName = "VentasAcademicas";
            if (local && config.GetValue<bool>("MongoDB:CaptureDiagnostics"))
                settings.ClusterConfigurator = c => c.Subscribe<MongoDB.Driver.Core.Events.CommandStartedEvent>(e =>
                { if (e.CommandName == "aggregate") Pipelines.Enqueue(e.Command["pipeline"].ToJson()); });
            var db = config["MongoDB:Database"] ?? "VentasAcademicas";
            if (!System.Text.RegularExpressions.Regex.IsMatch(db, "^[A-Za-z][A-Za-z0-9_]{0,62}$")) throw new ArgumentException();
            database = new MongoClient(settings).GetDatabase(db);
            Origen = local ? "MongoDB local (prueba; no Atlas)" : "MongoDB Atlas";
        }
        catch { throw new InvalidOperationException("Configuración MongoDB inválida. Revise User Secrets o variables de entorno; no publique la conexión."); }
    }
    public async Task Ping(CancellationToken ct) => await (database ?? throw new BaseNoDisponibleException())
        .RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);
    public async Task<int> Sembrar(CancellationToken ct)
    {
        await Ping(ct);
        await Ventas.Indexes.CreateOneAsync(new CreateIndexModel<Venta>(Builders<Venta>.IndexKeys
            .Ascending(v => v.Moneda).Ascending(v => v.Estado).Ascending(v => v.Fecha)), cancellationToken: ct);
        var writes = DatosDemo.Crear().Select(v => new UpdateOneModel<Venta>(Builders<Venta>.Filter.Eq(x => x.Id, v.Id),
            new BsonDocument("$setOnInsert", v.ToBsonDocument())) { IsUpsert = true });
        var result = await Ventas.BulkWriteAsync(writes, cancellationToken: ct);
        return result.Upserts.Count;
    }
    public async Task<long> CompletarCostosDemo(CancellationToken ct)
    {
        // Migración acotada: solo añade costos ausentes de líneas demo que conservan su identidad e importes.
        // No reemplaza documentos ni modifica precios, descuentos, impuestos o totales.
        var writes = new List<WriteModel<Venta>>();
        foreach (var v in DatosDemo.Crear()) for (var i = 0; i < v.Productos.Count; i++)
        {
            var p = v.Productos[i]; var path = $"Productos.{i}."; var f = Builders<Venta>.Filter;
            var filter = f.Eq(x => x.Id, v.Id) & f.Eq(x => x.Total, v.Total) &
                f.Eq(path + "ProductoId", p.ProductoId) & f.Eq(path + "Cantidad", p.Cantidad) &
                f.Eq(path + "PrecioUnitario", p.PrecioUnitario) & f.Eq(path + "IngresoNeto", p.IngresoNeto) &
                f.Exists(path + "CostoUnitario", false) & f.Exists(path + "CostoTotal", false);
            writes.Add(new UpdateOneModel<Venta>(filter, Builders<Venta>.Update
                .Set(path + "CostoUnitario", p.CostoUnitario).Set(path + "CostoTotal", p.CostoTotal)));
        }
        return (await Ventas.BulkWriteAsync(writes, cancellationToken: ct)).ModifiedCount;
    }
}
