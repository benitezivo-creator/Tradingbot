import logging
from collections import deque
from datetime import datetime

class ExcesoRotacionEngine:
    """
    Motor cuantitativo-discrecional de Exceso y Rotación (E&R) estricto:
    - REGLA DE ORO 1: Si ya está en posición (EN_POSICION), NO emite más órdenes.
    - REGLA DE ORO 2: Solo opera en NIVELES CLAVE (Europa H/L, Asia H/L, VWAP). Prohibido en 'ZONA_LIBRE'.
    - REGLA DE ORO 3: Cooldown obligatorio tras cerrar un trade.
    """

    def __init__(self, config_manager):
        self.config = config_manager
        
        # Niveles institucionales
        self.asia_high = float('-inf')
        self.asia_low = float('inf')
        self.europa_high = float('-inf')
        self.europa_low = float('inf')
        self.vwap = 0.0
        self.vwap_sum_pv = 0.0
        self.vwap_sum_vol = 0.0

        # Historial de trades
        self.price_history = deque(maxlen=100)
        self.flow_window = deque(maxlen=20)
        self.delta_acumulado = 0.0

        # Máquina de estados: IDLE -> EXCESO -> ROTACION -> EN_POSICION -> COOLDOWN
        self.state = "IDLE"
        self.lado_exceso = 0  # +1 Compra, -1 Venta
        self.extremo_exceso = 0.0
        self.ticks_en_exceso = 0
        self.cooldown_ticks = 0
        self.trades_hoy = 0

    def notificar_trade_cerrado(self, pnl_puntos: float):
        """
        Llamado cuando NinjaTrader notifica que el trade se cerró (SL o TP).
        Libera la máquina de estados y activa un cooldown riguroso.
        """
        cfg_cd = int(self.config.get("operativa", {}).get("cooldown_ticks_post_trade", 100))
        self.state = "IDLE"
        self.cooldown_ticks = cfg_cd
        self.trades_hoy += 1
        logging.info(f"[E&R ESTADO] Trade finalizado (PnL: {pnl_puntos:+.2f} pts). Máquina liberada. Cooldown activo por {cfg_cd} ticks.")

    def procesar_tick_nq(self, market_data: dict) -> tuple[str, str, dict]:
        close_price = float(market_data.get("close", 0.0))
        volume = float(market_data.get("volume", 1.0))

        if close_price <= 0:
            return "HOLD", "Precio inválido", {}

        self.price_history.append(close_price)
        if self.cooldown_ticks > 0:
            self.cooldown_ticks -= 1

        # Si ya estamos en posición, BLOQUEAR terminantemente nuevas señales
        if self.state == "EN_POSICION":
            return "HOLD", "Posición abierta en mercado. Gestionando 3 tercios...", {}

        # Límite de trades diarios
        max_trades = int(self.config.get("operativa", {}).get("max_trades_dia", 4))
        if self.trades_hoy >= max_trades:
            return "HOLD", f"Límite diario alcanzado ({self.trades_hoy}/{max_trades} trades).", {}

        # Si está en cooldown post-trade, esperar
        if self.cooldown_ticks > 0:
            return "HOLD", f"En cooldown post-trade ({self.cooldown_ticks} ticks restantes)", {}

        # 1. Actualizar VWAP acumulativo
        self.vwap_sum_pv += close_price * volume
        self.vwap_sum_vol += volume
        if self.vwap_sum_vol > 0:
            self.vwap = self.vwap_sum_pv / self.vwap_sum_vol

        # 2. Actualizar Niveles de Sesión si vienen en el payload
        self._actualizar_niveles_sesion(market_data, close_price)

        # 3. Flujo y Delta
        delta_tick = float(market_data.get("delta", 0.0))
        if delta_tick == 0.0 and len(self.price_history) >= 2:
            delta_tick = volume if close_price >= self.price_history[-2] else -volume

        self.delta_acumulado += delta_tick
        self.flow_window.append(abs(delta_tick))
        velocidad_flujo = sum(self.flow_window) / max(1, len(self.flow_window))

        # 4. Parámetros en caliente desde config.json
        cfg_er = self.config.get("exceso_rotacion", {})
        umbral_vel = float(cfg_er.get("umbral_velocidad_exceso", 120.0))
        rotacion_min_pts = float(cfg_er.get("rotacion_minima_puntos", 8.0))
        max_ticks_espera = int(cfg_er.get("ventana_ticks_rotacion", 50))
        
        cfg_op = self.config.get("operativa", {})
        bias = cfg_op.get("bias_permitido", "AMBOS")
        requerir_nivel_estricto = cfg_op.get("requerir_nivel_estricto", True)

        # 5. Detección de Nivel Clave
        nivel_nombre, nivel_precio = self._buscar_nivel_cercano(close_price)

        # --- REGLA ESTRICTA DE NIVEL: En Zona Libre NO se opera ---
        if requerir_nivel_estricto and nivel_nombre == "ZONA_LIBRE":
            if self.state != "IDLE":
                self.state = "IDLE"
            return "HOLD", f"Precio @ {close_price:.2f} en Zona Libre (Sin nivel institucional). No se opera.", {}

        # --- ESTADO IDLE: Buscar inicio de Exceso EN EL NIVEL ---
        if self.state == "IDLE":
            if velocidad_flujo >= umbral_vel:
                if delta_tick > 0:
                    self.lado_exceso = +1
                    self.extremo_exceso = close_price
                else:
                    self.lado_exceso = -1
                    self.extremo_exceso = close_price

                self.ticks_en_exceso = 0
                self.state = "EXCESO"
                logging.info(f"[E&R] >> EXCESO INSTITUCIONAL en {nivel_nombre} (NQ @ {close_price:.2f} | Vel: {velocidad_flujo:.0f})")

            return "HOLD", f"Monitoreando en {nivel_nombre} @ {close_price:.2f}", {}

        # --- ESTADO EXCESO: Rastrear extremo mientras dure la agresión ---
        elif self.state == "EXCESO":
            self.ticks_en_exceso += 1

            if self.lado_exceso > 0 and close_price > self.extremo_exceso:
                self.extremo_exceso = close_price
            elif self.lado_exceso < 0 and close_price < self.extremo_exceso:
                self.extremo_exceso = close_price

            if velocidad_flujo < (umbral_vel * 0.60) or self.ticks_en_exceso > 8:
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
                        return self._disparar_senal("SELL", close_price, nivel_nombre, nivel_precio, velocidad_flujo)

            elif self.lado_exceso < 0:
                distancia = close_price - self.extremo_exceso
                if distancia >= rotacion_min_pts:
                    if bias in ["AMBOS", "SOLO_LARGOS"]:
                        return self._disparar_senal("BUY", close_price, nivel_nombre, nivel_precio, velocidad_flujo)

            return "HOLD", f"Esperando rotación en {nivel_nombre}", {}

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
            f"Rotación confirmada ({accion}) tras Exceso en {self.extremo_exceso:.2f} | "
            f"Nivel: {nivel_nombre} | Delta: {delta_tipo}"
        )

        # BLOQUEAR la máquina en EN_POSICION hasta que el trade se cierre
        self.state = "EN_POSICION"

        logging.info(f"*** [ORDEN DISPARADA] -> {accion} en {nivel_nombre} @ {precio:.2f} ({motivo}) ***")
        return accion, motivo, setup_snapshot

    def _buscar_nivel_cercano(self, precio: float) -> tuple[str, float]:
        cfg_niveles = self.config.get("niveles_relevantes", {})
        tolerancia = float(cfg_niveles.get("tolerancia_nivel_puntos", 6.0))

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
        if "europa_h" in market_data: self.europa_high = float(market_data["europa_h"])
        if "europa_l" in market_data: self.europa_low = float(market_data["europa_l"])
        if "asia_h" in market_data: self.asia_high = float(market_data["asia_h"])
        if "asia_l" in market_data: self.asia_low = float(market_data["asia_l"])
        if "vwap" in market_data: self.vwap = float(market_data["vwap"])
