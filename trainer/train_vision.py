import os
import json
import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import Dataset, DataLoader
from torchvision import transforms, models
from PIL import Image

BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATASET_DIR = os.getenv("VISION_DATASET_DIR", os.path.join(BASE_DIR, "vision_dataset"))
LABELS_FILE = os.path.join(DATASET_DIR, "labels.json")

class TradingVisionDataset(Dataset):
    def __init__(self, labels_file, img_dir, transform=None):
        self.img_dir = img_dir
        self.transform = transform
        self.samples = []
        
        if os.path.exists(labels_file):
            with open(labels_file, "r", encoding="utf-8") as f:
                content = f.read().strip()
                if content:
                    if content.endswith(","):
                        content = "[" + content[:-1] + "]"
                    elif not content.startswith("["):
                        content = "[" + content + "]"
                    try:
                        self.samples = json.loads(content)
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
        label = 1 if item.get("signal") == "Compra" else 0
        
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
    
    # Modelo ResNet18 transfer learning
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

    output_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "model_vision.pth")
    torch.save(model.state_dict(), output_path)
    print(f"[✓] Modelo guardado en: {output_path}")

if __name__ == "__main__":
    entrenar_modelo()
