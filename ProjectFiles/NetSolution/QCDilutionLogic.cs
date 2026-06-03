#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.CoreBase;
using FTOptix.Core;
using FTOptix.DataLogger;
using FTOptix.RAEtherNetIP;
using FTOptix.CommunicationDriver;
using FTOptix.SerialPort;
#endregion


public class QCDilutionLogic : BaseNetLogic
{
[ExportMethod]
    public void ProcessDilutionQC()
    {
        try
        {
            Log.Info("QC_Dilution", "Iniciando validación multivariable de Control de Calidad para Dilución...");

            // 1. RECUPERAR LOS INPUTS DEL OPERADOR (Asegúrate de crear estas variables en Model/BatchTracking/)
            float opParticle = Convert.ToSingle(Project.Current.GetVariable("Model/BatchTracking/dil_qc_particula")?.Value.Value ?? 0.0f);
            float opViscosity = Convert.ToSingle(Project.Current.GetVariable("Model/BatchTracking/dil_qc_viscosidad")?.Value.Value ?? 0.0f);
            float opPH = Convert.ToSingle(Project.Current.GetVariable("Model/BatchTracking/dil_qc_ph")?.Value.Value ?? 0.0f);
            float opTone = Convert.ToSingle(Project.Current.GetVariable("Model/BatchTracking/dil_qc_tono")?.Value.Value ?? 0.0f);
            float opDensity = Convert.ToSingle(Project.Current.GetVariable("Model/BatchTracking/dil_qc_densidad")?.Value.Value ?? 0.0f);

            // 2. RECUPERAR LOS THRESHOLDS CALCULADOS DE ACTIVEBATCHPARAMETERS
            float pMin = Convert.ToSingle(Project.Current.GetVariable("Model/ActiveBatchParameters/calc_qc_dil_particula_min")?.Value.Value ?? 0.0f);
            float pMax = Convert.ToSingle(Project.Current.GetVariable("Model/ActiveBatchParameters/calc_qc_dil_particula_max")?.Value.Value ?? 0.0f);
            
            float vMin = Convert.ToSingle(Project.Current.GetVariable("Model/ActiveBatchParameters/calc_qc_dil_viscosidad_min")?.Value.Value ?? 0.0f);
            float vMax = Convert.ToSingle(Project.Current.GetVariable("Model/ActiveBatchParameters/calc_qc_dil_viscosidad_max")?.Value.Value ?? 0.0f);
            
            float phMin = Convert.ToSingle(Project.Current.GetVariable("Model/ActiveBatchParameters/calc_qc_dil_ph_min")?.Value.Value ?? 0.0f);
            float phMax = Convert.ToSingle(Project.Current.GetVariable("Model/ActiveBatchParameters/calc_qc_dil_ph_max")?.Value.Value ?? 0.0f);
            
            float tMin = Convert.ToSingle(Project.Current.GetVariable("Model/ActiveBatchParameters/calc_qc_dil_tono_min")?.Value.Value ?? 0.0f);
            float tMax = Convert.ToSingle(Project.Current.GetVariable("Model/ActiveBatchParameters/calc_qc_dil_tono_max")?.Value.Value ?? 0.0f);
            
            float dMin = Convert.ToSingle(Project.Current.GetVariable("Model/ActiveBatchParameters/calc_qc_dil_densidad_min")?.Value.Value ?? 0.0f);
            float dMax = Convert.ToSingle(Project.Current.GetVariable("Model/ActiveBatchParameters/calc_qc_dil_densidad_max")?.Value.Value ?? 0.0f);

            var stateNode = Project.Current.GetVariable("Model/BatchTracking/BatchState");
            var statusNode = Project.Current.GetVariable("Model/BatchTracking/BatchStatus");

            if (stateNode == null) return;

            // 3. VALIDACIÓN MULTIVARIABLE (Todas las condiciones deben ser verdaderas)
            bool particleOk = (opParticle >= pMin && opParticle <= pMax);
            bool viscosityOk = (opViscosity >= vMin && opViscosity <= vMax);
            bool phOk = (opPH >= phMin && opPH <= phMax);
            bool toneOk = (opTone >= tMin && opTone <= tMax);
            bool densityOk = (opDensity >= dMin && opDensity <= dMax);

            if (particleOk && viscosityOk && phOk && toneOk && densityOk)
            {
                // 🟢 CASO: PASÓ TODO EL CONTROL DE CALIDAD DE DILUCIÓN
                Log.Info("QC_Dilution", "¡PRODUCTO TERMINADO APROBADO! Todas las variables están dentro de especificación.");

                // Cambiamos el estado a Packaging (Empaque) o directamente a Completed usando tu máquina de estados
                stateNode.Value = "Packaging"; 
                if (statusNode != null) statusNode.Value = "Running"; 

                var qcResultNode = Project.Current.GetVariable("Model/BatchTracking/QC_Dilution_Passed");
                if (qcResultNode != null) qcResultNode.Value = true;

                Log.Info("QC_Dilution", "Fase de control de calidad cerrada exitosamente. Avanzando a Envasado.");
            }
            else
            {
                // 🔴 CASO: FALLÓ AL MENOS UNA VARIABLE (REWORK / ALARMA)
                Log.Warning("QC_Dilution", $"QC RECHAZADO. Resultados -> Partícula: {particleOk}, Viscosidad: {viscosityOk}, pH: {phOk}, Tono: {toneOk}, Densidad: {densityOk}");

                var qcResultNode = Project.Current.GetVariable("Model/BatchTracking/QC_Dilution_Passed");
                if (qcResultNode != null) qcResultNode.Value = false;

                // Forzamos el Rework regresando el estado a Dilution para que vuelvan a ajustar parámetros
                stateNode.Value = "Dilution"; 

                // DISPARAR ALARMA DE DILUCIÓN EN OPTIX (Auditoría Rockwell)
                var qcAlarmNode = Project.Current.GetVariable("Model/Alarms/QC_Dilution_Fail");
                if (qcAlarmNode != null) 
                {
                    qcAlarmNode.Value = true; 
                }

                Log.Warning("QC_Dilution", "Alarma de calidad de producto terminada encendida. Requiere adición de ajustadores en Dilución.");
            }
        }
        catch (Exception ex)
        {
            Log.Error("QC_Dilution", "Error procesando el QC de Dilución: " + ex.Message);
        }
    }
}
