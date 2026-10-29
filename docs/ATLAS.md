# Conexión real con MongoDB Atlas

Estado verificado: clúster Atlas Free con MongoDB 8.0.34, base `VentasAcademicas`, colección `Ventas` y 18 documentos. La primera carga insertó 18; repetirla insertó 0. Se verificaron 34 solicitudes HTTP contra Atlas y se compararon con resultados esperados. La configuración privada está en .NET User Secrets, no en este repositorio. El asistente de Atlas autorizó la IP actual del equipo; no se habilitó acceso universal. El usuario de base se creó mediante el asistente de la cuenta; para reutilizar o desplegar, limitar sus permisos a la base académica.

1. Iniciar sesión en [Atlas](https://cloud.mongodb.com). Si no existe cuenta, completar personalmente su registro y condiciones.
2. Crear un proyecto académico y seleccionar explícitamente el clúster **Free**. No seleccionar Flex ni Dedicated ni añadir facturación.
3. Crear un usuario de base de datos limitado a `readWrite` en `VentasAcademicas`. La cuenta de Atlas y este usuario son distintos. Guardar su contraseña en un gestor personal.
4. En Network Access autorizar exclusivamente la IP pública actual del equipo (`/32`), preferiblemente con vencimiento. No autorizar `0.0.0.0/0` ni `::/0`.
5. En Connect > Drivers > C# copiar la URI `mongodb+srv://...mongodb.net/`. Codificar caracteres reservados de usuario/contraseña según el formato URI. Mantener TLS y validación de certificados.
6. Ejecutar en PowerShell 7: `./scripts/Configurar-Atlas.ps1`. Pegar la conexión en su entrada oculta. El script la envía a User Secrets mediante entrada estándar, fuera del historial de comandos y del repositorio. User Secrets es configuración local de desarrollo, no una bóveda cifrada.
7. No establecer `MongoDB__LocalTest=true` al usar Atlas. Las variables de entorno tienen prioridad sobre User Secrets: retirar las variables locales de prueba si existieran en esa consola.
8. Ejecutar con `ASPNETCORE_ENVIRONMENT=Development`:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project src/Ventas.Api --no-launch-profile -- --seed
dotnet run --project src/Ventas.Api --no-launch-profile -- --seed
dotnet run --project src/Ventas.Api --no-launch-profile --urls http://127.0.0.1:5088
```

La primera carga inserta 18 documentos; la segunda inserta 0 en una base nueva. `_id` es estable; `$setOnInsert` no modifica ventas existentes. Si ya hay registros con esos identificadores, comprobar su contenido antes de interpretar las cifras de referencia.

9. En otra consola, ejecutar `./scripts/Verificar-Http.ps1 -ExpectAtlas -OutputDirectory ./evidencias/atlas`. Exige que `/health/ready` declare Atlas y comprueba la lectura de la venta de referencia desde esa conexión.
10. En Atlas, abrir Browse Collections > VentasAcademicas > Ventas. Verificar los 18 documentos, la venta `DEMO-V001` y los importes Decimal128. Conservar evidencia sin credenciales. Volver a ejecutar los reportes y actualizar informe y progreso con resultados reales.

Si falla: comprobar IP autorizada, nombre del usuario de base, contraseña codificada, DNS SRV y salida de red permitida. No desactivar TLS ni ampliar la red a acceso universal.

Fuentes oficiales: [Conectar al clúster](https://www.mongodb.com/docs/atlas/connect-to-database-deployment/), [Usuarios de base de datos](https://www.mongodb.com/docs/atlas/security-add-mongodb-users/), [Problemas de conexión](https://www.mongodb.com/docs/atlas/troubleshoot-connection/).
