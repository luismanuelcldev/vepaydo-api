-- =====================================================================
-- Cuadra (Conciliador de Comprobantes) - Esquema PostgreSQL 16 - MVP
-- Version 1.0 - Octubre 2026
--
-- Convenciones
--  * Todo dato de un negocio lleva tenant_id y esta protegido con RLS.
--  * Las relaciones entre tablas de negocio usan claves compuestas
--    (tenant_id, id): la base de datos impide referencias entre negocios.
--  * Dinero: numeric(14,2) + moneda. Fechas con zona: timestamptz.
--  * Nada se borra: los registros se anulan (con motivo y auditoria).
--
-- Contexto por transaccion (lo establece el backend en CADA transaccion):
--   BEGIN;
--   SELECT set_config('app.tenant_id',  '<uuid>', true),
--          set_config('app.usuario_id', '<uuid>', true),
--          set_config('app.rol',        'dueno' | 'verificador' | 'sistema', true);
--   ... consultas ...
--   COMMIT;
-- (true = local a la transaccion; funciona con PgBouncer en modo transaccion)
--
-- Ejecutar con un rol propietario (migraciones). La aplicacion se conecta
-- como cuadra_app, que NO es propietario ni tiene BYPASSRLS.
-- =====================================================================

BEGIN;

CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS citext;

CREATE SCHEMA IF NOT EXISTS app;

-- ---------------------------------------------------------------------
-- 0. Roles (asignar LOGIN y contrasena fuera de este archivo)
-- ---------------------------------------------------------------------
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'cuadra_app') THEN
    CREATE ROLE cuadra_app NOLOGIN NOSUPERUSER NOBYPASSRLS;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'cuadra_mantenimiento') THEN
    -- Solo para la tarea de purga por retencion y cancelaciones. No lo usa la API.
    CREATE ROLE cuadra_mantenimiento NOLOGIN NOSUPERUSER BYPASSRLS;
  END IF;
END $$;

-- ---------------------------------------------------------------------
-- 1. Funciones de contexto
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION app.tenant_actual() RETURNS uuid
LANGUAGE sql STABLE AS $$
  SELECT NULLIF(current_setting('app.tenant_id', true), '')::uuid
$$;

CREATE OR REPLACE FUNCTION app.usuario_actual() RETURNS uuid
LANGUAGE sql STABLE AS $$
  SELECT NULLIF(current_setting('app.usuario_id', true), '')::uuid
$$;

CREATE OR REPLACE FUNCTION app.rol_actual() RETURNS text
LANGUAGE sql STABLE AS $$
  SELECT NULLIF(current_setting('app.rol', true), '')
$$;

-- Distancia de Hamming entre dos hashes perceptuales de 64 bits.
CREATE OR REPLACE FUNCTION app.distancia_hamming(a bigint, b bigint) RETURNS integer
LANGUAGE sql IMMUTABLE PARALLEL SAFE AS $$
  SELECT bit_count((a # b)::bit(64))::integer
$$;

CREATE OR REPLACE FUNCTION app.set_actualizado_en() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  NEW.actualizado_en := now();
  RETURN NEW;
END $$;

-- ---------------------------------------------------------------------
-- 2. Tipos
-- ---------------------------------------------------------------------
CREATE TYPE rol_usuario          AS ENUM ('dueno', 'verificador');
CREATE TYPE estado_procesamiento AS ENUM ('recibido', 'procesando', 'extraido', 'fallido');
CREATE TYPE estado_conciliacion  AS ENUM ('pendiente_revision', 'confirmado', 'parcial',
                                          'excedente', 'duplicado', 'sospechoso',
                                          'sin_match', 'rechazado');
CREATE TYPE severidad_alerta     AS ENUM ('informativa', 'advertencia', 'critica');
CREATE TYPE estado_alerta        AS ENUM ('abierta', 'resuelta');
CREATE TYPE resolucion_alerta    AS ENUM ('descartada', 'confirmada_igual', 'rechazada');
CREATE TYPE estado_cobro         AS ENUM ('abierto', 'parcial', 'pagado', 'anulado');
CREATE TYPE origen_comprobante   AS ENUM ('carga_web', 'camara', 'share_target');
CREATE TYPE tipo_transferencia   AS ENUM ('mismo_banco', 'interbancaria', 'desconocida');

-- ---------------------------------------------------------------------
-- 3. Catalogo global y cuentas de usuario (sin RLS: no contienen datos de negocio)
-- ---------------------------------------------------------------------
CREATE TABLE bancos (
  id      smallint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  codigo  text     NOT NULL UNIQUE,
  nombre  text     NOT NULL,
  oleada  smallint NOT NULL DEFAULT 3 CHECK (oleada BETWEEN 1 AND 3),  -- prioridad de parsers
  activo  boolean  NOT NULL DEFAULT true
);

INSERT INTO bancos (codigo, nombre, oleada) VALUES
  ('banreservas',   'Banreservas',                                         1),
  ('popular',       'Banco Popular Dominicano',                            1),
  ('bhd',           'Banco BHD',                                           1),
  ('santa_cruz',    'Banco Santa Cruz',                                    2),
  ('apap',          'Asociacion Popular de Ahorros y Prestamos (APAP)',    2),
  ('scotiabank',    'Scotiabank',                                          2),
  ('acap',          'Asociacion Cibao de Ahorros y Prestamos',             2),
  ('promerica',     'Banco Promerica',                                     3),
  ('caribe',        'Banco Caribe',                                        3),
  ('banesco',       'Banesco',                                             3),
  ('vimenca',       'Banco Vimenca',                                       3),
  ('lopez_de_haro', 'Banco Lopez de Haro',                                 3),
  ('ademi',         'Banco Ademi',                                         3),
  ('lafise',        'Banco Lafise',                                        3),
  ('otro',          'Otro / no identificado',                              3);

CREATE TABLE usuarios (
  id             uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  email          citext      NOT NULL UNIQUE,
  nombre         text        NOT NULL,
  password_hash  text        NOT NULL,           -- argon2
  activo         boolean     NOT NULL DEFAULT true,
  ultimo_acceso  timestamptz,
  creado_en      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE sesiones (                          -- refresh tokens
  id          uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  usuario_id  uuid        NOT NULL REFERENCES usuarios (id) ON DELETE CASCADE,
  token_hash  bytea       NOT NULL UNIQUE,       -- nunca el token en claro
  expira_en   timestamptz NOT NULL,
  revocada_en timestamptz,
  user_agent  text,
  creada_en   timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX sesiones_usuario ON sesiones (usuario_id) WHERE revocada_en IS NULL;

-- ---------------------------------------------------------------------
-- 4. Negocios (tenants) y configuracion
-- ---------------------------------------------------------------------
CREATE TABLE tenants (
  id             uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  nombre         text        NOT NULL,
  rnc            text,
  zona_horaria   text        NOT NULL DEFAULT 'America/Santo_Domingo',
  plan           text        NOT NULL DEFAULT 'piloto' CHECK (plan IN ('piloto', 'esencial', 'negocio')),
  estado         text        NOT NULL DEFAULT 'activo' CHECK (estado IN ('activo', 'suspendido', 'cancelado')),
  cancelado_en   timestamptz,
  creado_en      timestamptz NOT NULL DEFAULT now(),
  actualizado_en timestamptz NOT NULL DEFAULT now(),
  CHECK ((estado = 'cancelado') = (cancelado_en IS NOT NULL))
);

CREATE TABLE tenant_config (
  tenant_id                uuid         PRIMARY KEY REFERENCES tenants (id) ON DELETE CASCADE,
  ventana_fecha_dias       smallint     NOT NULL DEFAULT 7  CHECK (ventana_fecha_dias BETWEEN 1 AND 60),
  tolerancia_monto         numeric(14,2) NOT NULL DEFAULT 0 CHECK (tolerancia_monto >= 0),
  tope_monto_verificador   numeric(14,2) CHECK (tope_monto_verificador IS NULL OR tope_monto_verificador > 0),
  retencion_imagenes_meses smallint     NOT NULL DEFAULT 24 CHECK (retencion_imagenes_meses BETWEEN 1 AND 120),
  actualizado_en           timestamptz  NOT NULL DEFAULT now()
);
COMMENT ON COLUMN tenant_config.tope_monto_verificador IS
  'NULL = sin limite. Si se define, un verificador no puede confirmar montos mayores.';

CREATE TABLE membresias (
  tenant_id  uuid        NOT NULL REFERENCES tenants (id)  ON DELETE CASCADE,
  usuario_id uuid        NOT NULL REFERENCES usuarios (id) ON DELETE CASCADE,
  rol        rol_usuario NOT NULL,
  activo     boolean     NOT NULL DEFAULT true,
  creado_en  timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (tenant_id, usuario_id)
);
CREATE INDEX membresias_usuario ON membresias (usuario_id) WHERE activo;

CREATE TABLE dispositivos_push (
  id          uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id   uuid        NOT NULL,
  usuario_id  uuid        NOT NULL,
  proveedor   text        NOT NULL DEFAULT 'fcm' CHECK (proveedor IN ('fcm', 'webpush')),
  token       text        NOT NULL,
  user_agent  text,
  creado_en   timestamptz NOT NULL DEFAULT now(),
  ultimo_uso  timestamptz,
  FOREIGN KEY (tenant_id, usuario_id) REFERENCES membresias (tenant_id, usuario_id) ON DELETE CASCADE,
  UNIQUE (proveedor, token)
);

-- ---------------------------------------------------------------------
-- 5. Datos del negocio
-- ---------------------------------------------------------------------
CREATE TABLE cuentas_negocio (
  id             uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id      uuid        NOT NULL REFERENCES tenants (id) ON DELETE CASCADE,
  banco_id       smallint    NOT NULL REFERENCES bancos (id),
  alias          text        NOT NULL,
  ultimos4       char(4)     NOT NULL CHECK (ultimos4 ~ '^[0-9]{4}$'),
  moneda         char(3)     NOT NULL DEFAULT 'DOP' CHECK (moneda IN ('DOP', 'USD')),
  activa         boolean     NOT NULL DEFAULT true,
  creada_en      timestamptz NOT NULL DEFAULT now(),
  actualizado_en timestamptz NOT NULL DEFAULT now(),
  UNIQUE (tenant_id, id),
  UNIQUE (tenant_id, banco_id, ultimos4, moneda)
);

CREATE TABLE clientes (
  id             uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id      uuid        NOT NULL REFERENCES tenants (id) ON DELETE CASCADE,
  nombre         text        NOT NULL,
  nombre_norm    text        NOT NULL,           -- minusculas, sin acentos (lo calcula la app)
  telefono       text,
  notas          text,
  activo         boolean     NOT NULL DEFAULT true,
  creado_en      timestamptz NOT NULL DEFAULT now(),
  actualizado_en timestamptz NOT NULL DEFAULT now(),
  UNIQUE (tenant_id, id)
);
CREATE INDEX clientes_nombre_trgm ON clientes USING gin (nombre_norm gin_trgm_ops);
CREATE INDEX clientes_tenant_nombre ON clientes (tenant_id, nombre_norm);

CREATE TABLE clientes_alias (                    -- como aparece el remitente en las transferencias
  id          uuid  PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id   uuid  NOT NULL,
  cliente_id  uuid  NOT NULL,
  alias_norm  text  NOT NULL,
  FOREIGN KEY (tenant_id, cliente_id) REFERENCES clientes (tenant_id, id) ON DELETE CASCADE,
  UNIQUE (tenant_id, alias_norm)
);
CREATE INDEX clientes_alias_trgm ON clientes_alias USING gin (alias_norm gin_trgm_ops);

CREATE TABLE cobros (
  id                uuid          PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id         uuid          NOT NULL REFERENCES tenants (id) ON DELETE CASCADE,
  cliente_id        uuid,
  codigo            text          NOT NULL,      -- codigo corto para el concepto de la transferencia
  concepto          text          NOT NULL,
  monto             numeric(14,2) NOT NULL CHECK (monto > 0),
  moneda            char(3)       NOT NULL DEFAULT 'DOP' CHECK (moneda IN ('DOP', 'USD')),
  fecha_emision     date          NOT NULL DEFAULT current_date,
  fecha_vencimiento date,
  estado            estado_cobro  NOT NULL DEFAULT 'abierto',
  anulado_en        timestamptz,
  anulado_por       uuid          REFERENCES usuarios (id),
  motivo_anulacion  text,
  creado_por        uuid          REFERENCES usuarios (id),
  creado_en         timestamptz   NOT NULL DEFAULT now(),
  actualizado_en    timestamptz   NOT NULL DEFAULT now(),
  UNIQUE (tenant_id, id),
  UNIQUE (tenant_id, codigo),
  FOREIGN KEY (tenant_id, cliente_id) REFERENCES clientes (tenant_id, id),
  CHECK (fecha_vencimiento IS NULL OR fecha_vencimiento >= fecha_emision),
  CHECK ((estado = 'anulado') = (anulado_en IS NOT NULL))
);
CREATE INDEX cobros_candidatos ON cobros (tenant_id, monto, fecha_emision)
  WHERE estado IN ('abierto', 'parcial');
CREATE INDEX cobros_cliente ON cobros (tenant_id, cliente_id, estado);

CREATE TABLE comprobantes (
  id                      uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id               uuid         NOT NULL REFERENCES tenants (id) ON DELETE CASCADE,
  idempotency_key         uuid         NOT NULL,           -- generado por el cliente; evita dobles subidas
  subido_por              uuid         NOT NULL REFERENCES usuarios (id),
  origen                  origen_comprobante NOT NULL,

  -- Archivo
  storage_key             text         NOT NULL,
  mime                    text         NOT NULL CHECK (mime IN ('image/jpeg', 'image/png', 'image/webp', 'application/pdf')),
  bytes                   integer      NOT NULL CHECK (bytes > 0),
  ancho                   integer,
  alto                    integer,
  sha256                  bytea        NOT NULL CHECK (octet_length(sha256) = 32),
  phash                   bigint,                          -- hash perceptual de 64 bits
  imagen_eliminada_en     timestamptz,                     -- politica de retencion

  -- Estados (dos ejes independientes)
  estado_procesamiento    estado_procesamiento NOT NULL DEFAULT 'recibido',
  estado_conciliacion     estado_conciliacion  NOT NULL DEFAULT 'pendiente_revision',
  riesgo                  severidad_alerta,                -- severidad maxima de sus alertas abiertas
  intentos                smallint     NOT NULL DEFAULT 0,
  error_procesamiento     text,

  -- Datos vigentes (extraidos y/o corregidos por una persona)
  banco_id                smallint     REFERENCES bancos (id),
  tipo_transferencia      tipo_transferencia NOT NULL DEFAULT 'desconocida',
  monto                   numeric(14,2) CHECK (monto IS NULL OR monto > 0),
  moneda                  char(3)      CHECK (moneda IS NULL OR moneda IN ('DOP', 'USD')),
  fecha_pago              date,
  hora_pago               time,
  referencia              text,
  referencia_norm         text,
  remitente               text,
  remitente_norm          text,
  cuenta_destino_ultimos4 char(4),
  cuenta_destino_id       uuid,
  cliente_id              uuid,
  es_ingreso_libre        boolean      NOT NULL DEFAULT false,

  -- Decision humana
  decidido_por            uuid         REFERENCES usuarios (id),
  decidido_en             timestamptz,
  motivo_decision         text,

  creado_en               timestamptz  NOT NULL DEFAULT now(),
  actualizado_en          timestamptz  NOT NULL DEFAULT now(),

  UNIQUE (tenant_id, id),
  UNIQUE (tenant_id, idempotency_key),
  FOREIGN KEY (tenant_id, cuenta_destino_id) REFERENCES cuentas_negocio (tenant_id, id),
  FOREIGN KEY (tenant_id, cliente_id)        REFERENCES clientes (tenant_id, id),

  CONSTRAINT final_requiere_datos CHECK (
    estado_conciliacion NOT IN ('confirmado', 'parcial', 'excedente')
    OR (monto IS NOT NULL AND moneda IS NOT NULL AND fecha_pago IS NOT NULL AND banco_id IS NOT NULL)
  ),
  CONSTRAINT final_requiere_decision CHECK (
    estado_conciliacion NOT IN ('confirmado', 'parcial', 'excedente', 'rechazado')
    OR (decidido_por IS NOT NULL AND decidido_en IS NOT NULL)
  )
);

-- Bandeja: "que necesita mi accion"
CREATE INDEX comprobantes_bandeja ON comprobantes (tenant_id, estado_conciliacion, creado_en DESC);
-- Cuadre diario
CREATE INDEX comprobantes_cuadre ON comprobantes (tenant_id, fecha_pago, banco_id)
  WHERE estado_conciliacion IN ('confirmado', 'parcial', 'excedente');
-- Duplicados por campos. NO es UNIQUE a proposito: el Dueño puede "confirmar igual"
-- tras revisar la alerta, y eso debe quedar permitido y auditado.
CREATE INDEX comprobantes_dup_campos ON comprobantes
  (tenant_id, banco_id, referencia_norm, monto, fecha_pago)
  WHERE referencia_norm IS NOT NULL;
-- Duplicados por imagen
CREATE INDEX comprobantes_sha256 ON comprobantes (tenant_id, sha256);
-- Similares por hash perceptual: indice cubriente para recorrer una ventana reciente
-- y filtrar con app.distancia_hamming(phash, :nuevo) <= 6.
CREATE INDEX comprobantes_phash_reciente ON comprobantes (tenant_id, creado_en DESC)
  INCLUDE (phash) WHERE phash IS NOT NULL;
-- Purga por retencion
CREATE INDEX comprobantes_retencion ON comprobantes (tenant_id, creado_en)
  WHERE imagen_eliminada_en IS NULL;

CREATE TABLE extracciones (                      -- una por intento; permite medir precision
  id                 uuid          PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id          uuid          NOT NULL,
  comprobante_id     uuid          NOT NULL,
  motor              text          NOT NULL,     -- 'paddleocr-3.x' | 'vision-llm'
  parser             text,                       -- 'popular@1.2.0'
  banco_detectado_id smallint      REFERENCES bancos (id),
  campos             jsonb         NOT NULL,     -- {"monto":{"valor":"1500.00","confianza":0.98,"crudo":"RD$1,500.00"}, ...}
  confianza_min      numeric(4,3)  CHECK (confianza_min BETWEEN 0 AND 1),
  crudo              jsonb,                      -- lineas y cajas del OCR
  crudo_eliminado_en timestamptz,
  duracion_ms        integer,
  exitosa            boolean       NOT NULL,
  error              text,
  creada_en          timestamptz   NOT NULL DEFAULT now(),
  FOREIGN KEY (tenant_id, comprobante_id) REFERENCES comprobantes (tenant_id, id) ON DELETE CASCADE,
  UNIQUE (tenant_id, id)
);
CREATE INDEX extracciones_comprobante ON extracciones (tenant_id, comprobante_id, creada_en DESC);
COMMENT ON TABLE extracciones IS
  'Valor original del OCR. Las correcciones humanas viven en comprobantes y en eventos_auditoria (antes/despues).';

CREATE TABLE aplicaciones (                      -- comprobante -> cobro(s)
  id               uuid          PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id        uuid          NOT NULL,
  comprobante_id   uuid          NOT NULL,
  cobro_id         uuid          NOT NULL,
  monto_aplicado   numeric(14,2) NOT NULL CHECK (monto_aplicado > 0),
  creada_por       uuid          NOT NULL REFERENCES usuarios (id),
  creada_en        timestamptz   NOT NULL DEFAULT now(),
  anulada_en       timestamptz,
  anulada_por      uuid          REFERENCES usuarios (id),
  motivo_anulacion text,
  FOREIGN KEY (tenant_id, comprobante_id) REFERENCES comprobantes (tenant_id, id),
  FOREIGN KEY (tenant_id, cobro_id)       REFERENCES cobros (tenant_id, id),
  CHECK ((anulada_en IS NULL) = (anulada_por IS NULL))
);
CREATE UNIQUE INDEX aplicaciones_vigente_unica ON aplicaciones (tenant_id, comprobante_id, cobro_id)
  WHERE anulada_en IS NULL;
CREATE INDEX aplicaciones_cobro ON aplicaciones (tenant_id, cobro_id) WHERE anulada_en IS NULL;

CREATE TABLE alertas (
  id                uuid             PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id         uuid             NOT NULL,
  comprobante_id    uuid             NOT NULL,
  regla             text             NOT NULL CHECK (regla IN (
                      'referencia_duplicada', 'imagen_identica', 'imagen_similar',
                      'cuenta_destino_ajena', 'fecha_futura', 'fecha_anterior_cobro',
                      'fecha_antigua', 'referencia_formato_invalido',
                      'monto_distinto', 'remitente_distinto', 'confianza_baja')),
  severidad         severidad_alerta NOT NULL,
  mensaje           text             NOT NULL,           -- lenguaje claro para la persona
  detalle           jsonb            NOT NULL DEFAULT '{}',  -- ej. {"comprobante_relacionado":"<uuid>"}
  estado            estado_alerta    NOT NULL DEFAULT 'abierta',
  resolucion        resolucion_alerta,
  resuelta_por      uuid             REFERENCES usuarios (id),
  resuelta_en       timestamptz,
  motivo_resolucion text,
  creada_en         timestamptz      NOT NULL DEFAULT now(),
  FOREIGN KEY (tenant_id, comprobante_id) REFERENCES comprobantes (tenant_id, id) ON DELETE CASCADE,
  CHECK ((estado = 'resuelta') = (resolucion IS NOT NULL AND resuelta_por IS NOT NULL AND resuelta_en IS NOT NULL)),
  CHECK (resolucion IS DISTINCT FROM 'descartada' OR motivo_resolucion IS NOT NULL)
);
CREATE UNIQUE INDEX alertas_regla_abierta ON alertas (tenant_id, comprobante_id, regla)
  WHERE estado = 'abierta';
CREATE INDEX alertas_abiertas ON alertas (tenant_id, severidad, creada_en DESC)
  WHERE estado = 'abierta';

CREATE TABLE eventos_auditoria (                 -- solo se agrega, nunca se modifica
  id          bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  tenant_id   uuid        NOT NULL REFERENCES tenants (id),
  usuario_id  uuid        REFERENCES usuarios (id),            -- NULL = sistema
  actor       text        NOT NULL DEFAULT 'usuario' CHECK (actor IN ('usuario', 'sistema')),
  entidad     text        NOT NULL,                            -- 'comprobante', 'cobro', 'alerta', ...
  entidad_id  uuid,
  accion      text        NOT NULL,                            -- 'confirmar', 'editar_campo', 'anular', 'exportar', ...
  antes       jsonb,
  despues     jsonb,
  motivo      text,
  ip          inet,
  creado_en   timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX auditoria_entidad ON eventos_auditoria (tenant_id, entidad, entidad_id, creado_en DESC);
CREATE INDEX auditoria_tiempo  ON eventos_auditoria (tenant_id, creado_en DESC);

-- ---------------------------------------------------------------------
-- 6. Triggers de integridad y reglas de negocio
-- ---------------------------------------------------------------------

-- 6.1 updated_at
DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['tenants', 'tenant_config', 'cuentas_negocio', 'clientes', 'cobros', 'comprobantes'] LOOP
    EXECUTE format(
      'CREATE TRIGGER %I BEFORE UPDATE ON %I FOR EACH ROW EXECUTE FUNCTION app.set_actualizado_en()',
      't_' || t || '_actualizado', t);
  END LOOP;
END $$;

-- 6.2 Lo aplicado a cobros nunca excede el monto del comprobante; misma moneda
CREATE OR REPLACE FUNCTION validar_aplicacion() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
  v_monto numeric; v_moneda char(3); v_moneda_cobro char(3); v_aplicado numeric;
BEGIN
  IF NEW.anulada_en IS NOT NULL THEN RETURN NEW; END IF;

  SELECT monto, moneda INTO v_monto, v_moneda
    FROM comprobantes WHERE tenant_id = NEW.tenant_id AND id = NEW.comprobante_id
    FOR UPDATE;                                   -- serializa aplicaciones concurrentes
  SELECT moneda INTO v_moneda_cobro
    FROM cobros WHERE tenant_id = NEW.tenant_id AND id = NEW.cobro_id;

  IF v_monto IS NULL THEN
    RAISE EXCEPTION 'El comprobante no tiene monto' USING ERRCODE = 'check_violation';
  END IF;
  IF v_moneda IS DISTINCT FROM v_moneda_cobro THEN
    RAISE EXCEPTION 'La moneda del comprobante y la del cobro no coinciden' USING ERRCODE = 'check_violation';
  END IF;

  SELECT COALESCE(sum(monto_aplicado), 0) INTO v_aplicado
    FROM aplicaciones
   WHERE tenant_id = NEW.tenant_id AND comprobante_id = NEW.comprobante_id
     AND anulada_en IS NULL AND id <> NEW.id;

  IF v_aplicado + NEW.monto_aplicado > v_monto THEN
    RAISE EXCEPTION 'Lo aplicado (%) excede el monto del comprobante (%)',
      v_aplicado + NEW.monto_aplicado, v_monto USING ERRCODE = 'check_violation';
  END IF;
  RETURN NEW;
END $$;

CREATE TRIGGER t_aplicaciones_validar
  BEFORE INSERT OR UPDATE OF monto_aplicado, anulada_en, comprobante_id, cobro_id ON aplicaciones
  FOR EACH ROW EXECUTE FUNCTION validar_aplicacion();

-- 6.3 Matriz de permisos aplicada tambien en la base de datos
CREATE OR REPLACE FUNCTION comprobantes_reglas_estado() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE v_tope numeric;
BEGIN
  IF NEW.estado_conciliacion IS NOT DISTINCT FROM OLD.estado_conciliacion THEN
    RETURN NEW;
  END IF;

  -- Revertir un estado final: solo el Dueño
  IF OLD.estado_conciliacion IN ('confirmado', 'parcial', 'excedente', 'rechazado')
     AND COALESCE(app.rol_actual(), '') <> 'dueno' THEN
    RAISE EXCEPTION 'Solo el Dueño puede revertir un estado final' USING ERRCODE = 'insufficient_privilege';
  END IF;

  -- Un verificador confirma dentro de su tope y sin alertas criticas abiertas
  IF NEW.estado_conciliacion IN ('confirmado', 'parcial', 'excedente')
     AND app.rol_actual() = 'verificador' THEN
    SELECT tope_monto_verificador INTO v_tope FROM tenant_config WHERE tenant_id = NEW.tenant_id;
    IF v_tope IS NOT NULL AND NEW.monto > v_tope THEN
      RAISE EXCEPTION 'El monto excede el tope de confirmacion del verificador' USING ERRCODE = 'insufficient_privilege';
    END IF;
    IF EXISTS (SELECT 1 FROM alertas
                WHERE tenant_id = NEW.tenant_id AND comprobante_id = NEW.id
                  AND estado = 'abierta' AND severidad = 'critica') THEN
      RAISE EXCEPTION 'Hay alertas criticas abiertas: las resuelve el Dueño' USING ERRCODE = 'insufficient_privilege';
    END IF;
  END IF;
  RETURN NEW;
END $$;

CREATE TRIGGER t_comprobantes_reglas_estado
  BEFORE UPDATE OF estado_conciliacion ON comprobantes
  FOR EACH ROW EXECUTE FUNCTION comprobantes_reglas_estado();

-- 6.4 Auditoria inmutable
CREATE OR REPLACE FUNCTION auditoria_inmutable() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' AND current_user = 'cuadra_mantenimiento' THEN
    RETURN OLD;                                   -- unica via: purga por retencion/cancelacion
  END IF;
  RAISE EXCEPTION 'eventos_auditoria es de solo escritura' USING ERRCODE = 'insufficient_privilege';
END $$;

CREATE TRIGGER t_auditoria_inmutable
  BEFORE UPDATE OR DELETE OR TRUNCATE ON eventos_auditoria
  FOR EACH STATEMENT EXECUTE FUNCTION auditoria_inmutable();

-- ---------------------------------------------------------------------
-- 7. Vistas (security_invoker: respetan el RLS de quien consulta)
-- ---------------------------------------------------------------------
CREATE VIEW v_cobros_saldo WITH (security_invoker = true) AS
SELECT c.tenant_id, c.id AS cobro_id, c.codigo, c.cliente_id, c.monto, c.moneda, c.estado,
       COALESCE(sum(a.monto_aplicado) FILTER (WHERE a.anulada_en IS NULL), 0)             AS monto_aplicado,
       c.monto - COALESCE(sum(a.monto_aplicado) FILTER (WHERE a.anulada_en IS NULL), 0)   AS saldo
  FROM cobros c
  LEFT JOIN aplicaciones a ON a.tenant_id = c.tenant_id AND a.cobro_id = c.id
 GROUP BY c.id;

-- Cuadre diario: vista calculada (sin cierre de dia en el MVP)
CREATE VIEW v_cuadre_diario WITH (security_invoker = true) AS
SELECT tenant_id, fecha_pago, banco_id, cuenta_destino_id, moneda, estado_conciliacion,
       count(*) AS cantidad, sum(monto) AS total
  FROM comprobantes
 WHERE fecha_pago IS NOT NULL AND monto IS NOT NULL
 GROUP BY tenant_id, fecha_pago, banco_id, cuenta_destino_id, moneda, estado_conciliacion;

-- ---------------------------------------------------------------------
-- 8. Row Level Security
-- ---------------------------------------------------------------------

-- 8.1 Tablas con aislamiento simple por tenant_id
DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['tenant_config', 'dispositivos_push', 'cuentas_negocio', 'clientes',
                           'clientes_alias', 'cobros', 'comprobantes', 'extracciones', 'aplicaciones'] LOOP
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', t);
    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', t);
    EXECUTE format(
      'CREATE POLICY aislamiento_tenant ON %I
         USING (tenant_id = app.tenant_actual())
         WITH CHECK (tenant_id = app.tenant_actual())', t);
  END LOOP;
END $$;

-- 8.2 tenants: el negocio actual, y los negocios del usuario autenticado (selector de negocio)
ALTER TABLE tenants ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenants FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_propio ON tenants
  USING (id = app.tenant_actual()) WITH CHECK (id = app.tenant_actual());
CREATE POLICY tenants_del_usuario ON tenants FOR SELECT
  USING (id IN (SELECT tenant_id FROM membresias WHERE usuario_id = app.usuario_actual() AND activo));

-- 8.3 membresias: las del negocio actual, y las propias del usuario (login)
ALTER TABLE membresias ENABLE ROW LEVEL SECURITY;
ALTER TABLE membresias FORCE ROW LEVEL SECURITY;
CREATE POLICY aislamiento_tenant ON membresias
  USING (tenant_id = app.tenant_actual()) WITH CHECK (tenant_id = app.tenant_actual());
CREATE POLICY membresias_propias ON membresias FOR SELECT
  USING (usuario_id = app.usuario_actual());

-- 8.4 alertas: las criticas solo las modifica el Dueño (o el sistema al reevaluar)
ALTER TABLE alertas ENABLE ROW LEVEL SECURITY;
ALTER TABLE alertas FORCE ROW LEVEL SECURITY;
CREATE POLICY alertas_select ON alertas FOR SELECT
  USING (tenant_id = app.tenant_actual());
CREATE POLICY alertas_insert ON alertas FOR INSERT
  WITH CHECK (tenant_id = app.tenant_actual());
CREATE POLICY alertas_update ON alertas FOR UPDATE
  USING      (tenant_id = app.tenant_actual()
              AND (severidad <> 'critica' OR app.rol_actual() IN ('dueno', 'sistema')))
  WITH CHECK (tenant_id = app.tenant_actual()
              AND (severidad <> 'critica' OR app.rol_actual() IN ('dueno', 'sistema')));
-- Sin politica DELETE: las alertas no se borran.

-- 8.5 auditoria: cualquiera inserta; solo el Dueño lee; nadie modifica ni borra
ALTER TABLE eventos_auditoria ENABLE ROW LEVEL SECURITY;
ALTER TABLE eventos_auditoria FORCE ROW LEVEL SECURITY;
CREATE POLICY auditoria_insert ON eventos_auditoria FOR INSERT
  WITH CHECK (tenant_id = app.tenant_actual());
CREATE POLICY auditoria_select ON eventos_auditoria FOR SELECT
  USING (tenant_id = app.tenant_actual() AND app.rol_actual() = 'dueno');

-- ---------------------------------------------------------------------
-- 9. Alta de negocio (onboarding): unica via para crear tenants
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION app.crear_negocio(p_nombre text, p_usuario uuid) RETURNS uuid
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_id uuid := gen_random_uuid();
BEGIN
  PERFORM set_config('app.tenant_id',  v_id::text,      true);
  PERFORM set_config('app.usuario_id', p_usuario::text, true);
  PERFORM set_config('app.rol',        'dueno',         true);

  INSERT INTO tenants (id, nombre)       VALUES (v_id, p_nombre);
  INSERT INTO tenant_config (tenant_id)  VALUES (v_id);
  INSERT INTO membresias (tenant_id, usuario_id, rol) VALUES (v_id, p_usuario, 'dueno');
  INSERT INTO eventos_auditoria (tenant_id, usuario_id, entidad, entidad_id, accion, despues)
    VALUES (v_id, p_usuario, 'tenant', v_id, 'crear', jsonb_build_object('nombre', p_nombre));
  RETURN v_id;
END $$;

-- ---------------------------------------------------------------------
-- 10. Permisos
-- ---------------------------------------------------------------------
REVOKE ALL ON FUNCTION app.crear_negocio(text, uuid) FROM PUBLIC;
GRANT USAGE ON SCHEMA app TO cuadra_app, cuadra_mantenimiento;
GRANT EXECUTE ON FUNCTION app.crear_negocio(text, uuid), app.tenant_actual(), app.usuario_actual(),
      app.rol_actual(), app.distancia_hamming(bigint, bigint) TO cuadra_app;

GRANT SELECT ON bancos TO cuadra_app;
GRANT SELECT, INSERT, UPDATE ON usuarios, tenants, tenant_config, cuentas_negocio, clientes, cobros,
      comprobantes, extracciones, aplicaciones, alertas TO cuadra_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON sesiones, membresias, dispositivos_push, clientes_alias TO cuadra_app;
GRANT SELECT, INSERT ON eventos_auditoria TO cuadra_app;          -- sin UPDATE ni DELETE
GRANT SELECT ON v_cobros_saldo, v_cuadre_diario TO cuadra_app;

-- tenants no se inserta directamente: solo via app.crear_negocio()
REVOKE INSERT ON tenants FROM cuadra_app;

-- Mantenimiento (purga por retencion, cancelaciones): BYPASSRLS + DELETE acotado
GRANT SELECT, UPDATE, DELETE ON comprobantes, extracciones, aplicaciones, alertas, cobros, clientes,
      clientes_alias, cuentas_negocio, tenant_config, membresias, dispositivos_push, tenants TO cuadra_mantenimiento;
GRANT SELECT, DELETE ON eventos_auditoria TO cuadra_mantenimiento;

COMMIT;

-- =====================================================================
-- PRUEBA DE AISLAMIENTO (ejecutar como cuadra_app; debe devolver 0 filas)
-- =====================================================================
-- BEGIN;
--   SELECT set_config('app.tenant_id', '<UUID_NEGOCIO_A>', true),
--          set_config('app.rol', 'dueno', true);
--   SELECT count(*) FROM comprobantes WHERE tenant_id = '<UUID_NEGOCIO_B>';  -- 0
--   INSERT INTO cobros (tenant_id, codigo, concepto, monto)
--     VALUES ('<UUID_NEGOCIO_B>', 'X1', 'intruso', 100);                     -- ERROR: viola RLS
-- ROLLBACK;
--
-- CONSULTA DE IMAGENES SIMILARES (ventana de 90 dias, distancia <= 6)
-- SELECT id FROM comprobantes
--  WHERE tenant_id = app.tenant_actual() AND phash IS NOT NULL
--    AND creado_en > now() - interval '90 days'
--    AND app.distancia_hamming(phash, :phash_nuevo) <= 6;
