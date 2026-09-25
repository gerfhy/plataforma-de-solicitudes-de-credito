# Plataforma de Gestión de Solicitudes de Crédito 💳

Sistema web empresarial desarrollado con **ASP.NET Core MVC (.NET 10)** para la originación, evaluación y auditoría de créditos bajo reglas de negocio estrictas en servidor, sesiones distribuidas y caché con **Redis Cloud**, mensajería asíncrona desacoplada con **Cloud MQ (RabbitMQ en CloudAMQP)** y comunicación reactiva bidireccional en tiempo real con **WebSockets (PieSocket)**, desplegado en **Render.com**.

---

## 🌐 Enlaces del Proyecto y Despliegue

* **Repositorio en GitHub:** [https://github.com/gerfhy/plataforma-de-solicitudes-de-credito.git](https://github.com/gerfhy/plataforma-de-solicitudes-de-credito.git)
* **Despliegue Web Service (Render):** [https://plataforma-de-solicitudes-de-credito.onrender.com/](https://plataforma-de-solicitudes-de-credito.onrender.com/)

---

## 🛠️ Stack Tecnológico y Arquitectura

* **Framework Backend:** ASP.NET Core MVC (.NET 10.0 LTS)
* **Autenticación y Control de Acceso:** ASP.NET Core Identity con asignación de Roles (`Analista` y Clientes).
* **Capa de Datos:** Entity Framework Core 10 con SQLite (`app.db`).
* **Sesión Distribuida y Caché:** StackExchange.Redis conectado a **Redis Cloud** (TTL de 60 segundos con invalidación proactiva y rastreo de última solicitud en Topbar).
* **Mensajería Asíncrona (Cloud MQ):** **CloudAMQP (LavinMQ / RabbitMQ)** con colas durables, mensajes persistentes, *Publisher Confirms* y consumidor en segundo plano con confirmación manual `ACK` y deduplicación por `MessageId`.
* **Tiempo Real:** **PieSocket** (`free.blr2`) y Hub SignalR en `/hubs/solicitudes`, con aislamiento de canales por usuario y sincronización de reconexión.
* **Infraestructura y Contenerización:** Docker multi-stage sobre Linux desplegado en **Render.com**.

---

## 👥 Cuentas de Acceso Pre-cargadas (Seed Data)

La base de datos se inicializa automáticamente al arrancar mediante `DbInitializer`:

| Rol | Correo Electrónico | Contraseña | Ingresos Mensuales | Estado Inicial |
| :--- | :--- | :--- | :--- | :--- |
| **Analista de Riesgo** | `analista@creditos.com` | `Password123!` | N/A | Acceso a `/Analista` y auditoría de notificaciones |
| **Cliente 1** | `cliente1@creditos.com` | `Password123!` | $3,500.00 | Activo (Solicitudes aprobadas y catálogo) |
| **Cliente 2** | `cliente2@creditos.com` | `Password123!` | $2,000.00 | Activo (Pruebas de aislamiento en tiempo real) |

---

## 📋 Reglas de Negocio Implementadas en Servidor

1. **Capacidad de Endeudamiento (Registro)**: Un cliente no puede solicitar un monto mayor a **10 veces sus ingresos mensuales** ($35,000 para Cliente 1).
2. **Límite de Solicitudes Pendientes**: Un cliente solo puede tener **una única solicitud en estado Pendiente** a la vez.
3. **Estado de Cliente**: Clientes inactivos no pueden radicar solicitudes de crédito.
4. **Política de Aprobación de Riesgo**: El analista no puede aprobar créditos que excedan **5 veces los ingresos mensuales** del cliente ($17,500 para Cliente 1).
5. **Obligatoriedad de Motivo en Rechazo**: Toda solicitud rechazada debe incluir obligatoriamente una justificación técnica o de riesgo.
6. **Estados Terminales**: Las solicitudes en estado `Aprobado` o `Rechazado` no pueden volver a ser procesadas.

---

## ⚙️ Variables de Entorno y Configuración

El proyecto admite variables tanto en formato JSON local (`appsettings.json` o `dotnet user-secrets`) como variables de entorno con doble guión bajo (`__`) requeridas por Linux/Render:

| Variable | Tipo / Ejemplo | Propósito |
| :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | `Production` / `Development` | Entorno de ASP.NET Core |
| `ASPNETCORE_URLS` | `http://0.0.0.0:${PORT}` | Enlace de puertos para Render |
| `ConnectionStrings__DefaultConnection` | `Data Source=app.db` o `Data Source=/var/data/app.db` | Ruta SQLite (soporte disco persistente) |
| `Redis__ConnectionString` | `redis://default:<password>@<host>:<port>` | Conexión a Redis Cloud (Caché + Sesión) |
| `RabbitMq__ConnectionString` | `amqps://<user>:<password>@<host>/<vhost>` | URI AMQPS para CloudAMQP / RabbitMQ |
| `RabbitMq__QueueName` | `solicitudes.notificaciones` | Nombre de la cola durable de Cloud MQ |
| `RabbitMq__ConsumerEnabled` | `true` / `false` | Activa/Desactiva el consumidor en segundo plano |
| `PieSocket__ClusterId` | `free.blr2` | Cluster de PieSocket para WebSockets |
| `PieSocket__ApiKey` | `<tu_api_key_piesocket>` | API Key pública de PieSocket |
| `PieSocket__Secret` | `<tu_api_secret_piesocket>` | API Secret para publicación de eventos |

---

## 🚀 Despliegue en Render.com (Paso a Paso)

### Opción A: Despliegue Automático con `render.yaml` (Blueprint)
1. Conectar el repositorio en [Render Dashboard](https://dashboard.render.com/).
2. Ir a **Blueprints** -> **New Blueprint Instance**.
3. Seleccionar el repositorio `gerfhy/plataforma-de-solicitudes-de-credito`.
4. Render detectará automáticamente el archivo [`render.yaml`](file:///c:/Users/User/Documents/Gemini%20cli%20proyects/creditos/render.yaml).
5. Completar los valores de las variables marcadas con `sync: false` (`Redis__ConnectionString`, `RabbitMq__ConnectionString`, `PieSocket__ApiKey`, `PieSocket__Secret`).
6. Hacer clic en **Apply**.

### Opción B: Creación Manual de Web Service (Docker)
1. En Render Dashboard, hacer clic en **New +** -> **Web Service**.
2. Seleccionar el repositorio desde GitHub.
3. Configuración del servicio:
   * **Name:** `plataforma-de-solicitudes-de-credito`
   * **Region:** Oregon (US West) o Frankfurt (EU)
   * **Branch:** `main` (o `deploy/render`)
   * **Runtime:** `Docker`
   * **Dockerfile Path:** `./Dockerfile`
   * **Instance Type:** `Free` o `Starter`
4. En la sección **Environment Variables**, agregar las variables de la tabla anterior.
5. **Expansión de Puerto en Render:**
   El `Dockerfile` incluye la instrucción explícita requerida para expandir la variable `$PORT` inyectada dinámicamente por Render:
   ```dockerfile
   CMD ["sh", "-c", "dotnet creditos.dll --urls http://0.0.0.0:${PORT:-8080}"]
   ```
   Adicionalmente, [`Program.cs`](file:///c:/Users/User/Documents/Gemini%20cli%20proyects/creditos/Program.cs#L10-L15) inspecciona `Environment.GetEnvironmentVariable("PORT")` para garantizar doble compatibilidad.

---

## 💾 Persistencia de SQLite en Render entre Despliegues y Reinicios

Los contenedores de Render en el plan Free poseen un sistema de archivos efímero. Para conservar la base de datos entre despliegues sucesivos y reinicios:

1. **En Plan Starter (Recomendado para Producción con Disco Persistente):**
   * En la pestaña **Disks** del Web Service en Render, agregar un nuevo disco persistente:
     * **Name:** `sqlite-data`
     * **Mount Path:** `/var/data`
     * **Size:** `1 GB`
   * Configurar la variable de entorno:
     ```env
     ConnectionStrings__DefaultConnection=Data Source=/var/data/app.db
     ```
   * El código de [`Program.cs`](file:///c:/Users/User/Documents/Gemini%20cli%20proyects/creditos/Program.cs#L17-L27) verifica automáticamente si la carpeta `/var/data` existe y la crea en caso de requerirse antes de invocar EF Core. Al estar montada en un disco persistente gestionado por Render, el archivo `app.db` permanece intacto tras cualquier redeploy o reinicio.

2. **En Plan Gratuito (Free Tier):**
   * Se utiliza `Data Source=app.db`.
   * Gracias a [`DbInitializer.SeedAsync`](file:///c:/Users/User/Documents/Gemini%20cli%20proyects/creditos/Data/DbInitializer.cs#L17), el sistema ejecuta `await context.Database.MigrateAsync()` y vuelve a inicializar de forma idempotente los roles, clientes y solicitudes de prueba en caso de reinicio de la instancia efímera.

---

## 📬 Mensajería Cloud MQ: Procedimiento de Resiliencia y Reenvío

1. **Confirmación del Publicador (*Publisher Confirms*):**
   Al crearse una solicitud, se genera un identificador persistente `MessageId` (UUID v4) y se invoca `WaitForConfirmsOrDie`. Si el broker CloudAMQP no responde o la red se interrumpe:
   * La solicitud de crédito se almacena con éxito en SQLite (no se cancela la transacción del cliente).
   * Se muestra una alerta visual al usuario advirtiendo que la notificación no pudo encolarse en ese instante.
   * Se registra el incidente en los logs del servidor con nivel `LogError` detallando el `MessageId` y `SolicitudId`.

2. **Procedimiento de Reenvío Idempotente:**
   Para reintentar el mensaje sin duplicar información:
   * El evento puede ser reenviado utilizando el **mismo `MessageId`** original.
   * El consumidor [`RabbitMqConsumerService`](file:///c:/Users/User/Documents/Gemini%20cli%20proyects/creditos/Services/RabbitMqConsumerService.cs#L130-L137) realiza una consulta de deduplicación contra la tabla `Notificaciones` (`AnyAsync(n => n.MessageId == mensaje.MessageId)`).
   * Si el mensaje ya fue procesado con anterioridad, el consumidor emite inmediatamente un `BasicAck` manual al broker y descarta la inserción redundante, protegiendo la integridad de los datos.

3. **Manejo de Cargas Inválidas (*Poison Pill*):**
   Cualquier mensaje mal formado (JSON corrupto, sin `MessageId` o con datos faltantes) es rechazado inmediatamente con `BasicReject(deliveryTag, requeue: false)`, evitando que la cola se bloquee en ciclos infinitos.

---

## 💻 Ejecución Local y Pruebas

### 1. Clonar el repositorio
```bash
git clone https://github.com/gerfhy/plataforma-de-solicitudes-de-credito.git
cd plataforma-de-solicitudes-de-credito
```

### 2. Configurar Secretos Locales (opcional si se usan variables de entorno)
```bash
dotnet user-secrets init
dotnet user-secrets set "Redis:ConnectionString" "redis://..."
dotnet user-secrets set "RabbitMq:ConnectionString" "amqps://..."
dotnet user-secrets set "PieSocket:ClusterId" "free.blr2"
dotnet user-secrets set "PieSocket:ApiKey" "tu_api_key"
dotnet user-secrets set "PieSocket:ApiSecret" "tu_api_secret"
```

### 3. Aplicar Migraciones y Ejecutar
```bash
dotnet build
dotnet run
```
Acceder en el navegador a `http://localhost:5176`.

---

## 🌿 Flujo de Ramas y Pull Requests en GitHub

| Pregunta | Rama | Pull Request | Estado |
| :--- | :--- | :--- | :--- |
| **Pregunta 1** | `feature/bootstrap-dominio` | [PR #1](https://github.com/gerfhy/plataforma-de-solicitudes-de-credito/pull/1) | ✅ Mergeado a `main` |
| **Pregunta 2** | `feature/catalogo-solicitudes` | [PR #2](https://github.com/gerfhy/plataforma-de-solicitudes-de-credito/pull/2) | ✅ Mergeado a `main` |
| **Pregunta 3** | `feature/solicitudes` | [PR #3](https://github.com/gerfhy/plataforma-de-solicitudes-de-credito/pull/3) | ✅ Mergeado a `main` |
| **Pregunta 4** | `feature/sesion-redis` | [PR #4](https://github.com/gerfhy/plataforma-de-solicitudes-de-credito/pull/4) | ✅ Mergeado a `main` |
| **Pregunta 5** | `feature/panel-analista` | [PR #5](https://github.com/gerfhy/plataforma-de-solicitudes-de-credito/pull/5) | ✅ Mergeado a `main` |
| **Pregunta 6** | `feature/websocket-notificaciones` | [PR #6](https://github.com/gerfhy/plataforma-de-solicitudes-de-credito/pull/6) | ✅ Mergeado a `main` |
| **Pregunta 7** | `feature/cloudmq-notificaciones` | [PR #7](https://github.com/gerfhy/plataforma-de-solicitudes-de-credito/pull/7) | ✅ Mergeado a `main` |
| **Pregunta 8** | `deploy/render` | [PR #8](https://github.com/gerfhy/plataforma-de-solicitudes-de-credito/pull/8) | 🚀 En Despliegue |
