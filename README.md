# Axiom Harness IA

Harness local de agentes IA para Windows, creado con C# / .NET 10, WinForms, Clean Architecture, Ollama y SQL Server.

## Requisitos

- .NET SDK 10 y Visual Studio 2026 con la carga de trabajo de escritorio .NET.
- Ollama activo en `http://localhost:11434` y al menos un modelo instalado. La aplicación preselecciona `qwen3.5:4b-q4_K_M` si existe.
- SQL Server local por defecto (`localhost`) con autenticación Windows.

## Uso

1. Abra `Axiom.Harness.sln` en Visual Studio y establezca `Axiom.Harness.WinForms` como proyecto de inicio, o ejecute `dotnet run --project src/Axiom.Harness.WinForms`.
2. Al iniciar se aplican las migraciones y se crea la base de datos `AxiomHarness` si la cuenta actual dispone de permisos.
3. Seleccione el modelo, indique las carpetas que el agente puede leer (separadas por `;`) y escriba el objetivo.
4. La pestaña **Chat y ejecución** muestra la respuesta en streaming y cada paso de herramientas. La pestaña **Ejecuciones** permite revisar el historial persistido.

## Seguridad de herramientas

La primera versión sólo ofrece acciones de lectura: `list_files`, `read_file` y `http_get`. Las dos primeras no pueden salir de las carpetas autorizadas. HTTP GET queda auditado y admite únicamente URL HTTP/HTTPS. No se ejecutan comandos ni se escriben archivos.

La conexión de SQL Server y la URL de Ollama están en `src/Axiom.Harness.WinForms/appsettings.json`.
