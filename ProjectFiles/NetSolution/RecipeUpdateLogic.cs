#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
using FTOptix.HMIProject;
using FTOptix.Store;
using FTOptix.NetLogic;
using FTOptix.Core;
using FTOptix.DataLogger;
using FTOptix.RAEtherNetIP;
using FTOptix.CommunicationDriver;
using FTOptix.SerialPort;
#endregion

public class RecipeUpdateLogic : BaseNetLogic
{
    [ExportMethod]
    public void UpdateExistingRecipe(NodeId dataGridNodeId)
    {
        try
        {
            Log.Info("RecipeUpdater", "Iniciando actualización y limpieza...");

            var store = Project.Current.Get<Store>("DataStores/SQLiteDatabase");
            if (store == null) return;

            var modelPath = "Model/CurrentRecipe/";

            // 1. EL IDENTIFICADOR
            string recipeId = Project.Current.GetVariable(modelPath + "RecipeId")?.Value.Value.ToString() ?? "";
            
            if (string.IsNullOrEmpty(recipeId))
            {
                Log.Error("RecipeUpdater", "No hay un RecipeID cargado. No se puede actualizar.");
                return;
            }

            // 2. CAPTURAR VALORES MODIFICADOS DE LA PANTALLA
            string recipeName = Project.Current.GetVariable(modelPath + "RecipeName")?.Value ?? "";
            string productType = Project.Current.GetVariable(modelPath + "ProductType")?.Value ?? "";
            string version = Project.Current.GetVariable(modelPath + "Version")?.Value ?? "1.0";
            string status = Project.Current.GetVariable(modelPath + "Status")?.Value ?? "";
            float batchSize = Project.Current.GetVariable(modelPath + "BatchSize")?.Value ?? 0.0f;

            float dAgua = Project.Current.GetVariable(modelPath + "Disp_Agua")?.Value ?? 0.0f;
            float dBiocida = Project.Current.GetVariable(modelPath + "Disp_Biocida")?.Value ?? 0.0f;
            float dDispersantes = Project.Current.GetVariable(modelPath + "Disp_Dispersantes")?.Value ?? 0.0f;
            float dAntiespumante = Project.Current.GetVariable(modelPath + "Disp_Antiespumante")?.Value ?? 0.0f;
            float dPigmentos = Project.Current.GetVariable(modelPath + "Disp_Pigmentos")?.Value ?? 0.0f;
            float dCargas = Project.Current.GetVariable(modelPath + "Disp_Cargas")?.Value ?? 0.0f;
            int dRPM = Project.Current.GetVariable(modelPath + "Disp_AgitadorRPM")?.Value ?? 0;
            int dTime = Project.Current.GetVariable(modelPath + "Disp_MixingTime")?.Value ?? 0;
            float qcDPMin = Project.Current.GetVariable(modelPath + "QC_Disp_ParticleMin")?.Value ?? 0.0f;
            float qcDPMax = Project.Current.GetVariable(modelPath + "QC_Disp_ParticleMax")?.Value ?? 0.0f;

            float lAgua = Project.Current.GetVariable(modelPath + "Dil_Agua")?.Value ?? 0.0f;
            float lResina = Project.Current.GetVariable(modelPath + "Dil_Resina")?.Value ?? 0.0f;
            float lEspesante = Project.Current.GetVariable(modelPath + "Dil_Espesante")?.Value ?? 0.0f;
            float lPH = Project.Current.GetVariable(modelPath + "Dil_ReguladorPH")?.Value ?? 0.0f;
            float lAntiespumante = Project.Current.GetVariable(modelPath + "Dil_Antiespumante")?.Value ?? 0.0f;
            float lBiocida = Project.Current.GetVariable(modelPath + "Dil_Biocida")?.Value ?? 0.0f;
            float lOtros = Project.Current.GetVariable(modelPath + "Dil_OtrosAditivos")?.Value ?? 0.0f;
            int lRPM = Project.Current.GetVariable(modelPath + "Dil_AgitadorRPM")?.Value ?? 0;
            int lTime = Project.Current.GetVariable(modelPath + "Dil_MixingTime")?.Value ?? 0;

            float qcLVicMin = Project.Current.GetVariable(modelPath + "QC_Dil_ViscosityMin")?.Value ?? 0.0f;
            float qcLVicMax = Project.Current.GetVariable(modelPath + "QC_Dil_ViscosityMax")?.Value ?? 0.0f;
            float qcLDenMin = Project.Current.GetVariable(modelPath + "QC_Dil_DensityMin")?.Value ?? 0.0f;
            float qcLDenMax = Project.Current.GetVariable(modelPath + "QC_Dil_DensityMax")?.Value ?? 0.0f;
            float qcLTonMin = Project.Current.GetVariable(modelPath + "QC_Dil_TonoMin")?.Value ?? 0.0f;
            float qcLTonMax = Project.Current.GetVariable(modelPath + "QC_Dil_TonoMax")?.Value ?? 0.0f;
            float qcLParMin = Project.Current.GetVariable(modelPath + "QC_Dil_ParticleMin")?.Value ?? 0.0f;
            float qcLParMax = Project.Current.GetVariable(modelPath + "QC_Dil_ParticleMax")?.Value ?? 0.0f;
            float qcLPHMin = Project.Current.GetVariable(modelPath + "QC_Dil_PHMin")?.Value ?? 0.0f;
            float qcLPHMax = Project.Current.GetVariable(modelPath + "QC_Dil_PHMax")?.Value ?? 0.0f;

            // 3. ACTUALIZACIÓN EN SQLITE
            string updateQuery = $"UPDATE recipes SET " +
                $"recipe_name = '{recipeName}', product_type = '{productType}', version = '{version}', status = '{status}', batch_size = {batchSize}, " +
                $"disp_agua_kg = {dAgua}, disp_biocida_kg = {dBiocida}, disp_dispersantes_kg = {dDispersantes}, disp_antiespumante_kg = {dAntiespumante}, " +
                $"disp_pigmentos_kg = {dPigmentos}, disp_cargas_kg = {dCargas}, disp_agitador_rpm = {dRPM}, disp_mixing_time_min = {dTime}, " +
                $"qc_disp_particula_min = {qcDPMin}, qc_disp_particula_max = {qcDPMax}, " +
                $"dil_agua_kg = {lAgua}, dil_resina_kg = {lResina}, dil_espesantes_kg = {lEspesante}, dil_ph_regulator_kg = {lPH}, " +
                $"dil_antiespumante_kg = {lAntiespumante}, dil_biocida_kg = {lBiocida}, dil_aditivos_kg = {lOtros}, dil_agitador_rpm = {lRPM}, " +
                $"dil_mixing_time_min = {lTime}, qc_dil_viscosidad_min = {qcLVicMin}, qc_dil_viscosidad_max = {qcLVicMax}, " +
                $"qc_dil_densidad_min = {qcLDenMin}, qc_dil_densidad_max = {qcLDenMax}, qc_dil_tono_min = {qcLTonMin}, qc_dil_tono_max = {qcLTonMax}, " +
                $"qc_dil_particula_min = {qcLParMin}, qc_dil_particula_max = {qcLParMax}, qc_dil_ph_min = {qcLPHMin}, qc_dil_ph_max = {qcLPHMax} " +
                $"WHERE recipe_id = '{recipeId}'";

            store.Query(updateQuery, out string[] header, out object[,] resultSet);
            Log.Info("RecipeUpdater", $"¡Receta '{recipeId}' actualizada con éxito!");

            // 4. RECARGA VISUAL (El Parpadeo)
            try
            {
                if (dataGridNodeId != null)
                {
                    var dataGrid = InformationModel.Get<DataGrid>(dataGridNodeId);
                    if (dataGrid != null)
                    {
                        var currentModel = dataGrid.Model;
                        dataGrid.Model = NodeId.Empty;
                        dataGrid.Model = currentModel;
                    }
                }
            }
            catch { /* Ignorar errores visuales menores */ }

            // --- 🔥 5. FUNCIÓN DE LIMPIEZA AUTOMÁTICA 🔥 ---
            void ResetVar(string name, object defaultValue)
            {
                var v = Project.Current.GetVariable(modelPath + name);
                if (v != null) v.Value = new UAValue(defaultValue);
            }

            ResetVar("RecipeId", "");
            ResetVar("RecipeName", "");
            ResetVar("BatchSize", 0.0f);
            ResetVar("Version", "");
            ResetVar("Status", "");
            ResetVar("ProductType", "");

            // Dispersión
            ResetVar("Disp_Agua", 0.0f);
            ResetVar("Disp_Biocida", 0.0f);
            ResetVar("Disp_Dispersantes", 0.0f);
            ResetVar("Disp_Antiespumante", 0.0f);
            ResetVar("Disp_Pigmentos", 0.0f);
            ResetVar("Disp_Cargas", 0.0f);
            ResetVar("Disp_AgitadorRPM", 0);
            ResetVar("Disp_MixingTime", 0);
            ResetVar("QC_Disp_ParticleMin", 0.0f);
            ResetVar("QC_Disp_ParticleMax", 0.0f);

            // Dilución
            ResetVar("Dil_Agua", 0.0f);
            ResetVar("Dil_Resina", 0.0f);
            ResetVar("Dil_Espesante", 0.0f);
            ResetVar("Dil_ReguladorPH", 0.0f);
            ResetVar("Dil_Antiespumante", 0.0f);
            ResetVar("Dil_Biocida", 0.0f);
            ResetVar("Dil_OtrosAditivos", 0.0f);
            ResetVar("Dil_AgitadorRPM", 0);
            ResetVar("Dil_MixingTime", 0);

            // Calidad Final
            ResetVar("QC_Dil_ViscosityMin", 0.0f);
            ResetVar("QC_Dil_ViscosityMax", 0.0f);
            ResetVar("QC_Dil_DensityMin", 0.0f);
            ResetVar("QC_Dil_DensityMax", 0.0f);
            ResetVar("QC_Dil_TonoMin", 0.0f);
            ResetVar("QC_Dil_TonoMax", 0.0f);
            ResetVar("QC_Dil_ParticleMin", 0.0f);
            ResetVar("QC_Dil_ParticleMax", 0.0f);
            ResetVar("QC_Dil_PHMin", 0.0f);
            ResetVar("QC_Dil_PHMax", 0.0f);

            Log.Info("RecipeUpdater", "Pantalla reseteada tras actualización.");
        }
        catch (Exception ex)
        {
            Log.Error("RecipeUpdater", "Error fatal: " + ex.Message);
        }
    }
}
