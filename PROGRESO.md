# Progreso verificable

Actualización: 29 de octubre de 2026, Costa Rica.
Repositorio: https://github.com/Matias2710/api-ventas-linq-mongodb

## Auditoría de todos los requisitos

| Área original | Estado y evidencia |
|---|---|
| Moodle y requisitos | Revisados enunciado de Asignación II, reglas del curso, acuerdo de aprendizaje (6 páginas) y recursos pertinentes de semana 6. Materiales guardados en materiales-moodle/, excluidos de Git. |
| Entorno | .NET SDK 10.0.301, Git, driver oficial 3.13.0, OpenAPI, xUnit y herramientas PDF utilizados; sin cambios globales de seguridad. |
| Modelos | Venta, cliente, vendedor y productos anidados; decimal/Decimal128; identificadores estables; 18 ventas ficticias. |
| Atlas | Clúster Free 8.0.34; colección Ventas; primera carga 18, segunda 0; panel y driver confirmaron documentos. User Secrets fuera de Git; acceso de red limitado a IP añadida por el asistente de Atlas. |
| LINQ | Todos los operadores requeridos; IQueryable hasta materialización final; pipelines reales registrados localmente y consultas verificadas en Atlas. |
| Indicadores | Los 12 indicadores originales, más estados, ciudades distintas y rentabilidad mediante margen bruto con costos ficticios explícitos. |
| API REST | Minimal APIs, DTO, filtros, paginación, errores y OpenAPI. No usa controladores MVC. |
| Pruebas | Última ejecución: 17 aprobadas, 0 fallidas, 0 omitidas. 36 solicitudes comparadas contra Atlas. Evidencia histórica adicional: 20 solicitudes HTTP locales. |
| Informe | PDF en actualización con portada verificada, índice dinámico, referencias, resultados y capturas auténticas; pendiente revisión visual final. |
| GitHub | Repositorio público existente y commit remoto comprobado. Pendiente publicar la ampliación de rentabilidad y el cierre documental. |
| Entrega | Solo PDF con enlace GitHub; no ZIP ni código adjunto. Cierre: 29/10/2026 a las 18:00. Todavía no se ha enviado. |
| Seguimiento | Este archivo mantiene el estado y las evidencias; no se declara entrega sin confirmación de Moodle. |

## Hallazgos y correcciones

- El PDF inicial carecía del índice exigido por las reglas; se añadió un índice paginado automático.
- El enunciado oficial menciona productos rentables. Se añadieron CostoUnitario y CostoTotal ficticios, y /api/reportes/rentabilidad con margen absoluto y porcentual. No se confunde ingreso con utilidad.
- Migración limitada a campos ausentes en líneas demo coincidentes: Atlas actualizó 36 líneas; una repetición actualizó 0. Conserva documentos, precios, descuentos e impuestos.
- La ejecución anterior de Probar-Local.ps1 chocó con apphost.exe en uso. Se corrigió la causa aislando los artefactos de compilación en .local-test/artifacts; el script completo pasó con 17 pruebas.
- Se conservan las correcciones previas de filtros opcionales, validación de ambas fechas y redondeo del promedio en MongoDB.
- No existe rúbrica separada, plantilla adicional ni proyecto base obligatorio en los recursos pertinentes revisados. No se exige una versión concreta de .NET en la actividad.
- El acuerdo general contiene referencias históricas a Visual Basic y SQL Server; se prioriza el enunciado vigente que exige Minimal APIs y MongoDB Atlas. La página de reglas menciona otro número de curso en su texto, pero está enlazada como lectura obligatoria en este curso; sus requisitos de PDF, índice y GitHub coinciden con la actividad.
- No se encontró prohibición específica de IA en los materiales revisados. La actividad docente Creación de Consultas con LINQ enlaza explícitamente pasos de IA. El informe declara asistencia de IA; no afirma autoría sin apoyo.

## Evidencias

- evidencias/pruebas.trx: ejecución automatizada final (local; fuera de Git por rutas del equipo).
- evidencias/integracion-http.json: respuestas de integración contra MongoDB real local.
- evidencias/pipelines-ejecutados.json: pipelines observados, no resultados simulados.
- evidencias/ventas-demo.json: documentos de referencia, ahora con costos ficticios.
- evidencias/http/verificacion.json: 20 solicitudes locales de la versión inicial, conservadas como evidencia histórica.
- evidencias/atlas/verificacion.json: 36 solicitudes y comparaciones de la versión ampliada.
- ../informe/atlas-documentos.png y api-resumen.png: capturas auténticas, fuera del repositorio público.

## Pendiente inmediato

1. Revisar todas las páginas del PDF final y sus referencias cruzadas.
2. Publicar las modificaciones, comprobar remoto y registrar versión.
3. Subir únicamente el PDF a la actividad y comprobar recepción.
