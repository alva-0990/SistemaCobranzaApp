------------------------------------------------------
------------------------------------------------------
--. Procedimiento Almacenado: pr_unificar_cuentas_ventas
------------------------------------------------------
------------------------------------------------------

CREATE OR REPLACE PROCEDURE pr_unificar_cuentas_ventas(
    p_id_venta_vieja BIGINT, p_id_venta_nueva BIGINT, p_id_usuario BIGINT
) AS $$
DECLARE
    v_saldo NUMERIC(14,2);
    v_codigo TEXT;
    v_cliente_v BIGINT;
    v_cliente_n BIGINT;
    v_estado BIGINT;
    v_numero INT;
    v_total NUMERIC(14,2);
    v_enganche NUMERIC(14,2);
    v_financiado NUMERIC(14,2);
    v_cuota NUMERIC(14,2);
    v_usuario BIGINT;
    v_folio TEXT;
BEGIN
    IF p_id_venta_vieja = p_id_venta_nueva THEN
        RAISE EXCEPTION 'La cuenta de origen debe ser distinta de la nueva.';
    END IF;
    SELECT IdCliente, SaldoPendiente, CodigoVenta, IdEstatusVenta
      INTO v_cliente_v, v_saldo, v_codigo, v_estado
      FROM Venta WHERE IdVenta = p_id_venta_vieja FOR UPDATE;
    SELECT IdCliente, NumeroCuotas, TotalFinal, Enganche, IdUsuarioRegistro
      INTO v_cliente_n, v_numero, v_total, v_enganche, v_usuario
      FROM Venta WHERE IdVenta = p_id_venta_nueva FOR UPDATE;
    IF v_cliente_v IS NULL OR v_cliente_n IS NULL THEN
        RAISE EXCEPTION 'No se encontró la cuenta de origen o destino.';
    END IF;
    IF v_saldo <= 0 OR v_estado = 4 THEN
        RAISE EXCEPTION 'La cuenta de origen no tiene saldo disponible para renovar.';
    END IF;
    IF v_cliente_v <> v_cliente_n THEN
        RAISE EXCEPTION 'No se pueden unificar cuentas de diferentes clientes.';
    END IF;
    IF v_numero <= 0 OR NOT EXISTS (SELECT 1 FROM VentaCuotas WHERE IdVenta = p_id_venta_nueva) THEN
        RAISE EXCEPTION 'La cuenta nueva debe tener un plan de crédito.';
    END IF;

    v_folio := 'RENOV-' || p_id_venta_vieja;
    v_total := v_total + v_saldo;
    v_financiado := v_total - v_enganche;
    v_cuota := ROUND(v_financiado / v_numero, 2);
    v_usuario := COALESCE(p_id_usuario, v_usuario);

    UPDATE Venta SET TotalFinal = v_total, SaldoPendiente = v_financiado,
        MontoCuotaFija = v_cuota, IdEstatusVenta = 1,
        VentasUnificadas = CASE WHEN NULLIF(TRIM(VentasUnificadas), '') IS NULL THEN v_codigo
            ELSE VentasUnificadas || ', ' || v_codigo END,
        Observaciones = COALESCE(Observaciones, '') || ' [CARGO POR LIQUIDACIÓN: Cuenta anterior '
            || COALESCE(v_codigo, p_id_venta_vieja::TEXT) || ' unificada por $' || v_saldo || ']',
        FechaModificacion = now()
      WHERE IdVenta = p_id_venta_nueva;

    -- La última cuota absorbe únicamente el redondeo; la suma coincide con el saldo.
    UPDATE VentaCuotas SET
        MontoCuota = CASE WHEN NumeroCuota = v_numero THEN v_financiado - v_cuota * (v_numero - 1) ELSE v_cuota END,
        SaldoCuota = CASE WHEN NumeroCuota = v_numero THEN v_financiado - v_cuota * (v_numero - 1) ELSE v_cuota END,
        EstatusCuota = 'PENDIENTE'
      WHERE IdVenta = p_id_venta_nueva;

    INSERT INTO VentaUnificaciones (IdVentaNueva, IdVentaVieja, MontoTraspasado, CodigoLiquidacion, IdUsuarioRegistro)
      VALUES (p_id_venta_nueva, p_id_venta_vieja, v_saldo, v_folio, v_usuario);
    INSERT INTO Pagos (IdVenta, IdCliente, MontoAbona, IdMovimiento, Observaciones, IdUsuarioRegistro, FolioPago)
      VALUES (p_id_venta_vieja, v_cliente_v, v_saldo, 7,
          'Cuenta liquidada por renovación de crédito. Saldo transferido a Venta ID: ' || p_id_venta_nueva,
          v_usuario, v_folio);
    UPDATE Venta SET IdEstatusVenta = 3, SaldoPendiente = 0, FechaModificacion = now()
      WHERE IdVenta = p_id_venta_vieja;
    UPDATE VentaCuotas SET SaldoCuota = 0, EstatusCuota = 'PAGADA'
      WHERE IdVenta = p_id_venta_vieja;
END;
$$ LANGUAGE plpgsql;
COMMIT;


------------------------------------------------------
------------------------------------------------------
--. Trigger 2: fn_revertir_abono_cascada (AFTER DELETE)
------------------------------------------------------
------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_revertir_abono_cascada()
RETURNS TRIGGER AS $$
DECLARE
monto_a_devolver     NUMERIC(14,2);
    r_cuota              RECORD;
    capacidad_cuota      NUMERIC(14,2);
    monto_inyectado      NUMERIC(14,2);
BEGIN
    -- =========================================================================
    -- A. REVERSIÓN EN CADENA AUTOMÁTICA (El Trigger se encarga de todo)
    -- =========================================================================
    -- Si se elimina el abono físico principal (ID 1), limpiamos todo su bloque transaccional.
    IF OLD.IdMovimiento = 1 AND OLD.ConsecutivoPago IS NOT NULL THEN
        
        -- 1. Borra bonificaciones automáticas con el MISMO consecutivo (ID 4 y 6)
DELETE FROM Pagos
WHERE IdVenta = OLD.IdVenta
  AND ConsecutivoPago = OLD.ConsecutivoPago
  AND IdMovimiento IN (4, 6);

-- 2. Borra Ajustes/Condonaciones (ID 5) creados en el consecutivo siguiente (+1)
-- en el mismo segundo (Ventana de 2 segundos de seguridad por el servidor)
DELETE FROM Pagos
WHERE IdVenta = OLD.IdVenta
  AND ConsecutivoPago = OLD.ConsecutivoPago + 1
  AND IdMovimiento = 5
  AND FechaPago >= OLD.FechaPago - INTERVAL '2 second'
  AND FechaPago <= OLD.FechaPago + INTERVAL '2 second';

END IF;

    -- =========================================================================
    -- B. AFECTACIÓN DE SALDOS GLOBALES EN LA CABECERA VENTA
    -- =========================================================================
    IF OLD.IdMovimiento = 1 THEN
        -- DINERO REAL: Restamos de la caja real (TotalAbonado) y lo regresamos al saldo pendiente
UPDATE Venta
SET TotalAbonado      = TotalAbonado - OLD.MontoAbona,
    SaldoPendiente    = SaldoPendiente + OLD.MontoAbona,
    FechaModificacion = now(),
    IdEstatusVenta    = CASE WHEN IdEstatusVenta = 3 THEN 2 ELSE IdEstatusVenta END
WHERE IdVenta = OLD.IdVenta;

-- Penalización automática de la racha por eliminar el abono físico
UPDATE Venta
SET PagosConsecutivosPuntuales = CASE WHEN PagosConsecutivosPuntuales > 0
                                          THEN PagosConsecutivosPuntuales - 1
                                      ELSE 0 END
WHERE IdVenta = OLD.IdVenta;
ELSE
        -- DINERO VIRTUAL / AJUSTES (4, 5, 6, 7): Regresa el dinero al saldo pendiente pero no toca caja
UPDATE Venta
SET SaldoPendiente    = SaldoPendiente + OLD.MontoAbona,
    FechaModificacion = now(),
    IdEstatusVenta    = CASE WHEN IdEstatusVenta = 3 THEN 2 ELSE IdEstatusVenta END
WHERE IdVenta = OLD.IdVenta;
END IF;

    -- =========================================================================
    -- C. REVERSO DE LA CASCADA INVERSA EN CUOTAS (De la más nueva a la más vieja)
    -- =========================================================================
    monto_a_devolver := OLD.MontoAbona;

FOR r_cuota IN
SELECT IdCuota, MontoCuota, SaldoCuota
FROM VentaCuotas
WHERE IdVenta = OLD.IdVenta
ORDER BY NumeroCuota DESC
    LOOP
        EXIT WHEN monto_a_devolver <= 0;

capacidad_cuota := r_cuota.MontoCuota - r_cuota.SaldoCuota;

        IF capacidad_cuota > 0 THEN
            IF monto_a_devolver >= capacidad_cuota THEN
                monto_inyectado  := capacidad_cuota;
                monto_a_devolver := monto_a_devolver - monto_inyectado;

UPDATE VentaCuotas
SET SaldoCuota   = MontoCuota,
    EstatusCuota = 'PENDIENTE'
WHERE IdCuota = r_cuota.IdCuota;
ELSE
                monto_inyectado  := monto_a_devolver;
                monto_a_devolver := 0.00;

UPDATE VentaCuotas
SET SaldoCuota   = SaldoCuota + monto_inyectado,
    EstatusCuota = 'PENDIENTE'
WHERE IdCuota = r_cuota.IdCuota;
END IF;
END IF;
END LOOP;

RETURN OLD;
END;
$$ LANGUAGE plpgsql;



------------------------------------------------------
------------------------------------------------------
--. Trigger 1: fn_procesar_abono_cascada (BEFORE INSERT)
------------------------------------------------------
------------------------------------------------------
CREATE
OR REPLACE FUNCTION fn_procesar_abono_cascada()
RETURNS TRIGGER AS $$
DECLARE
monto_restante          NUMERIC(14,2);
    r_cuota
RECORD;
    monto_a_pagar_cuota
NUMERIC(14,2);
    saldo_anterior_venta
NUMERIC(14,2);
    saldo_actual_venta
NUMERIC(14,2);
    
    -- VARIABLES DE CONTROL DINÁMICO
    v_pagos_consecutivos
INT;
    v_meta_pagos_promo
INT;
    v_promo_esta_activa
TEXT;
    v_monto_liquidacion
NUMERIC(14,2);
    v_meses_transcurridos
INT;
    v_descuento_por_aplicar
NUMERIC(14,2);
BEGIN
    -- 1. Consultar parámetros de la promoción de racha desde tus datos semilla
SELECT COALESCE(ValorTexto, 'NO')
INTO v_promo_esta_activa
FROM ParametrosSistema
WHERE Clave = 'PROMO_PAGOS_CONSECUTIVOS_ACTIVA';
SELECT COALESCE(ValorNumerico::INT, 5)
INTO v_meta_pagos_promo
FROM ParametrosSistema
WHERE Clave = 'PROMO_PAGOS_CONSECUTIVOS_META';

-- 2. Obtener saldo global y racha actual de la venta antes de aplicar el pago
SELECT SaldoPendiente, PagosConsecutivosPuntuales
INTO saldo_anterior_venta, v_pagos_consecutivos
FROM Venta
WHERE IdVenta = NEW.IdVenta;

-- =========================================================================
-- DETECCIÓN AUTOMÁTICA DE PRONTO PAGO (Solo si viene de un abono físico inicial)
-- =========================================================================
v_descuento_por_aplicar
:= 0.00;
    
    IF
NEW.IdMovimiento = 1 AND NEW.Observaciones LIKE '%LIQUIDACION PRONTO PAGO%' THEN
        -- Calcular meses transcurridos de vida del crédito
SELECT COALESCE(EXTRACT(MONTH FROM AGE(now(), FechaCompra)), 0) + 1
INTO v_meses_transcurridos
FROM Venta
WHERE IdVenta = NEW.IdVenta;

-- Buscar el monto preferencial en VentaPlan
SELECT MontoLiquidacion
INTO v_monto_liquidacion
FROM VentaPlan
WHERE IdVenta = NEW.IdVenta
  AND NumeroMeses >= v_meses_transcurridos
  AND Activo = TRUE
ORDER BY NumeroMeses ASC LIMIT 1;

-- Si califica, calculamos el perdón financiero automático
IF
v_monto_liquidacion IS NOT NULL AND NEW.MontoAbona >= v_monto_liquidacion THEN
            v_descuento_por_aplicar := saldo_anterior_venta - NEW.MontoAbona;
END IF;
END IF;
    
    -- Validar que el monto no supere la deuda actual (aplica para efectivo y virtual)
    IF
NEW.MontoAbona > saldo_anterior_venta THEN
        RAISE EXCEPTION 'Operación denegada: El monto a aplicar (%) es mayor al saldo pendiente (%)', 
            NEW.MontoAbona, saldo_anterior_venta;
END IF;
    
    -- **CUALQUIER MOVIMIENTO (FÍSICO O VIRTUAL) PROCESA LA CASCADA EN CUOTAS**
    monto_restante
:= NEW.MontoAbona;
    
    -- =========================================================================
    -- B. FLUJO EN CASCADA ORIGINAL (EJECUTADO PARA TODOS)
    -- =========================================================================
FOR r_cuota IN
SELECT IdCuota, SaldoCuota
FROM VentaCuotas
WHERE IdVenta = NEW.IdVenta
  AND EstatusCuota IN ('PENDIENTE', 'VENCIDA')
ORDER BY NumeroCuota ASC LOOP
        EXIT WHEN monto_restante <= 0;

IF
monto_restante >= r_cuota.SaldoCuota THEN
            monto_a_pagar_cuota := r_cuota.SaldoCuota;
            monto_restante
:= monto_restante - monto_a_pagar_cuota;

UPDATE VentaCuotas
SET SaldoCuota   = 0.00,
    EstatusCuota = 'PAGADA'
WHERE IdCuota = r_cuota.IdCuota;
ELSE
            monto_a_pagar_cuota := monto_restante;
            monto_restante
:= 0.00;

UPDATE VentaCuotas
SET SaldoCuota = SaldoCuota - monto_a_pagar_cuota
WHERE IdCuota = r_cuota.IdCuota;
END IF;
END LOOP;
    
    -- =========================================================================
    -- C. ACTUALIZACIÓN DE LA CABECERA GLOBAL DE LA VENTA
    -- =========================================================================
    saldo_actual_venta
:= saldo_anterior_venta - NEW.MontoAbona;
    
    IF
NEW.IdMovimiento = 1 THEN
        -- DINERO REAL: Incrementa caja real (TotalAbonado) y evalúa racha
        IF v_promo_esta_activa = 'SI' AND (NEW.Observaciones IS NULL OR NEW.Observaciones NOT LIKE '%LIQUIDACION PRONTO PAGO%') THEN
            
            -- Buscamos la cuota correspondiente a este periodo (pendiente o recién pagada por la cascada)
            IF EXISTS (
                SELECT 1 FROM VentaCuotas 
                WHERE IdVenta = NEW.IdVenta 
                  AND (EstatusCuota = 'PENDIENTE' OR (EstatusCuota = 'PAGADA' AND SaldoCuota = 0.00))
                  AND (
                     -- Regla 1: Pagó antes de la fecha (Puntualidad estricta histórica)
                     NEW.FechaPago::DATE <= FechaVencimiento::DATE 
                     OR 
                     -- Regla 2: Tolerancia semanal (Mismo año y misma semana del calendario)
                     (
                        EXTRACT(YEAR FROM NEW.FechaPago) = EXTRACT(YEAR FROM FechaVencimiento)
                        AND EXTRACT(WEEK FROM NEW.FechaPago) = EXTRACT(WEEK FROM FechaVencimiento)
                     )
                  )
                ORDER BY NumeroCuota ASC LIMIT 1
            ) THEN
                v_pagos_consecutivos := v_pagos_consecutivos + 1;
ELSE
                v_pagos_consecutivos := 0; -- Rompe racha si ya es una semana posterior (atrasado)
END IF;
ELSE
            v_pagos_consecutivos := 0;
END IF;


UPDATE Venta
SET TotalAbonado               = TotalAbonado + NEW.MontoAbona,
    SaldoPendiente             = saldo_actual_venta,
    FechaUltimoPago            = NEW.FechaPago,
    PagosConsecutivosPuntuales = v_pagos_consecutivos,
    FechaModificacion          = now(),
    IdEstatusVenta             = CASE WHEN saldo_actual_venta <= 0 THEN 3 ELSE IdEstatusVenta END
WHERE IdVenta = NEW.IdVenta;

-- ACCIÓN DE PRONTO PAGO: Inyectar fila de Descuento (ID 4)
IF
v_descuento_por_aplicar > 0 THEN
            INSERT INTO Pagos (
                IdVenta, IdCliente, SaldoAnterior, SaldoActual, MontoAbona, IdCobrador, FechaPago, 
                IdMovimiento, Observaciones, IdUsuarioRegistro, 
                FolioPago, ConsecutivoPago
            )
            VALUES (
                NEW.IdVenta, NEW.IdCliente, saldo_actual_venta, 0.00, v_descuento_por_aplicar, NEW.IdCobrador, now(), 
                4, -- BonificacionProntoPago
                'DESCUENTO AUTOMÁTICO: Cuenta liquidada anticipadamente según plan de venta.', 
                NEW.IdUsuarioRegistro,
                'BPAG-' || LPAD(COALESCE(NEW.ConsecutivoPago, 1)::TEXT, 3, '0'),
                NEW.ConsecutivoPago
            );
            saldo_actual_venta
:= 0.00;
END IF;
        
        -- ACCIÓN DE RACHA DE PAGOS: Inyectar beneficio de Promoción (ID 6)
        IF
v_promo_esta_activa = 'SI' AND v_pagos_consecutivos = v_meta_pagos_promo THEN
            INSERT INTO Pagos (
                IdVenta, IdCliente, SaldoAnterior, SaldoActual, MontoAbona, IdCobrador, FechaPago, 
                IdMovimiento, Observaciones, IdUsuarioRegistro,
                FolioPago, ConsecutivoPago
            )
            VALUES (
                NEW.IdVenta, NEW.IdCliente, saldo_actual_venta, saldo_actual_venta - NEW.MontoAbona, NEW.MontoAbona, NEW.IdCobrador, now(), 
                6, -- BonificacionPromocion
                'PROMO AUTOMÁTICA: Se otorga beneficio por racha de ' || v_meta_pagos_promo || ' pagos consecutivos.', 
                NEW.IdUsuarioRegistro,
                'BPRO-' || LPAD(COALESCE(NEW.ConsecutivoPago, 1)::TEXT, 3, '0'),
                NEW.ConsecutivoPago
            );
UPDATE Venta
SET PagosConsecutivosPuntuales = 0
WHERE IdVenta = NEW.IdVenta;
END IF;

ELSE
        -- DINERO VIRTUAL (4, 5, 6, 7): Solo reduce la deuda sin duplicar promociones ni alterar la caja física
UPDATE Venta
SET SaldoPendiente    = saldo_actual_venta,
    FechaUltimoPago   = NEW.FechaPago,
    FechaModificacion = now(),
    IdEstatusVenta    = CASE WHEN saldo_actual_venta <= 0 THEN 3 ELSE IdEstatusVenta END
WHERE IdVenta = NEW.IdVenta;
END IF;
    
    -- D. ASIGNACIÓN INMUTABLE DE AUDITORÍA
    NEW.SaldoAnterior
:= saldo_anterior_venta;
    NEW.SaldoActual
:= COALESCE(saldo_actual_venta, 0.00);

RETURN NEW;
END;
$$
LANGUAGE plpgsql;

-- 2. FUNCIÓN DE CÁLCULO (SE QUEDA EN BEFORE INSERT)
CREATE OR REPLACE FUNCTION fn_calcular_comision_detalle()
RETURNS TRIGGER AS $$
DECLARE
v_id_condicion BIGINT;
    v_id_vendedor BIGINT;
    v_tipo_calculo TEXT;
    v_valor_comision NUMERIC(14,2);
    v_monto_calculado NUMERIC(14,2) := 0.00;
    v_porcentaje NUMERIC(5,2) := 0.00;
BEGIN
SELECT IdCondicion, IdVendedor INTO v_id_condicion, v_id_vendedor
FROM Venta WHERE IdVenta = NEW.IdVenta;

IF v_id_vendedor IS NOT NULL THEN
SELECT cc.TipoCalculo,
       CASE WHEN v_id_condicion = 2 THEN cc.ComisionContado ELSE cc.ComisionCredito END
INTO v_tipo_calculo, v_valor_comision
FROM ConfiguracionComisiones cfg
         INNER JOIN CatalogoComisiones cc ON cfg.IdCatalogoComision = cc.IdCatalogoComision
WHERE cfg.IdProducto = NEW.IdProducto AND cc.Activo = TRUE;

IF FOUND THEN
            IF v_tipo_calculo = 'PORCENTAJE' THEN
                v_porcentaje := v_valor_comision;
                v_monto_calculado := (NEW.Importe * (v_valor_comision / 100.00));
            ELSIF v_tipo_calculo = 'FIJO' THEN
                v_porcentaje := 0.00;
                v_monto_calculado := v_valor_comision * NEW.Cantidad;
END IF;
END IF;
END IF;

    -- Estampamos los valores calculados en la fila actual de DetallesVenta
    NEW.PorcentajeComisionAplicado := v_porcentaje;
    NEW.MontoComisionGenerado := v_monto_calculado;

RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- ASOCIAMOS EL PRIMER TRIGGER
CREATE TRIGGER trg_calcular_comision_antes_insert
    BEFORE INSERT ON DetallesVenta
    FOR EACH ROW
    EXECUTE FUNCTION fn_calcular_comision_detalle();


-- 3. NUEVA FUNCIÓN DE REGISTRO DE BITÁCORA (SE VA A AFTER INSERT)
CREATE OR REPLACE FUNCTION fn_registrar_bitacora_comision()
RETURNS TRIGGER AS $$
DECLARE
v_id_condicion BIGINT;
    v_id_vendedor BIGINT;
BEGIN
SELECT IdCondicion, IdVendedor INTO v_id_condicion, v_id_vendedor
FROM Venta WHERE IdVenta = NEW.IdVenta;

-- Si hay vendedor y se generó una comisión válida, procedemos al guardado histórico
IF v_id_vendedor IS NOT NULL AND NEW.MontoComisionGenerado > 0 THEN
        
        -- Aquí NEW.IdDetalle ya existe y es perfectamente válido
        INSERT INTO ComisionesPorPagar (IdVenta, IdDetalle, IdVendedor, MontoComision, EstatusComision)
        VALUES (
            NEW.IdVenta, 
            NEW.IdDetalle, 
            v_id_vendedor, 
            NEW.MontoComisionGenerado, 
            CASE WHEN v_id_condicion = 2 THEN 'DISPONIBLE' ELSE 'PENDIENTE' END
        );

        -- Sumamos el acumulado directo al renglón maestro de Venta
UPDATE Venta
SET TotalComisionVenta = TotalComisionVenta + NEW.MontoComisionGenerado
WHERE IdVenta = NEW.IdVenta;
END IF;

RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_registrar_bitacora_comision_despues
    AFTER INSERT ON DetallesVenta
    FOR EACH ROW
    EXECUTE FUNCTION fn_registrar_bitacora_comision();

CREATE
OR REPLACE TRIGGER trg_pago_venta_cascada
    BEFORE INSERT ON Pagos
    FOR EACH ROW
    EXECUTE FUNCTION fn_procesar_abono_cascada();

CREATE
OR REPLACE TRIGGER trg_pago_cancelado_cascada
    AFTER DELETE
ON Pagos
    FOR EACH ROW
    EXECUTE FUNCTION fn_revertir_abono_cascada();
