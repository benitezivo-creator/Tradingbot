@echo off
title ExcesoRotacionBot - Vision AI Inference Server (Port 5005)
cd /d "%~dp0"
echo =====================================================================
echo  Iniciando Servidor de Inferencia Vision AI (FastAPI + ResNet-18)
echo =====================================================================
python trainer/inference_server.py
pause
