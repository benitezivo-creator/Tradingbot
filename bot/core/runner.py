import socket
import json
import logging
import os
import sys
from collections import deque
import torch
import torch.nn.functional as F

# Añadir raíz del proyecto al sys.path
BASE_DIR = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
if BASE_DIR not in sys.path:
    sys.path.insert(0, BASE_DIR)

from bot.models.train import LocalDecisionModel

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S"
)

class TradingBotServer:
    def __init__(self, host: str = "127.0.0.1", port: int = 5555, weights_path: str = None):
        self.host = host
        self.port = port
        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        logging.info(f"Iniciando Bot local en PyTorch (Dispositivo: {self.device})")
        
        # Buffer de precios (últimos 20 precios)
        self.price_buffer = deque(maxlen=20)
        self.actions = {0: "HOLD", 1: "BUY", 2: "SELL"}
        self.min_confidence = 0.60  # Exigir al menos 60% de certeza para emitir orden
        self.ticks_since_last_signal = 0
        self.cooldown_ticks = 15    # Esperar al menos 15 ticks entre señales para evitar sobre-operar

        # Cargar modelo entrenado si existe
        if weights_path is None:
            weights_path = os.path.join(BASE_DIR, "bot", "models", "model_weights.pth")
            
        self.model = LocalDecisionModel(input_size=10, num_classes=3).to(self.device)
        self.has_weights = False
        if os.path.exists(weights_path):
            try:
                self.model.load_state_dict(torch.load(weights_path, map_location=self.device))
                self.model.eval()
                self.has_weights = True
                logging.info(f"Pesos de red neuronal cargados desde: {weights_path}")
            except Exception as e:
                logging.warning(f"Error cargando pesos ({e}).")

    def evaluate_decision(self, market_data: dict) -> tuple[str, str]:
        """
        Evalúa el tick de mercado y devuelve (Acción, Motivo detallado).
        """
        close_price = float(market_data.get("close", 0.0))
        if close_price <= 0:
            return "HOLD", "Precio inválido"

        self.price_buffer.append(close_price)
        self.ticks_since_last_signal += 1

        # Requerir al menos 10 precios para alimentar la red
        if len(self.price_buffer) < 10:
            return "HOLD", f"Acumulando datos ({len(self.price_buffer)}/10 ticks)"

        # Extraer ventana de los últimos 10 precios
        prices = list(self.price_buffer)[-10:]

        try:
            with torch.no_grad():
                # 1. Normalización de precios respecto al primer tick de la ventana
                norm_prices = [(p - prices[0]) / (prices[0] * 0.001 + 1e-6) for p in prices]
                input_tensor = torch.tensor(norm_prices, dtype=torch.float32).unsqueeze(0).to(self.device)
                
                # 2. Inferencia y cálculo de probabilidades (Softmax)
                logits = self.model(input_tensor)
                probs = F.softmax(logits, dim=1).squeeze(0)
                
                prob_hold = float(probs[0].item())
                prob_buy = float(probs[1].item())
                prob_sell = float(probs[2].item())

                decision_idx = int(torch.argmax(probs).item())
                max_prob = max(prob_hold, prob_buy, prob_sell)
                raw_action = self.actions.get(decision_idx, "HOLD")

                # Criterio explicativo para el usuario
                detalle = (
                    f"P(HOLD): {prob_hold*100:.1f}% | P(BUY): {prob_buy*100:.1f}% | "
                    f"P(SELL): {prob_sell*100:.1f}%"
                )

                # Si no supera el umbral de confianza, no enviar orden
                if max_prob < self.min_confidence:
                    return "HOLD", f"{detalle} -> Confianza {max_prob*100:.1f}% < {self.min_confidence*100:.0f}%"

                # Cooldown entre señales consecutivas
                if raw_action != "HOLD" and self.ticks_since_last_signal < self.cooldown_ticks:
                    return "HOLD", f"{detalle} -> En cooldown ({self.ticks_since_last_signal}/{self.cooldown_ticks} ticks)"

                if raw_action != "HOLD":
                    self.ticks_since_last_signal = 0

                return raw_action, detalle

        except Exception as e:
            logging.error(f"Error durante inferencia: {e}")
            return "HOLD", f"Excepción en inferencia: {e}"

    def start(self):
        server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server_socket.bind((self.host, self.port))
        server_socket.listen(1)
        
        logging.info("=" * 70)
        logging.info(f" Servidor TCP listo en {self.host}:{self.port}")
        logging.info(f" Umbral de confianza mínimo: {self.min_confidence*100:.0f}% | Cooldown: {self.cooldown_ticks} ticks")
        logging.info("=" * 70)

        tick_count = 0
        while True:
            try:
                conn, addr = server_socket.accept()
                logging.info(f"[+] Conexión establecida con NinjaTrader desde {addr}")
                
                buffer = ""
                while True:
                    data = conn.recv(2048).decode('utf-8')
                    if not data:
                        logging.warning("[-] NinjaTrader cerró la conexión.")
                        break
                    
                    buffer += data
                    while "\n" in buffer:
                        line, buffer = buffer.split("\n", 1)
                        line = line.strip()
                        if not line:
                            continue
                            
                        try:
                            payload = json.loads(line)
                            action, motivo = self.evaluate_decision(payload)
                            tick_count += 1
                            
                            # Mostrar detalle periódico o cuando hay señal
                            if action != "HOLD" or tick_count % 15 == 0:
                                logging.info(
                                    f"Tick #{tick_count} | {payload.get('instrument')} @ {payload.get('close')} | "
                                    f"Acción: {action} | {motivo}"
                                )

                            if action != "HOLD":
                                logging.info(f">>> [ORDEN EMITIDA A NINJA] -> {action} ({motivo})")
                                conn.sendall(f"{action}\n".encode('utf-8'))
                        except json.JSONDecodeError as err:
                            logging.error(f"Error decodificando JSON: '{line}' ({err})")
                            continue

            except KeyboardInterrupt:
                logging.info("Servidor detenido manualmente.")
                break
            except Exception as e:
                logging.error(f"Error en el servidor: {e}")
            finally:
                if 'conn' in locals() and conn:
                    conn.close()

if __name__ == "__main__":
    bot = TradingBotServer()
    bot.start()
