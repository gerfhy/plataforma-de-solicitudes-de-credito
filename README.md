# Plataforma de Gestión de Solicitudes de Crédito 💳

Sistema web interno desarrollado con **ASP.NET Core MVC (.NET 10)** para la gestión y evaluación de solicitudes de crédito bajo reglas de negocio automatizadas, mensajería asíncrona y notificaciones en tiempo real.

---

## 🛠️ Stack Tecnológico
* **Backend:** ASP.NET Core MVC (.NET 10)
* **Autenticación y Autorización:** ASP.NET Core Identity
* **Base de Datos:** Entity Framework Core + SQLite
* **Sesión y Caché:** Redis Cloud / Redis Labs
* **Notificaciones en Tiempo Real:** WebSockets (ASP.NET Hub en `/hubs/solicitudes` + PieSocket)
* **Mensajería Asíncrona (Cloud MQ):** RabbitMQ gestionado en CloudAMQP
* **Despliegue:** Render.com (Web Service)

---

## 🎨 Diseño y Experiencia de Usuario (UI/UX)
* Interfaz basada en diseño moderno fintech neumórfico / minimalista.
* Tipografía: **Plus Jakarta Sans**.
* Paleta: Fondo sage/pizarra suave, tarjetas con radio redondeado grande, acentos verde neón, coral y ámbar para estados de crédito.

---

## 🚀 Requisitos de Ejecución Local
* [.NET 10 SDK](https://dotnet.microsoft.com/)
* SQLite (incluido por EF Core)
* Instancia de Redis (opcional en local o Redis Cloud)
* Instancia de CloudAMQP (opcional para mensajería en local)

---

## 🌿 Flujo de Ramas (Git Workflow)
Cada pregunta del examen se resuelve en una rama individual con su respectivo Pull Request hacia `main`:
1. `feature/bootstrap-dominio`: Bootstrap + Modelo de datos y Seed inicial.
2. `feature/catalogo-solicitudes`: Catálogo "Mis solicitudes" y filtros.
3. `feature/solicitudes`: Formulario de registro y validaciones de negocio.
4. `feature/sesion-redis`: Sesión y Caché con Redis.
5. `feature/panel-analista`: Panel de analista de riesgo y evaluación de créditos.
6. `feature/websocket-notificaciones`: Notificaciones en tiempo real vía WebSocket.
7. `feature/cloudmq-notificaciones`: Mensajería asíncrona con RabbitMQ (CloudAMQP).
8. `deploy/render`: Configuración y despliegue en Render.
