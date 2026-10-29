using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Ventas.Api;

public sealed record Cliente(string Identificacion, string Nombre, string Ciudad, string Provincia, string Categoria);
public sealed record Vendedor(string Identificacion, string Nombre, string Sucursal, string Region);
public sealed record Producto
{
    public required string ProductoId { get; init; }
    public required string Nombre { get; init; }
    public required string Categoria { get; init; }
    public int Cantidad { get; init; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal PrecioUnitario { get; init; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal PorcentajeDescuento { get; init; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal Subtotal { get; init; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal Descuento { get; init; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal IngresoNeto { get; init; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal? CostoUnitario { get; init; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal? CostoTotal { get; init; }
}
public sealed record Venta
{
    [BsonId] public required string Id { get; init; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime Fecha { get; init; }
    public required string Estado { get; init; }
    public required string MetodoPago { get; init; }
    public required string Moneda { get; init; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal Subtotal { get; init; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal DescuentoTotal { get; init; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal IngresoNeto { get; init; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal TasaImpuesto { get; init; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal Impuesto { get; init; }
    [BsonRepresentation(BsonType.Decimal128)] public decimal Total { get; init; }
    public required string Canal { get; init; }
    public bool TienePromocion { get; init; }
    public required Cliente Cliente { get; init; }
    public required Vendedor Vendedor { get; init; }
    public required List<Producto> Productos { get; init; }
}

public static class Calculos
{
    public static decimal Redondear(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
    public static Producto Linea(string id, string nombre, string categoria, int cantidad, decimal precio, decimal descuento, decimal? costoUnitario = null)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(categoria))
            throw new ArgumentException("El producto requiere identificación, nombre y categoría.");
        if (cantidad <= 0 || precio < 0 || precio != Redondear(precio) || descuento < 0 || descuento > 100)
            throw new ArgumentException("Cantidad, precio o descuento inválidos.");
        if (costoUnitario is { } costo && (costo < 0 || costo != Redondear(costo)))
            throw new ArgumentException("Costo unitario inválido.");
        var subtotal = Redondear(cantidad * precio);
        var rebaja = Redondear(subtotal * descuento / 100m);
        return new Producto { ProductoId = id, Nombre = nombre, Categoria = categoria, Cantidad = cantidad,
            PrecioUnitario = precio, PorcentajeDescuento = descuento, Subtotal = subtotal,
            Descuento = rebaja, IngresoNeto = subtotal - rebaja,
            CostoUnitario = costoUnitario, CostoTotal = costoUnitario is { } c ? Redondear(cantidad * c) : null };
    }
    public static Venta Crear(string id, DateTime fecha, string estado, string pago, string moneda, string canal,
        Cliente cliente, Vendedor vendedor, IEnumerable<Producto> productos, decimal tasa = 0.13m)
    {
        var lineas = productos.ToList(); // Entrada de una venta, no consulta analítica de MongoDB.
        if (string.IsNullOrWhiteSpace(id) || fecha.Kind != DateTimeKind.Utc || !Estados.Contains(estado) ||
            moneda is not ("CRC" or "USD") || tasa < 0 || tasa > 1 || lineas.Count == 0)
            throw new ArgumentException("Datos de venta inválidos; fecha UTC y al menos un producto requeridos.");
        lineas = lineas.Select(p => Linea(p.ProductoId, p.Nombre, p.Categoria, p.Cantidad, p.PrecioUnitario, p.PorcentajeDescuento, p.CostoUnitario)).ToList();
        var subtotal = lineas.Sum(p => p.Subtotal);
        var descuento = lineas.Sum(p => p.Descuento);
        var neto = subtotal - descuento;
        var impuesto = Redondear(neto * tasa);
        return new Venta { Id = id, Fecha = fecha, Estado = estado, MetodoPago = pago, Moneda = moneda,
            Canal = canal, Cliente = cliente, Vendedor = vendedor, Productos = lineas, Subtotal = subtotal,
            DescuentoTotal = descuento, IngresoNeto = neto, Impuesto = impuesto, Total = neto + impuesto,
            TasaImpuesto = tasa, TienePromocion = lineas.Any(p => p.PorcentajeDescuento > 0) };
    }
    public static readonly string[] Estados = ["Completada", "Pendiente", "Cancelada"];
}
