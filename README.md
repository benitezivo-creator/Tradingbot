# 🤖 Tradingbot — Exceso & Rotación + Vision AI Pipeline

Estrategia algorítmica institucional para **NinjaTrader 8** desacoplada de renderizado visual pesado, acompañada por un módulo de entrenamiento basado en imágenes del gráfico (Computer Vision) y gestión en 3 tercios.

---

## 📁 Estructura del Repositorio

```text
Tradingbot/
├── ExcesoRotacionBot.cs            # Strategy Core, Ciclo de vida y Exportador Dataset Visión
├── ExcesoRotacionBot.Execution.cs  # Gestión de Posición en 3 Tercios (SL, TP1, TP2, Runner Trailing)
├── ExcesoRotacionBot.OrderFlow.cs  # Motor de Delta, Cinta, Velocidad y Bracket VND
├── ExcesoRotacionBot.Sessions.cs   # Lógica de Sesiones (Asia, Europa, NY) y VAH/VAL/POC
├── ExcesoRotacionBot.StateMachine.cs # Máquina de Estados (Exceso, Rotación, Scoring y Gatillo)
├── ExcesoRotacionBot.Properties.cs # Parámetros editables en la GUI de NT8
├── ExcesoRotacionBot.Niveles.cs    # Niveles fijos institucionales
├── trainer/
│   └── dataset_generator.py        # Pipeline en Python para entrenar modelos con las imágenes
├── vision_dataset/                 # Dataset de capturas e índices de muestras etiquetadas
│   ├── images/
│   └── labels.json
├── .gitignore
└── README.md
```

---

## ⚙️ Características y Arquitectura de la Estrategia

### 1. Desacople y Rendimiento
* **Cero renderizado pesado**: No dibuja histogramas de volumen pesados, cajas de sesiones ni heatmaps que saturen la GPU/CPU o deformen las escalas del gráfico.
* **Procesamiento limpio**: Todo el cálculo de Order Flow, VWAP de sesión, Delta y perfiles se procesa en memoria mediante una serie de datos secundaria interna de 1 tick.
* **Marcas mínimas**: Únicamente dibuja flechas y textos limpios en el momento exacto en que se valida una señal.

### 2. Reglas Operativas Principales
* **Horarios de Sesión (Hora NY)**:
  * **Asia**: 19:00 → 03:00 (Sídney integrada). Al cerrar, fija `Asia High/Low`.
  * **Europa**: 03:00 → 09:30. Al cerrar, fija `Europa High/Low` (**nivel rector**).
  * **Nueva York**: 09:30 → 17:00 (Cierre preventivo antes del cierre).
* **Detección de Exceso**: Calculado en ventana rodante de velocidad de flujo (`UmbralVelocidadExceso`). Registra el extremo del movimiento.
* **Confirmación de Rotación**: Exige un retroceso mínimo (`DistanciaMinimaRotacionTicks`) dentro de un plazo límite (`PlazoMaximoRotacionBarras`).
* **Gatillo de Apertura (09:30 NY)**: Primera barra de 15m sobre High/Low de Europa que presente exceso y rotación recibe bonificación decisiva (+8 pts).
* **Scoring Engine de Confluencia**: Pondera niveles clave (Europa, Asia, VWAP, VAH/VAL, POC, niveles fijos institucionales) y delta para autorizar la entrada (`ScoreTotal >= ScoreMinimoSenal`).

### 3. Gestión de Posición en 3 Tercios
* **Entrada**: 3 sub-órdenes iguales (`Entrada_1`, `Entrada_2`, `Entrada_3`).
* **Stop Loss**: 20 puntos inicial para toda la posición.
* **TP1 (1/3)**: +15 puntos. Al completarse $\rightarrow$ Stop Loss de los tercios 2 y 3 se mueve a **Break-Even**.
* **TP2 (1/3)**: +30 puntos asegurados.
* **Runner (1/3)**:
  * **En Largos**: Corre libre. Al superar los **+100 puntos**, se activa un trailing stop a **20 puntos** del máximo.
  * **En Cortos**: Por regla institucional, el runner no corre libre; se liquida en target acotado (+30 pts).

---

## 🧠 Pipeline de Visión por Computadora (Vision Dataset)

Cada vez que el bot detecta una señal válida con `ExportarDatasetVision = true`:
1. Registra automáticamente los metadatos en `vision_dataset/labels.json` (`id`, `timestamp`, `signal`, `score`, `confluences`, `price`, `delta`, `image_path`).
2. Captura de forma asíncrona el fotograma del gráfico mediante el `ChartControl` de NinjaTrader y lo guarda en `vision_dataset/images/{id}.png`.
3. El módulo Python `trainer/dataset_generator.py` permite cargar, validar y preprocesar las imágenes (normalización y redimensionado a 224x224) para entrenar clasificadores de Computer Vision (CNNs, ResNet, ViT, YOLOv8).

Para ejecutar el preprocesador:
```bash
python trainer/dataset_generator.py
```

---

## 🚀 Despliegue en NinjaTrader 8

1. **Copiar los archivos de la estrategia**:
   Copia todos los archivos `ExcesoRotacionBot*.cs` a:
   ```text
   C:\Users\SIBS\Documents\NinjaTrader 8\bin\Custom\Strategies\
   ```
2. **Compilar en NinjaTrader 8**:
   * Abre NinjaTrader 8.
   * Abre el **NinjaScript Editor** y presiona **F5** (o botón derecho $\rightarrow$ *Compile*).
3. **Aplicar al gráfico**:
   * Abre un gráfico del activo a operar (ej. `NQ 03-26` o `MNQ` en 1m, 15m o la temporalidad deseada).
   * Clic derecho en el gráfico $\rightarrow$ **Strategies** (`Ctrl + S`).
   * Selecciona **`ExcesoRotacionBot`**.
   * Revisa los parámetros en el panel derecho (sección `06. AI & Dataset` para captura de imágenes).
   * Marca **Enabled = True** y haz clic en **OK**.

---

## 📦 Sincronización con GitHub

Para inicializar y subir el repositorio a GitHub:

```bash
cd C:\Users\SIBS\Documents\Tradingbot
git init
git add .
git commit -m "Initial commit: Estrategia desacoplada NT8 + Pipeline de entrenamiento por imágenes"
git branch -M main
git remote add origin https://github.com/TU_USUARIO/Tradingbot.git
git push -u origin main
```
