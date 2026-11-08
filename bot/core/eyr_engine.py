import logging
from collections import deque
from datetime import datetime, time

class ExcesoRotacionEngine:
    """
    Motor cuantitativo-discrecional de Exceso y Rotación (E&R) basado en:
    - Niveles de Sesión (Europa H/L, Asia H/L, VWAP)
    - Detección de Exceso por velocidad de flujo
    - Rotación y retroceso al 50%
    - Delta Acumulado (Convergencia / Divergencia)
    """

    def __init__(self, config_manager):
        self.config = config_manager
        
        # Estado de sesión y niveles
        self.asia_high = float('-inf')
        self.asia_low = float('inf')
        self.europa_high = float('-inf')
        self.europa_low = float('inf')
        self.ny_high = float('-inf')
        self.ny_low = float('inf')

        # VWAP acumulado
        self.vwap_sum_pv = 0.0
        self.vwap_sum_vol = 0.0
        self.vwap = 0.0

        # Order flow y velocidad
        self.delta_acumulado = 0.0
        self.flow_window = deque(maxlen=30) # ventana de últimos trades
        self.price_history = deque(maxlen=100)

        # Máquina de estados E&R
        self.state = "IDLE"  # IDLE, EXCESO, ROTACION
        self.lado_exceso = 0  # +1 Compra (máximo), -1 Venta (mínimo)
        self.extremo_exceso = 0.0
        self.precio_exceso_inicio = 0.0
        self.ticks_en_exceso = 0
        self.cooldown_ticks = 0

    def procesar_tick_nq(self, market_data: dict) -> tuple[str, str, dict]:
        """
        Procesa cada tick proveniente del análisis de NQ.
        Devuelve (Acción: 'HOLD'|'BUY'|'SELL', Motivo: str, SnapshotSetup: dict)
        """
        close_price = float(market_data.get("close", 0.0))
        volume = float(market_data.get("volume", 1.0))
        tick_time_str = market_data.get("time", "")

        if close_price <= 0:
            return "HOLD", "Precio inválido", {}

        self.price_history.append(close_price)
        if self.cooldown_ticks > 0:
            self.cooldown_ticks -= 1

        # 1. Actualizar VWAP acumulativo
        self.vwap_sum_pv += close_price * volume
        self.vwap_sum_vol += volume
        if self.vwap_sum_vol > 0:
            self.vwap = self.vwap_sum_pv / self.vwap_sum_vol

        # 2. Actualizar Niveles y Sesiones (si vienen en el payload o estimación por precio)
        self._actualizar_niveles_sesion(market_data, close_price)

        # 3. Estimar velocidad de flujo y agresión
        # (Si NinjaTrader envía delta o aproximamos con el cambio de tick * volumen)
        delta_tick = float(market_data.get("delta", 0.0))
        if delta_tick == 0.0 and len(self.price_history) >= 2:
            delta_tick = volume if close_price >= self.price_history[-2] else -volume

        self.delta_acumulado += delta_tick
        self.flow_window.append(abs(delta_tick))
        velocidad_flujo = sum(self.flow_window) / max(1, len(self.flow_window))

        # 4. Parámetros en caliente desde config.json
        cfg_er = self.config.get("exceso_rotacion", {})
        umbral_vel = float(cfg_er.get("umbral_velocidad_exceso", 80.0))
        rotacion_min_pts = float(cfg_er.get("rotacion_minima_puntos", 6.0))
        max_ticks_espera = int(cfg_er.get("ventana_ticks_rotacion", 40))
        
        cfg_op = self.config.get("operativa", {})
        bias = cfg_op.get("bias_permitido", "AMBOS")

        # 5. Máquina de Estados E&R
        nivel_nombre, nivel_precio = self._buscar_nivel_cercano(close_price)

        # --- ESTADO IDLE: Buscar inicio de Exceso ---
        if self.state == "IDLE":
            if velocidad_flujo >= umbral_vel and self.cooldown_ticks == 0:
                # Determinar lado del exceso
                if delta_tick > 0:
                    self.lado_exceso = +1  # Exceso comprador buscando techo
                    self.extremo_exceso = close_price
                else:
                    self.lado_exceso = -1  # Exceso vendedor buscando suelo
                    self.extremo_exceso = close_price

                self.precio_exceso_inicio = close_price
                self.ticks_en_exceso = 0
                self.state = "EXCESO"
                logging.info(f"[E&R] >> EXCESO DETECTADO ({'COMPRA' if self.lado_exceso > 0 else 'VENTA'}) en NQ {close_price} (Velocidad: {velocidad_flujo:.1f} contratos/tick | Nivel: {nivel_nombre})")

            return "HOLD", f"Monitoreando NQ @ {close_price:.2f} | Vel: {velocidad_flujo:.1f}/{umbral_vel:.0f}", {}

        # --- ESTADO EXCESO: Seguir el extremo mientras continúe la velocidad ---
        elif self.state == "EXCESO":
            self.ticks_en_exceso += 1

            if self.lado_exceso > 0:
                if close_price > self.extremo_exceso:
                    self.extremo_exceso = close_price
            else:
                if close_price < self.extremo_exceso:
                    self.extremo_exceso = close_price

            # Si la velocidad empieza a ceder, pasar a vigilar la rotación
            if velocidad_flujo < (umbral_vel * 0.70) or self.ticks_en_exceso > 5:
                self.state = "ROTACION"
                logging.info(f"[E&R] >> Fin de Exceso. Extremo marcado en {self.extremo_exceso:.2f}. Esperando rotación...")

            return "HOLD", f"En Exceso ({self.extremo_exceso:.2f})", {}

        # --- ESTADO ROTACION: Confirmar giro de precio ---
        elif self.state == "ROTACION":
            self.ticks_en_exceso += 1

            # Si vence el plazo de espera sin rotar, descartar
            if self.ticks_en_exceso > max_ticks_espera:
                logging.info("[E&R] Rotación no confirmada a tiempo. Evento descartado (Vuelto a IDLE).")
                self.state = "IDLE"
                self.cooldown_ticks = 10
                return "HOLD", "Rotación caducada", {}

            # Calcular distancia girada desde el extremo
            distancia_rotada = 0.0
            if self.lado_exceso > 0:
                # Exceso fue compra: rotación es hacia abajo (VENTA)
                distancia_rotada = self.extremo_exceso - close_price
                if distancia_rotada >= rotacion_min_pts:
                    # Señal de VENTA confirmada por rotación
                    if bias in ["AMBOS", "SOLO_CORTOS"]:
                        return self._disparar_senal("SELL", close_price, nivel_nombre, nivel_precio, velocidad_flujo)

            elif self.lado_exceso < 0:
                # Exceso fue venta: rotación es hacia arriba (COMPRA)
                distancia_rotada = close_price - self.extremo_exceso
                if distancia_rotada >= rotacion_min_pts:
                    # Señal de COMPRA confirmada por rotación
                    if bias in ["AMBOS", "SOLO_LARGOS"]:
                        return self._disparar_senal("BUY", close_price, nivel_nombre, nivel_precio, velocidad_flujo)

            return "HOLD", f"Esperando rotación ({distancia_rotada:.2f}/{rotacion_min_pts:.2f} pts)", {}

        return "HOLD", "Hold", {}

    def _disparar_senal(self, accion: str, precio: float, nivel_nombre: str, nivel_precio: float, velocidad: float):
        delta_tipo = "CONVERGENTE" if (accion == "BUY" and self.delta_acumulado > 0) or (accion == "SELL" and self.delta_acumulado < 0) else "DIVERGENTE"

        setup_snapshot = {
            "direccion": accion,
            "precio_nq": precio,
            "nivel_cercano": nivel_nombre,
            "distancia_nivel": abs(precio - nivel_precio) if nivel_precio > 0 else 0.0,
            "distancia_vwap": abs(precio - self.vwap) if self.vwap > 0 else 0.0,
            "velocidad_exceso": velocidad,
            "delta_acumulado": self.delta_acumulado,
            "delta_tipo": delta_tipo,
            "parametros": self.config.get("exceso_rotacion", {})
        }

        motivo = (
            f"Rotación confirmada tras Exceso en {self.extremo_exceso:.2f} | "
            f"Nivel: {nivel_nombre} | Delta: {delta_tipo} ({self.delta_acumulado:+.0f})"
        )

        # Resetear máquina a IDLE con cooldown para evitar órdenes duplicadas inmediatas
        self.state = "IDLE"
        self.cooldown_ticks = 40

        logging.info(f"*** [SEÑAL E&R GENERADA] -> {accion} en NQ {precio:.2f} ({motivo}) ***")
        return accion, motivo, setup_snapshot

    def _buscar_nivel_cercano(self, precio: float) -> tuple[str, float]:
        cfg_niveles = self.config.get("niveles_relevantes", {})
        tolerancia = float(cfg_niveles.get("tolerancia_nivel_puntos", 5.0))

        candidatos = []
        if cfg_niveles.get("usar_europa_hl", True):
            if self.europa_high > 0: candidatos.append(("EUROPA_HIGH", self.europa_high))
            if self.europa_low < float('inf'): candidatos.append(("EUROPA_LOW", self.europa_low))
        if cfg_niveles.get("usar_asia_hl", True):
            if self.asia_high > 0: candidatos.append(("ASIA_HIGH", self.asia_high))
            if self.asia_low < float('inf'): candidatos.append(("ASIA_LOW", self.asia_low))
        if cfg_niveles.get("usar_vwap", True) and self.vwap > 0:
            candidatos.append(("RTH_VWAP", self.vwap))

        for nombre, nivel_px in candidatos:
            if abs(precio - nivel_px) <= tolerancia:
                return nombre, nivel_px

        return "ZONA_LIBRE", precio

    def _actualizar_niveles_sesion(self, market_data: dict, close_price: float):
        # Si NinjaTrader envía niveles precalculados en el payload, usarlos directamente
        if "europa_h" in market_data: self.europa_high = float(market_data["europa_h"])
        if "europa_l" in market_data: self.europa_low = float(market_data["europa_l"])
        if "asia_h" in market_data: self.asia_high = float(market_data["asia_h"])
        if "asia_l" in market_data: self.asia_low = float(market_data["asia_l"])
        if "vwap" in market_data: self.vwap = float(market_data["vwap"])

