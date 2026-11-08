import os
import json
import csv
import logging
from datetime import datetime

class DatasetRecorder:
    def __init__(self, dataset_path: str = None):
        base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        if dataset_path is None:
            dataset_path = os.path.join(base_dir, "dataset", "setups_history.json")
            
        self.dataset_path = dataset_path
        self.csv_path = dataset_path.replace(".json", ".csv")
        self.active_trades = {}
        self.history = self._load_history()

    def _load_history(self) -> list:
        if os.path.exists(self.dataset_path):
            try:
                with open(self.dataset_path, "r", encoding="utf-8") as f:
                    return json.load(f)
            except Exception as e:
                logging.warning(f"No se pudo cargar historial existente: {e}")
        return []

    def record_setup(self, setup_data: dict) -> str:
        """
        Registra la huella matemática de un setup detectado por Exceso y Rotación.
        """
        trade_id = f"TRADE_{datetime.now().strftime('%Y%m%d_%H%M%S_%f')}"
        record = {
            "id": trade_id,
            "timestamp": setup_data.get("timestamp", datetime.now().isoformat()),
            "instrumento_analisis": setup_data.get("instrumento_analisis", "NQ"),
            "instrumento_ejecucion": setup_data.get("instrumento_ejecucion", "MNQ"),
            "direccion": setup_data.get("direccion", "UNKNOWN"),
            "precio_nq": setup_data.get("precio_nq", 0.0),
            "nivel_cercano": setup_data.get("nivel_cercano", "NINGUNO"),
            "distancia_nivel": setup_data.get("distancia_nivel", 0.0),
            "distancia_vwap": setup_data.get("distancia_vwap", 0.0),
            "velocidad_exceso": setup_data.get("velocidad_exceso", 0.0),
            "delta_acumulado": setup_data.get("delta_acumulado", 0.0),
            "delta_tipo": setup_data.get("delta_tipo", "CONVERGENTE"),
            "parametros_activos": setup_data.get("parametros", {}),
            "resultado": "PENDIENTE",
            "pnl_puntos": 0.0
        }

        self.history.append(record)
        self.active_trades[trade_id] = record
        self._save_to_disk()

        logging.info(f"[MEMORIA/DATASET] Setup registrado -> {trade_id} [{record['direccion']}] en {record['nivel_cercano']} (NQ: {record['precio_nq']})")
        return trade_id

    def update_trade_result(self, trade_id: str, resultado: str, pnl_puntos: float):
        """
        Actualiza el resultado del trade cuando se cierra o toca targets/stop.
        """
        target = None
        if trade_id in self.active_trades:
            target = self.active_trades[trade_id]
        else:
            for item in reversed(self.history):
                if item.get("id") == trade_id or item.get("resultado") == "PENDIENTE":
                    target = item
                    break

        if target:
            target["resultado"] = resultado  # WIN / LOSS / BREAK_EVEN
            target["pnl_puntos"] = pnl_puntos
            target["cierre_tiempo"] = datetime.now().isoformat()
            self._save_to_disk()
            logging.info(f"[MEMORIA/DATASET] Trade actualizado -> {target['id']} | Resultado: {resultado} ({pnl_puntos:+.2f} pts)")

    def _save_to_disk(self):
        try:
            os.makedirs(os.path.dirname(self.dataset_path), exist_ok=True)
            with open(self.dataset_path, "w", encoding="utf-8") as f:
                json.dump(self.history, f, indent=2, ensure_ascii=False)

            # También exportar a CSV para fácil inspección en Excel o Pandas
            if self.history:
                fieldnames = [
                    "id", "timestamp", "instrumento_analisis", "instrumento_ejecucion",
                    "direccion", "precio_nq", "nivel_cercano", "distancia_nivel",
                    "distancia_vwap", "velocidad_exceso", "delta_acumulado", "delta_tipo",
                    "resultado", "pnl_puntos"
                ]
                with open(self.csv_path, "w", newline="", encoding="utf-8") as csvfile:
                    writer = csv.DictWriter(csvfile, fieldnames=fieldnames, extrasaction='ignore')
                    writer.writeheader()
                    for row in self.history:
                        writer.writerow(row)
        except Exception as e:
            logging.error(f"Error guardando dataset: {e}")

