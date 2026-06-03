#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.UI;
using FTOptix.DataLogger;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.NativeUI;
using FTOptix.SQLiteStore;
using FTOptix.Store;
using FTOptix.Retentivity;
using FTOptix.CoreBase;
using FTOptix.Core;
using FTOptix.RAEtherNetIP;
using FTOptix.CommunicationDriver;
using FTOptix.SerialPort;
#endregion

public class GlobalPhaseTracker : BaseNetLogic
{
    // 1. Declaramos la variable para nuestra Tarea Periódica
    private PeriodicTask updateTask;

    // 2. Este método se ejecuta AUTOMÁTICAMENTE cuando arranca tu HMI
    public override void Start()
    {
        // Creamos un bucle que llamará a "ActualizarProgresos" cada 1000 milisegundos (1 segundo)
        updateTask = new PeriodicTask(ActualizarProgresos, 500, LogicObject);
        updateTask.Start();
        
        Log.Info("PhaseTracker", "Motor de progreso iniciado. Actualizando barras cada segundo...");
    }

    // 3. Este método limpia la memoria cuando apagas el sistema
    public override void Stop()
    {
        if (updateTask != null)
        {
            updateTask.Dispose();
            updateTask = null;
        }
    }

    // =========================================================
    // A PARTIR DE AQUÍ VA TU CÓDIGO EXACTAMENTE COMO LO TIENES
    // =========================================================

    [ExportMethod]
    public void ActualizarProgresos()
    {
        try
        {
            // 1. Calculamos todas las fases individuales
            CalcularProgresoDispersion();
            CalcularProgresoQCDispersion();
            CalcularProgresoDilucion();
            CalcularProgresoQCDilucion();
            CalcularProgresoPackaging();

            // 2. Leemos los resultados (0 a 100) de cada fase
            float dispPct = Project.Current.GetVariable("Model/PhaseTracker/Fase_Disp/Progreso_UI").Value;
            float qcDispPct = Project.Current.GetVariable("Model/PhaseTracker/Fase_QC_Disp/Progreso_UI").Value;
            float dilPct = Project.Current.GetVariable("Model/PhaseTracker/Fase_Dil/Progreso_UI").Value;
            float qcDilPct = Project.Current.GetVariable("Model/PhaseTracker/Fase_QC_Dil/Progreso_UI").Value;
            float packPct = Project.Current.GetVariable("Model/PhaseTracker/Fase_Pack/Progreso_UI").Value;

            // 3. Aplicamos la ponderación de tiempo de ciclo
            float batchTotal =
                (dispPct * 0.40f) +
                (qcDispPct * 0.05f) +
                (dilPct * 0.35f) +
                (qcDilPct * 0.10f) +
                (packPct * 0.10f);

            // 4. Escribimos el gran total
            Project.Current.GetVariable("Model/ActiveBatchParameters/Progreso_Batch_Total").Value = batchTotal;
        }
        catch (Exception ex)
        {
            Log.Error("PhaseTracker", "Error general en actualización: " + ex.Message);
        }
    }

    // --- LÓGICAS PRIVADAS EXISTENTES ---

    private void CalcularProgresoDispersion()
    {
        int subPasoPLC = Project.Current.GetVariable("Model/PhaseTracker/Fase_Disp/SubStep_Actual").Value;
        float progresoTotalFase = 0.0f;

        if (subPasoPLC == 1)
        {
            // 🔥 AHORA SOLO LEEMOS UNA VARIABLE MAESTRA 🔥
            float totalKilosTarget = Project.Current.GetVariable("Model/ActiveBatchParameters/Target_Total_Dispersion_Kg").Value;
            float totalKilosActual = Project.Current.GetVariable("Model/PhaseTracker/Fase_Disp/Bascula_Dispersion").Value;

            if (totalKilosTarget > 0)
            {
                float porcentajeCarga = (totalKilosActual / totalKilosTarget);
                progresoTotalFase = (porcentajeCarga > 1.0f ? 1.0f : porcentajeCarga) * 50.0f;
            }
        }
        else if (subPasoPLC == 2)
        {
            float tiempoTarget = Project.Current.GetVariable("Model/PhaseTracker/Fase_Disp/Timer_PRE").Value;
            float tiempoActual = Project.Current.GetVariable("Model/PhaseTracker/Fase_Disp/Timer_ACC").Value;

            if (tiempoTarget > 0)
            {
                float porcentajeMezcla = (tiempoActual / tiempoTarget);
                progresoTotalFase = 50.0f + ((porcentajeMezcla > 1.0f ? 1.0f : porcentajeMezcla) * 50.0f);
            }
        }

        Project.Current.GetVariable("Model/PhaseTracker/Fase_Disp/Progreso_UI").Value = progresoTotalFase;
    }

    private void CalcularProgresoDilucion()
    {
        int subPasoPLC = Project.Current.GetVariable("Model/PhaseTracker/Fase_Dil/SubStep_Actual").Value;
        float progresoTotalFase = 0.0f;

        if (subPasoPLC == 1)
        {
            // 🔥 AHORA SOLO LEEMOS UNA VARIABLE MAESTRA 🔥
            float totalKilosTarget = Project.Current.GetVariable("Model/ActiveBatchParameters/Target_Total_Dilucion_Kg").Value;
            float totalKilosActual = Project.Current.GetVariable("Model/PhaseTracker/Fase_Dil/Bascula_Dilucion").Value;

            if (totalKilosTarget > 0)
            {
                float porcentajeCarga = (totalKilosActual / totalKilosTarget);
                progresoTotalFase = (porcentajeCarga > 1.0f ? 1.0f : porcentajeCarga) * 50.0f;
            }
        }
        else if (subPasoPLC == 2)
        {
            float tiempoTarget = Project.Current.GetVariable("Model/PhaseTracker/Fase_Dil/Timer_PRE").Value;
            float tiempoActual = Project.Current.GetVariable("Model/PhaseTracker/Fase_Dil/Timer_ACC").Value;

            if (tiempoTarget > 0)
            {
                float porcentajeMezcla = (tiempoActual / tiempoTarget);
                progresoTotalFase = 50.0f + ((porcentajeMezcla > 1.0f ? 1.0f : porcentajeMezcla) * 50.0f);
            }
        }

        Project.Current.GetVariable("Model/PhaseTracker/Fase_Dil/Progreso_UI").Value = progresoTotalFase;
    }

    // --- LÓGICAS PRIVADAS ACTUALIZADAS PARA MAQUINA DE ESTADOS QC ---

    private void CalcularProgresoQCDispersion()
    {
        // Lógica de máquina de estados (Int32)
        // 0 = IDLE | 1 = Aprobado | 2 = Fallido (Rework) | 4 = In Progress
        int estadoQC = Project.Current.GetVariable("Model/ActiveBatchParameters/QC_Dispersion_Result").Value;

        // Solo aportamos el 100% a esta fase si el estado es exactamente 1 (Aprobado)
        float progreso = (estadoQC == 1) ? 100.0f : 0.0f;

        Project.Current.GetVariable("Model/PhaseTracker/Fase_QC_Disp/Progreso_UI").Value = progreso;
    }

    private void CalcularProgresoQCDilucion()
    {
        // Lógica de máquina de estados (Int32)
        // 0 = IDLE | 1 = Aprobado | 2 = Fallido (Rework) | 4 = In Progress
        int estadoQC = Project.Current.GetVariable("Model/ActiveBatchParameters/QC_Dilution_Result").Value;

        // Solo aportamos el 100% a esta fase si el estado es exactamente 1 (Aprobado)
        float progreso = (estadoQC == 1) ? 100.0f : 0.0f;

        Project.Current.GetVariable("Model/PhaseTracker/Fase_QC_Dil/Progreso_UI").Value = progreso;
    }

    private void CalcularProgresoPackaging()
    {
        // Lógica basada puramente en tiempo simulado
        float tiempoTarget = Project.Current.GetVariable("Model/PhaseTracker/Fase_Pack/Timer_PRE").Value;
        float tiempoActual = Project.Current.GetVariable("Model/PhaseTracker/Fase_Pack/Timer_ACC").Value;
        float progreso = 0.0f;

        if (tiempoTarget > 0)
        {
            progreso = (tiempoActual / tiempoTarget) * 100.0f;
            if (progreso > 100.0f) progreso = 100.0f; // Clampear a 100%
        }

        Project.Current.GetVariable("Model/PhaseTracker/Fase_Pack/Progreso_UI").Value = progreso;
    }
}
