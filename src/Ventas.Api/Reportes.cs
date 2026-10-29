using System.Globalization;

namespace Ventas.Api;

public sealed record Filtro(string? Desde = null, string? Hasta = null, string? Ciudad = null,
    string? Categoria = null, string Moneda = "CRC", string? Estado = null, int Pagina = 1, int Tamano = 20)
{
    public DateTime? Inicio => Parse(Desde);
    public DateTime? Fin => Parse(Hasta)?.AddDays(1);
    private static DateTime? Parse(string? value)
    {
        if (value is null) return null;
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) || d.Year > 9998)
            throw new ArgumentException("Las fechas deben usar yyyy-MM-dd y un año entre 0001 y 9998.");
        return d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }
    public void Validar(bool financiero = true)
    {
        var inicio = Inicio; var fin = Fin;
        if (inicio is { } a && fin is { } b && a >= b) throw new ArgumentException("Desde no puede ser posterior a Hasta.");
        if (Moneda is not ("CRC" or "USD")) throw new ArgumentException("Moneda debe ser CRC o USD.");
        if (Estado is not null && !Calculos.Estados.Contains(Estado)) throw new ArgumentException("Estado inválido.");
        if (financiero && Estado is not null && Estado != "Completada") throw new ArgumentException("Los indicadores financieros solo admiten Completada.");
        if (Pagina < 1 || Pagina > 100000 || Tamano < 1 || Tamano > 100) throw new ArgumentException("Pagina: 1..100000; Tamano: 1..100.");
        if (Ciudad?.Length > 100 || Categoria?.Length > 100) throw new ArgumentException("Filtro demasiado largo.");
    }
}
public sealed record ResumenDto(int Ventas, decimal Facturacion, decimal IngresosNetos, decimal Impuestos, decimal TicketPromedio);
public sealed record EstadoDto(string Estado, int Ventas);
public sealed record ProductoDto(string ProductoId, string Nombre, string Categoria, int Unidades, decimal IngresosNetos);
public sealed record CategoriaDto(string Categoria, int Unidades, decimal IngresosNetos);
public sealed record RentabilidadDto(string ProductoId, string Nombre, decimal IngresosNetos,
    decimal Costos, decimal MargenBruto, decimal? MargenPorcentaje);
public sealed record GrupoDto(string Id, string Nombre, int Ventas, decimal Facturacion, decimal IngresosNetos);
public sealed record TendenciaDto(int Anio, int Mes, int Ventas, decimal Facturacion, decimal IngresosNetos);
public sealed record ExtremosDto(decimal Mayor, decimal Menor);
public sealed record MetaDto(string Moneda, string Origen, string Alcance, string Periodo = "Fechas UTC; Desde y Hasta inclusivas");
public sealed record Respuesta<T>(T Datos, MetaDto Meta);
public sealed record PaginaDto(IReadOnlyList<Venta> Ventas, int Pagina, int Tamano, int Total);

// Los IQueryable se materializan solo después de agregar, filtrar y ordenar en MongoDB.
public static class Consultas
{
    public static IQueryable<Venta> Filtrar(IQueryable<Venta> q, Filtro f, bool financiero = true)
    {
        f.Validar(financiero);
        q = q.Where(v => v.Moneda == f.Moneda);
        if (financiero) q = q.Where(v => v.Estado == "Completada");
        else if (f.Estado is not null) q = q.Where(v => v.Estado == f.Estado);
        if (f.Inicio is { } a) q = q.Where(v => v.Fecha >= a);
        if (f.Fin is { } b) q = q.Where(v => v.Fecha < b);
        if (f.Ciudad is not null) q = q.Where(v => v.Cliente.Ciudad == f.Ciudad);
        if (f.Categoria is not null) q = q.Where(v => v.Productos.Any(p => p.Categoria == f.Categoria));
        return q;
    }
    public static IQueryable<ResumenDto> Resumen(IQueryable<Venta> q) => q.GroupBy(v => 1)
        .Select(g => new ResumenDto(g.Count(), g.Sum(v => v.Total), g.Sum(v => v.IngresoNeto), g.Sum(v => v.Impuesto), Math.Round(g.Average(v => v.Total), 2)));
    public static IQueryable<EstadoDto> Estados(IQueryable<Venta> q) => q.GroupBy(v => v.Estado)
        .Select(g => new EstadoDto(g.Key, g.Count())).OrderBy(x => x.Estado);
    public static IQueryable<Producto> Lineas(IQueryable<Venta> q, string? categoria)
    {
        var lineas = q.SelectMany(v => v.Productos);
        return categoria is null ? lineas : lineas.Where(p => p.Categoria == categoria);
    }
    public static IQueryable<ProductoDto> Productos(IQueryable<Venta> q, string? categoria) => Lineas(q, categoria)
        .GroupBy(p => new { p.ProductoId, p.Nombre, p.Categoria })
        .Select(g => new ProductoDto(g.Key.ProductoId, g.Key.Nombre, g.Key.Categoria, g.Sum(p => p.Cantidad), g.Sum(p => p.IngresoNeto)));
    public static IQueryable<CategoriaDto> Categorias(IQueryable<Venta> q, string? categoria) => Lineas(q, categoria)
        .GroupBy(p => p.Categoria).Select(g => new CategoriaDto(g.Key, g.Sum(p => p.Cantidad), g.Sum(p => p.IngresoNeto)))
        .OrderByDescending(x => x.IngresosNetos).ThenBy(x => x.Categoria);
    public static IQueryable<GrupoDto> Clientes(IQueryable<Venta> q) => q.GroupBy(v => new { v.Cliente.Identificacion, v.Cliente.Nombre })
        .Select(g => new GrupoDto(g.Key.Identificacion, g.Key.Nombre, g.Count(), g.Sum(v => v.Total), g.Sum(v => v.IngresoNeto)))
        .OrderByDescending(x => x.Facturacion).ThenByDescending(x => x.Ventas).ThenBy(x => x.Id);
    public static IQueryable<GrupoDto> Ciudades(IQueryable<Venta> q) => q.GroupBy(v => v.Cliente.Ciudad)
        .Select(g => new GrupoDto(g.Key, g.Key, g.Count(), g.Sum(v => v.Total), g.Sum(v => v.IngresoNeto)))
        .OrderByDescending(x => x.Facturacion).ThenBy(x => x.Id);
    public static IQueryable<GrupoDto> Vendedores(IQueryable<Venta> q) => q.GroupBy(v => new { v.Vendedor.Identificacion, v.Vendedor.Nombre })
        .Select(g => new GrupoDto(g.Key.Identificacion, g.Key.Nombre, g.Count(), g.Sum(v => v.Total), g.Sum(v => v.IngresoNeto)))
        .OrderByDescending(x => x.Facturacion).ThenBy(x => x.Id);
    public static IQueryable<GrupoDto> Canales(IQueryable<Venta> q) => q.GroupBy(v => v.Canal)
        .Select(g => new GrupoDto(g.Key, g.Key, g.Count(), g.Sum(v => v.Total), g.Sum(v => v.IngresoNeto)))
        .OrderByDescending(x => x.Facturacion).ThenBy(x => x.Id);
    public static IQueryable<TendenciaDto> Tendencias(IQueryable<Venta> q) => q.GroupBy(v => new { v.Fecha.Year, v.Fecha.Month })
        .Select(g => new TendenciaDto(g.Key.Year, g.Key.Month, g.Count(), g.Sum(v => v.Total), g.Sum(v => v.IngresoNeto)))
        .OrderBy(x => x.Anio).ThenBy(x => x.Mes);
    public static IQueryable<ExtremosDto> Extremos(IQueryable<Venta> q) => q.GroupBy(v => 1)
        .Select(g => new ExtremosDto(g.Max(v => v.Total), g.Min(v => v.Total)));
    public static IQueryable<string> CiudadesDistintas(IQueryable<Venta> q) => q.Select(v => v.Cliente.Ciudad).Distinct().OrderBy(x => x);
    public static IQueryable<RentabilidadDto> Rentabilidad(IQueryable<Venta> q, string? categoria) => Lineas(q, categoria)
        .Where(p => p.CostoTotal != null)
        .GroupBy(p => new { p.ProductoId, p.Nombre })
        .Select(g => new RentabilidadDto(g.Key.ProductoId, g.Key.Nombre, g.Sum(p => p.IngresoNeto),
            g.Sum(p => p.CostoTotal ?? 0m), g.Sum(p => p.IngresoNeto) - g.Sum(p => p.CostoTotal ?? 0m),
            g.Sum(p => p.IngresoNeto) == 0m ? null : Math.Round(
                (g.Sum(p => p.IngresoNeto) - g.Sum(p => p.CostoTotal ?? 0m)) / g.Sum(p => p.IngresoNeto) * 100m, 2)))
        .OrderByDescending(p => p.MargenBruto).ThenBy(p => p.ProductoId);
}
