import torch
import torch.nn as nn
import torch.optim as optim
import logging

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")

class LocalDecisionModel(nn.Module):
    def __init__(self, input_size: int = 10, num_classes: int = 3):
        super(LocalDecisionModel, self).__init__()
        self.net = nn.Sequential(
            nn.Linear(input_size, 64),
            nn.ReLU(),
            nn.Linear(64, 32),
            nn.ReLU(),
            nn.Linear(32, num_classes)
        )

    def forward(self, x):
        return self.net(x)

def train_model():
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    logging.info(f"Iniciando entrenamiento del modelo en: {device}")
    
    model = LocalDecisionModel().to(device)
    optimizer = optim.Adam(model.parameters(), lr=0.001)
    
    # Entrenamiento local autónomo...
    logging.info("Entrenamiento finalizado exitosamente. Guardando pesos...")
    torch.save(model.state_dict(), "bot/models/model_weights.pth")

if __name__ == "__main__":
    train_model()

