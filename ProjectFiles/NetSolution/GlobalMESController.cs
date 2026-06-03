#region Using directives
using System;
using UAManagedCore;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.CoreBase;
using FTOptix.UI;
using FTOptix.DataLogger;
using FTOptix.RAEtherNetIP;
using FTOptix.CommunicationDriver;
using FTOptix.SerialPort;
#endregion

public class GlobalMESController : BaseNetLogic
{
    public override void Start()
    {
        // El HMI se suscribe a la variable del PLC
        var phaseNode = Project.Current.GetVariable("Model/BatchTracking/PLC_Phase_Step");
        if (phaseNode != null)
        {
            phaseNode.VariableChange += OnPhaseChanged;
            Log.Info("GlobalController", "Suscrito a los cambios de fase del PLC (Fases 2, 4 y 6 activas).");
        }
    }

    private void OnPhaseChanged(object sender, VariableChangeEventArgs e)
    {
        try
        {
            int newPhase = Convert.ToInt32(e.NewValue.Value);
            
            if (newPhase == 2) 
            {
                Log.Info("GlobalController", "Fase 2: Abriendo QC de Dispersión.");
                TriggerPopup("DispersionQC");
            }
            else if (newPhase == 4) 
            {
                Log.Info("GlobalController", "Fase 4: Abriendo QC de Dilución.");
                TriggerPopup("DilutionQC");
            }
            // ============================================================
            // 🏁 EL LISTENER AUTOMÁTICO PARA EL CIERRE DEL BATCH (FASE 6)
            // ============================================================
            else if (newPhase == 6) 
            {
                Log.Info("GlobalController", "Fase 6 detectada (Completed): Ejecutando cierre automático del lote...");
                
                // Buscamos el script central de BatchIdGenerator en la carpeta Logic
                var batchIdGeneratorScript = Project.Current.GetObject("Logic/BatchIdGenerator");
                if (batchIdGeneratorScript != null)
                {
                    // Ejecutamos de forma asíncrona el método CompleteBatch que guarda el end_time
                    batchIdGeneratorScript.ExecuteMethod("CompleteBatch");
                }
                else
                {
                    Log.Error("GlobalController", "No se encontró el script BatchIdGenerator en 'Logic/'. No se pudo guardar el end_time.");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("GlobalController", $"Error en el evento de cambio de fase: {ex.Message}");
        }
    }

    [ExportMethod]
    public void TriggerPopup(string dialogName)
    {
        try
        {
            var dialog = Project.Current.Get<DialogType>($"UI/Dialogs/{dialogName}");
            if (dialog == null) return;

            string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            if (dialogName == "DispersionQC")
            {
                // ✅ AQUÍ ESTABA EL ERROR: Cambiado a QCDispersionTimeStamp
                var tsNode = Project.Current.GetVariable("Model/BatchTracking/QCDispersionTimeStamp");
                if (tsNode != null) 
                {
                    tsNode.Value = currentTime;
                    Log.Info("GlobalController", $"Timestamp de Dispersión guardado: {currentTime}");
                }
                else
                {
                    Log.Error("GlobalController", "No se encontró QCDispersionTimeStamp en el modelo.");
                }

                var resultNode = Project.Current.GetVariable("Model/ActiveBatchParameters/QC_Dispersion_Result");
                if (resultNode != null) resultNode.Value = 4; // 4 = In Progress
            }
            else if (dialogName == "DilutionQC")
            {
                // Revisa que este nombre también sea el correcto en tu modelo
                var tsNode = Project.Current.GetVariable("Model/BatchTracking/QCDilutionTimeStamp");
                if (tsNode != null) tsNode.Value = currentTime;

                var resultNode = Project.Current.GetVariable("Model/ActiveBatchParameters/QC_Dilution_Result");
                if (resultNode != null) resultNode.Value = 4; // 4 = In Progress
            }

            var uiOwner = LogicObject.Owner as Window;
            if (uiOwner != null)
            {
                FTOptix.UI.UICommands.OpenDialog(uiOwner, dialog);
            }
        }
        catch (Exception ex) { Log.Error("GlobalController", ex.Message); }
    }

    public override void Stop()
    {
        var phaseNode = Project.Current.GetVariable("Model/BatchTracking/PLC_Phase_Step");
        if (phaseNode != null) phaseNode.VariableChange -= OnPhaseChanged;
    }
}
