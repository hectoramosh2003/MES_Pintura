#region Using directives
using System;
using System.Threading;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
using FTOptix.HMIProject;
using FTOptix.Store;
using FTOptix.NetLogic;
using FTOptix.CoreBase;
using FTOptix.DataLogger;
using FTOptix.RAEtherNetIP;
using FTOptix.CommunicationDriver;
using FTOptix.SerialPort;
#endregion

public class BatchIdGenerator : BaseNetLogic
{
    // Tiempo de duración del pulso en milisegundos (500ms es el estándar seguro para redes industriales)
    private const int PulseDuration = 500;

    // ==========================================
    // 📦 GENERACIÓN DE IDENTIFICADOR DE BATCH
    // ==========================================
    [ExportMethod]
    public void GenerateBatchID()
    {
        try
        {
            Log.Info("BatchIdGenerator", "Generando ID de Lote...");

            string datePart = DateTime.Now.ToString("yyyyMMdd");
            var store = Project.Current.Get<Store>("DataStores/SQLiteDatabase");

            if (store == null) return;

            string query = $"SELECT COUNT(*) FROM batches WHERE batch_id LIKE '%{datePart}%'";

            string[] columns;
            object[,] resultSet;
            store.Query(query, out columns, out resultSet);

            int count = 0;
            if (resultSet != null && resultSet.GetLength(0) > 0 && resultSet[0, 0] != null)
            {
                count = Convert.ToInt32(resultSet[0, 0]);
            }

            int sequence = count + 1;
            string sequencePart = sequence.ToString("D3");

            string batchId = $"BATCH-{datePart}-{sequencePart}";
            string reducedBatchId = $"Batch-{sequencePart}";

            Project.Current.GetVariable("Model/BatchCreation/GeneratedBatchID").Value = batchId;
            Project.Current.GetVariable("Model/BatchCreation/ReducedBatchID").Value = reducedBatchId;
            Project.Current.GetVariable("Model/BatchCreation/DailyBatchCount").Value = count;

            Log.Info("BatchIdGenerator", $"ID Generado: {batchId} (Secuencia: {sequence})");
        }
        catch (Exception ex)
        {
            Log.Error("BatchIdGenerator", $"ERROR: {ex.Message}");
        }
    }

    // ==========================================
    // 📝 CREACIÓN DE NUEVO LOTE (UI)
    // ==========================================
    [ExportMethod]
    public void CreateBatch()
    {
        try
        {
            Log.Info("Batch", "Iniciando secuencia de creación de lote...");

            // 1. Generamos el ID
            GenerateBatchID();

            // 2. AHORA SÍ: Llamamos al método local que tiene el Timestamp
            ShowPopup("NewBatchCreationDialog");
        }
        catch (Exception ex)
        {
            Log.Error("Batch", $"ERROR abriendo popup: {ex.Message}");
        }
    }

    // ==========================================
    // 🖼️ MOTOR LOCAL DE POPUPS CON TIMESTAMP
    // ==========================================
    private void ShowPopup(string dialogName)
    {
        try
        {
            var dialog = Project.Current.Get<DialogType>($"UI/Dialogs/{dialogName}");
            if (dialog == null)
            {
                Log.Error("Popup", $"No se encontró el diseño en la ruta: UI/Dialogs/{dialogName}");
                return;
            }

            IUANode currentNode = LogicObject.Owner;
            Item uiOwner = null;
            while (currentNode != null)
            {
                uiOwner = currentNode as Item;
                if (uiOwner != null) break;
                currentNode = currentNode.Owner;
            }

            if (uiOwner != null)
            {
                // 1. Abrir el cuadro de diálogo en pantalla
                FTOptix.UI.UICommands.OpenDialog(uiOwner, dialog);
                Log.Info("Popup", $"Ventana {dialogName} abierta correctamente.");

                // 2. REGISTRO DE TIMESTAMP SEGÚN EL DIÁLOGO
                if (dialogName == "NewBatchCreationDialog")
                {
                    // Buscamos la variable dedicada para la creación del lote
                    var timestampNode = Project.Current.GetVariable("Model/BatchCreation/CreationTimeStamp");

                    if (timestampNode != null)
                    {
                        // 🟢 SI TU VARIABLE EN OPTIX ES DE TIPO "STRING":
                        timestampNode.Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        // 🔵 NOTA: Si tu variable en Optix es de tipo "DATETIME" (no string), 
                        // debes comentar la línea de arriba y desatar la de abajo:
                        // timestampNode.Value = DateTime.Now;

                        Log.Info("Popup", "Timestamp de creación de lote actualizado.");
                    }
                    else
                    {
                        Log.Warning("Popup", "No se encontró la variable 'Model/BatchCreation/CreationTimeStamp'. Asegúrate de crearla.");
                    }
                }
            }
            else
            {
                Log.Error("Popup", "No se encontró un contexto de pantalla válido (UI Owner).");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Popup", $"Error al registrar Timestamp: {ex.Message}");
        }
    }

    // ==========================================
    // 🟢 COMANDO ISA-88: START BATCH
    // ==========================================
    [ExportMethod]
    public void StartBatch()
    {
        try
        {
            Log.Info("BatchExecution", "Ejecutando comando START BATCH...");

            var store = Project.Current.Get<Store>("DataStores/SQLiteDatabase");
            var batchNode = Project.Current.GetVariable("Model/BatchTracking/SelectedBatchID");

            if (batchNode == null || string.IsNullOrEmpty(batchNode.Value) || store == null)
            {
                Log.Error("BatchExecution", "No se puede iniciar: No hay lote seleccionado en el sistema.");
                return;
            }

            string batchId = batchNode.Value;
            string formattedTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

            string query = $"UPDATE batches SET status = 'Running', start_time = '{formattedTime}' WHERE batch_id = '{batchId}'";
            store.Query(query, out _, out _);

            var statusNode = Project.Current.GetVariable("Model/BatchTracking/BatchStatus");
            if (statusNode != null) statusNode.Value = "Running";

            var stateNode = Project.Current.GetVariable("Model/BatchTracking/PLC_Phase_Step");
            if (stateNode != null) stateNode.Value = 1;

            // ⚡ Disparamos el comando de Start como un Pulso seguro
            ExecutePulseCommand("Model/BatchTracking/PLC_Start_Cmd");

            Log.Info("BatchExecution", $"¡Lote {batchId} iniciado exitosamente a las {formattedTime}!");
            RefreshBatchTable();
        }
        catch (Exception ex) { Log.Error("BatchExecution", $"Error en StartBatch: {ex.Message}"); }
    }

    // ==========================================
    // 🟡 COMANDO ISA-88: HOLD (PAUSA)
    // ==========================================
    [ExportMethod]
    public void HoldBatch()
    {
        try
        {
            var statusNode = Project.Current.GetVariable("Model/BatchTracking/BatchStatus");
            if (statusNode != null) statusNode.Value = "Hold";

            // ⚡ Disparamos el comando de Pause como un Pulso seguro
            ExecutePulseCommand("Model/BatchTracking/PLC_Pause_Cmd");

            Log.Info("BatchExecution", "Comando HOLD enviado como pulso. Estatus actualizado a Paused.");
        }
        catch (Exception ex) { Log.Error("BatchExecution", ex.Message); }
    }

    // ==========================================
    // 🔵 COMANDO ISA-88: RESTART (REANUDAR)
    // ==========================================
    [ExportMethod]
    public void RestartBatch()
    {
        try
        {
            var statusNode = Project.Current.GetVariable("Model/BatchTracking/BatchStatus");
            if (statusNode != null) statusNode.Value = "Running";

            // ⚡ Disparamos el comando de Restart como un Pulso seguro
            ExecutePulseCommand("Model/BatchTracking/PLC_Restart_Cmd");

            Log.Info("BatchExecution", "Comando RESTART enviado como pulso. Estatus restablecido a Running.");
        }
        catch (Exception ex) { Log.Error("BatchExecution", ex.Message); }
    }

    // ==========================================
    // 🔴 COMANDO ISA-88: STOP (PARADA DE CONTROL)
    // ==========================================
    [ExportMethod]
    public void StopBatch()
    {
        try
        {
            var statusNode = Project.Current.GetVariable("Model/BatchTracking/BatchStatus");
            if (statusNode != null) statusNode.Value = "Stoped";

            // ⚡ Disparamos el comando de Stop como un Pulso seguro
            ExecutePulseCommand("Model/BatchTracking/PLC_Stop_Cmd");


            Log.Info("BatchExecution", "Comando STOP enviado de forma controlada al PLC.");
        }
        catch (Exception ex) { Log.Error("BatchExecution", ex.Message); }
    }

    // ==========================================
    // 🔥 COMANDO ISA-88: ABORT (PARO DE EMERGENCIA)
    // ==========================================
    [ExportMethod]
    public void AbortBatch()
    {
        try
        {
            var store = Project.Current.Get<Store>("DataStores/SQLiteDatabase");
            string batchId = Project.Current.GetVariable("Model/BatchTracking/SelectedBatchID")?.Value;

            if (!string.IsNullOrEmpty(batchId) && store != null)
            {
                string endTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                string query = $"UPDATE batches SET status = 'Aborted', end_time = '{endTime}' WHERE batch_id = '{batchId}'";
                store.Query(query, out _, out _);
            }

            var statusNode = Project.Current.GetVariable("Model/BatchTracking/BatchStatus");
            if (statusNode != null) statusNode.Value = "Aborted";

            // ⚡ Disparamos el comando de Abort como un Pulso seguro
            ExecutePulseCommand("Model/BatchTracking/PLC_Abort_Cmd");

            Log.Warning("BatchExecution", "¡ALERTA! Comando ABORT crítico ejecutado.");
            RefreshBatchTable();
        }
        catch (Exception ex) { Log.Error("BatchExecution", ex.Message); }
    }

    // ==========================================
    // ⚪ COMANDO ISA-88: RESET (RESTABLECER)
    // ==========================================
    [ExportMethod]
    public void ResetBatch()
    {
        try
        {
            //Project.Current.GetVariable("Model/BatchTracking/SelectedBatchID").Value = "";
            var statusNode = Project.Current.GetVariable("Model/BatchTracking/BatchStatus");
            if (statusNode != null) statusNode.Value = "Idle";

            var stateNode = Project.Current.GetVariable("Model/BatchTracking/BatchState");
            if (stateNode != null) stateNode.Value = "Pre-treatment";

            Project.Current.GetVariable("Model/BatchTracking/qc_disp_particula_status").Value = 0;
            Project.Current.GetVariable("Model/BatchTracking/qc_dil_particula_status").Value = 0;
            Project.Current.GetVariable("Model/BatchTracking/qc_dil_viscosidad_status").Value = 0;
            Project.Current.GetVariable("Model/BatchTracking/qc_dil_ph_status").Value = 0;
            Project.Current.GetVariable("Model/BatchTracking/qc_dil_tono_status").Value = 0;
            Project.Current.GetVariable("Model/BatchTracking/qc_dil_densidad_status").Value = 0;

            // ⚡ Disparamos el comando de Reset como un Pulso seguro
            ExecutePulseCommand("Model/BatchTracking/PLC_Reset_Cmd");

            Log.Info("BatchExecution", "Comando RESET finalizado. Interface limpia.");
            RefreshBatchTable();
        }
        catch (Exception ex) { Log.Error("BatchExecution", ex.Message); }
    }

    // ==========================================
    // ⚡ COMANDO EXTRA: FORCE TRANSITION
    // ==========================================
    [ExportMethod]
    public void ForceTransitionBatch()
    {
        try
        {
            // ⚡ Disparamos el comando de Force como un Pulso seguro
            ExecutePulseCommand("Model/BatchTracking/PLC_ForceTransition_Cmd");

            Log.Warning("BatchExecution", "Forzando transición de paso en el SFC.");
        }
        catch (Exception ex) { Log.Error("BatchExecution", ex.Message); }
    }

    // ==========================================
    // ⚙️ MÉTODO CENTRALIZADOR DE PULSOS (THREADING)
    // ==========================================
    private void ExecutePulseCommand(string variablePath)
    {
        var targetNode = Project.Current.GetVariable(variablePath);
        if (targetNode == null)
        {
            Log.Error("PulseEngine", $"No se encontró la variable: {variablePath}");
            return;
        }

        // Encendemos el bit inmediatamente
        targetNode.Value = true;

        // Creamos un hilo secundario asíncrono para apagarlo sin congelar la pantalla del operador
        Thread pulseThread = new Thread(() =>
        {
            try
            {
                Thread.Sleep(PulseDuration); // Se espera 500 milisegundos en segundo plano
                targetNode.Value = false;     // Apaga el bit de forma segura en el PLC
            }
            catch (Exception ex)
            {
                Log.Error("PulseEngine", $"Error al apagar el pulso de {variablePath}: {ex.Message}");
            }
        });

        pulseThread.IsBackground = true;
        pulseThread.Start(); // Arranca el temporizador
    }

    // ==========================================
    // 🏁 CIERRE AUTOMÁTICO
    // ==========================================
    [ExportMethod]
    public void CompleteBatch()
    {
        try
        {
            Log.Info("BatchExecution", "Finalizando lote de forma automática...");

            var store = Project.Current.Get<Store>("DataStores/SQLiteDatabase");
            if (store == null) return;

            string batchId = Project.Current.GetVariable("Model/BatchTracking/SelectedBatchID")?.Value;
            if (string.IsNullOrEmpty(batchId)) return;

            string endTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            string query = $"UPDATE batches SET status = 'Completed', end_time = '{endTime}' WHERE batch_id = '{batchId}'";
            store.Query(query, out _, out _);

            var stateNode = Project.Current.GetVariable("Model/BatchTracking/BatchState");
            if (stateNode != null) stateNode.Value = "Completed";

            var statusNode = Project.Current.GetVariable("Model/BatchTracking/BatchStatus");
            if (statusNode != null) statusNode.Value = "Completed";

            Log.Info("BatchExecution", $"Lote {batchId} completado exitosamente.");
            RefreshBatchTable();
        }
        catch (Exception ex) { Log.Error("BatchExecution", ex.Message); }
    }

    [ExportMethod]
    public void DeleteSelectedBatch(NodeId batchGridNode)
    {
        try
        {
            if (batchGridNode == null) return;
            var batchGrid = InformationModel.Get<DataGrid>(batchGridNode);
            if (batchGrid.SelectedItem == NodeId.Empty) return;

            var selectedRow = InformationModel.Get(batchGrid.SelectedItem);
            string batchIdToDelete = selectedRow.GetVariable("batch_id")?.Value ?? "";
            if (string.IsNullOrEmpty(batchIdToDelete)) return;

            var store = Project.Current.Get<Store>("DataStores/SQLiteDatabase");
            if (store == null) return;

            // --- BORRADO EN CASCADA DE LA BASE DE DATOS ---
            store.Query($"DELETE FROM batches WHERE batch_id = '{batchIdToDelete}'", out _, out _);
            store.Query($"DELETE FROM batch_plc_setpoints WHERE batch_id = '{batchIdToDelete}'", out _, out _);

            // ✨ LÍNEA NUEVA: Eliminamos los registros de calidad asociados al Lote
            store.Query($"DELETE FROM coa_history WHERE batch_id = '{batchIdToDelete}'", out _, out _);
            // ----------------------------------------------

            var trackingNode = Project.Current.GetVariable("Model/BatchTracking/SelectedBatchID");
            if (trackingNode != null && trackingNode.Value == batchIdToDelete)
            {
                trackingNode.Value = "";
                var statusNode = Project.Current.GetVariable("Model/BatchTracking/BatchStatus");
                if (statusNode != null) statusNode.Value = "Idle";
            }

            RefreshBatchTable();
        }
        catch (Exception ex) { Log.Error("DeleteBatch", ex.Message); }
    }

    /*[ExportMethod]
    public void ResetGeneralSistema()
    {
        try
        {
            var store = Project.Current.Get<Store>("DataStores/SQLiteDatabase");
            if (store == null)
            {
                Log.Error("DatabaseReset", "No se encontró la base de datos SQLite en la ruta especificada.");
                return;
            }

            Log.Info("DatabaseReset", "Iniciando secuencia de Reset General del Sistema...");

            // =========================================================================
            // 1. VACIADO COMPLETO DE TABLAS SQL (Borrado total)
            // =========================================================================
            // Al no usar la cláusula WHERE, DELETE FROM vacía la tabla por completo
            store.Query("DELETE FROM coa_history", out _, out _);
            store.Query("DELETE FROM batch_plc_setpoints", out _, out _);
            store.Query("DELETE FROM batches", out _, out _);

            Log.Info("DatabaseReset", "¡Tablas SQL limpiadas con éxito! (coa_history, batch_plc_setpoints, batches).");

            // =========================================================================
            // 2. RESET DE VARIABLES EN MEMORIA (Evita datos fantasmas en la UI)
            // =========================================================================
            // Limpieza de Batch Activo en el Tracking
            var trackingBatchNode = Project.Current.GetVariable("Model/BatchTracking/SelectedBatchID");
            if (trackingBatchNode != null) trackingBatchNode.Value = "";

            var trackingStatusNode = Project.Current.GetVariable("Model/BatchTracking/BatchStatus");
            if (trackingStatusNode != null) trackingStatusNode.Value = "Idle";

            // Reset de las Máquinas de Estado de QC a IDLE (0)
            var qcDispResult = Project.Current.GetVariable("Model/ActiveBatchParameters/QC_Dispersion_Result");
            if (qcDispResult != null) qcDispResult.Value = 0;

            var qcDilResult = Project.Current.GetVariable("Model/ActiveBatchParameters/QC_Dilution_Result");
            if (qcDilResult != null) qcDilResult.Value = 0;

            // Reset de Barras de Progreso e Indicadores visuales globales
            var progressNode = Project.Current.GetVariable("Model/ActiveBatchParameters/Progreso_Batch_Total");
            if (progressNode != null) progressNode.Value = 0.0f;

            var progressDispUI = Project.Current.GetVariable("Model/PhaseTracker/Fase_Disp/Progreso_UI");
            if (progressDispUI != null) progressDispUI.Value = 0.0f;

            var progressDilUI = Project.Current.GetVariable("Model/PhaseTracker/Fase_Dil/Progreso_UI");
            if (progressDilUI != null) progressDilUI.Value = 0.0f;

            var progressQCDispUI = Project.Current.GetVariable("Model/PhaseTracker/Fase_QC_Disp/Progreso_UI");
            if (progressQCDispUI != null) progressQCDispUI.Value = 0.0f;

            var progressQCDilUI = Project.Current.GetVariable("Model/PhaseTracker/Fase_QC_Dil/Progreso_UI");
            if (progressQCDilUI != null) progressQCDilUI.Value = 0.0f;

            var progressPackUI = Project.Current.GetVariable("Model/PhaseTracker/Fase_Pack/Progreso_UI");
            if (progressPackUI != null) progressPackUI.Value = 0.0f;

            Log.Info("DatabaseReset", "Variables dinámicas del Modelo restauradas a valores iniciales de seguridad.");

            // =========================================================================
            // 3. REFRESCAR TABLAS VISUALES EN LA INTERFAZ
            // =========================================================================
            // Forzamos el refresco de los componentes visuales de las bases de datos para actualizar pantallas abiertas
            var batchesTableObj = Project.Current.Get<IUAObject>("DataStores/SQLiteDatabase/Tables/batches");
            if (batchesTableObj != null) batchesTableObj.ExecuteMethod("Refresh");

            var coaTableObj = Project.Current.Get<IUAObject>("DataStores/SQLiteDatabase/Tables/coa_history");
            if (coaTableObj != null) coaTableObj.ExecuteMethod("Refresh");

            Log.Info("DatabaseReset", "¡Reset General ejecutado exitosamente. Sistema listo para nuevos lotes!");
        }
        catch (Exception ex)
        {
            Log.Error("DatabaseReset", "Error crítico durante la ejecución del reset general: " + ex.Message);
        }
    }*/

    private void RefreshBatchTable()
    {
        try
        {
            var tableObj = Project.Current.Get<IUAObject>("DataStores/SQLiteDatabase/Tables/batches");
            if (tableObj != null) tableObj.ExecuteMethod("Refresh");
        }
        catch (Exception) { }
    }
}
