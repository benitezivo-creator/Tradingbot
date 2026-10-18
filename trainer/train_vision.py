import os
import json
import re
import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import Dataset, DataLoader
from torchvision import transforms, models
from PIL import Image

DATASET_DIR = r"C:\Users\SIBS\Documents\Tradingbot\vision_dataset"
LABELS_FILE = os.path.join(DATASET_DIR, "labels.json")

class TradingVisionDataset(Dataset):
    def __init__(self, labels_file, img_dir, transform=None, solo_ganadores=True):
        self.img_dir = img_dir
        self.transform = transform
        self.samples = []
        
        if os.path.exists(labels_file):
            try:
                with open(labels_file, "r", encoding="utf-8") as f:
                    content = f.read().strip()
                
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
                
                raw_samples = json.loads(content)
                print(f"[+] Se cargaron exitosamente {len(raw_samples)} muestras válidas del JSON.")

                # Filtro inteligente de PnL: entrenar preferentemente con trades ganadores (WIN)
                tiene_etiquetas_pnl = any("WIN_" in s.get("signal", "") or "LOSS_" in s.get("signal", "") for s in raw_samples)
                if solo_ganadores and tiene_etiquetas_pnl:
                    muestras_win = [s for s in raw_samples if s.get("signal", "").startswith("WIN_") or s.get("pnl", 0) > 0]
                    if len(muestras_win) >= 5:
                        self.samples = muestras_win
                        print(f"[+] Filtro PnL activo: {len(self.samples)} muestras GANADORAS (WIN) seleccionadas para entrenamiento.")
                    else:
                        print(f"[i] Muestras WIN insuficientes ({len(muestras_win)}). Usando todas las muestras ({len(raw_samples)}).")
                        self.samples = raw_samples
                else:
                    self.samples = raw_samples
            except Exception as e:
                print(f"[!] Error leyendo JSON de etiquetas: {e}")
                self.samples = []

    def __len__(self):
        return len(self.samples)

    def __getitem__(self, idx):
        item = self.samples[idx]
        img_path = os.path.join(self.img_dir, os.path.basename(item["image_path"]))
        image = Image.open(img_path).convert("RGB")
        
        # Etiqueta: 1 para Compra (o WIN_Compra), 0 para Venta (o WIN_Venta)
        signal = item.get("signal", "")
        label = 1 if "Compra" in signal else 0
        
        if self.transform:
            image = self.transform(image)
            
        return image, torch.tensor(label, dtype=torch.long)

def entrenar_modelo():
    transformaciones = transforms.Compose([
        transforms.Resize((224, 224)),
        transforms.ToTensor(),
        transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225])
    ])

    dataset = TradingVisionDataset(LABELS_FILE, os.path.join(DATASET_DIR, "images"), transform=transformaciones)
    
    if len(dataset) < 10:
        print(f"[!] Dataset insuficiente ({len(dataset)} muestras). Acumula más capturas antes de entrenar (mínimo 10).")
        return

    loader = DataLoader(dataset, batch_size=8, shuffle=True)
    
    # Cargar ResNet-18 compatible con todas las versiones de torchvision
    try:
        model = models.resnet18(weights=models.ResNet18_Weights.DEFAULT)
    except (AttributeError, TypeError):
        model = models.resnet18(pretrained=True)

    model.fc = nn.Linear(model.fc.in_features, 2)
    
    criterion = nn.CrossEntropyLoss()
    optimizer = optim.Adam(model.parameters(), lr=0.0001)

    print(f"[+] Iniciando entrenamiento con {len(dataset)} imágenes...")
    model.train()
    for epoch in range(5):
        running_loss = 0.0
        for images, labels in loader:
            optimizer.zero_grad()
            outputs = model(images)
            loss = criterion(outputs, labels)
            loss.backward()
            optimizer.step()
            running_loss += loss.item()
            
        print(f"Época {epoch+1}/5 - Pérdida: {running_loss/len(loader):.4f}")

    output_path = r"C:\Users\SIBS\Documents\Tradingbot\trainer\model_vision.pth"
    torch.save(model.state_dict(), output_path)
    print(f"[✓] Modelo guardado en: {output_path}")

if __name__ == "__main__":
    entrenar_modelo()
