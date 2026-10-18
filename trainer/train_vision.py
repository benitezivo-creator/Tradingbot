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
                
                if not content.startswith("["):
                    content = "[" + content
                if content.endswith(","):
                    content = content[:-1]
                if not content.endswith("]"):
                    content = content + "]"
                
                content = re.sub(r',\s*\]', ']', content)
                # Sanitizar números con comas decimales regionales (ej. :19,00 -> :19.00)
                content = re.sub(r'(:\s*-?\d+),(\d+)', r'\1.\2', content)
                
                raw_samples = json.loads(content)
                
                # Filtrar muestras según el criterio
                for item in raw_samples:
                    signal = item.get("signal", "")
                    if solo_ganadores:
                        # Si hay etiquetas WIN/LOSS, tomar solo WIN. Si es esquema viejo, tomar todas.
                        if "WIN" in signal or ("LOSS" not in signal and signal in ["Compra", "Venta"]):
                            self.samples.append(item)
                    else:
                        self.samples.append(item)
                        
                print(f"[+] Se cargaron exitosamente {len(self.samples)} muestras válidas (Filtrado WIN: {solo_ganadores}).")
            except Exception as e:
                print(f"[!] Error leyendo JSON de etiquetas: {e}")
                self.samples = []

    def __len__(self):
        return len(self.samples)

    def __getitem__(self, idx):
        item = self.samples[idx]
        img_path = os.path.join(self.img_dir, os.path.basename(item["image_path"]))
        image = Image.open(img_path).convert("RGB")
        
        # Etiqueta: 1 para Compra, 0 para Venta
        signal_str = item.get("signal", "")
        label = 1 if "Compra" in signal_str else 0
        
        if self.transform:
            image = self.transform(image)
            
        return image, torch.tensor(label, dtype=torch.long)

def entrenar_modelo():
    # Transformaciones avanzadas con Data Augmentation
    transformaciones = transforms.Compose([
        transforms.Resize((224, 224)),
        transforms.ColorJitter(brightness=0.1, contrast=0.1),
        transforms.ToTensor(),
        transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225])
    ])

    dataset = TradingVisionDataset(
        LABELS_FILE, 
        os.path.join(DATASET_DIR, "images"), 
        transform=transformaciones,
        solo_ganadores=True
    )
    
    if len(dataset) < 10:
        print(f"[!] Dataset insuficiente ({len(dataset)} muestras). Acumula más capturas antes de entrenar.")
        return

    loader = DataLoader(dataset, batch_size=4, shuffle=True)
    
    # Cargar ResNet-18 con compatibilidad de versión
    try:
        model = models.resnet18(weights=models.ResNet18_Weights.DEFAULT)
    except (AttributeError, TypeError):
        model = models.resnet18(pretrained=True)

    model.fc = nn.Linear(model.fc.in_features, 2)
    
    criterion = nn.CrossEntropyLoss()
    optimizer = optim.Adam(model.parameters(), lr=0.0003)
    scheduler = optim.lr_scheduler.StepLR(optimizer, step_size=5, gamma=0.5)

    print(f"[+] Iniciando entrenamiento con {len(dataset)} imágenes ganadoras por 20 épocas...")
    model.train()
    
    for epoch in range(20):
        running_loss = 0.0
        for images, labels in loader:
            optimizer.zero_grad()
            outputs = model(images)
            loss = criterion(outputs, labels)
            loss.backward()
            optimizer.step()
            running_loss += loss.item()
            
        scheduler.step()
        loss_promedio = running_loss / len(loader)
        print(f"Época {epoch+1:02d}/20 - Pérdida (Loss): {loss_promedio:.4f}")

    output_path = r"C:\Users\SIBS\Documents\Tradingbot\trainer\model_vision.pth"
    torch.save(model.state_dict(), output_path)
    print(f"[✓] Modelo optimizado guardado correctamente en: {output_path}")

if __name__ == "__main__":
    entrenar_modelo()
