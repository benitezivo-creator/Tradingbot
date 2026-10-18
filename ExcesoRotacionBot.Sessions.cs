#region Using declarations
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Data;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class ExcesoRotacionBot : Strategy
    {
        // =========================================================================
        // MOTOR DE SESIONES (ASIA, EUROPA, NUEVA YORK) Y PERFIL DE VOLUMEN
        // =========================================================================

        public enum TipoSesion
        {
            Ninguna,
            Asia,       // 19:00 -> 03:00 NY
            Europa,     // 03:00 -> 09:30 NY
            NuevaYork   // 09:30 -> 17:00 NY
        }

        private TimeZoneInfo nyTimeZone;
        private TipoSesion sesionActual = TipoSesion.Ninguna;

        // Niveles de Asia
        private double asiaHigh = double.MinValue;
        private double asiaLow = double.MaxValue;
        private bool asiaCompleta = false;
        private double ultimoAsiaHigh = double.NaN;
        private double ultimoAsiaLow = double.NaN;

        // Niveles de Europa (Referencia Primaria)
        private double europaHigh = double.MinValue;
        private double europaLow = double.MaxValue;
        private bool europaCompleta = false;
        private double ultimoEuropaHigh = double.NaN;
        private double ultimoEuropaLow = double.NaN;

        // Niveles de Nueva York (Sesión actual)
        private double nyHigh = double.MinValue;
        private double nyLow = double.MaxValue;

        // VWAP de la sesión activa
        private double vwapSumPrecioVol = 0.0;
        private double vwapSumVolumen = 0.0;
        private double vwapActual = double.NaN;

        // Perfil de volumen de la sesión activa (Volumen por nivel de precio)
        private readonly Dictionary<long, long> perfilVolumenSesion = new Dictionary<long, long>();
        private long pocTicks = 0;
        private double pocPrecio = double.NaN;
        private double vahPrecio = double.NaN;
        private double valPrecio = double.NaN;
        private long volumenTotalSesion = 0;

        // Inicialización de zona horaria de Nueva York
        private void InicializarZonaHoraria()
        {
            try
            {
                nyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            }
            catch
            {
                try
                {
                    nyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
                }
                catch
                {
                    nyTimeZone = TimeZoneInfo.Local;
                }
            }
        }

        // Conversión a hora de Nueva York
        private DateTime AHoraNuevaYork(DateTime tiempo)
        {
            if (nyTimeZone == null)
                InicializarZonaHoraria();

            if (tiempo.Kind == DateTimeKind.Utc)
                return TimeZoneInfo.ConvertTimeFromUtc(tiempo, nyTimeZone);

            DateTime local = tiempo.Kind == DateTimeKind.Local ? tiempo : DateTime.SpecifyKind(tiempo, DateTimeKind.Local);
            return TimeZoneInfo.ConvertTime(local, TimeZoneInfo.Local, nyTimeZone);
        }

        // Determinar tipo de sesión según hora de Nueva York
        // ASIA:        19:00 -> 03:00 (Sídney integrada)
        // EUROPA:      03:00 -> 09:30
        // NUEVA YORK:  09:30 -> 17:00
        private TipoSesion ObtenerTipoSesion(DateTime tiempoNy)
        {
            TimeSpan hora = tiempoNy.TimeOfDay;

            if (hora >= new TimeSpan(19, 0, 0) || hora < new TimeSpan(3, 0, 0))
                return TipoSesion.Asia;

            if (hora >= new TimeSpan(3, 0, 0) && hora < new TimeSpan(9, 30, 0))
                return TipoSesion.Europa;

            if (hora >= new TimeSpan(9, 30, 0) && hora < new TimeSpan(17, 0, 0))
                return TipoSesion.NuevaYork;

            return TipoSesion.Ninguna; // 17:00 -> 19:00 (Pausa / Mantenimiento)
        }

        // Procesa un trade para actualizar rangos de sesión, VWAP y Perfil de Volumen
        private void ActualizarSesionesYPerfil(DateTime tiempo, double precio, long volumen)
        {
            if (volumen <= 0 || precio <= 0.0 || double.IsNaN(precio))
                return;

            DateTime tiempoNy = AHoraNuevaYork(tiempo);
            TipoSesion nuevaSesion = ObtenerTipoSesion(tiempoNy);

            // Transición o cambio de sesión
            if (nuevaSesion != sesionActual)
            {
                CambiarSesion(nuevaSesion);
            }

            // Actualizar High / Low según sesión
            switch (sesionActual)
            {
                case TipoSesion.Asia:
                    if (precio > asiaHigh) asiaHigh = precio;
                    if (precio < asiaLow) asiaLow = precio;
                    break;

                case TipoSesion.Europa:
                    if (precio > europaHigh) europaHigh = precio;
                    if (precio < europaLow) europaLow = precio;
                    break;

                case TipoSesion.NuevaYork:
                    if (precio > nyHigh) nyHigh = precio;
                    if (precio < nyLow) nyLow = precio;
                    break;
            }

            // Actualizar VWAP de la sesión actual
            vwapSumPrecioVol += precio * volumen;
            vwapSumVolumen += volumen;
            if (vwapSumVolumen > 0)
                vwapActual = vwapSumPrecioVol / vwapSumVolumen;

            // Actualizar Perfil de Volumen de la sesión
            double tick = Math.Max(1E-10, TickSize);
            long precioTicks = (long)Math.Round(precio / tick, MidpointRounding.AwayFromZero);

            long volumenPrevio;
            perfilVolumenSesion.TryGetValue(precioTicks, out volumenPrevio);
            long volumenNuevo = volumenPrevio + volumen;
            perfilVolumenSesion[precioTicks] = volumenNuevo;
            volumenTotalSesion += volumen;

            // POC tracking en tiempo real
            long pocVolumen = 0;
            if (pocTicks != 0 && perfilVolumenSesion.TryGetValue(pocTicks, out pocVolumen))
            {
                if (volumenNuevo > pocVolumen)
                {
                    pocTicks = precioTicks;
                    pocPrecio = precioTicks * tick;
                }
            }
            else
            {
                pocTicks = precioTicks;
                pocPrecio = precioTicks * tick;
            }
        }

        // Cambio de sesión: reinicia delta, perfil y archiva extremos
        private void CambiarSesion(TipoSesion nuevaSesion)
        {
            // Archivar sesión previa
            if (sesionActual == TipoSesion.Asia)
            {
                if (asiaHigh > double.MinValue && asiaLow < double.MaxValue)
                {
                    ultimoAsiaHigh = asiaHigh;
                    ultimoAsiaLow = asiaLow;
                    asiaCompleta = true;
                }
            }
            else if (sesionActual == TipoSesion.Europa)
            {
                if (europaHigh > double.MinValue && europaLow < double.MaxValue)
                {
                    ultimoEuropaHigh = europaHigh;
                    ultimoEuropaLow = europaLow;
                    europaCompleta = true;
                }
            }

            sesionActual = nuevaSesion;

            // Al abrir cada sesión se reinicia su delta y su perfil
            ReiniciarPerfilSesion();
            ReiniciarDeltaSesion();

            if (sesionActual == TipoSesion.Asia)
            {
                asiaHigh = double.MinValue;
                asiaLow = double.MaxValue;
            }
            else if (sesionActual == TipoSesion.Europa)
            {
                europaHigh = double.MinValue;
                europaLow = double.MaxValue;
            }
            else if (sesionActual == TipoSesion.NuevaYork)
            {
                nyHigh = double.MinValue;
                nyLow = double.MaxValue;
                gatilloAperturaProcesado = false;
                primeraBarra15mAperturaProcesada = false;
            }
        }

        private void ReiniciarPerfilSesion()
        {
            perfilVolumenSesion.Clear();
            volumenTotalSesion = 0;
            pocTicks = 0;
            pocPrecio = double.NaN;
            vahPrecio = double.NaN;
            valPrecio = double.NaN;
            vwapSumPrecioVol = 0.0;
            vwapSumVolumen = 0.0;
            vwapActual = double.NaN;
        }

        // Calcula VAH (Value Area High) y VAL (Value Area Low) cubriendo el 70% del volumen
        private void RecalcularValueArea(double porcentajeVA = 0.70)
        {
            if (perfilVolumenSesion.Count == 0 || volumenTotalSesion <= 0 || pocTicks == 0)
                return;

            long volumenObjetivo = (long)(volumenTotalSesion * porcentajeVA);
            long volumenAcumulado = 0;
            perfilVolumenSesion.TryGetValue(pocTicks, out volumenAcumulado);

            List<long> preciosOrdenados = perfilVolumenSesion.Keys.OrderBy(k => k).ToList();
            int pocIndex = preciosOrdenados.IndexOf(pocTicks);
            if (pocIndex < 0) return;

            int arriba = pocIndex + 1;
            int abajo = pocIndex - 1;

            double tick = Math.Max(1E-10, TickSize);
            long maxTick = pocTicks;
            long minTick = pocTicks;

            while (volumenAcumulado < volumenObjetivo && (arriba < preciosOrdenados.Count || abajo >= 0))
            {
                long volArriba = arriba < preciosOrdenados.Count ? perfilVolumenSesion[preciosOrdenados[arriba]] : 0;
                long volAbajo = abajo >= 0 ? perfilVolumenSesion[preciosOrdenados[abajo]] : 0;

                if (volArriba >= volAbajo && arriba < preciosOrdenados.Count)
                {
                    volumenAcumulado += volArriba;
                    if (preciosOrdenados[arriba] > maxTick) maxTick = preciosOrdenados[arriba];
                    arriba++;
                }
                else if (abajo >= 0)
                {
                    volumenAcumulado += volAbajo;
                    if (preciosOrdenados[abajo] < minTick) minTick = preciosOrdenados[abajo];
                    abajo--;
                }
                else
                {
                    break;
                }
            }

            vahPrecio = maxTick * tick;
            valPrecio = minTick * tick;
        }
    }
}
