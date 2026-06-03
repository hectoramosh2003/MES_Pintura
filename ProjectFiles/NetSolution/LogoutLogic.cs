using System;
using FTOptix.CoreBase;
using FTOptix.NetLogic;
using UAManagedCore;
using FTOptix.HMIProject;

public class LogoutLogic : BaseNetLogic
{
    [ExportMethod]
    public void PerformLogout()
    {
        try
        {
            // 1. Limpiamos los buffers del lector RFID para que no se quede el usuario viejo en pantalla
            var userStoreVar = Project.Current.GetVariable("DataStores/RfidStorage/UserBuffer");
            var passStoreVar = Project.Current.GetVariable("DataStores/RfidStorage/PassBuffer");

            if (userStoreVar != null) userStoreVar.Value = "";
            if (passStoreVar != null) passStoreVar.Value = "";

            Log.Info("LogoutLogic", "Buffers RFID limpios.");

            // 2. Cerramos la sesión nativa
            Session.Logout();
            Log.Info("LogoutLogic", "Sesión cerrada. El sistema regresará al Login por enlace dinámico.");
        }
        catch (Exception ex)
        {
            Log.Error("LogoutLogic", "Error: " + ex.Message);
        }
    }
}