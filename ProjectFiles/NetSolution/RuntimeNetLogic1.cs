using System;
using FTOptix.CoreBase;
using FTOptix.NetLogic;
using FTOptix.SerialPort; 
using UAManagedCore;
using System.Text; 
using FTOptix.HMIProject; 
using FTOptix.Core;
using System.Text.RegularExpressions;

public class RuntimeNetLogic1 : BaseNetLogic
{
    private SerialPort serialPort;
    private LongRunningTask task;

    public override void Start()
    {
        serialPort = (SerialPort)Owner;
        serialPort.Timeout = TimeSpan.FromMilliseconds(0.0);

        LimpiarDataStore();
        task = new LongRunningTask(Run, Owner);
        task.Start();
    }

    [ExportMethod]
    public void OnClick()
    {
        if (serialPort != null) serialPort.CancelRead();
        if (task != null) task.Cancel();
        task = new LongRunningTask(Run, Owner);
        task.Start();
    }

    // --- NUEVO MÉTODO EXPORTADO PARA EL BOTÓN DE LOGOUT ---
    [ExportMethod]
    public void LimpiarBuffersBoton()
    {
        LimpiarDataStore();
        Log.Info("RFID_Lector", "Buffers limpiados con éxito desde el botón de Logout.");
    }

    private void Run()
    {
        while (!task.IsCancellationRequested)
        {
            try
            {
                var result = serialPort.ReadBytes(10);
                
                if (result != null && result.Length > 0)
                {
                    string rawUid = Encoding.ASCII.GetString(result);
                    string uid = Regex.Replace(rawUid, @"[^\w\d]", "").Trim();
                    
                    Log.Info("RFID_Lector", $"Tag leído (Sanitizado): '{uid}'");

                    if (string.IsNullOrWhiteSpace(uid)) continue;

                    string username = "";
                    string password = "";
                    bool usuarioEncontrado = false;

                    var usersFolder = Project.Current.Get("Security/Users");
                    
                    if (usersFolder != null)
                    {
                        foreach (var child in usersFolder.Children)
                        {
                            var rfidProperty = child.GetVariable("RfidTag");
                            
                            if (rfidProperty != null && rfidProperty.Value != null)
                            {
                                string pureTagValue = (string)rfidProperty.Value;
                                string tagAsignado = Regex.Replace(pureTagValue ?? "", @"[^\w\d]", "").Trim();

                                Log.Info("RFID_Debug", $"Comparando Lector: '{uid}' vs Optix ({child.BrowseName}): '{tagAsignado}'");

                                if (!string.IsNullOrWhiteSpace(tagAsignado) && tagAsignado == uid)
                                {
                                    username = child.BrowseName;
                                    
                                    var claveProperty = child.GetVariable("ClaveRFID");
                                    if (claveProperty != null && claveProperty.Value != null)
                                    {
                                        password = (string)claveProperty.Value;
                                    }
                                    else
                                    {
                                        password = "1234"; 
                                    }
                                    
                                    usuarioEncontrado = true;
                                    break; 
                                }
                            }
                        }
                    }

                    if (!usuarioEncontrado)
                    {
                        Log.Error("RFID_Lector", $"Acceso Denegado: El tag '{uid}' no coincide.");
                        LimpiarDataStore();
                        continue; 
                    }

                    var userStoreVar = Project.Current.GetVariable("DataStores/RfidStorage/UserBuffer");
                    var passStoreVar = Project.Current.GetVariable("DataStores/RfidStorage/PassBuffer");

                    if (userStoreVar != null && passStoreVar != null)
                    {
                        userStoreVar.Value = username;
                        passStoreVar.Value = password;
                        Log.Info("RFID_Lector", $"¡Login Exitoso! Preparado para: {username}");
                    }
                }
            }
            catch (ReadCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Log.Error("RFID_Lector_Error", e.Message);
            }
        }
    }

    private void LimpiarDataStore()
    {
        try
        {
            var userStoreVar = Project.Current.GetVariable("DataStores/RfidStorage/UserBuffer");
            var passStoreVar = Project.Current.GetVariable("DataStores/RfidStorage/PassBuffer");

            if (userStoreVar != null && passStoreVar != null)
            {
                userStoreVar.Value = "";
                passStoreVar.Value = "";
            }
        }
        catch { }
    }

    public override void Stop()
    {
        if (serialPort != null) serialPort.Close();
        if (task != null) task.Cancel();
    }
}