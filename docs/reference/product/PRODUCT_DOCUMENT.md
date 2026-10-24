**Conciliador de Comprobantes**

Documento de producto y arquitectura del MVP

Nombre de trabajo · Versión 1.0 · Octubre 2026 · Autor: Luis Manuel De La Cruz, Desarrollador Full Stack · Estado: MVP en definición

**Contenido**

> 1\. Resumen ejecutivo
>
> 2\. Problema y oportunidad
>
> 3\. Propuesta de valor
>
> 4\. Cliente objetivo
>
> 5\. Alcance
>
> 6\. Lógica de negocio
>
> 7\. Decisiones de producto tomadas
>
> 8\. Experiencia de usuario
>
> 9\. Arquitectura
>
> 10\. Infraestructura y seguridad
>
> 11\. Plan de construcción
>
> 12\. Validación y métricas
>
> 13\. Riesgos y mitigaciones
>
> 14\. Definiciones comerciales, de datos y de retención

1\. Resumen ejecutivo

Construiremos una aplicación web instalable (PWA) que verifica y concilia comprobantes de pago por transferencia para negocios de República Dominicana. El cliente envía una captura del comprobante; el sistema extrae monto, fecha, referencia, banco y cuenta destino; la cruza con lo que el negocio esperaba cobrar y alerta sobre duplicados o señales sospechosas. El dueño obtiene un cuadre diario listo para su contador.

**Principio rector:** el sistema procesa todo; la persona solo decide las excepciones.

**Objetivo del MVP:** ponerlo frente a 3 a 5 negocios piloto y medir dos cosas: cuánto tiempo ahorran en verificar pagos y cuántos errores o irregularidades se detectan.

2\. Problema y oportunidad

En RD, gran parte de las ventas de pequeñas y medianas empresas se cobra por transferencia. El proceso actual es manual: el cliente envía una captura por WhatsApp, alguien la compara con el estado del banco, la anota en un Excel o en el sistema y la asocia a un pedido o factura.

Consecuencias:

- Tiempo perdido en verificación y digitación.

- Errores de registro y cuadres que se atrasan.

- Exposición a comprobantes falsos, editados o reutilizados.

- Poca trazabilidad de quién confirmó qué.

**Hipótesis a validar:** que este dolor sea lo bastante frecuente y costoso como para que un negocio pague por resolverlo. Aún no está confirmado. La validación consiste en unas 10 conversaciones con dueños de negocio; si 6 o 7 lo confirman como un problema real, hay mercado. También hay que mapear competidores locales, incluyendo los que solo aparecen en redes sociales.

3\. Propuesta de valor

- **Menos tiempo por pago:** la lectura del comprobante es automática; la persona solo revisa y confirma.

- **Menos errores:** cada comprobante se cruza contra un cobro esperado y contra el historial.

- **Alertas tempranas:** referencias repetidas, cuentas destino ajenas, montos o fechas inconsistentes.

- **Control y trazabilidad:** auditoría completa de cada confirmación y edición.

- **Cuadre sin trabajo extra:** resumen diario por banco y cuenta, exportable.

La app **alerta, no certifica**. La verificación definitiva contra el banco queda para la versión 1.1.

4\. Cliente objetivo

Negocios que cobran por transferencia: tiendas, distribuidores, clínicas, escuelas y servicios. No es para negocios que cobran únicamente en efectivo o con tarjeta.

Usuarios dentro de cada negocio:

- **Dueño:** configura, supervisa, resuelve alertas críticas y revisa el cuadre.

- **Verificador o cajero:** sube comprobantes, revisa y confirma.

5\. Alcance

Dentro del MVP

- Ingreso de comprobantes: carga múltiple, cámara y Share Target desde WhatsApp (Android).

- Extracción con OCR y pantalla de revisión con confianza por campo.

- Cobros esperados (a mano o por CSV), con cobro opcional.

- Estados de conciliación con su razón visible.

- Detección de duplicados y señales sospechosas.

- Cuentas del negocio, bandeja del día, cuadre diario y exportación a Excel, CSV y PDF.

- Roles (Dueño y Verificador), auditoría y notificaciones push.

- Multitenant desde el primer día.

Fuera del MVP

Bot de WhatsApp Business, importación de estados de cuenta, recordatorios de cobro, API pública y webhooks, múltiples sucursales y cierre de día.

Versión 1.1

Importación de estados de cuenta (la verificación real y probablemente el mayor argumento de venta), abonos y cuentas por cobrar por cliente, recordatorios, reportes, sucursales y cierre de día.

6\. Lógica de negocio

6.1 Modelo de dominio

Tres conceptos que no deben mezclarse:

- **Cobro:** lo que se espera recibir (cliente, monto, concepto, vencimiento).

- **Comprobante:** la evidencia subida (imagen y datos extraídos).

- **Pago:** el hecho verificado, es decir, un comprobante confirmado y aplicado.

Una **Aplicación** vincula un comprobante con uno o varios cobros y registra el monto aplicado. El cobro es **opcional**: un comprobante puede confirmarse como ingreso libre, o crear el cobro desde el propio comprobante.

6.2 Ciclo de vida del comprobante

Dos ejes independientes:

- **Procesamiento:** recibido, procesando, extraído o fallido.

- **Conciliación:** pendiente de revisión, confirmado, parcial, excedente, duplicado, sospechoso, sin match o rechazado.

Un procesamiento fallido nunca bloquea: pasa a captura manual con la imagen al lado. Nada se borra; los estados finales se **anulan** con motivo y quedan en auditoría.

6.3 Extracción y normalización

- Obligatorios para continuar: monto, fecha y banco. La referencia es necesaria para detectar duplicados.

- Normalización de montos, fechas (zona America/Santo_Domingo), banco por palabras clave y cuenta destino por últimos 4 dígitos.

- Cada campo conserva el valor original del OCR, el valor corregido y la confianza.

- Con confianza baja en monto o referencia, el comprobante va a revisión aunque exista match. El umbral es interno y no configurable en el MVP.

6.4 Motor de matching

Se puntúan los cobros abiertos del negocio con estas señales, de mayor a menor peso: código de cobro en el concepto, monto exacto (o dentro de la tolerancia), similitud entre remitente y cliente, y fecha dentro de la ventana.

- **Un candidato fuerte:** match propuesto; el usuario confirma con un toque.

- **Varios candidatos:** se muestran ordenados para elegir.

- **Ninguno:** sin match, con opciones de crear cobro, asignar a un cliente o registrar ingreso libre.

Reglas de monto: igual es confirmado; menor es parcial (queda saldo abierto); mayor es excedente (el usuario decide si es propina, crédito o error). En el MVP **nada se confirma sin una persona**. La confirmación automática será una opción posterior.

Truco de producto: generar un código corto por cobro y pedir al cliente que lo incluya en el concepto de la transferencia.

6.5 Alertas

| **Regla**                                             | **Severidad** |
|-------------------------------------------------------|---------------|
| Misma referencia, banco, monto y fecha ya registrados | Crítica       |
| Imagen idéntica a otra                                | Crítica       |
| Cuenta destino que no es del negocio                  | Crítica       |
| Imagen muy similar a otra                             | Advertencia   |
| Fecha futura, anterior al cobro o muy antigua         | Advertencia   |
| Formato de referencia inválido para el banco          | Advertencia   |
| Monto o remitente distintos a lo esperado             | Informativa   |
| Confianza baja del OCR                                | Informativa   |

El riesgo del comprobante es la severidad máxima, no la suma. Las alertas críticas solo las resuelve el Dueño. El lenguaje siempre es de alerta, nunca de veredicto.

6.6 Roles y permisos

| **Acción**                        | **Dueño** | **Verificador** |
|-----------------------------------|-----------|-----------------|
| Subir y revisar comprobantes      | Sí        | Sí              |
| Confirmar o rechazar              | Sí        | Sí              |
| Resolver alertas críticas         | Sí        | No              |
| Revertir un estado final          | Sí        | No              |
| Crear y editar cobros             | Sí        | Sí              |
| Cuentas, usuarios y configuración | Sí        | No              |
| Cuadre                            | Completo  | Solo el del día |

6.7 Auditoría

Registro inmutable (solo se agrega) con quién, qué, cuándo, valor anterior, valor nuevo y motivo. Cubre confirmaciones, ediciones de campos, resolución de alertas, anulaciones y exportaciones.

6.8 Cuadre diario

Se agrupa por **fecha del pago**, no por la de subida. Muestra confirmado, pendiente, sospechoso y rechazado, por banco y cuenta, más los cobros aún abiertos. En el MVP es una vista calculada, sin cierre de día.

6.9 Multitenancy

Cada tabla lleva el identificador del negocio, protegido con Row Level Security de PostgreSQL. Las restricciones de unicidad se acotan por negocio, el almacenamiento se separa por rutas y la configuración (ventana de fecha, tolerancia de monto, tope del verificador) es por negocio.

7\. Decisiones de producto tomadas

1.  **El cobro es opcional** desde el MVP.

2.  **Tope de monto para el verificador:** ajuste opcional, vacío por defecto (sin límite).

3.  **Ventana de fecha y tolerancia de monto:** configurables por negocio, con valores por defecto (7 días y RD\$0).

4.  **Cuadre sin cierre de día** en el MVP; el cierre llega en la versión 1.1.

8\. Experiencia de usuario

- **Bandeja por acción, no por estado:** “Necesita tu decisión”, “Procesando” y “Resuelto hoy”.

- **Camino feliz en un toque:** compartir desde WhatsApp, ver el comprobante con match sugerido y confirmar.

- **Pantalla de revisión:** imagen con zoom a un lado, campos editables al otro, razón del match o alerta arriba y un solo botón primario.

- **Acciones masivas** para confirmar varios comprobantes de match fuerte sin alertas.

- **Deshacer** durante 5 segundos tras confirmar.

- **Razones en lenguaje claro** y estados de carga honestos (“Procesando 3 de 8”).

- **Móvil primero** para el cajero; escritorio para cuadre y reportes del dueño.

- **Onboarding de 2 minutos** con un comprobante de ejemplo.

- **Notificaciones solo cuando exigen acción:** alertas críticas, revisiones atrasadas y fallos de procesamiento.

9\. Arquitectura

Monolito modular con un servicio de OCR y un worker. Es lo más simple que se puede operar con un solo desarrollador y no cierra puertas.

- **API NestJS** con módulos: auth, tenants, cuentas, clientes, cobros, comprobantes, extracción, matching, alertas, auditoría, reportes y notificaciones.

- **Worker NestJS (BullMQ)** que orquesta: llama al OCR, parsea, hace matching y genera alertas. Comparte código con la API.

- **Servicio OCR** en Python con FastAPI y PaddleOCR: recibe una imagen y devuelve texto con coordenadas, confianza y hash perceptual. No contiene reglas de negocio.

- **Parsers por banco** en TypeScript, como funciones puras y versionadas, probadas con capturas reales.

- **Interfaz de extractor de visión** preparada para un modelo de respaldo cuando la confianza sea baja; se activa solo si las pruebas con 50 capturas reales lo justifican.

Stack tecnológico

| **Capa**       | **Tecnología**                                                                |
|----------------|-------------------------------------------------------------------------------|
| Backend        | NestJS (Fastify), TypeScript, Zod, Drizzle ORM                                |
| Autenticación  | Passport, JWT de acceso corto, refresh en cookie httpOnly, argon2             |
| Cola           | BullMQ y Redis                                                                |
| Imágenes       | sharp, SHA-256 al subir, hash perceptual en el servicio OCR                   |
| Base de datos  | PostgreSQL 16 con RLS, pg_trgm, dinero en numeric(14,2) con moneda            |
| Frontend       | Vite, React, TypeScript, vite-plugin-pwa (Workbox)                            |
| UI y datos     | Tailwind, shadcn/ui, TanStack Query, TanStack Router y Table, React Hook Form |
| Offline        | Dexie (IndexedDB) y Background Sync                                           |
| Notificaciones | Web Push (Firebase FCM)                                                       |
| Exportación    | exceljs y pdfmake                                                             |

10\. Infraestructura y seguridad

- **Un VPS con Docker Compose** (4 vCPU y 8 GB de RAM como punto de partida) en una región del este de EE. UU. Contenedores: Caddy, API, worker, OCR, PostgreSQL y Redis.

- **Cloudflare** para DNS y protección básica.

- **Imágenes** en un bucket S3 compatible y privado (Cloudflare R2 o Backblaze B2), con URLs firmadas de corta duración.

- **Backups:** volcado nocturno cifrado a otro bucket, con una restauración probada antes del primer piloto.

- **CI/CD:** GitHub Actions construye las imágenes y el servidor las despliega con Docker Compose.

- **Observabilidad:** Sentry, monitor de disponibilidad y bull-board para la cola.

- **Repositorio:** monorepo con pnpm y Turborepo (web, api, ocr y paquete compartido de esquemas).

- **Costo orientativo:** del orden de US\$20 a US\$40 al mes; verificar precios vigentes.

**Seguridad y datos:** los comprobantes contienen información financiera personal. Mínimos obligatorios: bucket privado, EXIF removido, HTTPS, auditoría de accesos, política de retención y revisión de la Ley 172-13 de protección de datos personales antes de operar con clientes reales. Los términos de uso deben decir claramente que la app alerta y no certifica.

11\. Plan de construcción

Estimación inicial: de 6 a 8 semanas con dedicación completa, a recalibrar al terminar la fase 2.

1.  **Base:** monorepo, auth, tenants con RLS y pruebas de aislamiento.

2.  **Pipeline de imágenes:** subida, storage, cola y servicio OCR con salida cruda.

3.  **Parsers:** dos bancos primero, con fixtures reales; luego el resto.

4.  **Núcleo de negocio:** cobros, comprobantes, motor de matching y estados.

5.  **Control:** alertas, auditoría y pantalla de revisión.

6.  **Operación:** bandeja, cuadre y exportación.

7.  **PWA:** Share Target, cola offline y notificaciones push.

8.  **Piloto:** onboarding de 3 a 5 negocios y medición.

12\. Validación y métricas

Antes de construir: 10 entrevistas y una prueba de PaddleOCR (y un modelo de visión como comparación) con unas 50 capturas reales de distintos bancos.

Métricas del piloto:

- Porcentaje de campos críticos extraídos correctamente sin edición (meta inicial a fijar con las 50 capturas).

- Porcentaje de comprobantes resueltos con un solo toque.

- Tiempo promedio de verificación por pago, antes y después.

- Alertas útiles frente a falsos positivos.

- Retención de los negocios piloto al final de las primeras semanas.

13\. Riesgos y mitigaciones

| **Riesgo**                                                       | **Mitigación**                                                                |
|------------------------------------------------------------------|-------------------------------------------------------------------------------|
| Precisión del OCR variable entre bancos y capturas               | Pruebas con datos reales, revisión humana y modelo de visión como respaldo    |
| Cambios de diseño en las apps bancarias                          | Parsers versionados con fixtures y alerta ante caída de confianza             |
| Interpretar la app como garantía antifraude                      | Lenguaje de alerta, términos claros y verificación bancaria en 1.1            |
| iPhone sin Share Target y con push solo si la PWA está instalada | Subir desde galería o cámara; verificar el soporte actual antes de prometerlo |
| Los negocios no tienen el hábito de registrar cobros             | Cobro opcional y creación desde el comprobante                                |
| Fuga de datos entre negocios                                     | RLS, pruebas de aislamiento y roles de base de datos sin privilegios de dueño |
| Competencia local no mapeada                                     | Investigación previa, incluyendo redes sociales                               |
| Dependencia de un solo desarrollador                             | Alcance acotado, documentación y arquitectura simple                          |

14\. Definiciones comerciales, de datos y de retención

Este bloque cierra los pendientes de la versión anterior. Todo lo comercial son **hipótesis** a contrastar en las entrevistas y el piloto; ningún número tiene datos de mercado detrás.

14.1 Modelo de precios y de cobro

Suscripción mensual por negocio, en pesos dominicanos, con planes por volumen de comprobantes. Se cobra por volumen y no por usuario, porque el valor crece con los pagos verificados y un negocio con un solo cajero no debería pagar menos por usar menos asientos.

| **Plan** | **Precio mensual (RD\$)** | **Comprobantes al mes** | **Usuarios** | **Incluye**                                                       |
|----------|---------------------------|-------------------------|--------------|-------------------------------------------------------------------|
| Esencial | 1,500                     | Hasta 300               | 2            | Bandeja, revisión, alertas, cuadre y exportación                  |
| Negocio  | 3,500                     | Hasta 1,500             | 5            | Lo anterior, cobros por CSV, varias cuentas y soporte prioritario |

Un plan Pro (más volumen, estados de cuenta, API) se define cuando exista la versión 1.1.

- **Piloto:** 60 días gratis para 3 a 5 negocios, a cambio de capturas reales (anonimizadas para pruebas), retroalimentación semanal y permiso para usarlos como caso de éxito. Después, 30% de descuento durante 12 meses como precio de fundador.

- **Excedente de volumen:** RD\$3 por comprobante adicional, hasta alcanzar el precio del plan siguiente.

- **Sin permanencia:** se cancela cuando quieran y se incluye la exportación de sus datos.

- **Cobro:** mensual anticipado, por transferencia o tarjeta. Las transferencias de nuestros propios clientes se concilian con la propia app. La pasarela de tarjeta se elige después de comparar proveedores locales (disponibilidad y comisiones por verificar).

- **Facturación:** para cobrar hay que estar formalizado ante la DGII y emitir comprobante fiscal; confirmarlo con un contador antes del primer cobro.

- **Economía unitaria orientativa:** el costo variable por negocio es casi nulo con OCR propio. Con una infraestructura de US\$20 a US\$40 al mes, dos clientes del plan Esencial la cubren; el costo real es soporte e implantación.

- **Cómo validarlo:** en las 10 entrevistas, preguntar cuántas horas al día se dedican a verificar pagos y qué precio mensual les parecería tan bajo que dudarían, razonable, caro y demasiado caro. Ajustar la tabla antes del lanzamiento.

14.2 Nombre comercial

Recomendación: **Cuadra**. “Cuadre” es el término cotidiano en RD para cerrar y verificar las cuentas del día; el verbo es corto, se recuerda y lo entienden el dueño y el cajero. Eslogan tentativo: “Cuadra tus pagos sin perseguir capturas”.

Alternativas: **Cuadre.do** (el dominio .do refuerza el origen local) y **ConfirmaPago** (más descriptivo, menos memorable).

Una búsqueda rápida no encontró un producto dominicano con ese nombre, pero eso no es una verificación. Antes de comprometerlo hay que revisar la marca en ONAPI, la disponibilidad de dominios .do y .com, y los nombres en las tiendas de aplicaciones y redes sociales.

14.3 Bancos y cuentas prioritarias para los parsers

Criterio de partida: participación en los activos del sistema financiero a febrero de 2026, según [El Día](https://eldia.com.do/sistema-financiero-supera-los-rd4-18-millones-en-activos/): Banreservas 30.7%, Banco Popular 22.7%, Banco BHD 15.3%, Banco Santa Cruz 5.3%, APAP 4.5%, Scotiabank 4.3% y Asociación Cibao 2.7%. Es un indicador indirecto; la prioridad real la dictan los bancos que usan los clientes de los negocios piloto, y se ajusta con las 50 capturas de prueba.

| **Oleada** | **Bancos**                                                                   | **Cuándo**                                  |
|------------|------------------------------------------------------------------------------|---------------------------------------------|
| 1          | Banreservas, Banco Popular, Banco BHD (juntos 68.7% de los activos)          | MVP                                         |
| 2          | Banco Santa Cruz, APAP, Scotiabank, Asociación Cibao (relevante en Santiago) | MVP si los pilotos los usan; si no, 1.1     |
| 3          | Promerica, Caribe, Banesco, Vimenca, López de Haro, Ademi, Lafise y otros    | Bajo demanda; captura manual mientras tanto |

- **Comprobantes soportados en el MVP:** captura de la app móvil del banco y captura o PDF de la banca por internet, para transferencias dentro del mismo banco e interbancarias. Quedan fuera los mensajes SMS o de correo, y las fotos de papeles o vouchers.

- **Antes de escribir un parser:** al menos 10 capturas reales por banco, que incluyan transferencia interna, interbancaria y tema claro y oscuro.

- **Criterio de aceptación inicial:** al menos 90% de monto, fecha y referencia correctos sobre ese conjunto de prueba (meta a recalibrar con los datos).

- **Cuentas:** cada negocio registra banco, últimos 4 dígitos y moneda en el onboarding, empezando por su cuenta principal de cobro. El modelo soporta cuentas en USD; la verificación se concentra primero en DOP.

14.4 Política de retención de imágenes y datos

Principios: minimizar lo que se guarda, el negocio es dueño de sus datos y nada se conserva sin un propósito. Los plazos son una propuesta, no asesoría legal; deben revisarse con un abogado a la luz de la Ley 172-13 antes de operar con clientes.

| **Dato**                            | **Retención por defecto**          | **Al vencer**                                                            |
|-------------------------------------|------------------------------------|--------------------------------------------------------------------------|
| Imagen original del comprobante     | 24 meses; el Dueño puede ampliarla | Se elimina el archivo y se conservan los datos extraídos                 |
| Datos extraídos y pagos confirmados | Mientras el contrato esté activo   | Exportación y eliminación tras la cancelación                            |
| JSON crudo del OCR                  | 12 meses                           | Se elimina                                                               |
| Hash de imagen y referencia         | Junto con los datos extraídos      | Se conservan para detectar duplicados; no permiten reconstruir la imagen |
| Registro de auditoría               | Contrato activo más 12 meses       | Se elimina o anonimiza                                                   |
| Logs técnicos                       | 90 días                            | Se eliminan                                                              |
| Copias de seguridad                 | 30 días en rotación                | Se sobrescriben; las eliminaciones se propagan al rotar                  |
| Cuenta cancelada                    | 30 días para exportar              | Eliminación total a los 90 días                                          |

- Algunos negocios deben conservar soportes contables por varios años; por eso la retención de imágenes es configurable. Confirmar plazos con el contador de cada piloto.

- Una tarea nocturna purga lo vencido y marca la fecha de eliminación en el registro.

- Un negocio puede pedir la eliminación de un comprobante concreto: se purga la imagen y queda el evento de auditoría.

- Los accesos a imágenes se registran; en campos de datos solo se guardan los últimos 4 dígitos de cuentas (la imagen puede mostrar más, de ahí la retención limitada y el acceso restringido).

- Las imágenes de clientes no se usan para entrenar ni afinar modelos sin consentimiento escrito.

14.5 Modelo de datos

El esquema completo (tablas, claves, índices, triggers, vistas y políticas RLS) está en el archivo cuadra_schema.sql, para PostgreSQL 16. Es la base del prompt para el agente de código.

| **Tabla**                | **Propósito**                                                     | **Relaciones**                      | **Índices clave**                                                                                                 |
|--------------------------|-------------------------------------------------------------------|-------------------------------------|-------------------------------------------------------------------------------------------------------------------|
| bancos                   | Catálogo global con la oleada de parsers                          | —                                   | código único                                                                                                      |
| usuarios, sesiones       | Identidad global y tokens de refresco                             | sesiones → usuarios                 | email único; hash de token único                                                                                  |
| tenants, tenant_config   | Negocio y su configuración (ventana, tolerancia, tope, retención) | config → tenants, 1 a 1             | —                                                                                                                 |
| membresias               | Rol del usuario en cada negocio                                   | → tenants, usuarios                 | PK (negocio, usuario)                                                                                             |
| dispositivos_push        | Tokens de notificación (FCM o Web Push)                           | → membresias                        | (proveedor, token) único                                                                                          |
| cuentas_negocio          | Cuentas propias del negocio                                       | → bancos                            | único por banco, últimos 4 y moneda                                                                               |
| clientes, clientes_alias | Clientes y nombres con que aparecen en las transferencias         | alias → clientes                    | búsqueda por similitud (trigramas)                                                                                |
| cobros                   | Lo que se espera recibir                                          | → clientes                          | código único por negocio; índice parcial de abiertos                                                              |
| comprobantes             | Evidencia, estados y datos vigentes                               | → cuentas_negocio, clientes, bancos | bandeja, cuadre, duplicados por campos, SHA-256, hash perceptual reciente, retención; clave de idempotencia única |
| extracciones             | Salida original del OCR por intento                               | → comprobantes                      | por comprobante y fecha                                                                                           |
| aplicaciones             | Comprobante aplicado a un cobro, con monto                        | → comprobantes, cobros              | única vigente por par                                                                                             |
| alertas                  | Reglas disparadas por comprobante                                 | → comprobantes                      | única abierta por regla; abiertas por severidad                                                                   |
| eventos_auditoria        | Registro inmutable de cambios                                     | → tenants, usuarios                 | por entidad y por tiempo                                                                                          |

**Aislamiento entre negocios.** Todas las tablas de negocio tienen RLS activo y forzado. El backend fija negocio, usuario y rol en cada transacción. Además, las relaciones usan claves compuestas (negocio, id), así que la base de datos impide enlazar datos de dos negocios aunque el código falle.

**Reglas que la base de datos también hace cumplir:**

- Solo el Dueño revierte un estado final y resuelve alertas críticas.

- El verificador respeta el tope de monto y no confirma con alertas críticas abiertas.

- Lo aplicado a cobros no excede el monto del comprobante y comparte su moneda.

- La auditoría solo se agrega; la única vía de borrado es el rol de mantenimiento, para la purga por retención.

- Los negocios se crean únicamente con la función de alta, que deja la auditoría y la membresía del Dueño.

**Dos decisiones de diseño a tener presentes:**

- Los usuarios son globales y se vinculan a los negocios por membresías; así, un contador podrá atender varios negocios en el futuro.

- El índice de referencia duplicada no es único a propósito, para permitir que el Dueño “confirme igual” una alerta, con motivo y auditoría.

**Validación.** El esquema se ejecutó completo en PostgreSQL 16 y se probaron el aislamiento entre negocios, el rechazo de escrituras y referencias cruzadas, los permisos del verificador, la auditoría de solo escritura, el límite de aplicaciones y las vistas. No incluye pruebas de carga ni de la capa de aplicación.

14.6 Pendientes que siguen abiertos

- Contrastar los precios con las 10 entrevistas y el piloto.

- Verificar el nombre en ONAPI, dominios y tiendas de aplicaciones.

- Revisión legal de la retención y los términos de uso (Ley 172-13) y formalización ante la DGII para poder facturar.

- Reunir las 50 capturas reales y medir la precisión del OCR por banco.

- Elegir la pasarela de cobro con tarjeta.

- Armar el prompt detallado para el agente de código.
