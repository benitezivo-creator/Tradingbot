import os
import json
import re
import numpy as np
from PIL import Image

BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATASET_DIR = os.getenv("VISION_DATASET_DIR", os.path.join(BASE_DIR, "vision_dataset"))
LABELS_FILE = os.path.join(DATASET_DIR, "labels.json")
OUTPUT_MODEL_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "output_model")

def cargar_dataset():
    """
    Carga los metadatos de las imágenes guardadas por el ExcesoRotacionBot.
    """
    if not os.path.exists(LABELS_FILE):
        print(f"[!] No se encontró el archivo de etiquetas en: {LABELS_FILE}")
        return []

    samples = []
    with open(LABELS_FILE, "r", encoding="utf-8") as f:
        content = f.read().strip()
        if not content:
            print(f"[!] El archivo de etiquetas está vacío: {LABELS_FILE}")
            return []
        
        # Arreglar formato si las entradas fueron agregadas como objetos sueltos
        if not content.startswith("["):
            content = "[" + content
        if content.endswith(","):
            content = content[:-1]
        if not content.endswith("]"):
            content = content + "]"
        
        # Reemplazar posibles trailing commas entre objetos
        content = re.sub(r',\s*\]', ']', content)
        
        # Sanitizar números con comas decimales regionales (ej. :19,00 -> :19.00)
        content = re.sub(r'(:\s*-?\d+),(\d+)', r'\1.\2', content)
        
        try:
            samples = json.loads(content)
        except Exception as e:
            print(f"[!] Error leyendo JSON de etiquetas: {e}")
            return []

    print(f"[+] Se cargaron exitosamente {len(samples)} muestras del dataset de visión.")
    return samples

def preparar_datos_vision(samples, target_size=(224, 224)):
    """
    Preprocesa las imágenes del gráfico y sus etiquetas (COMPRA/VENTA/SCORE)
    para alimentar modelos de Visión (CNN / ResNet / ViT / YOLO).
    """
    X_images = []
    y_labels = []

    for sample in samples:
        img_relative_path = sample.get("image_path")
        if not img_relative_path:
            continue

        img_full_path = os.path.join(DATASET_DIR, img_relative_path)
        if not os.path.exists(img_full_path):
            continue

        try:
            # Cargar y redimensionar imagen del gráfico
            img = Image.open(img_full_path).convert("RGB")
            img = img.resize(target_size)
            img_array = np.array(img, dtype=np.float32) / 255.0  # Normalización [0, 1]

            X_images.append(img_array)

            # 1 = COMPRA, -1 = VENTA
            signal = sample.get("signal", "")
            label = 1 if "Compra" in signal else -1
            y_labels.append(label)
        except Exception as e:
            print(f"[!] Error procesando imagen {img_full_path}: {e}")

    return np.array(X_images), np.array(y_labels)

if __name__ == "__main__":
    print("=== TRAINER DE VISIÓN PARA ESTRATEGIA DE TRADING ===")
    os.makedirs(OUTPUT_MODEL_DIR, exist_ok=True)
    muestras = cargar_dataset()
    if muestras:
        X, y = preparar_datos_vision(muestras)
        print(f"[+] Dataset procesado: {X.shape[0]} imágenes listas.")
        if len(y) > 0:
            print(f"    - Forma de tensores de imagen: {X.shape}")
            compras = int(np.sum(y > 0))
            ventas = int(np.sum(y < 0))
            print(f"    - Balance de clases (Compras/Ventas): {compras} / {ventas}")
            
        wins = sum(1 for s in muestras if s.get("signal", "").startswith("WIN_") or s.get("pnl", 0) > 0)
        losses = sum(1 for s in muestras if s.get("signal", "").startswith("LOSS_") or s.get("pnl", 0) < 0)
        if wins + losses > 0:
            print(f"    - Desglose PnL: {wins} WINs | {losses} LOSSes")
    else:
        print("[i] Para comenzar a entrenar, genera señales en NinjaTrader 8 o añade imágenes manuales a vision_dataset/images/")
