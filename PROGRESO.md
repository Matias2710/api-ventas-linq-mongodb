# Progreso verificable

Fecha de trabajo: 28 de octubre de 2026 (Costa Rica).

## Requisitos
- Verificados: enunciado del usuario; .NET SDK 10.0.301 y Git disponibles.
- Pendientes: actividad, rúbrica, fecha de entrega, plantilla, datos de portada y política de IA de Moodle. No se accedió al campus; no se reintenta Computer Use bloqueado.
- Se aplica el enunciado del usuario mientras no existan instrucciones institucionales verificadas.

## Decisiones
- ASP.NET Core 10, Minimal APIs, driver oficial MongoDB, colección `Ventas`.
- Datos ficticios, identificadores deterministas, carga idempotente.
- CRC por defecto; consultas financieras filtran moneda y estado Completada.
- Ingresos: neto después de descuentos antes de impuesto. Facturación: total con impuesto. Sin costos no se calcula rentabilidad.
- Impuesto didáctico uniforme 13 %, según el ejemplo; no es validación fiscal.
- Pruebas locales identificadas como tales; no acreditan acceso a Atlas.

## Estado
- [x] Solución y proyectos creados.
- [x] Modelos, 18 documentos ficticios y consultas LINQ en servidor.
- [x] Compilación correcta. 16 pruebas automatizadas aprobadas, sin omisiones; 20 solicitudes HTTP locales y 34 contra Atlas verificadas.
- [ ] Documentación, evidencias e informe PDF.
- [x] Atlas Free 8.0.34 conectado; primera carga 18, segunda 0; lectura real verificada. Credenciales en User Secrets.
- [ ] GitHub: Matias2710 seleccionado y autenticación comprobada; publicación en preparación.
- [ ] Moodle: revisión y entrega pendientes por falta de acceso.

## Correcciones comprobadas
- Filtros opcionales implementados como record con valores predeterminados para el enlace de Minimal APIs.
- Validación independiente de ambas fechas, aunque solo se indique Hasta.
- Promedio redondeado a dos decimales en MongoDB para evitar diferencias de representación decimal repetitiva.
- OpenAPI verificado contra sus 15 rutas documentadas.

## Evidencias
- `evidencias/pruebas.trx`: ejecución final automatizada.
- `evidencias/integracion-http.json`: respuestas de integración contra MongoDB local.
- `evidencias/pipelines-ejecutados.json`: agregaciones realmente enviadas al servidor local.
- `evidencias/http/verificacion.json`: solicitudes HTTP sobre Kestrel local.
- `evidencias/atlas/verificacion.json`: 34 solicitudes sobre Kestrel conectado a Atlas, contrastadas con resultados esperados.
