import logging
from collections import deque
from datetime import datetime

class ExcesoRotacionEngine:
    """
    Motor cuantitativo-discrecional de Exceso y Rotación (E&R) estricto:
    - REGLA DE ORO 1: Si ya está en posición (EN_POSICION), NO emite más órdenes.
    - REGLA DE ORO 2: Solo opera en NIVELES CLAVE (Europa H/L, Asia H/L, VWAP). Prohibido en 'ZONA_LIBRE'.
    - REGLA DE ORO 3: Cooldown obligatorio tras cerrar un trade.
    - AUTO-ADAPTACIÓN: Umbral de velocidad adaptativo para NQ vs MNQ.
    """

    def __init__(self, config_manager):
        self.config = config_manager
        
        # Niveles institucionales
        self.asia_high = 0.0
        self.asia_low = 0.0
        self.europa_high = 0.0
        self.europa_low = 0.0
        self.vwap = 0.0

        # Historial de trades
        self.price_history = deque(maxlen=100)
        self.flow_window = deque(maxlen=20)
        self.delta_acumulado = 0.0

        # Máquina de estados: IDLE -> EXCESO -> ROTACION -> EN_POSICION
        self.state = "IDLE"
        self.lado_exceso = 0  # +1 Compra, -1 Venta
        self.extremo_exceso = 0.0
        self.ticks_en_exceso = 0
        self.cooldown_ticks = 0
        self.trades_hoy = 0
        self.ticks_totales = 0

    def notificar_trade_cerrado(self, pnl_puntos: float):
        cfg_cd = int(self.config.get("operativa", {}).get("cooldown_ticks_post_trade", 80))
        self.state = "IDLE"
        self.cooldown_ticks = cfg_cd
        self.trades_hoy += 1
        logging.info(f"[E&R ESTADO] Trade finalizado (PnL: {pnl_puntos:+.2f} pts). Máquina liberada. Cooldown activo por {cfg_cd} ticks.")

    def procesar_tick_nq(self, market_data: dict) -> tuple[str, str, dict]:
        close_price = float(market_data.get("close", 0.0))
        high_price = float(market_data.get("high", close_price))
        low_price = float(market_data.get("low", close_price))
        volume = float(market_data.get("volume", 1.0))
        instrument = market_data.get("instrument", "").upper()

        if close_price <= 0:
            return "HOLD", "Precio inválido", {}

        self.ticks_totales += 1
        self.price_history.append(close_price)
        if self.cooldown_ticks > 0:
            self.cooldown_ticks -= 1

        # 1. Actualizar Niveles y VWAP desde el payload de NinjaTrader
        self._actualizar_niveles_sesion(market_data, close_price)

        # 2. Bloqueo si ya estamos en posición
        if self.state == "EN_POSICION":
            return "HOLD", "Posición abierta en mercado. Gestionando 3 tercios...", {}

        # 3. Límite de trades diarios
        max_trades = int(self.config.get("operativa", {}).get("max_trades_dia", 5))
        if self.trades_hoy >= max_trades:
            return "HOLD", f"Límite diario alcanzado ({self.trades_hoy}/{max_trades} trades).", {}

        # 4. Cooldown post-trade
        if self.cooldown_ticks > 0:
            return "HOLD", f"En cooldown post-trade ({self.cooldown_ticks} ticks restantes)", {}

        # 5. Cálculo de velocidad de agresión
        delta_tick = float(market_data.get("delta", 0.0))
        if delta_tick == 0.0 and len(self.price_history) >= 2:
            delta_tick = volume if close_price >= self.price_history[-2] else -volume

        self.delta_acumulado += delta_tick
        self.flow_window.append(abs(delta_tick))
        velocidad_flujo = sum(self.flow_window) / max(1, len(self.flow_window))

        # 6. Umbral adaptativo (MNQ vs NQ)
        cfg_er = self.config.get("exceso_rotacion", {})
        umbral_base = float(cfg_er.get("umbral_velocidad_exceso", 60.0))
        es_micro = "MNQ" in instrument
        umbral_vel = max(8.0, umbral_base * 0.15) if es_micro else umbral_base
        rotacion_min_pts = float(cfg_er.get("rotacion_minima_puntos", 6.0))
        max_ticks_espera = int(cfg_er.get("ventana_ticks_rotacion", 50))
        
        cfg_op = self.config.get("operativa", {})
        bias = cfg_op.get("bias_permitido", "AMBOS")
        requerir_nivel = cfg_op.get("requerir_nivel_estricto", True)

        # 7. Detección de Nivel Clave (considerando mecha High/Low del rango)
        nivel_nombre, nivel_precio = self._buscar_nivel_cercano(high_price, low_price, close_price)

        # Heartbeat de diagnóstico periódico cada 25 ticks
        if self.ticks_totales % 25 == 0:
            dist_txt = f"{abs(close_price - nivel_precio):.1f} pts" if nivel_precio > 0 else "N/A"
            logging.info(
                f"[DIAGNÓSTICO] #{self.ticks_totales} | {instrument} @ {close_price:.2f} | "
                f"Nivel: {nivel_nombre} (dist: {dist_txt}) | Vel: {velocidad_flujo:.1f}/{umbral_vel:.1f} | Estado: {self.state}"
            )

        # Si se exige nivel y estamos en Zona Libre, no operar
        if requerir_nivel and nivel_nombre == "ZONA_LIBRE":
            if self.state != "IDLE":
                self.state = "IDLE"
            return "HOLD", f"Precio @ {close_price:.2f} en Zona Libre (Sin nivel institucional).", {}

        # --- ESTADO IDLE: Buscar inicio de Exceso EN EL NIVEL ---
        if self.state == "IDLE":
            if velocidad_flujo >= umbral_vel:
                if delta_tick > 0:
                    self.lado_exceso = +1
                    self.extremo_exceso = high_price
                else:
                    self.lado_exceso = -1
                    self.extremo_exceso = low_price

                self.ticks_en_exceso = 0
                self.state = "EXCESO"
                logging.info(f"[E&R] >> EXCESO INSTITUCIONAL en {nivel_nombre} ({instrument} @ {close_price:.2f} | Vel: {velocidad_flujo:.1f} >= {umbral_vel:.1f})")

            return "HOLD", f"Monitoreando en {nivel_nombre} @ {close_price:.2f}", {}

        # --- ESTADO EXCESO: Rastrear extremo mientras dure la agresión ---
        elif self.state == "EXCESO":
            self.ticks_en_exceso += 1

            if self.lado_exceso > 0 and high_price > self.extremo_exceso:
                self.extremo_exceso = high_price
            elif self.lado_exceso < 0 and low_price < self.extremo_exceso:
                self.extremo_exceso = low_price

            if velocidad_flujo < (umbral_vel * 0.70) or self.ticks_en_exceso > 10:
                self.state = "ROTACION"
                logging.info(f"[E&R] >> Agotamiento de Exceso. Extremo: {self.extremo_exceso:.2f}. Esperando rotación...")

            return "HOLD", f"Siguiendo Exceso ({self.extremo_exceso:.2f})", {}

        # --- ESTADO ROTACION: Confirmar giro ---
        elif self.state == "ROTACION":
            self.ticks_en_exceso += 1

            if self.ticks_en_exceso > max_ticks_espera:
                logging.info("[E&R] Rotación caducada sin giro suficiente. Cancelando evento.")
                self.state = "IDLE"
                self.cooldown_ticks = 30
                return "HOLD", "Rotación caducada", {}

            # Rotación desde extremo
            if self.lado_exceso > 0:
                distancia = self.extremo_exceso - close_price
                if distancia >= rotacion_min_pts:
                    if bias in ["AMBOS", "SOLO_CORTOS"]:
                        return self._disparar_senal("SELL", close_price, nivel_nombre, nivel_precio, velocidad_flujo, instrument)

            elif self.lado_exceso < 0:
                distancia = close_price - self.extremo_exceso
                if distancia >= rotacion_min_pts:
                    if bias in ["AMBOS", "SOLO_LARGOS"]:
                        return self._disparar_senal("BUY", close_price, nivel_nombre, nivel_precio, velocidad_flujo, instrument)

            return "HOLD", f"Esperando rotación en {nivel_nombre}", {}

        return "HOLD", "Hold", {}

    def _disparar_senal(self, accion: str, precio: float, nivel_nombre: str, nivel_precio: float, velocidad: float, instrument: str):
        delta_tipo = "CONVERGENTE" if (accion == "BUY" and self.delta_acumulado > 0) or (accion == "SELL" and self.delta_acumulado < 0) else "DIVERGENTE"

        setup_snapshot = {
            "direccion": accion,
            "precio_nq": precio,
            "instrumento_analisis": instrument,
            "nivel_cercano": nivel_nombre,
            "distancia_nivel": abs(precio - nivel_precio) if nivel_precio > 0 else 0.0,
            "distancia_vwap": abs(precio - self.vwap) if self.vwap > 0 else 0.0,
            "velocidad_exceso": velocidad,
            "delta_acumulado": self.delta_acumulado,
            "delta_tipo": delta_tipo,
            "parametros": self.config.get("exceso_rotacion", {})
        }

        motivo = (
            f"Rotación confirmada ({accion}) tras Exceso en {self.extremo_exceso:.2f} | "
            f"Nivel: {nivel_nombre} | Delta: {delta_tipo}"
        )

        # BLOQUEAR la máquina en EN_POSICION hasta que el trade se cierre
        self.state = "EN_POSICION"

        logging.info(f"*** [ORDEN DISPARADA] -> {accion} en {nivel_nombre} @ {precio:.2f} ({motivo}) ***")
        return accion, motivo, setup_snapshot

    def _buscar_nivel_cercano(self, high: float, low: float, close: float) -> tuple[str, float]:
        cfg_niveles = self.config.get("niveles_relevantes", {})
        tolerancia = float(cfg_niveles.get("tolerancia_nivel_puntos", 10.0))

        candidatos = []
        if cfg_niveles.get("usar_europa_hl", True):
            if self.europa_high > 0: candidatos.append(("EUROPA_HIGH", self.europa_high))
            if self.europa_low > 0: candidatos.append(("EUROPA_LOW", self.europa_low))
        if cfg_niveles.get("usar_asia_hl", True):
            if self.asia_high > 0: candidatos.append(("ASIA_HIGH", self.asia_high))
            if self.asia_low > 0: candidatos.append(("ASIA_LOW", self.asia_low))
        if cfg_niveles.get("usar_vwap", True) and self.vwap > 0:
            candidatos.append(("RTH_VWAP", self.vwap))

        # Verificar si la mecha del rango [low - tol, high + tol] testea el nivel
        for nombre, nivel_px in candidatos:
            if (low - tolerancia) <= nivel_px <= (high + tolerancia):
                return nombre, nivel_px

        # Si aún no hay niveles cargados en el histórico (ej. apertura 00:00), permitir VWAP o zona activa
        if not candidatos:
            return "VWAP_INICIAL", close

        return "ZONA_LIBRE", close

    def _actualizar_niveles_sesion(self, market_data: dict, close_price: float):
        if float(market_data.get("europa_h", 0)) > 0: self.europa_high = float(market_data["europa_h"])
        if float(market_data.get("europa_l", 0)) > 0: self.europa_low = float(market_data["europa_l"])
        if float(market_data.get("asia_h", 0)) > 0: self.asia_high = float(market_data["asia_h"])
        if float(market_data.get("asia_l", 0)) > 0: self.asia_low = float(market_data["asia_l"])
        if float(market_data.get("vwap", 0)) > 0: self.vwap = float(market_data["vwap"])
