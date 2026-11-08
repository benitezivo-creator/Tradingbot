import logging
from collections import deque
from datetime import datetime

class ExcesoRotacionEngine:
    """
    Motor cuantitativo-discrecional de Exceso y Rotación (E&R):
    - Conforme a E&R/EyR_logica.txt:
        1. Niveles Institucionales: Europa H/L > Asia H/L > VWAP > Intradía
        2. Detección de Exceso por velocidad de agresión (adaptativo NQ vs MNQ)
        3. Detección de Rotación desde el extremo del exceso
        4. Gatillo de Apertura (09:30 - 09:45 NY)
        5. Validación de Delta Convergente vs Divergente
        6. Gestión estricta de posición única (sin re-entradas hasta FLAT)
    """

    def __init__(self, config_manager):
        self.config = config_manager
        
        # Niveles institucionales
        self.asia_high = 0.0
        self.asia_low = 0.0
        self.europa_high = 0.0
        self.europa_low = 0.0
        self.vwap = 0.0

        # Historial de trades y agresión
        self.price_history = deque(maxlen=100)
        self.flow_window = deque(maxlen=20)
        self.delta_acumulado = 0.0
        self.last_price = 0.0

        # Máquina de estados: IDLE -> EXCESO -> ROTACION -> EN_POSICION
        self.state = "IDLE"
        self.lado_exceso = 0  # +1 Compra, -1 Venta
        self.extremo_exceso = 0.0
        self.ticks_en_exceso = 0
        self.cooldown_ticks = 0
        self.trades_hoy = 0
        self.ticks_totales = 0
        self.ultimo_diagnostico = ""

    def notificar_trade_cerrado(self, pnl_puntos: float):
        cfg_cd = int(self.config.get("operativa", {}).get("cooldown_ticks_post_trade", 50))
        self.state = "IDLE"
        self.cooldown_ticks = cfg_cd
        self.trades_hoy += 1
        logging.info(f"[E&R ESTADO] Trade finalizado (PnL: {pnl_puntos:+.2f} pts). Máquina liberada. Cooldown activo por {cfg_cd} ticks.")

    def procesar_tick_nq(self, market_data: dict) -> tuple[str, str, dict, str]:
        """
        Retorna: (accion, motivo, setup_snapshot, hud_info)
        """
        close_price = float(market_data.get("close", 0.0))
        high_price = float(market_data.get("high", close_price))
        low_price = float(market_data.get("low", close_price))
        instrument = market_data.get("instrument", "").upper()
        time_str = market_data.get("time", "")

        if close_price <= 0:
            return "HOLD", "Precio inválido", {}, "IDLE|ERROR_PX|0.0"

        self.ticks_totales += 1
        self.price_history.append(close_price)
        if self.cooldown_ticks > 0:
            self.cooldown_ticks -= 1

        # 1. Actualizar Niveles y VWAP desde el payload
        self._actualizar_niveles_sesion(market_data)

        # 2. Bloqueo si ya estamos en posición
        if self.state == "EN_POSICION":
            hud_info = f"EN_POSICION|{close_price:.2f}|GESTION_3_TERCIOS"
            return "HOLD", "Posición abierta en mercado. Gestionando 3 tercios...", {}, hud_info

        # 3. Límite de trades diarios
        max_trades = int(self.config.get("operativa", {}).get("max_trades_dia", 10))
        if self.trades_hoy >= max_trades:
            hud_info = f"BLOQUEADO|LIMITE_TRADES|{self.trades_hoy}/{max_trades}"
            return "HOLD", f"Límite diario alcanzado ({self.trades_hoy}/{max_trades} trades).", {}, hud_info

        # 4. Cooldown post-trade
        if self.cooldown_ticks > 0:
            hud_info = f"COOLDOWN|{self.cooldown_ticks}t|{close_price:.2f}"
            return "HOLD", f"En cooldown post-trade ({self.cooldown_ticks} ticks restantes)", {}, hud_info

        # 5. Cálculo de delta y velocidad de flujo de agresión
        # Si NinjaTrader envía el delta de tick directo:
        delta_tick = float(market_data.get("delta", 0.0))
        tick_vol = float(market_data.get("volume", 1.0))
        
        # Fallback si no viene delta explícito
        if delta_tick == 0.0:
            if self.last_price > 0:
                if close_price > self.last_price:
                    delta_tick = tick_vol
                elif close_price < self.last_price:
                    delta_tick = -tick_vol
                else:
                    delta_tick = tick_vol * (1 if self.lado_exceso >= 0 else -1)
            else:
                delta_tick = tick_vol
        self.last_price = close_price

        self.delta_acumulado += delta_tick
        self.flow_window.append(abs(delta_tick))
        velocidad_flujo = sum(self.flow_window) / max(1, len(self.flow_window))

        # 6. Umbral de velocidad según instrumento (NQ institucional vs MNQ micro)
        cfg_er = self.config.get("exceso_rotacion", {})
        es_micro = "MNQ" in instrument
        if es_micro:
            umbral_vel = float(cfg_er.get("umbral_velocidad_exceso_mnq", 6.0))
        else:
            umbral_vel = float(cfg_er.get("umbral_velocidad_exceso_nq", 35.0))

        rotacion_min_pts = float(cfg_er.get("rotacion_minima_puntos", 5.0))
        max_ticks_espera = int(cfg_er.get("ventana_ticks_rotacion", 100))
        
        cfg_op = self.config.get("operativa", {})
        bias = cfg_op.get("bias_permitido", "AMBOS")
        requerir_nivel = cfg_op.get("requerir_nivel_estricto", False)

        # 7. Detección de Nivel Clave (Europa H/L, Asia H/L, VWAP)
        nivel_nombre, nivel_precio = self._buscar_nivel_cercano(high_price, low_price, close_price)
        dist_nivel = abs(close_price - nivel_precio) if nivel_precio > 0 else 0.0

        # HUD format: ESTADO|NIVEL|VEL/UMBRAL
        hud_info = f"{self.state}|{nivel_nombre}|{velocidad_flujo:.1f}/{umbral_vel:.1f}"

        # Diagnóstico en consola cada 20 ticks
        if self.ticks_totales % 20 == 0:
            logging.info(
                f"[E&R #{self.ticks_totales}] {instrument} @ {close_price:.2f} | "
                f"Nivel: {nivel_nombre} ({dist_nivel:.1f}p) | Vel: {velocidad_flujo:.1f}/{umbral_vel:.1f} | "
                f"Delta: {self.delta_acumulado:+.0f} | Estado: {self.state}"
            )

        # Si se exige nivel estricto y estamos en zona libre, no operar
        if requerir_nivel and nivel_nombre == "ZONA_LIBRE":
            if self.state != "IDLE":
                self.state = "IDLE"
            return "HOLD", f"Precio @ {close_price:.2f} fuera de nivel institucional.", {}, hud_info

        # --- ESTADO IDLE: Detectar inicio de Exceso ---
        if self.state == "IDLE":
            if velocidad_flujo >= umbral_vel:
                if delta_tick >= 0:
                    self.lado_exceso = +1
                    self.extremo_exceso = high_price
                else:
                    self.lado_exceso = -1
                    self.extremo_exceso = low_price

                self.ticks_en_exceso = 0
                self.state = "EXCESO"
                tipo_agresion = "COMPRA" if self.lado_exceso > 0 else "VENTA"
                logging.info(f">>> [E&R] EXCESO DETECTADO ({tipo_agresion}) en {nivel_nombre} ({instrument} @ {close_price:.2f} | Vel: {velocidad_flujo:.1f} >= {umbral_vel:.1f})")

            return "HOLD", f"Monitoreando en {nivel_nombre} @ {close_price:.2f}", {}, hud_info

        # --- ESTADO EXCESO: Rastrear extremo mientras dure la aceleración ---
        elif self.state == "EXCESO":
            self.ticks_en_exceso += 1

            if self.lado_exceso > 0 and high_price > self.extremo_exceso:
                self.extremo_exceso = high_price
            elif self.lado_exceso < 0 and low_price < self.extremo_exceso:
                self.extremo_exceso = low_price

            # Agotamiento de la velocidad o exceso sostenido -> pasar a esperar rotación
            if velocidad_flujo < (umbral_vel * 0.75) or self.ticks_en_exceso > 15:
                self.state = "ROTACION"
                self.ticks_en_exceso = 0
                logging.info(f">>> [E&R] Agotamiento de Exceso. Extremo: {self.extremo_exceso:.2f}. Esperando rotación de {rotacion_min_pts:.1f} pts...")

            return "HOLD", f"Siguiendo Exceso ({self.extremo_exceso:.2f})", {}, hud_info

        # --- ESTADO ROTACION: Confirmar giro desde el extremo ---
        elif self.state == "ROTACION":
            self.ticks_en_exceso += 1

            if self.ticks_en_exceso > max_ticks_espera:
                logging.info(f"[E&R] Rotación caducada ({self.ticks_en_exceso} ticks sin giro de {rotacion_min_pts} pts). Regresando a IDLE.")
                self.state = "IDLE"
                self.cooldown_ticks = 25
                return "HOLD", "Rotación caducada", {}, hud_info

            # Rotación tras exceso de COMPRA -> Giro a la baja -> VENTA (SELL)
            if self.lado_exceso > 0:
                distancia = self.extremo_exceso - close_price
                if distancia >= rotacion_min_pts:
                    if bias in ["AMBOS", "SOLO_CORTOS"]:
                        accion, motivo, snap = self._disparar_senal("SELL", close_price, nivel_nombre, nivel_precio, velocidad_flujo, instrument, time_str)
                        return accion, motivo, snap, f"ORDEN|SELL|{close_price:.2f}"

            # Rotación tras exceso de VENTA -> Giro al alza -> COMPRA (BUY)
            elif self.lado_exceso < 0:
                distancia = close_price - self.extremo_exceso
                if distancia >= rotacion_min_pts:
                    if bias in ["AMBOS", "SOLO_LARGOS"]:
                        accion, motivo, snap = self._disparar_senal("BUY", close_price, nivel_nombre, nivel_precio, velocidad_flujo, instrument, time_str)
                        return accion, motivo, snap, f"ORDEN|BUY|{close_price:.2f}"

            return "HOLD", f"Esperando rotación en {nivel_nombre} (Extremo: {self.extremo_exceso:.2f})", {}, hud_info

        return "HOLD", "Hold", {}, hud_info

    def _disparar_senal(self, accion: str, precio: float, nivel_nombre: str, nivel_precio: float, velocidad: float, instrument: str, time_str: str):
        delta_tipo = "CONVERGENTE" if (accion == "BUY" and self.delta_acumulado > 0) or (accion == "SELL" and self.delta_acumulado < 0) else "DIVERGENTE"

        # Detección de Gatillo de Apertura (09:30 - 09:45 NY)
        es_apertura = False
        if time_str and ("T09:3" in time_str or "T09:4" in time_str or " 09:3" in time_str or " 09:4" in time_str):
            es_apertura = True

        tag_setup = f"GATILLO_APERTURA_{accion}" if es_apertura else f"ROTACION_{accion}"

        setup_snapshot = {
            "direccion": accion,
            "tipo_setup": tag_setup,
            "precio_analisis": precio,
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
            f"Nivel: {nivel_nombre} | Delta: {delta_tipo} ({self.delta_acumulado:+.0f})"
        )

        # BLOQUEAR la máquina en EN_POSICION hasta que el trade se cierre completamente
        self.state = "EN_POSICION"

        logging.info("=" * 80)
        logging.info(f"*** [ORDEN DISPARADA] >>> {accion} @ {precio:.2f} ({motivo}) ***")
        logging.info("=" * 80)
        return accion, motivo, setup_snapshot

    def _buscar_nivel_cercano(self, high: float, low: float, close: float) -> tuple[str, float]:
        cfg_niveles = self.config.get("niveles_relevantes", {})
        tolerancia = float(cfg_niveles.get("tolerancia_nivel_puntos", 15.0))

        # Jerarquía según EyR_logica.txt: Europa H/L > Asia H/L > VWAP
        candidatos = []
        if cfg_niveles.get("usar_europa_hl", True):
            if self.europa_high > 0: candidatos.append(("EUROPA_HIGH", self.europa_high))
            if self.europa_low > 0: candidatos.append(("EUROPA_LOW", self.europa_low))
        if cfg_niveles.get("usar_asia_hl", True):
            if self.asia_high > 0: candidatos.append(("ASIA_HIGH", self.asia_high))
            if self.asia_low > 0: candidatos.append(("ASIA_LOW", self.asia_low))
        if cfg_niveles.get("usar_vwap", True) and self.vwap > 0:
            candidatos.append(("RTH_VWAP", self.vwap))

        # Verificar si la mecha del rango [low - tol, high + tol] testea algún nivel
        for nombre, nivel_px in candidatos:
            if (low - tolerancia) <= nivel_px <= (high + tolerancia):
                return nombre, nivel_px

        # Si hay niveles registrados pero el precio está más lejos que tolerancia
        if candidatos:
            return "ZONA_LIBRE", close

        # Si aún no hay niveles cargados en el histórico (ej. inicio inmediato)
        return "INTRADIA_ROTACION", close

    def _actualizar_niveles_sesion(self, market_data: dict):
        if float(market_data.get("europa_h", 0)) > 0: self.europa_high = float(market_data["europa_h"])
        if float(market_data.get("europa_l", 0)) > 0: self.europa_low = float(market_data["europa_l"])
        if float(market_data.get("asia_h", 0)) > 0: self.asia_high = float(market_data["asia_h"])
        if float(market_data.get("asia_l", 0)) > 0: self.asia_low = float(market_data["asia_l"])
        if float(market_data.get("vwap", 0)) > 0: self.vwap = float(market_data["vwap"])
