#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.Core;
using FTOptix.DataLogger;
using FTOptix.RAEtherNetIP;
using FTOptix.CommunicationDriver;
using FTOptix.SerialPort;
#endregion

public class RecipeClearLogic : BaseNetLogic
{
    [ExportMethod]
    public void ClearCurrentRecipe()
    {
        try
        {
            Log.Info("RecipeClear", "Limpiando formulario...");

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

            Log.Info("RecipeClear", "¡Pantalla reseteada exitosamente a 0!");
        }
        catch (Exception ex)
        {
            Log.Error("RecipeClear", "Error al limpiar: " + ex.Message);
        }
    }
}
