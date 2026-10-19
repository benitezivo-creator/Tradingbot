import os
import torch
import torch.nn as nn
from torchvision import transforms, models
from PIL import Image
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import uvicorn

# Configuración de rutas y dispositivo
BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODEL_PATH = os.path.join(BASE_DIR, "trainer", "model_vision.pth")
DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")

# Inicializar FastAPI
app = FastAPI(title="ExcesoRotacionBot - Vision Inference Gate")

# Transformaciones de imagen idénticas al entrenamiento
transformaciones = transforms.Compose([
    transforms.Resize((224, 224)),
    transforms.ToTensor(),
    transforms.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225])
])

# Cargar arquitectura ResNet-18
try:
    modelo = models.resnet18(weights=None)
except (AttributeError, TypeError):
    modelo = models.resnet18(pretrained=False)

modelo.fc = nn.Linear(modelo.fc.in_features, 2)

if os.path.exists(MODEL_PATH):
    try:
        modelo.load_state_dict(torch.load(MODEL_PATH, map_location=DEVICE))
        modelo.to(DEVICE)
        modelo.eval()
        print(f"[+] Modelo de visión cargado exitosamente desde: {MODEL_PATH} en dispositivo: {DEVICE}")
    except Exception as e:
        print(f"[!] Error al cargar pesos de {MODEL_PATH}: {e}")
else:
    print(f"[!] ADVERTENCIA: No se encontró {MODEL_PATH}. El servidor responderá con valores por defecto.")

class InferenceRequest(BaseModel):
    image_path: str
    umbral: float = 0.70

@app.get("/health")
def health_check():
    return {
        "status": "online",
        "model_loaded": os.path.exists(MODEL_PATH),
        "device": str(DEVICE)
    }

@app.post("/predict")
def predict_trade(request: InferenceRequest):
    img_path = request.image_path
    
    if not os.path.exists(img_path):
        raise HTTPException(status_code=404, detail=f"Imagen no encontrada en: {img_path}")

    try:
        image = Image.open(img_path).convert("RGB")
        tensor_img = transformaciones(image).unsqueeze(0).to(DEVICE)

        with torch.no_grad():
            outputs = modelo(tensor_img)
            probabilidades = torch.softmax(outputs, dim=1)
            prob_win = probabilidades[0][1].item()  # Clase 1 = WIN / Compra Valida

        # Filtro: Aprobado si P(WIN) >= umbral configurado
        aprobado = prob_win >= request.umbral
        
        return {
            "aprobado": aprobado,
            "prob_win": round(prob_win, 4),
            "umbral": request.umbral,
            "decision": "APROBADO" if aprobado else "RECHAZADO"
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error en inferencia: {str(e)}")

if __name__ == "__main__":
    print(f"[+] Iniciando Servidor de Inferencia en http://127.0.0.1:5005 (Dispositivo: {DEVICE})")
    uvicorn.run(app, host="127.0.0.1", port=5005, log_level="warning")
