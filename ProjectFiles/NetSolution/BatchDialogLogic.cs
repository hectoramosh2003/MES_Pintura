#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
using FTOptix.HMIProject;
using FTOptix.Store;
using FTOptix.NetLogic;
using FTOptix.NativeUI;
using FTOptix.SQLiteStore;
using FTOptix.CoreBase;
using FTOptix.Core;
using FTOptix.DataLogger;
using FTOptix.RAEtherNetIP;
using FTOptix.CommunicationDriver;
using FTOptix.SerialPort;
#endregion

public class BatchDialogLogic : BaseNetLogic
{
    [ExportMethod]
    public void ConfirmBatchCreation(NodeId recipeDataGridNode)
    {
        try
        {
            Log.Info("DialogLogic", "Iniciando creación de lote...");

            // 1. RECUPERAR DATOS DEL MODELO
            string batchId = Project.Current.GetVariable("Model/BatchCreation/GeneratedBatchID")?.Value ?? "";
            string reducedBatchId = Project.Current.GetVariable("Model/BatchCreation/ReducedBatchID")?.Value ?? "";
            float targetQty = Project.Current.GetVariable("Model/BatchCreation/TargetQuantity")?.Value ?? 0.0f;
            string operatorName = Session.User.BrowseName ?? "Operator";

            if (string.IsNullOrEmpty(batchId)) return;

            // 2. RECUPERAR RECETA SELECCIONADA
            if (recipeDataGridNode == null) return;
            var recipeGrid = InformationModel.Get<DataGrid>(recipeDataGridNode);
            if (recipeGrid.SelectedItem == NodeId.Empty)
            {
                Log.Error("DialogLogic", "Selecciona una receta en la tabla.");
                return;
            }

            var selectedRowNode = InformationModel.Get(recipeGrid.SelectedItem);
            string recipeIdToSearch = selectedRowNode.GetVariable("recipe_id")?.Value ?? "";
            string recipeNamePure = selectedRowNode.GetVariable("recipe_name")?.Value ?? "";

            var store = Project.Current.Get<Store>("DataStores/SQLiteDatabase");
            if (store == null) return;

            // 3. CONSULTAR RECETA MAESTRA
            // Traemos todos los valores de 'recipes' para la receta seleccionada
            string recipeQuery = $"SELECT * FROM recipes WHERE recipe_id = '{recipeIdToSearch}'";
            store.Query(recipeQuery, out string[] header, out object[,] recipeData);

            if (recipeData == null || recipeData.GetLength(0) == 0) return;

            // Buscamos la columna batch_size en el header de la tabla 'recipes'
            int idxBatchSize = Array.IndexOf(header, "batch_size");
            if (idxBatchSize < 0)
            {
                Log.Error("DialogLogic", "No se encontró la columna batch_size en la tabla recipes");
                return;
            }

            // Obtenemos el valor maestro (de la tabla recipes)
            float masterBatchSize = Convert.ToSingle(recipeData[0, idxBatchSize]);

            // ✅ LÓGICA DE ESCALADO DE PRECISIÓN
            // Comparamos targetQty (lo que va a 'batches') vs masterBatchSize (de 'recipes')
            float factor = 1.0f;
            if (Math.Abs(targetQty - masterBatchSize) > 0.01f)
            {
                factor = targetQty / masterBatchSize;
            }
            else
            {
                factor = 1.0f; // Evitamos errores decimales si son iguales
            }

            Log.Info("DialogLogic", $"Escalando con factor: {factor} (Target: {targetQty} / Master: {masterBatchSize})");

            // =========================================================================
            // 4. INSERTAR EN TABLA 'batches' (✅ ACTUALIZADO CON recipe_id)
            // =========================================================================
            string[] columns = new string[] { "batch_id", "reduced_batch_id", "recipe_name", "recipe_id", "operator", "status", "target_quantity", "start_time", "end_time" };
            object[,] values = new object[1, 9] { { batchId, reducedBatchId, recipeNamePure, recipeIdToSearch, operatorName, "Idle", targetQty, null, null } };
            store.Insert("batches", columns, values);

            // 5. ESCALADO Y ENVÍO A 'batch_plc_setpoints'
            object Scale(string colName)
            {
                // Buscamos la columna ignorando mayúsculas/minúsculas
                int i = -1;
                for (int j = 0; j < header.Length; j++)
                {
                    if (header[j].ToLower() == colName.ToLower()) { i = j; break; }
                }

                if (i < 0 || recipeData[0, i] == null || recipeData[0, i] is DBNull)
                {
                    Log.Error("DialogLogic", $"No se encontró la columna '{colName}' en la receta.");
                    return 0.0f;
                }

                float originalValue = Convert.ToSingle(recipeData[0, i]);
                return originalValue * factor;
            }
            // Función B: Pasa el valor DIRECTO sin modificar (Para RPM, Tiempos y QC) ✅ NUEVA
            object PassThrough(string colName)
            {
                int i = -1;
                for (int j = 0; j < header.Length; j++)
                {
                    if (header[j].ToLower() == colName.ToLower()) { i = j; break; }
                }

                if (i < 0 || recipeData[0, i] == null || recipeData[0, i] is DBNull)
                {
                    Log.Error("DialogLogic", $"No se encontró la columna de parámetro '{colName}' en la receta.");
                    return 0.0f;
                }

                // Devolvemos el valor tal cual viene de la receta maestra
                return recipeData[0, i];
            }

            // Función C: Conversión de Minutos a Base de Tiempo PLC ✅ NUEVA
            object ConvertMinutesToTimeBase(string colName)
            {
                int i = -1;
                for (int j = 0; j < header.Length; j++)
                {
                    if (header[j].ToLower() == colName.ToLower()) { i = j; break; }
                }

                if (i < 0 || recipeData[0, i] == null || recipeData[0, i] is DBNull) return 0.0f;

                float minutes = Convert.ToSingle(recipeData[0, i]);

                // Conversión: 1 Minuto = 60,000,000 Microsegundos
                // Nota Técnica: Los temporizadores estándar de Rockwell (TON) suelen usar Milisegundos.
                // Si tu bloque de PLC espera milisegundos, cambia 60000000.0f a 60000.0f
                return minutes * 60000.0f;
            }

            // IMPORTANTE: Asegúrate de que estos nombres coincidan con los de la imagen 1
            // Si en la imagen 1 dice "agua_kg", aquí debe decir "agua_kg"
            string[] plcCols = new string[] {
                "batch_id", 
                //Dispercion
                "calc_disp_agua_kg",
                "calc_disp_biocida_kg",
                "calc_disp_dispersantes_kg",
                "calc_disp_antiespumante_kg",
                "calc_disp_pigmentos_kg",
                "calc_disp_cargas_kg",
                //Dispercion Parametros 
                "calc_disp_agitador_rpm",
                "calc_disp_mixing_time_min",
                //Dispercion QC
                "calc_qc_disp_particula_min",
                "calc_qc_disp_particula_max", 
                //Dilucion
                "calc_dil_agua_kg",
                "calc_dil_resina_kg",
                "calc_dil_espesantes_kg",
                "calc_dil_ph_regulator_kg",
                "calc_dil_antiespumante_kg",
                "calc_dil_biocida_kg",
                "calc_dil_aditivos_kg",
                //Dilucion Parametros
                "calc_dil_agitador_rpm",
                "calc_dil_mixing_time_min",
                //Dilucion QC
                "calc_qc_dil_particula_min",
                "calc_qc_dil_particula_max",
                "calc_qc_dil_viscosidad_min",
                "calc_qc_dil_viscosidad_max",
                "calc_qc_dil_ph_min",
                "calc_qc_dil_ph_max",
                "calc_qc_dil_tono_min",
                "calc_qc_dil_tono_max",
                "calc_qc_dil_densidad_min",
                "calc_qc_dil_densidad_max"
            };

            object[,] plcValues = new object[1, 30] {
                {
                    batchId, 
                    //Dispercion
                    Scale("disp_agua_kg"), // <-- VERIFICA ESTO CON TU TABLA RECIPES
                    Scale("disp_biocida_kg"),
                    Scale("disp_dispersantes_kg"),
                    Scale("disp_antiespumante_kg"),
                    Scale("disp_pigmentos_kg"),
                    Scale("disp_cargas_kg"),
                    //Dispercion Parametros
                    PassThrough("disp_agitador_rpm"),
                    ConvertMinutesToTimeBase("disp_mixing_time_min"),
                    //Dispercion QC
                    PassThrough("qc_disp_particula_min"), PassThrough("qc_disp_particula_max"),
                    //Dilucion
                    Scale("dil_agua_kg"),
                    Scale("dil_resina_kg"),
                    Scale("dil_espesantes_kg"),
                    Scale("dil_ph_regulator_kg"),
                    Scale("dil_antiespumante_kg"),
                    Scale("dil_biocida_kg"),
                    Scale("dil_aditivos_kg"),
                    //Dilucion Parametros
                    PassThrough("dil_agitador_rpm"),
                    ConvertMinutesToTimeBase("dil_mixing_time_min"),
                    //Dispercion QC
                    PassThrough("qc_dil_particula_min"), PassThrough("qc_dil_particula_max"),
                    PassThrough("qc_dil_viscosidad_min"), PassThrough("qc_dil_viscosidad_max"),
                    PassThrough("qc_dil_ph_min"), PassThrough("qc_dil_ph_max"),
                    PassThrough("qc_dil_tono_min"), PassThrough("qc_dil_tono_max"),
                    PassThrough("qc_dil_densidad_min"), PassThrough("qc_dil_densidad_max")
                }
            };
            // 6. GUARDAR EN BASE DE DATOS
            store.Insert("batch_plc_setpoints", plcCols, plcValues);

            // 7. 🚀 ESCRITURA DIRECTA AL MODELO (PLC)
            string modelFolderPath = "Model/ActiveBatchParameters";
            var folderNode = Project.Current.Get(modelFolderPath);

            if (folderNode != null)
            {
                // Empezamos desde i = 1 para saltarnos el "batch_id"
                for (int i = 1; i < plcCols.Length; i++)
                {
                    string variableName = plcCols[i];
                    var targetVariable = folderNode.GetVariable(variableName);

                    if (targetVariable != null)
                    {
                        // ✅ SOLUCIÓN: Envolvemos el objeto en un objeto UAValue nativo de Optix
                        targetVariable.Value = new UAValue(plcValues[0, i]);
                    }
                    else
                    {
                        Log.Warning("DialogLogic", $"Falta crear la variable '{variableName}' en {modelFolderPath}");
                    }
                }
                Log.Info("DialogLogic", "¡Setpoints cargados en tiempo real hacia las variables del Modelo/PLC!");
            }
            else
            {
                Log.Error("DialogLogic", $"No se encontró la carpeta '{modelFolderPath}'. Crea la carpeta en tu Modelo.");
            }

            // =========================================================================
            // 7.5. 🔥 CÁLCULO DE TOTALES MAESTROS PARA LA BÁSCULA 🔥
            // =========================================================================
            try
            {
                // Sumamos todos los componentes ya escalados de la fase de Dispersión
                float totalDispersion =
                    Convert.ToSingle(Scale("disp_agua_kg")) +
                    Convert.ToSingle(Scale("disp_biocida_kg")) +
                    Convert.ToSingle(Scale("disp_dispersantes_kg")) +
                    Convert.ToSingle(Scale("disp_antiespumante_kg")) +
                    Convert.ToSingle(Scale("disp_pigmentos_kg")) +
                    Convert.ToSingle(Scale("disp_cargas_kg"));

                // Sumamos todos los componentes ya escalados de la fase de Dilución
                float totalDilucion =
                    Convert.ToSingle(Scale("dil_agua_kg")) +
                    Convert.ToSingle(Scale("dil_resina_kg")) +
                    Convert.ToSingle(Scale("dil_espesantes_kg")) +
                    Convert.ToSingle(Scale("dil_ph_regulator_kg")) +
                    Convert.ToSingle(Scale("dil_antiespumante_kg")) +
                    Convert.ToSingle(Scale("dil_biocida_kg")) +
                    Convert.ToSingle(Scale("dil_aditivos_kg"));

                // Escribimos los totales directamente en las variables maestras del Modelo
                var nodeTotalDisp = Project.Current.GetVariable("Model/ActiveBatchParameters/Target_Total_Dispersion_Kg");
                if (nodeTotalDisp != null)
                {
                    nodeTotalDisp.Value = totalDispersion;
                }
                else
                {
                    Log.Warning("DialogLogic", "Falta crear la variable 'Target_Total_Dispersion_Kg' en Model/ActiveBatchParameters");
                }

                var nodeTotalDil = Project.Current.GetVariable("Model/ActiveBatchParameters/Target_Total_Dilucion_Kg");
                if (nodeTotalDil != null)
                {
                    nodeTotalDil.Value = totalDilucion;
                }
                else
                {
                    Log.Warning("DialogLogic", "Falta crear la variable 'Target_Total_Dilucion_Kg' en Model/ActiveBatchParameters");
                }

                Log.Info("DialogLogic", $"Pesos Totales Calculados -> Dispersión: {totalDispersion} kg | Dilución: {totalDilucion} kg");
            }
            catch (Exception ex)
            {
                Log.Error("DialogLogic", "Error calculando pesos totales: " + ex.Message);
            }

            // -------------------------------------------------------------------------
            // 8. ACTUALIZAR TRACKING EN HMI
            var selectedBatchNode = Project.Current.GetVariable("Model/BatchTracking/SelectedBatchID");
            if (selectedBatchNode != null) selectedBatchNode.Value = batchId;

            var trackingOperatorNode = Project.Current.GetVariable("Model/BatchTracking/Operator");
            if (trackingOperatorNode != null) trackingOperatorNode.Value = operatorName;

            var selectedRecipeNode = Project.Current.GetVariable("Model/BatchTracking/SelectedRecipe");
            if (selectedRecipeNode != null) selectedRecipeNode.Value = recipeIdToSearch;

            var selectedRecipeNNode = Project.Current.GetVariable("Model/BatchTracking/SelectedRecipeN");
            if (selectedRecipeNNode != null) selectedRecipeNNode.Value = recipeNamePure;

            var statusNode = Project.Current.GetVariable("Model/BatchTracking/BatchStatus");
            if (statusNode != null) statusNode.Value = "Idle";

            Log.Info("DialogLogic", $"¡Lote {reducedBatchId} creado con éxito!");
        }
        catch (Exception ex) { Log.Error("DialogLogic", "Error: " + ex.Message); }
    }
}
