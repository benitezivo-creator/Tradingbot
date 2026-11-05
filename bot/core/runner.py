import socket
import json
import logging
import time
import torch

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")

class TradingBotServer:
    def __init__(self, host: str = "127.0.0.1", port: int = 5555):
        self.host = host
        self.port = port
        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        logging.info(f"Iniciando Bot local en entorno PyTorch usando dispositivo: {self.device}")
        self.model = None

    def evaluate_decision(self, market_data: dict) -> str:
        close_price = market_data.get("close", 0.0)
        # Lógica de inferencia y toma de decisiones autónomas del modelo
        return "HOLD"

    def start(self):
        server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server_socket.bind((self.host, self.port))
        server_socket.listen(1)
        
        logging.info(f"Servidor Socket TCP local escuchando en {self.host}:{self.port}...")

        while True:
            try:
                conn, addr = server_socket.accept()
                logging.info(f"Conexión establecida con NinjaTrader 8.1.8.5 desde {addr}")
                
                buffer = ""
                while True:
                    data = conn.recv(1024).decode('utf-8')
                    if not data:
                        break
                    
                    buffer += data
                    while "\n" in buffer:
                        line, buffer = buffer.split("\n", 1)
                        if line.strip():
                            try:
                                payload = json.loads(line)
                                action = self.evaluate_decision(payload)
                                if action != "HOLD":
                                    conn.sendall(f"{action}\n".encode('utf-8'))
                            except json.JSONDecodeError:
                                continue

            except KeyboardInterrupt:
                logging.info("Servidor detenido manualmente.")
                break
            except Exception as e:
                logging.error(f"Error en servidor local: {e}")
            finally:
                if 'conn' in locals():
                    conn.close()

if __name__ == "__main__":
    bot = TradingBotServer()
    bot.start()

