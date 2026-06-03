using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FTOptix.Core;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using UAManagedCore;

public class AIHttpClientLogic : BaseNetLogic
{
    private static readonly HttpClient httpClient = new HttpClient();

    [ExportMethod]
    public void SendQuestion()
    {
        try
        {
            var questionVar = Project.Current.Get("Model/AI_Assistant/AI_Question") as IUAVariable;
            var responseVar = Project.Current.Get("Model/AI_Assistant/AI_Response") as IUAVariable;

            if (questionVar == null || responseVar == null)
            {
                Log.Error("AIHttpClientLogic", "No se encontraron AI_Question o AI_Response en Model/AI_Assistant.");
                return;
            }

            string question = questionVar.Value?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(question))
            {
                responseVar.Value = "Escribe una pregunta antes de enviar.";
                return;
            }

            responseVar.Value = "Procesando solicitud...";

            string jsonPayload = JsonSerializer.Serialize(new
            {
                question = question
            });

            var content = new StringContent(
                jsonPayload,
                Encoding.UTF8,
                "application/json"
            );

            string url = "http://127.0.0.1:8000/smart";

            HttpResponseMessage httpResponse = httpClient.PostAsync(url, content).Result;
            string responseContent = httpResponse.Content.ReadAsStringAsync().Result;

            if (!httpResponse.IsSuccessStatusCode)
            {
                responseVar.Value = "Error HTTP: " + httpResponse.StatusCode + "\n" + responseContent;
                Log.Error("AIHttpClientLogic", "Error HTTP: " + httpResponse.StatusCode);
                return;
            }

            using (JsonDocument doc = JsonDocument.Parse(responseContent))
            {
                JsonElement root = doc.RootElement;

                string answer = "";
                string filePath = "";

                if (root.TryGetProperty("answer", out JsonElement answerElement))
                {
                    answer = answerElement.GetString() ?? "";
                }

                if (root.TryGetProperty("file_path", out JsonElement pathElement) && pathElement.ValueKind != JsonValueKind.Null)
                {
                    filePath = pathElement.GetString() ?? "";
                }

                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    responseVar.Value = answer + "\n\nRuta del reporte:\n" + filePath;
                }
                else
                {
                    responseVar.Value = answer;
                }
            }

            Log.Info("AIHttpClientLogic", "Respuesta recibida correctamente.");
        }
        catch (Exception ex)
        {
            var responseVar = Project.Current.Get("Model/AI_Assistant/AI_Response") as IUAVariable;

            if (responseVar != null)
            {
                responseVar.Value = "Error al conectar con la IA:\n" + ex.Message;
            }

            Log.Error("AIHttpClientLogic", ex.Message);
        }
    }
}