# API de ventas con Minimal APIs, MongoDB Atlas y LINQ

API académica en español que analiza ventas ficticias de una tienda de tecnología de Costa Rica. Implementa consultas LINQ ejecutadas por MongoDB, importes `decimal`/BSON Decimal128 y respuestas JSON tipadas. No usa controladores MVC ni descarga la colección para agregarla en memoria.

## Estado verificado

- .NET SDK 10.0.301; MongoDB.Driver 3.13.0; ASP.NET Core OpenAPI 10.0.14.
- 18 documentos en `Ventas`, 12 completados, 3 pendientes y 3 cancelados.
- 16 pruebas automatizadas aprobadas, sin omisiones, contra MongoDB Community 8.0.34 local.
- 20 solicitudes HTTP locales y 34 solicitudes contra **MongoDB Atlas Free 8.0.34**, comparadas con resultados esperados.
- Carga real en Atlas: primera ejecución 18 inserciones, segunda 0; total 18.
- Moodle: actividad, rúbrica, plantilla, política de IA y entrega aún no verificadas. El proyecto sigue el enunciado facilitado por el usuario.

## Compilar y probar

Requisitos: .NET 10 SDK, Git; PowerShell 7 para los scripts; Python 3 solo para la comparación adicional de Atlas.

```powershell
dotnet restore Ventas.sln
dotnet build Ventas.sln -c Release --no-restore
dotnet test Ventas.sln -c Release
```

Sin `VENTAS_TEST_URI`, la prueba que necesita MongoDB se marca **omitida**, no aprobada. Para repetir todas las pruebas con un servidor real descargado desde [MongoDB Community oficial](https://www.mongodb.com/try/download/community):

```powershell
./scripts/Probar-Local.ps1 -MongodPath 'C:/ruta/mongodb/bin/mongod.exe'
```

El script inicia solo loopback, ejecuta las pruebas y detiene su propio proceso. La prueba crea y elimina únicamente una base temporal `VentasTests_<GUID>`. Nunca acepta una URI remota en este flujo. Los archivos del motor quedan en `.local-test/` (ignorados por Git); no modifica servicios de Windows.

## Configurar Atlas y ejecutar

Seguir [docs/ATLAS.md](docs/ATLAS.md). En un equipo nuevo, crear un clúster Free, autorizar solo la IP del equipo y configurar un usuario de base con los permisos necesarios. No habilitar acceso universal ni desactivar TLS.

```powershell
./scripts/Configurar-Atlas.ps1
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project src/Ventas.Api --no-launch-profile -- --seed
dotnet run --project src/Ventas.Api --no-launch-profile --urls http://127.0.0.1:5088
```

`Configurar-Atlas.ps1` solicita la URI de forma oculta y la guarda en .NET User Secrets, fuera del repositorio. También se admiten `MongoDB__ConnectionString`, `MongoDB__Database` y `MongoDB__LocalTest` como variables de entorno; tienen prioridad sobre User Secrets. El modo local requiere habilitación explícita y solo acepta loopback. La API no cae silenciosamente a datos simulados. Sin conexión devuelve 503.

Abrir `http://127.0.0.1:5088/openapi/v1.json` en Development o importar esa URL en Postman. Se incluye una colección en `pruebas/Ventas.postman_collection.json`. No se incluye interfaz Swagger adicional.

```powershell
./scripts/Verificar-Http.ps1 -ExpectAtlas -OutputDirectory ./evidencias/atlas-http
python scripts/verificar_atlas.py --url http://127.0.0.1:5088
```

El segundo comando compara 34 respuestas con el oráculo de integración local. El caso USD local utiliza un centinela temporal y espera cero ventas USD en la base demo de Atlas. Estas comprobaciones numéricas presuponen la colección demo intacta; una base con más datos legítimos requiere otros valores esperados.

## Modelo y reglas de cálculo

`Venta` contiene fecha UTC, estado, método de pago, moneda, canal, promoción, importes, un cliente, un vendedor y una lista de productos. `_id` es un identificador de negocio estable como `DEMO-V001`. La carga usa upsert con `$setOnInsert`: repetirla no duplica ni modifica documentos existentes. Índice adicional por moneda, estado y fecha.

Cada línea conserva cantidad, precio unitario, porcentaje de descuento, subtotal, descuento e ingreso neto. Los nombres e identificaciones son ficticios. No se usan identificaciones personales reales.

- Subtotal de línea = cantidad × precio unitario.
- Descuento de línea = subtotal × porcentaje / 100; se redondea a dos decimales, `AwayFromZero`.
- Ingreso neto = subtotal − descuento. La venta suma los importes de sus líneas.
- Impuesto = ingreso neto × 0.13, redondeado a dos decimales, `AwayFromZero`.
- Facturación = ingreso neto + impuesto. Se suman solo ventas completadas.
- Ticket promedio = promedio de totales con impuesto, redondeado a dos decimales por `$round` en MongoDB (empates al par).

El 13 % es un **supuesto didáctico del enunciado**, pendiente de contrastar con el material docente. No representa asesoría fiscal. No se calcula utilidad o rentabilidad porque no existen costos. CRC y USD nunca se suman entre sí ni se convierten automáticamente.

La venta de referencia contiene 2 laptops de CRC 450 000 con 5 % de descuento y 3 mouse de CRC 12 000: subtotal 936 000; descuento 45 000; neto 891 000; impuesto 115 830; total **1 006 830**.

## Endpoints

Todos son GET. Éxito: `{ "datos": ..., "meta": { "moneda", "origen", "alcance", "periodo" } }`. Errores: Problem Details con códigos 400, 404, 500 o 503, sin URI ni trazas privadas. Salud usa un objeto simple.

| Ruta | Contenido |
|---|---|
| `/health/live` | Proceso vivo; no prueba conexión |
| `/health/ready` | Ping real y origen local o Atlas |
| `/api/ventas` | Lista paginada; todos los estados por defecto |
| `/api/ventas/{id}` | Documento individual o 404 |
| `/api/reportes/resumen` | Facturación, ingresos, impuestos, ticket y cantidad |
| `/api/reportes/estados` | Cantidad por estado, incluidos pendiente y cancelado |
| `/api/reportes/productos` | Unidades e ingresos por producto |
| `/api/reportes/categorias` | Unidades e ingresos por categoría |
| `/api/reportes/clientes` | Facturación y frecuencia por cliente |
| `/api/reportes/ciudades` | Ventas e importes por ciudad |
| `/api/reportes/vendedores` | Desempeño por vendedor |
| `/api/reportes/canales` | Ventas e importes por canal |
| `/api/reportes/tendencias` | Año y mes UTC, ventas e importes |
| `/api/reportes/extremos` | Mayor y menor total de ventas completadas |
| `/api/reportes/ciudades-distintas` | Ciudades únicas, ordenadas |

Filtros: `Desde` y `Hasta` (`yyyy-MM-dd`, ambos días inclusivos en UTC), `Ciudad`, `Categoria`, `Moneda` (CRC por defecto) y `Estado`. Los reportes financieros admiten únicamente Completada. `Pagina` (1..100000) y `Tamano` (1..100, 20 por defecto) se aplican a la lista de ventas. `/productos` admite `ordenarPor=unidades` (predeterminado) o `ingresos`. Los nombres se comparan exactamente, con mayúsculas y acentos.

**Alcance de categoría:** en ventas y reportes de transacciones, selecciona ventas que contienen esa categoría y conserva el total de la transacción. En productos y categorías restringe también las líneas. La respuesta explica ese alcance. No se deben comparar ambos importes como si tuvieran la misma población.

Sin resultados: resumen en cero, listas vacías y extremos con `datos: null`; no se inventan ventas de importe cero.

## LINQ en el servidor

`Program.cs` inicia las consultas con `s.Ventas.AsQueryable()`. `Consultas` conserva `IQueryable<T>` hasta `ToListAsync` o `FirstOrDefaultAsync`. La lista de ventas se pagina antes de materializarse.

| Operador | Uso demostrado |
|---|---|
| `Where` | Estado, moneda, rango y ciudad |
| `Select` | DTO y ciudades |
| `GroupBy` | Resumen, producto, cliente, ciudad, vendedor, canal y mes |
| `Sum` | Totales, ingresos, impuestos y unidades |
| `Average` | Ticket promedio |
| `Count` | Ventas por grupo y total paginado |
| `Max`, `Min` | Extremos de ventas completadas |
| `OrderBy` | Cronología y ciudades; rankings usan también orden descendente |
| `Distinct` | Ciudades únicas |
| `SelectMany` | Desanidar productos, traducido a `$unwind` |

El driver LINQ3 traduce a pipelines. `evidencias/pipelines-ejecutados.json` contiene comandos realmente observados en el servidor local durante las pruebas, incluidos `$match`, `$group`, `$sum`, `$avg`, `$round`, `$unwind` y `$sort`. Las mismas consultas se ejecutaron correctamente contra Atlas. No se usa LINQ en memoria como sustitución de agregaciones. La generación de fixtures y el oráculo de pruebas sí trabajan localmente, de forma deliberada.

## Resultados de la demostración (CRC)

| Indicador | Resultado |
|---|---:|
| Ventas completadas | 12 |
| Ingresos sin impuesto | 4 008 250,00 |
| Impuestos | 521 072,50 |
| Facturación con impuesto | 4 529 322,50 |
| Ticket promedio con impuesto | 377 443,54 |
| Mayor venta | 1 006 830,00 |
| Menor venta | 103 960,00 |

Monitor lidera en unidades (17); Laptop lidera en ingresos netos (1 710 000). Son datos sintéticos, no conclusiones sobre una empresa real. La caída de facturación mensual se explica dentro del conjunto de demostración y no acredita estacionalidad.

## Estructura y evidencias

- `src/Ventas.Api`: modelos, cálculo, datos demo, conexión, consultas y rutas.
- `tests/Ventas.Tests`: reglas de negocio, serialización BSON, validación, integración, idempotencia y errores.
- `scripts`: configuración segura y comprobaciones reproducibles.
- `evidencias`: fixtures, respuestas reales locales y de Atlas, pipelines. Los `.trx` locales se excluyen de Git por contener rutas del equipo.
- `docs/ATLAS.md`: conexión y diagnóstico.
- `PROGRESO.md`: estado de entrega y requisitos por confirmar.

La aplicación escucha en loopback y es una demostración académica. Antes de exponerla a Internet se requieren autenticación, autorización, HTTPS, límites de consumo y paginación adicional de reportes con grandes volúmenes. No contiene endpoints HTTP de carga o borrado.

## Referencias oficiales

- [Microsoft: Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0).
- [Microsoft: OpenAPI](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0).
- [MongoDB: LINQ y agregación](https://www.mongodb.com/docs/drivers/csharp/current/aggregation/linq/).
- [MongoDB: conexión a Atlas](https://www.mongodb.com/docs/atlas/connect-to-database-deployment/).

El informe académico se entrega por separado para no publicar datos de portada ni documentos del campus. Se utilizó asistencia de IA en desarrollo y documentación; la compatibilidad con la política del curso sigue pendiente de revisión.
