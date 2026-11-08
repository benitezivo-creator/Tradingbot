import socket
import select
import json
import logging
import os
import sys
from datetime import datetime

# Añadir raíz del proyecto al sys.path
BASE_DIR = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
if BASE_DIR not in sys.path:
    sys.path.insert(0, BASE_DIR)

from bot.core.config_manager import ConfigManager
from bot.core.dataset_recorder import DatasetRecorder
from bot.core.eyr_engine import ExcesoRotacionEngine

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S"
)

class TradingBotServer:
    def __init__(self, host: str = "127.0.0.1", port: int = 5555):
        self.host = host
        self.port = port
        
        # Módulos del sistema
        self.config_manager = ConfigManager()
        self.recorder = DatasetRecorder()
        self.eyr_engine = ExcesoRotacionEngine(self.config_manager)

        # Clientes conectados (Socket -> Metadatos/Rol)
        self.clients = {}  # socket -> {"role": "ANALYZER"|"EXECUTOR"|"HYBRID", "instrument": str, "buffer": str}
        self.analyzer_sockets = set()
        self.executor_sockets = set()
        self.active_trade_id = None
        self.tick_counter = 0

    def start(self):
        server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server_socket.bind((self.host, self.port))
        server_socket.listen(5)
        server_socket.setblocking(False)

        logging.info("=" * 75)
        logging.info(" SERVIDOR DE TRADINGBOT INICIADO (ARQUITECTURA EXCESO & ROTACIÓN)")
        logging.info(f" Host: {self.host}:{self.port}")
        logging.info(" Hot-Reloading de config.json: ACTIVO")
        logging.info(" Grabadora de Setups & Memoria: ACTIVA")
        logging.info(" Modo Multi-Instrumento: NQ (Análisis) -> MNQ (Ejecución)")
        logging.info("=" * 75)

        sockets_to_read = [server_socket]

        while True:
            try:
                # Recargar config.json si fue modificado en disco
                self.config_manager.reload_if_modified()

                # Multiplexado no bloqueante con select
                readable, _, _ = select.select(sockets_to_read, [], [], 0.5)

                for s in readable:
                    if s is server_socket:
                        # Nueva conexión entrante desde NinjaTrader
                        client_socket, client_addr = server_socket.accept()
                        client_socket.setblocking(False)
                        sockets_to_read.append(client_socket)
                        self.clients[client_socket] = {"role": "UNKNOWN", "instrument": "UNKNOWN", "buffer": ""}
                        logging.info(f"[+] Nueva conexión establecida desde {client_addr}")

                    else:
                        # Datos recibidos de un cliente conectado
                        try:
                            data = s.recv(4096).decode('utf-8')
                            if not data:
                                self._desconectar_cliente(s, sockets_to_read)
                                continue

                            client_info = self.clients[s]
                            client_info["buffer"] += data

                            while "\n" in client_info["buffer"]:
                                line, client_info["buffer"] = client_info["buffer"].split("\n", 1)
                                line = line.strip()
                                if line:
                                    self._procesar_mensaje_nt(s, line)

                        except ConnectionResetError:
                            self._desconectar_cliente(s, sockets_to_read)
                        except Exception as e:
                            logging.error(f"Error procesando socket: {e}")

            except KeyboardInterrupt:
                logging.info("Servidor detenido manualmente.")
                break
            except Exception as e:
                logging.error(f"Error en el bucle principal: {e}")

    def _procesar_mensaje_nt(self, client_sock, json_str: str):
        try:
            payload = json.loads(json_str)
        except json.JSONDecodeError:
            return

        instrument = payload.get("instrument", "").upper()
        role = payload.get("role", "")

        # Asignar o autodetectar rol del cliente
        client_info = self.clients.get(client_sock, {})
        if not role:
            if "MNQ" in instrument:
                role = "EXECUTOR"
            elif "NQ" in instrument:
                role = "ANALYZER"
            else:
                role = "HYBRID"

        if client_info.get("role") != role:
            client_info["role"] = role
            client_info["instrument"] = instrument
            if role == "ANALYZER":
                self.analyzer_sockets.add(client_sock)
                logging.info(f"[CANAL IDENTIFICADO] Gráfico de Análisis (NQ) registrado: {instrument}")
            elif role == "EXECUTOR":
                self.executor_sockets.add(client_sock)
                logging.info(f"[CANAL IDENTIFICADO] Gráfico de Ejecución (MNQ) registrado: {instrument}")
            else:
                self.analyzer_sockets.add(client_sock)
                self.executor_sockets.add(client_sock)
                logging.info(f"[CANAL IDENTIFICADO] Gráfico Híbrido registrado: {instrument}")

        # Si es un evento de trade cerrado / ejecución
        if "event" in payload and payload["event"] == "TRADE_CLOSED":
            pnl = float(payload.get("pnl_points", 0.0))
            resultado = "WIN" if pnl > 0 else "LOSS"
            if self.active_trade_id:
                self.recorder.update_trade_result(self.active_trade_id, resultado, pnl)
                self.active_trade_id = None
            self.eyr_engine.notificar_trade_cerrado(pnl)
            return

        # Si es un tick de análisis (proviene de NQ o gráfico único MNQ si no hay NQ)
        if client_sock in self.analyzer_sockets or len(self.clients) == 1:
            self.tick_counter += 1
            accion, motivo, setup_snapshot, hud_info = self.eyr_engine.procesar_tick_nq(payload)

            # Enviar actualización de HUD a NinjaTrader periódicamente
            if self.tick_counter % 10 == 0 and hud_info:
                self._enviar_hud_info(hud_info)

            if accion in ["BUY", "SELL", "FLATTEN"]:
                # Registrar el setup en la memoria viva para entrenamiento de PyTorch
                if setup_snapshot:
                    setup_snapshot["instrumento_analisis"] = instrument
                    setup_snapshot["instrumento_ejecucion"] = "MNQ"
                    self.active_trade_id = self.recorder.record_setup(setup_snapshot)

                # Despachar orden a todos los ejecutores (MNQ)
                self._despachar_orden(accion, motivo)

    def _enviar_hud_info(self, hud_info: str):
        msg = f"HUD:{hud_info}\n".encode('utf-8')
        for sock in list(self.clients.keys()):
            try:
                sock.sendall(msg)
            except:
                pass

    def _despachar_orden(self, accion: str, motivo: str):
        destinatarios = self.executor_sockets if self.executor_sockets else list(self.clients.keys())
        logging.info(f">>> [DESPACHANDO ORDEN A NINJATRADER] -> {accion} | Destinatarios: {len(destinatarios)} | Motivo: {motivo}")

        mensaje = f"{accion}\n".encode('utf-8')
        for sock in destinatarios:
            try:
                sock.sendall(mensaje)
            except Exception as e:
                logging.error(f"Error enviando orden al socket ejecutor: {e}")

    def _desconectar_cliente(self, s, sockets_list):
        if s in sockets_list:
            sockets_list.remove(s)
        self.analyzer_sockets.discard(s)
        self.executor_sockets.discard(s)
        client_info = self.clients.pop(s, {})
        try:
            s.close()
        except:
            pass
        logging.warning(f"[-] Cliente desconectado ({client_info.get('role', 'DESCONOCIDO')} - {client_info.get('instrument', '')})")

if __name__ == "__main__":
    bot = TradingBotServer()
    bot.start()
