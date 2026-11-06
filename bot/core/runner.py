import socket
import json
import logging
import os
import sys
from collections import deque
import torch

# Añadir raíz del proyecto al sys.path para importaciones
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
        
        # Buffer de precios para alimentar el modelo (10 características)
        self.price_buffer = deque(maxlen=10)
        self.actions = {0: "HOLD", 1: "BUY", 2: "SELL"}

        # Cargar modelo entrenado si existe
        if weights_path is None:
            weights_path = os.path.join(BASE_DIR, "bot", "models", "model_weights.pth")
            
        self.model = LocalDecisionModel(input_size=10, num_classes=3).to(self.device)
        if os.path.exists(weights_path):
            try:
                self.model.load_state_dict(torch.load(weights_path, map_location=self.device))
                self.model.eval()
                logging.info(f"Pesos cargados exitosamente desde: {weights_path}")
            except Exception as e:
                logging.warning(f"No se pudieron cargar los pesos ({e}). Usando inicialización por defecto.")
        else:
            logging.info(f"No se encontró archivo de pesos en {weights_path}. Usando modelo base.")

    def evaluate_decision(self, market_data: dict) -> str:
        close_price = float(market_data.get("close", 0.0))
        if close_price <= 0:
            return "HOLD"

        self.price_buffer.append(close_price)

        # Esperar hasta tener 10 precios para la primera inferencia
        if len(self.price_buffer) < 10:
            return "HOLD"

        # Inferencia con PyTorch
        try:
            with torch.no_grad():
                # Normalizar ventana respecto al último precio para estabilidad numérica
                prices = list(self.price_buffer)
                norm_prices = [p / prices[-1] - 1.0 for p in prices]
                input_tensor = torch.tensor(norm_prices, dtype=torch.float32).unsqueeze(0).to(self.device)
                
                output = self.model(input_tensor)
                decision_idx = int(torch.argmax(output, dim=1).item())
                action = self.actions.get(decision_idx, "HOLD")
                return action
        except Exception as e:
            logging.error(f"Error durante inferencia: {e}")
            return "HOLD"

    def start(self):
        server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server_socket.bind((self.host, self.port))
        server_socket.listen(1)
        
        logging.info("=" * 60)
        logging.info(f" Servidor TCP listo. Esperando conexión de NinjaTrader en {self.host}:{self.port}")
        logging.info("=" * 60)

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
                            action = self.evaluate_decision(payload)
                            tick_count += 1
                            
                            # Mostrar log de tick cada 10 ticks o cuando hay señal
                            if action != "HOLD" or tick_count % 10 == 0:
                                logging.info(
                                    f"Tick #{tick_count} | {payload.get('instrument')} @ {payload.get('close')} | "
                                    f"Vol: {payload.get('volume')} | Acción: {action}"
                                )

                            if action != "HOLD":
                                logging.info(f">>> ENVIANDO SEÑAL A NINJATRADER: {action}")
                                conn.sendall(f"{action}\n".encode('utf-8'))
                        except json.JSONDecodeError as err:
                            logging.error(f"Error decodificando JSON: '{line}' ({err})")
                            continue

            except KeyboardInterrupt:
                logging.info("Servidor detenido por el usuario.")
                break
            except Exception as e:
                logging.error(f"Error en el socket del servidor: {e}")
            finally:
                if 'conn' in locals() and conn:
                    conn.close()

if __name__ == "__main__":
    bot = TradingBotServer()
    bot.start()
