using MongoDB.Bson;
using Ventas.Api;
namespace Ventas.Tests;

public class NegocioTests
{
    [Fact] public void Ejemplo_del_enunciado_conserva_importes_exactos()
    {
        var v = DatosDemo.Crear()[0];
        Assert.Equal(936000m, v.Subtotal); Assert.Equal(45000m, v.DescuentoTotal);
        Assert.Equal(891000m, v.IngresoNeto); Assert.Equal(115830m, v.Impuesto); Assert.Equal(1006830m, v.Total);
    }
    [Fact] public void Decimal128_y_redondeo_financiero()
    {
        var p = Calculos.Linea("T", "Prueba", "Prueba", 3, 0.05m, 10m);
        Assert.Equal(0.15m, p.Subtotal); Assert.Equal(0.02m, p.Descuento); Assert.Equal(0.13m, p.IngresoNeto);
        Assert.Equal(BsonType.Decimal128, p.ToBsonDocument()["PrecioUnitario"].BsonType);
        Assert.Equal(BsonType.Decimal128, DatosDemo.Crear()[0].ToBsonDocument()["Total"].BsonType);
    }
    [Theory]
    [InlineData(0, 10, 0)] [InlineData(-1, 10, 0)] [InlineData(1, -1, 0)]
    [InlineData(1, 10, -1)] [InlineData(1, 10, 101)]
    public void Rechaza_lineas_invalidas(int n, int precio, int descuento) =>
        Assert.Throws<ArgumentException>(() => Calculos.Linea("P", "Prueba", "Prueba", n, precio, descuento));
    [Fact] public void Descuento_total_permite_importe_cero() =>
        Assert.Equal(0, Calculos.Linea("P", "Prueba", "Prueba", 1, 100m, 100m).IngresoNeto);
    [Fact] public void Datos_coherentes_y_deterministas()
    {
        var ventas = DatosDemo.Crear();
        Assert.Equal(18, ventas.Count); Assert.Equal(18, ventas.Select(v => v.Id).Distinct().Count());
        Assert.Equal(12, ventas.Count(v => v.Estado == "Completada"));
        Assert.All(ventas, v => { Assert.Equal(v.Subtotal - v.DescuentoTotal, v.IngresoNeto);
            Assert.Equal(v.IngresoNeto + v.Impuesto, v.Total); Assert.Equal(DateTimeKind.Utc, v.Fecha.Kind); });
    }
    [Theory] [InlineData("2026-02-30", null)] [InlineData("2026-09-01", "2026-08-01")]
    [InlineData("incorrecta", null)] [InlineData(null, "9999-12-31")]
    public void Valida_rangos(string? desde, string? hasta) => Assert.Throws<ArgumentException>(() => new Filtro { Desde = desde, Hasta = hasta }.Validar());
    [Fact] public void Rechaza_estado_cancelado_en_finanzas() =>
        Assert.Throws<ArgumentException>(() => new Filtro { Estado = "Cancelada" }.Validar());
}
