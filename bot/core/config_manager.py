import os
import json
import logging
import time

class ConfigManager:
    def __init__(self, config_path: str = None):
        if config_path is None:
            base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
            config_path = os.path.join(base_dir, "config.json")
            
        self.config_path = config_path
        self._last_mtime = 0
        self.config = {}
        self.reload_if_modified()

    def reload_if_modified(self) -> bool:
        """
        Verifica si el archivo config.json ha cambiado en disco.
        Si cambió, lo recarga en caliente sin reiniciar la aplicación.
        """
        if not os.path.exists(self.config_path):
            return False

        try:
            mtime = os.path.getmtime(self.config_path)
            if mtime > self._last_mtime:
                with open(self.config_path, "r", encoding="utf-8") as f:
                    self.config = json.load(f)
                self._last_mtime = mtime
                logging.info(f"[HOT-RELOAD] Parámetros cargados en caliente desde {os.path.basename(self.config_path)}")
                return True
        except Exception as e:
            logging.error(f"[CONFIG ERROR] No se pudo leer config.json: {e}")
        return False

    def get(self, section: str, key: str = None, default = None):
        self.reload_if_modified()
        sec = self.config.get(section, {})
        if key is None:
            return sec
        return sec.get(key, default)
