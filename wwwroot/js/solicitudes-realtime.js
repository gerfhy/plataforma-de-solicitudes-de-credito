/**
 * Plataforma de Créditos - Cliente WebSocket en Tiempo Real con PieSocket y ASP.NET Hub
 * Proveedor WebSocket: PieSocket (https://piehost.com) + Hub local (/hubs/solicitudes)
 * Manejo de eventos: SolicitudEstadoActualizado, aislamiento por canal de usuario y reconexión automática sincronizada.
 */

(function () {
    "use strict";

    // Referencias de UI
    const statusBadge = document.getElementById("ws-status-badge");
    const toastContainer = document.getElementById("ws-toast-container");

    let pieSocketWs = null;
    let signalRConnection = null;
    let ultimoEventoId = null;
    let ultimoEventoTimestamp = 0;

    // Actualizar indicador de estado en la barra superior
    function actualizarEstadoConexion(estado, proveedor) {
        if (!statusBadge) return;

        const nombreProveedor = proveedor || "PieSocket";

        switch (estado) {
            case "conectado":
                statusBadge.className = "badge rounded-pill bg-success-subtle text-success border border-success-subtle px-2 py-1 d-inline-flex align-items-center gap-1";
                statusBadge.innerHTML = `<span class="status-dot bg-success"></span><span>${nombreProveedor} En Vivo</span>`;
                statusBadge.title = `Conectado al servidor WebSocket en tiempo real vía ${nombreProveedor}`;
                break;
            case "reconectando":
                statusBadge.className = "badge rounded-pill bg-warning-subtle text-warning-emphasis border border-warning-subtle px-2 py-1 d-inline-flex align-items-center gap-1";
                statusBadge.innerHTML = '<span class="spinner-border spinner-border-sm" style="width: 8px; height: 8px;"></span><span>Reconectando...</span>';
                statusBadge.title = "Reconectando con el broker WebSocket...";
                break;
            case "desconectado":
            default:
                statusBadge.className = "badge rounded-pill bg-danger-subtle text-danger border border-danger-subtle px-2 py-1 d-inline-flex align-items-center gap-1";
                statusBadge.innerHTML = '<span class="status-dot bg-danger"></span><span>Desconectado</span>';
                statusBadge.title = "Sin conexión WebSocket en tiempo real";
                break;
        }
    }

    // Renderizar aviso tipo Toast moderno flotante
    function mostrarAviso(solicitudId, estado, motivoRechazo) {
        if (!toastContainer) return;

        const isAprobado = (estado || "").toLowerCase() === "aprobado";
        const toastId = "toast-solicitud-" + Date.now();
        const borderColor = isAprobado ? "var(--brand-neon-green)" : "var(--brand-coral)";
        const bgIcon = isAprobado ? "bi-check-circle-fill text-success" : "bi-x-octagon-fill text-danger";
        const titulo = isAprobado ? `¡Solicitud #${solicitudId} APROBADA!` : `Solicitud #${solicitudId} RECHAZADA`;
        const detalle = isAprobado 
            ? "Tu crédito ha sido aprobado satisfactoriamente por el analista de riesgo."
            : `Motivo: ${motivoRechazo || "No cumple con las políticas crediticias actuales."}`;

        const toastHtml = `
            <div id="${toastId}" class="toast align-items-center text-bg-light border shadow-lg show mb-3" 
                 role="alert" aria-live="assertive" aria-atomic="true" 
                 style="border-left: 5px solid ${borderColor} !important; border-radius: 12px; min-width: 320px; max-width: 420px; animation: slideInRight 0.35s ease-out;">
                <div class="d-flex p-3">
                    <div class="me-3 fs-3">
                        <i class="bi ${bgIcon}"></i>
                    </div>
                    <div class="flex-grow-1">
                        <div class="d-flex align-items-center justify-content-between">
                            <h6 class="mb-1 fw-bold text-dark" style="font-size: 0.95rem;">${titulo}</h6>
                            <small class="text-secondary">Ahora</small>
                        </div>
                        <p class="mb-2 text-secondary" style="font-size: 0.85rem; line-height: 1.35;">${detalle}</p>
                        <div>
                            <a href="/Solicitudes/Details/${solicitudId}" class="btn-sleek-dark py-1 px-2" style="font-size: 0.78rem;">
                                <i class="bi bi-eye me-1"></i> Ver Detalle de Solicitud
                            </a>
                        </div>
                    </div>
                    <button type="button" class="btn-close ms-2" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
            </div>
        `;

        toastContainer.insertAdjacentHTML("beforeend", toastHtml);

        // Auto cerrar a los 12 segundos
        setTimeout(() => {
            const el = document.getElementById(toastId);
            if (el) {
                el.classList.remove("show");
                setTimeout(() => el.remove(), 400);
            }
        }, 12000);
    }

    // Actualizar fila en la tabla de "Mis Solicitudes" (Index)
    function actualizarFilaCatalogo(data) {
        if (!data || !data.solicitudId) return;

        const row = document.getElementById(`solicitud-row-${data.solicitudId}`);
        if (!row) return;

        const badge = document.getElementById(`status-badge-${data.solicitudId}`);
        const motivoSpan = document.getElementById(`motivo-span-${data.solicitudId}`);

        const isAprobado = (data.estado || "").toLowerCase() === "aprobado";

        if (badge) {
            badge.className = isAprobado ? "status-pill aprobado" : "status-pill rechazado";
            badge.textContent = isAprobado ? "Aprobado" : "Rechazado";
        }

        if (motivoSpan) {
            if (!isAprobado && data.motivoRechazo) {
                motivoSpan.innerHTML = `<span class="text-danger small"><i class="bi bi-info-circle me-1"></i>${data.motivoRechazo}</span>`;
            } else {
                motivoSpan.innerHTML = '<span class="text-muted small">-</span>';
            }
        }

        // Efecto visual de resaltado
        row.classList.add("row-updated-highlight");
        setTimeout(() => {
            row.classList.remove("row-updated-highlight");
        }, 4000);
    }

    // Actualizar vista de Detalle si coincide con la solicitud abierta
    function actualizarVistaDetalle(data) {
        if (!data || !data.solicitudId) return;

        const detailCard = document.getElementById("solicitud-detail-card");
        if (!detailCard) return;

        const currentId = detailCard.getAttribute("data-solicitud-id");
        if (currentId != data.solicitudId) return;

        const isAprobado = (data.estado || "").toLowerCase() === "aprobado";

        // Actualizar pill principal
        const pillHeader = document.getElementById("detail-status-pill");
        if (pillHeader) {
            pillHeader.className = isAprobado ? "status-pill aprobado fs-6 px-3 py-2" : "status-pill rechazado fs-6 px-3 py-2";
            pillHeader.textContent = isAprobado ? "Aprobado" : "Rechazado";
        }

        // Actualizar tabla informativa
        const tableEstado = document.getElementById("detail-table-estado");
        if (tableEstado) {
            tableEstado.textContent = data.estado;
        }

        // Manejar alerta de motivo de rechazo
        const motivoContainer = document.getElementById("detail-motivo-container");
        if (motivoContainer) {
            if (!isAprobado && data.motivoRechazo) {
                motivoContainer.innerHTML = `
                    <div class="alert-sleek alert-sleek-danger my-3">
                        <i class="bi bi-x-octagon-fill fs-4"></i>
                        <div>
                            <div class="fw-bold">Motivo de Rechazo por el Analista:</div>
                            <div>${data.motivoRechazo}</div>
                        </div>
                    </div>
                `;
            } else if (isAprobado) {
                motivoContainer.innerHTML = "";
            }
        }

        // Resaltar tarjeta
        detailCard.classList.add("card-updated-highlight");
        setTimeout(() => {
            detailCard.classList.remove("card-updated-highlight");
        }, 4000);
    }

    // Procesar evento unificado evitando duplicados entre ambos transportes
    function procesarEventoEstado(data, origen) {
        if (!data || !data.solicitudId) return;

        const claveEvento = `${data.solicitudId}_${data.estado}`;
        const ahora = Date.now();

        if (ultimoEventoId === claveEvento && (ahora - ultimoEventoTimestamp) < 2500) {
            console.log(`[WebSocket] Evento ${claveEvento} ya procesado recientemente (${origen}). Ignorando duplicado.`);
            return;
        }

        ultimoEventoId = claveEvento;
        ultimoEventoTimestamp = ahora;

        console.info(`>> Evento WebSocket procesado desde [${origen}]: SolicitudEstadoActualizado`, data);
        mostrarAviso(data.solicitudId, data.estado, data.motivoRechazo);
        actualizarFilaCatalogo(data);
        actualizarVistaDetalle(data);
    }

    // Sincronización al reconectar: consultar estado vigente al servidor
    async function sincronizarEstadoVigente() {
        console.log("WebSocket: Iniciando sincronización de estado vigente desde el servidor...");

        const detailCard = document.getElementById("solicitud-detail-card");
        if (detailCard) {
            const currentId = detailCard.getAttribute("data-solicitud-id");
            if (currentId) {
                try {
                    const response = await fetch(`/Solicitudes/EstadoActual/${currentId}`);
                    if (response.ok) {
                        const data = await response.json();
                        actualizarVistaDetalle(data);
                    }
                } catch (err) {
                    console.error("Error al sincronizar estado de detalle:", err);
                }
            }
        }

        const tablaSolicitudes = document.getElementById("tabla-solicitudes-catalogo");
        if (tablaSolicitudes) {
            try {
                const response = await fetch("/Solicitudes/ResumenMisSolicitudes");
                if (response.ok) {
                    const lista = await response.json();
                    lista.forEach(item => {
                        actualizarFilaCatalogo(item);
                    });
                }
            } catch (err) {
                console.error("Error al sincronizar resumen de solicitudes:", err);
            }
        }
    }

    // Iniciar conexión con PieSocket (piehost.com)
    async function iniciarConexionPieSocket() {
        try {
            const configResp = await fetch("/Solicitudes/WebSocketConfig");
            if (!configResp.ok) {
                console.warn("Usuario no autenticado para PieSocket (HTTP " + configResp.status + ")");
                actualizarEstadoConexion("desconectado");
                return;
            }

            const config = await configResp.json();
            if (!config.wsUrl || !config.apiKey) {
                console.warn("Configuración de PieSocket no disponible en servidor.");
                return;
            }

            console.info("Iniciando conexión WebSocket a PieSocket en canal:", config.canal);

            pieSocketWs = new WebSocket(config.wsUrl);

            pieSocketWs.onopen = function () {
                console.info(">> WebSocket conectado exitosamente a PieSocket (https://piehost.com)");
                actualizarEstadoConexion("conectado", "PieSocket");
            };

            pieSocketWs.onmessage = function (event) {
                try {
                    const rawData = JSON.parse(event.data);
                    if (rawData.event === "SolicitudEstadoActualizado") {
                        const data = typeof rawData.data === "string" ? JSON.parse(rawData.data) : rawData.data;
                        procesarEventoEstado(data, "PieSocket");
                    }
                } catch (e) {
                    console.log("Mensaje de PieSocket recibido:", event.data);
                }
            };

            pieSocketWs.onclose = function () {
                console.warn("Conexión PieSocket cerrada. Intentando reconectar...");
                actualizarEstadoConexion("reconectando", "PieSocket");
                setTimeout(iniciarConexionPieSocket, 3500);
            };

            pieSocketWs.onerror = function (err) {
                console.warn("Error en WebSocket de PieSocket:", err);
            };

        } catch (err) {
            console.error("Error al obtener configuración de PieSocket:", err);
        }
    }

    // Iniciar conexión con SignalR (/hubs/solicitudes)
    async function iniciarConexionSignalR() {
        if (typeof signalR === "undefined") return;

        try {
            signalRConnection = new signalR.HubConnectionBuilder()
                .withUrl("/hubs/solicitudes", {
                    transport: signalR.HttpTransportType.WebSockets
                })
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .configureLogging(signalR.LogLevel.Information)
                .build();

            signalRConnection.on("SolicitudEstadoActualizado", (data) => {
                procesarEventoEstado(data, "SignalR Hub");
            });

            signalRConnection.onreconnecting(() => {
                actualizarEstadoConexion("reconectando", "WebSocket");
            });

            signalRConnection.onreconnected(async () => {
                actualizarEstadoConexion("conectado", "PieSocket");
                await sincronizarEstadoVigente();
            });

            await signalRConnection.start();
            console.info(">> Hub SignalR conectado exitosamente a /hubs/solicitudes");
            actualizarEstadoConexion("conectado", "PieSocket");
        } catch (err) {
            console.warn("Hub SignalR local no disponible o no autenticado:", err);
        }
    }

    // Iniciar conexiones al cargar la página
    async function iniciar() {
        await Promise.all([
            iniciarConexionPieSocket(),
            iniciarConexionSignalR()
        ]);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", iniciar);
    } else {
        iniciar();
    }

    // Exportar para pruebas
    window.SolicitudesRealtime = {
        sincronizarEstadoVigente: sincronizarEstadoVigente,
        actualizarEstadoConexion: actualizarEstadoConexion
    };
})();
