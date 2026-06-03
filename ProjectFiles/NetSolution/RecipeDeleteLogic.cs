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

public class RecipeDeleteLogic : BaseNetLogic
{
    [ExportMethod]
    public void DeleteSelectedRecipe(NodeId dataGridNodeId)
    {
        try
        {
            if (dataGridNodeId == null) return;

            var dataGrid = InformationModel.Get<DataGrid>(dataGridNodeId);
            if (dataGrid == null || dataGrid.SelectedItem == NodeId.Empty || dataGrid.SelectedItem == null) 
            {
                Log.Warning("RecipeDeleter", "Selecciona una fila azul primero.");
                return;
            }

            var selectedRow = InformationModel.Get(dataGrid.SelectedItem);
            var recipeIdVar = selectedRow?.GetVariable("recipe_id");
            if (recipeIdVar == null || recipeIdVar.Value == null) return;

            // 🔥 LA SOLUCIÓN MAESTRA 🔥
            // .Value.Value extrae el texto primitivo sin la etiqueta "(String)"
            string exactRecipeId = recipeIdVar.Value.Value.ToString();

            var store = Project.Current.Get<Store>("DataStores/SQLiteDatabase");
            if (store == null) return;

            string deleteQuery = $"DELETE FROM recipes WHERE recipe_id = '{exactRecipeId}'";
            
            store.Query(deleteQuery, out string[] header, out object[,] resultSet);
            Log.Info("RecipeDeleter", $"¡BOMBA SQL DISPARADA! -> {deleteQuery}");

            var currentModel = dataGrid.Model;
            dataGrid.Model = NodeId.Empty;
            dataGrid.Model = currentModel;
            
            Log.Info("RecipeDeleter", "DataGrid recargado exitosamente. Receta eliminada.");
        }
        catch (Exception ex)
        {
            Log.Error("RecipeDeleter", "Error al borrar: " + ex.Message);
        }
    }
}
