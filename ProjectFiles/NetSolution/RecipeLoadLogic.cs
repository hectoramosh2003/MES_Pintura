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

public class RecipeLoadLogic : BaseNetLogic
{
    [ExportMethod]
    public void LoadSelectedRecipe(NodeId dataGridNodeId)
    {
        try
        {
            if (dataGridNodeId == null) return;

            var dataGrid = InformationModel.Get<DataGrid>(dataGridNodeId);
            if (dataGrid == null || dataGrid.SelectedItem == NodeId.Empty || dataGrid.SelectedItem == null)
            {
                Log.Warning("RecipeLoader", "Selecciona una receta (fila azul) primero para editar.");
                return;
            }

            // 1. Obtener el ID seleccionado
            var selectedRow = InformationModel.Get(dataGrid.SelectedItem);
            var recipeIdVar = selectedRow?.GetVariable("recipe_id");
            if (recipeIdVar == null || recipeIdVar.Value == null) return;

            string exactRecipeId = recipeIdVar.Value.Value.ToString();

            var store = Project.Current.Get<Store>("DataStores/SQLiteDatabase");
            if (store == null) return;

            // 2. Extraer TODA la fila de la base de datos
            string selectQuery = $"SELECT * FROM recipes WHERE recipe_id = '{exactRecipeId}'";
            store.Query(selectQuery, out string[] header, out object[,] resultSet);

            if (resultSet.GetLength(0) > 0)
            {
                Log.Info("RecipeLoader", $"Cargando receta '{exactRecipeId}' a la pantalla...");

                // Motor inteligente para emparejar base de datos con pantalla
                void SetVar(string modelName, string colName) 
                {
                    try 
                    {
                        int idx = Array.IndexOf(header, colName);
                        if (idx >= 0 && resultSet[0, idx] != null && resultSet[0, idx].GetType() != typeof(DBNull)) 
                        {
                            var variable = Project.Current.GetVariable("Model/CurrentRecipe/" + modelName);
                            if (variable != null) 
                            {
                                // 🔥 EL SECRETO REVELADO: En Optix se usa UAValue 🔥
                                variable.Value = new UAValue(resultSet[0, idx]);
                            }
                        }
                    }
                    catch { /* Si una columna falla, la ignora y sigue */ }
                }

                // --- IDENTIFICACIÓN ---
                SetVar("RecipeId", "recipe_id");
                SetVar("RecipeName", "recipe_name");
                SetVar("ProductType", "product_type");
                SetVar("Version", "version");
                SetVar("Status", "status");
                SetVar("BatchSize", "batch_size");

                // --- DISPERSIÓN ---
                SetVar("Disp_Agua", "disp_agua_kg");
                SetVar("Disp_Biocida", "disp_biocida_kg");
                SetVar("Disp_Dispersantes", "disp_dispersantes_kg");
                SetVar("Disp_Antiespumante", "disp_antiespumante_kg");
                SetVar("Disp_Pigmentos", "disp_pigmentos_kg");
                SetVar("Disp_Cargas", "disp_cargas_kg");
                SetVar("Disp_AgitadorRPM", "disp_agitador_rpm");
                SetVar("Disp_MixingTime", "disp_mixing_time_min");
                SetVar("QC_Disp_ParticleMin", "qc_disp_particula_min");
                SetVar("QC_Disp_ParticleMax", "qc_disp_particula_max");

                // --- DILUCIÓN ---
                SetVar("Dil_Agua", "dil_agua_kg");
                SetVar("Dil_Resina", "dil_resina_kg");
                SetVar("Dil_Espesante", "dil_espesantes_kg");
                SetVar("Dil_ReguladorPH", "dil_ph_regulator_kg");
                SetVar("Dil_Antiespumante", "dil_antiespumante_kg");
                SetVar("Dil_Biocida", "dil_biocida_kg");
                SetVar("Dil_OtrosAditivos", "dil_aditivos_kg");
                SetVar("Dil_AgitadorRPM", "dil_agitador_rpm");
                SetVar("Dil_MixingTime", "dil_mixing_time_min");

                // --- CALIDAD DILUCIÓN ---
                SetVar("QC_Dil_ParticleMin", "qc_dil_particula_min");
                SetVar("QC_Dil_ParticleMax", "qc_dil_particula_max");
                SetVar("QC_Dil_ViscosityMin", "qc_dil_viscosidad_min");
                SetVar("QC_Dil_ViscosityMax", "qc_dil_viscosidad_max");
                SetVar("QC_Dil_PHMin", "qc_dil_ph_min");
                SetVar("QC_Dil_PHMax", "qc_dil_ph_max");
                SetVar("QC_Dil_TonoMin", "qc_dil_tono_min");
                SetVar("QC_Dil_TonoMax", "qc_dil_tono_max");
                SetVar("QC_Dil_DensityMin", "qc_dil_densidad_min");
                SetVar("QC_Dil_DensityMax", "qc_dil_densidad_max");

                Log.Info("RecipeLoader", "¡Pantalla llena! Lista para ser editada.");
            }
        }
        catch (Exception ex)
        {
            Log.Error("RecipeLoader", "Error al cargar: " + ex.Message);
        }
    }
}
