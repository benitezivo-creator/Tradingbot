#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class ExcesoRotacionBot : Strategy
    {
        // =========================================================================
        // PARÁMETROS CONFIGURABLES (INTERFAZ NINJATRADER 8)
        // =========================================================================

        #region 01. Gestión de Posición y Riesgo

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Contratos por tercio", Description = "Número de contratos por tercio de posición (total operado = 3x este valor).", Order = 1, GroupName = "01. Gestión de Posición y Riesgo")]
        public int CantidadPorTercio { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, 500.0)]
        [Display(Name = "Stop Loss (puntos)", Description = "Distancia del stop loss inicial para toda la posición en puntos.", Order = 2, GroupName = "01. Gestión de Posición y Riesgo")]
        public double StopLossPuntos { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, 500.0)]
        [Display(Name = "TP1 (puntos)", Description = "Take Profit del primer tercio (1/3). Al alcanzarse, el Stop Loss se mueve a Break-Even.", Order = 3, GroupName = "01. Gestión de Posición y Riesgo")]
        public double TP1Puntos { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, 500.0)]
        [Display(Name = "TP2 (puntos)", Description = "Take Profit del segundo tercio (1/3).", Order = 4, GroupName = "01. Gestión de Posición y Riesgo")]
        public double TP2Puntos { get; set; }

        [NinjaScriptProperty]
        [Range(10.0, 1000.0)]
        [Display(Name = "Umbral runner > puntos", Description = "Distancia de recorrido favorable en puntos a partir de la cual se activa el trailing stop para el runner.", Order = 5, GroupName = "01. Gestión de Posición y Riesgo")]
        public double UmbralActivacionRunnerPuntos { get; set; }

        [NinjaScriptProperty]
        [Range(5.0, 200.0)]
        [Display(Name = "Distancia trailing runner (puntos)", Description = "Distancia de holgura en puntos para el trailing stop una vez activado el runner.", Order = 6, GroupName = "01. Gestión de Posición y Riesgo")]
        public double DistanciaTrailingRunnerPuntos { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Permitir runner en cortos", Description = "Por regla operativa, en cortos el runner NO se deja correr libre (falso = cierra en target acotado).", Order = 7, GroupName = "01. Gestión de Posición y Riesgo")]
        public bool PermitirRunnerEnCortos { get; set; }

        [NinjaScriptProperty]
        [Range(5.0, 200.0)]
        [Display(Name = "Target runner en cortos (puntos)", Description = "Si el runner en cortos no se deja correr libre, se cierra a este target acotado en puntos.", Order = 8, GroupName = "01. Gestión de Posición y Riesgo")]
        public double TPRunnerCortosPuntos { get; set; }

        #endregion

        #region 02. Detección de Exceso y Rotación

        [NinjaScriptProperty]
        [Range(5.0, 5000.0)]
        [Display(Name = "Umbral velocidad flujo (contratos/seg)", Description = "Agresión por unidad de ventana necesaria para activar un evento de exceso.", Order = 1, GroupName = "02. Detección de Exceso y Rotación")]
        public double UmbralVelocidadExceso { get; set; }

        [NinjaScriptProperty]
        [Range(100, 10000)]
        [Display(Name = "Ventana rodante velocidad (ms)", Description = "Ventana en milisegundos sobre la que se mide la velocidad de flujo y agresión.", Order = 2, GroupName = "02. Detección de Exceso y Rotación")]
        public int VentanaVelocidadMs { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Distancia mínima rotación (ticks)", Description = "Distancia mínima en ticks que debe retroceder el precio desde el extremo para confirmar la rotación.", Order = 3, GroupName = "02. Detección de Exceso y Rotación")]
        public int DistanciaMinimaRotacionTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, 50)]
        [Display(Name = "Plazo máximo rotación (barras)", Description = "Número máximo de barras de espera para que se confirme la rotación antes de descartar el exceso.", Order = 4, GroupName = "02. Detección de Exceso y Rotación")]
        public int PlazoMaximoRotacionBarras { get; set; }

        #endregion

        #region 03. Order Flow y Delta

        [NinjaScriptProperty]
        [Display(Name = "Clasificación Bid/Ask en vivo", Description = "Usa cotización viva para clasificar agresores en tiempo real (más preciso). En histórico se usa tick rule determinista.", Order = 1, GroupName = "03. Order Flow y Delta")]
        public bool UsarClasificacionBidAsk { get; set; }

        [NinjaScriptProperty]
        [Range(0, 10000)]
        [Display(Name = "Volumen mínimo por trade", Description = "Descarta del cálculo del delta prints por debajo de este tamaño (0 = no filtra nada, recomendado en ES/NQ).", Order = 2, GroupName = "03. Order Flow y Delta")]
        public int VolumenMinimoTrade { get; set; }

        [NinjaScriptProperty]
        [Range(100, 1000000)]
        [Display(Name = "Umbral bracket volumen (contratos)", Description = "Capacidad del bracket rodante VND para medir actividad constante y migración de delta.", Order = 3, GroupName = "03. Order Flow y Delta")]
        public long UmbralBracketContratos { get; set; }

        [NinjaScriptProperty]
        [Range(100, 50000)]
        [Display(Name = "Umbral estrés delta", Description = "Nivel de delta acumulado a partir del cual el mercado se considera en condición de estrés.", Order = 4, GroupName = "03. Order Flow y Delta")]
        public double UmbralEstresDelta { get; set; }

        #endregion

        #region 04. Confluencia y Ponderación

        [NinjaScriptProperty]
        [Range(1, 50)]
        [Display(Name = "Tolerancia niveles clave (ticks)", Description = "Distancia máxima en ticks al nivel para calificar que el exceso ocurrió sobre él.", Order = 1, GroupName = "04. Confluencia y Ponderación")]
        public int ToleranciaNivelTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, 50.0)]
        [Display(Name = "Score mínimo para señal", Description = "Puntuación de ponderación mínima necesaria para autorizar la entrada.", Order = 2, GroupName = "04. Confluencia y Ponderación")]
        public double ScoreMinimoSenal { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Activar gatillo apertura (09:30)", Description = "Evalúa la primera barra de 15m sobre High/Low de Europa para otorgar bonificación decisiva de apertura.", Order = 3, GroupName = "04. Confluencia y Ponderación")]
        public bool ActivarGatilloApertura { get; set; }

        #endregion

        #region 05. Diagnóstico y Logs

        [NinjaScriptProperty]
        [Display(Name = "Activar logs en ventana Output", Description = "Muestra la traza detallada de la máquina de estados, ponderaciones y ejecuciones en la consola de NinjaTrader.", Order = 1, GroupName = "05. Diagnóstico")]
        public bool ActivarLogs { get; set; }

        #endregion

        #region Cooldown y Modo Operativo

        [NinjaScriptProperty]
        [Range(1, 50)]
        [Display(Name = "Cooldown Barras Post-Trade", Description = "Número de barras mínimas a esperar después de cerrar una operación antes de autorizar otra señal.", Order = 9, GroupName = "01. Gestión de Posición y Riesgo")]
        public int CooldownBarrasPostTrade { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Modo Solo Dataset (Shadow)", Description = "Registra señales y toma capturas de pantalla para AI sin enviar órdenes reales al broker.", Order = 10, GroupName = "01. Gestión de Posición y Riesgo")]
        public bool ModoSoloDataset { get; set; }

        #endregion
    }
}
