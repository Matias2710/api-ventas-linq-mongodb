namespace Ventas.Api;

public static class DatosDemo
{
    public static List<Venta> Crear()
    {
        Cliente[] clientes = [
            new("DEMO-C01", "Cliente ficticio Ana", "San José", "San José", "Frecuente"),
            new("DEMO-C02", "Cliente ficticio Luis", "Alajuela", "Alajuela", "Nuevo"),
            new("DEMO-C03", "Cliente ficticio Sofía", "Cartago", "Cartago", "Corporativo"),
            new("DEMO-C04", "Cliente ficticio Diego", "Heredia", "Heredia", "Frecuente"),
            new("DEMO-C05", "Cliente ficticio Elena", "Liberia", "Guanacaste", "Nuevo"),
            new("DEMO-C06", "Cliente ficticio Pablo", "Limón", "Limón", "Corporativo")];
        Vendedor[] vendedores = [new("DEMO-E01", "Vendedora ficticia Alba", "Central", "Valle Central"),
            new("DEMO-E02", "Vendedor ficticio Bruno", "Norte", "Guanacaste"),
            new("DEMO-E03", "Vendedora ficticia Celia", "Caribe", "Caribe")];
        (string Id, string Nombre, string Categoria, decimal Precio)[] catalogo = [
            ("P01", "Laptop", "Computación", 450000m), ("P02", "Mouse", "Accesorios", 12000m),
            ("P03", "Monitor", "Computación", 95000m), ("P04", "Teclado", "Accesorios", 22000m),
            ("P05", "Auriculares", "Audio", 35000m), ("P06", "Parlante", "Audio", 48000m)];
        var ventas = new List<Venta>();
        for (var i = 0; i < 18; i++)
        {
            var a = catalogo[i % catalogo.Length]; var b = catalogo[(i + 1) % catalogo.Length];
            var lineas = new List<Producto> {
                Calculos.Linea(a.Id, a.Nombre, a.Categoria, i % 3 + 1, a.Precio, i % 2 == 0 ? 5m : 0m),
                Calculos.Linea(b.Id, b.Nombre, b.Categoria, i % 4 + 1, b.Precio, 0m) };
            if (i == 0) lineas = [Calculos.Linea("P01", "Laptop", "Computación", 2, 450000m, 5m),
                Calculos.Linea("P02", "Mouse", "Accesorios", 3, 12000m, 0m)];
            var estado = i % 6 == 4 ? "Pendiente" : i % 6 == 5 ? "Cancelada" : "Completada";
            ventas.Add(Calculos.Crear($"DEMO-V{i + 1:000}", new DateTime(2026, 7 + i / 6, 2 + i % 6 * 4, 12, 0, 0, DateTimeKind.Utc),
                estado, new[] { "Tarjeta", "SINPE", "Efectivo" }[i % 3], "CRC",
                new[] { "Tienda", "Web", "Teléfono" }[i % 3], clientes[(i + i / 6) % 6], vendedores[i % 3], lineas));
        }
        return ventas;
    }
}
