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

public class RecipeCreationManager : BaseNetLogic
{
    [ExportMethod]
    public void CreateNewRecipeFromPopup()
    {
        try
        {
            Log.Info("RecipeCreator", "Iniciando inserción de nueva receta en base de datos...");

            var store = Project.Current.Get<Store>("DataStores/SQLiteDatabase");
            if (store == null)
            {
                Log.Error("RecipeCreator", "ERROR FATAL: Base de datos SQLiteDatabase no encontrada.");
                return;
            }


            // 1. RECUPERAR DATOS DE IDENTIFICACIÓN
            string recipeId = Project.Current.GetVariable("Model/CurrentRecipe/RecipeId")?.Value ?? "REC-NEW";
            string recipeName = Project.Current.GetVariable("Model/CurrentRecipe/RecipeName")?.Value ?? "New Paint";
            string productType = Project.Current.GetVariable("Model/CurrentRecipe/ProductType")?.Value ?? "Acrylic";
            string version = Project.Current.GetVariable("Model/CurrentRecipe/Version")?.Value ?? "1.0";
            string status = Project.Current.GetVariable("Model/CurrentRecipe/Status")?.Value ?? "Draft";
            float batchSize = Project.Current.GetVariable("Model/CurrentRecipe/BatchSize")?.Value ?? 1000.0f;

            // 2. RECUPERAR DATOS DE PROCESO
            float dAgua = Project.Current.GetVariable("Model/CurrentRecipe/Disp_Agua")?.Value ?? 0.0f;
            float dBiocida = Project.Current.GetVariable("Model/CurrentRecipe/Disp_Biocida")?.Value ?? 0.0f;
            float dDispersantes = Project.Current.GetVariable("Model/CurrentRecipe/Disp_Dispersantes")?.Value ?? 0.0f;
            float dAntiespumante = Project.Current.GetVariable("Model/CurrentRecipe/Disp_Antiespumante")?.Value ?? 0.0f;
            float dPigmentos = Project.Current.GetVariable("Model/CurrentRecipe/Disp_Pigmentos")?.Value ?? 0.0f;
            float dCargas = Project.Current.GetVariable("Model/CurrentRecipe/Disp_Cargas")?.Value ?? 0.0f;
            int dRPM = Project.Current.GetVariable("Model/CurrentRecipe/Disp_AgitadorRPM")?.Value ?? 0;
            int dTime = Project.Current.GetVariable("Model/CurrentRecipe/Disp_MixingTime")?.Value ?? 0;

            float qcDPArticleMin = Project.Current.GetVariable("Model/CurrentRecipe/QC_Disp_ParticleMin")?.Value ?? 0.0f;
            float qcDPArticleMax = Project.Current.GetVariable("Model/CurrentRecipe/QC_Disp_ParticleMax")?.Value ?? 0.0f;

            float lAgua = Project.Current.GetVariable("Model/CurrentRecipe/Dil_Agua")?.Value ?? 0.0f;
            float lResina = Project.Current.GetVariable("Model/CurrentRecipe/Dil_Resina")?.Value ?? 0.0f;
            float lEspesante = Project.Current.GetVariable("Model/CurrentRecipe/Dil_Espesante")?.Value ?? 0.0f;
            float lPH = Project.Current.GetVariable("Model/CurrentRecipe/Dil_ReguladorPH")?.Value ?? 0.0f;
            float lAntiespumante = Project.Current.GetVariable("Model/CurrentRecipe/Dil_Antiespumante")?.Value ?? 0.0f;
            float lBiocida = Project.Current.GetVariable("Model/CurrentRecipe/Dil_Biocida")?.Value ?? 0.0f;
            float lOtros = Project.Current.GetVariable("Model/CurrentRecipe/Dil_OtrosAditivos")?.Value ?? 0.0f;
            int lRPM = Project.Current.GetVariable("Model/CurrentRecipe/Dil_AgitadorRPM")?.Value ?? 0;
            int lTime = Project.Current.GetVariable("Model/CurrentRecipe/Dil_MixingTime")?.Value ?? 0;

            float qcLVicocityMin = Project.Current.GetVariable("Model/CurrentRecipe/QC_Dil_ViscosityMin")?.Value ?? 0.0f;
            float qcLVicocityMax = Project.Current.GetVariable("Model/CurrentRecipe/QC_Dil_ViscosityMax")?.Value ?? 0.0f;
            float qcLDensityMin = Project.Current.GetVariable("Model/CurrentRecipe/QC_Dil_DensityMin")?.Value ?? 0.0f;
            float qcLDensityMax = Project.Current.GetVariable("Model/CurrentRecipe/QC_Dil_DensityMax")?.Value ?? 0.0f;
            float qcLTonoMin = Project.Current.GetVariable("Model/CurrentRecipe/QC_Dil_TonoMin")?.Value ?? 0.0f;
            float qcLTonoMax = Project.Current.GetVariable("Model/CurrentRecipe/QC_Dil_TonoMax")?.Value ?? 0.0f;
            float qcLPParticleMin = Project.Current.GetVariable("Model/CurrentRecipe/QC_Dil_ParticleMin")?.Value ?? 0.0f;
            float qcLPParticleMax = Project.Current.GetVariable("Model/CurrentRecipe/QC_Dil_ParticleMax")?.Value ?? 0.0f;
            float qcLPPHMin = Project.Current.GetVariable("Model/CurrentRecipe/QC_Dil_PHMin")?.Value ?? 0.0f;
            float qcLPPHMax = Project.Current.GetVariable("Model/CurrentRecipe/QC_Dil_PHMax")?.Value ?? 0.0f;

            Log.Info("RecipeCreator", $"Guardando Receta: ID='{recipeId}' | Nombre='{recipeName}'");

            // 3. PREPARAR EL INSERT (35 Columnas exactas)
            string[] columns = new string[]
            {
                "recipe_id", "recipe_name", "disp_agua_kg", "disp_biocida_kg", 
                "disp_dispersantes_kg", "disp_antiespumante_kg", "disp_pigmentos_kg", "disp_cargas_kg", 
                "disp_agitador_rpm", "disp_mixing_time_min", "qc_disp_particula_min", "qc_disp_particula_max", 
                "dil_agua_kg", "dil_resina_kg", "dil_espesantes_kg", "dil_ph_regulator_kg", 
                "dil_antiespumante_kg", "dil_biocida_kg", "dil_aditivos_kg", "dil_agitador_rpm", 
                "dil_mixing_time_min", "qc_dil_particula_min", "qc_dil_particula_max", "qc_dil_viscosidad_min", 
                "qc_dil_viscosidad_max", "qc_dil_ph_min", "qc_dil_ph_max", "qc_dil_tono_min", 
                "qc_dil_tono_max", "qc_dil_densidad_min", "qc_dil_densidad_max", "product_type", 
                "version", "batch_size", "status"
            };

            // 35 Valores exactos
            object[,] values = new object[1, 35]
            {
                {
                    recipeId, recipeName, dAgua, dBiocida, 
                    dDispersantes, dAntiespumante, dPigmentos, dCargas, 
                    dRPM, dTime, qcDPArticleMin, qcDPArticleMax, 
                    lAgua, lResina, lEspesante, lPH, 
                    lAntiespumante, lBiocida, lOtros, lRPM, 
                    lTime, qcLPParticleMin, qcLPParticleMax, qcLVicocityMin, 
                    qcLVicocityMax, qcLPPHMin, qcLPPHMax, qcLTonoMin, 
                    qcLTonoMax, qcLDensityMin, qcLDensityMax, productType, 
                    version, batchSize, status
                }
            };

            // 4. INYECTAR EN SQLITE
            store.Insert("recipes", columns, values);
            Log.Info("RecipeCreator", $"¡ÉXITO! Receta '{recipeId}' insertada en SQLite.");

            // 5. REFRESCAR LA TABLA DIRECTAMENTE (Con amortiguador de errores)
            try
            {
                var tableObj = Project.Current.Get<IUAObject>("DataStores/SQLiteDatabase/Tables/recipes");
                if (tableObj != null)
                {
                    tableObj.ExecuteMethod("Refresh");
                    Log.Info("RecipeCreator", "Tabla de recetas actualizada en pantalla.");
                }
            }
            catch (Exception)
            {
                Log.Info("RecipeCreator", "Receta guardada con éxito. (El auto-refresh visual omitido).");
            }

            // --- 🔥 6. FUNCIÓN DE LIMPIEZA (RESET A 0) 🔥 ---
            try
            {
                void ResetVar(string variablePath, object defaultValue)
                {
                    var variable = Project.Current.GetVariable(variablePath);
                    if (variable != null)
                    {
                        variable.Value = new UAValue(defaultValue);
                    }
                }

                // Identificación
                ResetVar("Model/CurrentRecipe/RecipeId", "");
                ResetVar("Model/CurrentRecipe/RecipeName", "");
                ResetVar("Model/CurrentRecipe/BatchSize", 0.0f);
                // Opcional: Si quieres que regrese a un default, puedes poner "Acrylic" o "1.0". 
                // Si prefieres que se queden como el usuario los dejó, puedes borrar estas líneas.
                ResetVar("Model/CurrentRecipe/Version", "");
                ResetVar("Model/CurrentRecipe/Status", "");
                ResetVar("Model/CurrentRecipe/ProductType", "");

                // Dispersión
                ResetVar("Model/CurrentRecipe/Disp_Agua", 0.0f);
                ResetVar("Model/CurrentRecipe/Disp_Biocida", 0.0f);
                ResetVar("Model/CurrentRecipe/Disp_Dispersantes", 0.0f);
                ResetVar("Model/CurrentRecipe/Disp_Antiespumante", 0.0f);
                ResetVar("Model/CurrentRecipe/Disp_Pigmentos", 0.0f);
                ResetVar("Model/CurrentRecipe/Disp_Cargas", 0.0f);
                ResetVar("Model/CurrentRecipe/Disp_AgitadorRPM", 0);
                ResetVar("Model/CurrentRecipe/Disp_MixingTime", 0);
                ResetVar("Model/CurrentRecipe/QC_Disp_ParticleMin", 0.0f);
                ResetVar("Model/CurrentRecipe/QC_Disp_ParticleMax", 0.0f);

                // Dilución
                ResetVar("Model/CurrentRecipe/Dil_Agua", 0.0f);
                ResetVar("Model/CurrentRecipe/Dil_Resina", 0.0f);
                ResetVar("Model/CurrentRecipe/Dil_Espesante", 0.0f);
                ResetVar("Model/CurrentRecipe/Dil_ReguladorPH", 0.0f);
                ResetVar("Model/CurrentRecipe/Dil_Antiespumante", 0.0f);
                ResetVar("Model/CurrentRecipe/Dil_Biocida", 0.0f);
                ResetVar("Model/CurrentRecipe/Dil_OtrosAditivos", 0.0f);
                ResetVar("Model/CurrentRecipe/Dil_AgitadorRPM", 0);
                ResetVar("Model/CurrentRecipe/Dil_MixingTime", 0);

                // Calidad Dilución
                ResetVar("Model/CurrentRecipe/QC_Dil_ViscosityMin", 0.0f);
                ResetVar("Model/CurrentRecipe/QC_Dil_ViscosityMax", 0.0f);
                ResetVar("Model/CurrentRecipe/QC_Dil_DensityMin", 0.0f);
                ResetVar("Model/CurrentRecipe/QC_Dil_DensityMax", 0.0f);
                ResetVar("Model/CurrentRecipe/QC_Dil_TonoMin", 0.0f);
                ResetVar("Model/CurrentRecipe/QC_Dil_TonoMax", 0.0f);
                ResetVar("Model/CurrentRecipe/QC_Dil_ParticleMin", 0.0f);
                ResetVar("Model/CurrentRecipe/QC_Dil_ParticleMax", 0.0f);
                ResetVar("Model/CurrentRecipe/QC_Dil_PHMin", 0.0f);
                ResetVar("Model/CurrentRecipe/QC_Dil_PHMax", 0.0f);

                Log.Info("RecipeCreator", "Formulario reseteado y listo para una nueva receta.");
            }
            catch (Exception ex)
            {
                Log.Warning("RecipeCreator", "Aviso durante limpieza: " + ex.Message);
            }
        }
        catch (Exception ex)
        {
            Log.Error("RecipeCreator", $"ERROR INSERTANDO RECETA: {ex.Message}");
        }
    }
}
