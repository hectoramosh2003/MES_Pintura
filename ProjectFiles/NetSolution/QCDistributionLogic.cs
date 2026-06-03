#region Using directives
using System;
using UAManagedCore;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.Store;
using FTOptix.CoreBase;
using FTOptix.DataLogger;
using FTOptix.RAEtherNetIP;
using FTOptix.CommunicationDriver;
using FTOptix.SerialPort;
#endregion

public class QCDistributionLogic : BaseNetLogic
{
    // ==========================================
    // 🧪 CONTROL DE CALIDAD: DISPERSIÓN (FASE 2)
    // ==========================================
    [ExportMethod]
    public void ProcessDispersionQC()
    {
        try
        {
            string batchId = Project.Current.GetVariable("Model/BatchTracking/SelectedBatchID")?.Value ?? "";
            if (string.IsNullOrEmpty(batchId)) return;

            string qcTimestamp = Project.Current.GetVariable("Model/BatchTracking/QCDispersionTimeStamp")?.Value ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            float operatorInput = Convert.ToSingle(Project.Current.GetVariable("Model/BatchTracking/disp_qc_particula")?.Value.Value ?? 0.0f);
            float minLimit = Convert.ToSingle(Project.Current.GetVariable("Model/ActiveBatchParameters/calc_qc_disp_particula_min")?.Value.Value ?? 0.0f);
            float maxLimit = Convert.ToSingle(Project.Current.GetVariable("Model/ActiveBatchParameters/calc_qc_disp_particula_max")?.Value.Value ?? 0.0f);

            var plcResultTag = Project.Current.GetVariable("Model/ActiveBatchParameters/QC_Dispersion_Result");
            var statusNode = Project.Current.GetVariable("Model/BatchTracking/qc_disp_particula_status");

            bool isPass = (operatorInput >= minLimit && operatorInput <= maxLimit);

            // Guardar en la base de datos de Certificado de Análisis (CoA)
            LogCOARecord(batchId, "Dispersion", "Particle Size", operatorInput, minLimit, maxLimit, isPass ? "PASS" : "FAIL", qcTimestamp);

            if (isPass)
            {
                if (statusNode != null) statusNode.Value = 1;
                if (plcResultTag != null) plcResultTag.Value = 1; // Avanza el PLC
                Log.Info("QC_Dispersion", "Aprobado. Registrado en CoA.");
            }
            else
            {
                if (statusNode != null) statusNode.Value = 2;
                if (plcResultTag != null) plcResultTag.Value = 2; // Rework en el PLC

                // ⚡ REWORK: Se fuerza la fase del PLC a 8 (Ajuste de dispersión)
                var plcPhaseNode = Project.Current.GetVariable("Model/BatchTracking/PLC_Phase_Step");
                if (plcPhaseNode != null) plcPhaseNode.Value = 8;

                Log.Warning("QC_Dispersion", "Falló. Rework registrado en CoA. PLC_Phase_Step cambiado a Fase 8.");
            }
        }
        catch (Exception ex) { Log.Error("QC_Dispersion", ex.Message); }
    }

    // ==========================================
    // 🧪 CONTROL DE CALIDAD: DILUCIÓN (FASE 4)
    // ==========================================
    [ExportMethod]
    public void ProcessDilutionQC()
    {
        try
        {
            string batchId = Project.Current.GetVariable("Model/BatchTracking/SelectedBatchID")?.Value ?? "";
            if (string.IsNullOrEmpty(batchId)) return;

            string qcTimestamp = Project.Current.GetVariable("Model/BatchTracking/QCDilutionTimeStamp")?.Value ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Inputs
            float opParticle = Convert.ToSingle(Project.Current.GetVariable("Model/BatchTracking/dil_qc_particula")?.Value.Value ?? 0.0f);
            float opViscosity = Convert.ToSingle(Project.Current.GetVariable("Model/BatchTracking/dil_qc_viscosidad")?.Value.Value ?? 0.0f);
            float opPH = Convert.ToSingle(Project.Current.GetVariable("Model/BatchTracking/dil_qc_ph")?.Value.Value ?? 0.0f);
            float opTone = Convert.ToSingle(Project.Current.GetVariable("Model/BatchTracking/dil_qc_tono")?.Value.Value ?? 0.0f);
            float opDensity = Convert.ToSingle(Project.Current.GetVariable("Model/BatchTracking/dil_qc_densidad")?.Value.Value ?? 0.0f);

            // Límites
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

            // Evaluaciones individuales
            bool pPass = (opParticle >= pMin && opParticle <= pMax);
            bool vPass = (opViscosity >= vMin && opViscosity <= vMax);
            bool phPass = (opPH >= phMin && opPH <= phMax);
            bool tPass = (opTone >= tMin && opTone <= tMax);
            bool dPass = (opDensity >= dMin && opDensity <= dMax);

            // Guardar TODO en la base de datos CoA
            LogCOARecord(batchId, "Dilution", "Particle Size", opParticle, pMin, pMax, pPass ? "PASS" : "FAIL", qcTimestamp);
            LogCOARecord(batchId, "Dilution", "Viscosity", opViscosity, vMin, vMax, vPass ? "PASS" : "FAIL", qcTimestamp);
            LogCOARecord(batchId, "Dilution", "pH", opPH, phMin, phMax, phPass ? "PASS" : "FAIL", qcTimestamp);
            LogCOARecord(batchId, "Dilution", "Tone", opTone, tMin, tMax, tPass ? "PASS" : "FAIL", qcTimestamp);
            LogCOARecord(batchId, "Dilution", "Density", opDensity, dMin, dMax, dPass ? "PASS" : "FAIL", qcTimestamp);

            SetStatusNodeValue("Model/BatchTracking/qc_dil_particula_status", pPass ? 1 : 2);
            SetStatusNodeValue("Model/BatchTracking/qc_dil_viscosidad_status", vPass ? 1 : 2);
            SetStatusNodeValue("Model/BatchTracking/qc_dil_ph_status", phPass ? 1 : 2);
            SetStatusNodeValue("Model/BatchTracking/qc_dil_tono_status", tPass ? 1 : 2);
            SetStatusNodeValue("Model/BatchTracking/qc_dil_densidad_status", dPass ? 1 : 2);

            var plcResultTag = Project.Current.GetVariable("Model/ActiveBatchParameters/QC_Dilution_Result");
            if (pPass && vPass && phPass && tPass && dPass)
            {
                if (plcResultTag != null) plcResultTag.Value = 1; // Avanza a Envasado
                Log.Info("QC_Dilution", "Control multivariable aprobado y registrado en CoA.");
            }
            else
            {
                if (plcResultTag != null) plcResultTag.Value = 2; // Rework a Dilución

                // ⚡ REWORK: Se fuerza la fase del PLC a 9 (Ajuste de dilución)
                var plcPhaseNode = Project.Current.GetVariable("Model/BatchTracking/PLC_Phase_Step");
                if (plcPhaseNode != null) plcPhaseNode.Value = 9;

                Log.Warning("QC_Dilution", "Desviación detectada. Rework registrado en CoA. PLC_Phase_Step cambiado a Fase 9.");
            }
        }
        catch (Exception ex) { Log.Error("QC_Dilution", ex.Message); }
    }

    // ==========================================
    // 📊 MOTOR DE ESCRITURA EN SQL PARA EL CoA
    // ==========================================
    private void LogCOARecord(string batchId, string stage, string parameter, float value, float min, float max, string result, string timestamp)
    {
        try
        {
            var store = Project.Current.Get<Store>("DataStores/SQLiteDatabase");
            if (store == null) return;

            string countQuery = $"SELECT COUNT(*) FROM coa_history WHERE batch_id = '{batchId}' AND qc_stage = '{stage}' AND parameter = '{parameter}'";
            store.Query(countQuery, out _, out object[,] resultSet);

            int attemptNumber = 1;
            if (resultSet != null && resultSet.GetLength(0) > 0 && resultSet[0, 0] != null)
            {
                attemptNumber = Convert.ToInt32(resultSet[0, 0]) + 1;
            }

            string[] columns = new string[] { "batch_id", "qc_stage", "parameter", "timestamp", "recorded_value", "min_limit", "max_limit", "result", "attempt" };
            object[,] values = new object[1, 9] {
                { batchId, stage, parameter, timestamp, value, min, max, result, attemptNumber }
            };

            // Guarda la información en la base de datos
            store.Insert("coa_history", columns, values);

            // 🛑 LÍNEAS ELIMINADAS AQUÍ: El Insert ya guardó los datos. No se requiere hacer Refresh al Store.
        }
        catch (Exception ex)
        {
            Log.Error("CoA_Database", $"Error guardando trazabilidad de {parameter}: {ex.Message}");
        }
    }

    private void SetStatusNodeValue(string nodePath, int value)
    {
        var node = Project.Current.GetVariable(nodePath);
        if (node != null) node.Value = value;
    }
}
