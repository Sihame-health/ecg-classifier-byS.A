```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;

namespace EcgClassifierWeb.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public IndexModel(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [BindProperty]
        public IFormFile? UploadedFile { get; set; }

        public string? PredictionLabel { get; set; }
        public double? Confidence { get; set; }
        public string? Recommendation { get; set; }
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task OnPostAsync()
        {
            if (UploadedFile == null || UploadedFile.Length == 0)
            {
                ErrorMessage = "Veuillez sélectionner une image.";
                return;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();

                // Timeout suffisamment long pour supporter le cold start Render
                client.Timeout = TimeSpan.FromMinutes(4);

                var apiUrl = _configuration["ApiSettings:PredictUrl"];

                if (string.IsNullOrWhiteSpace(apiUrl))
                {
                    ErrorMessage = "URL de l'API non configurée.";
                    return;
                }

                // ============================================================
                // 1. RÉVEIL DE L'API RENDER
                // ============================================================

                // On récupère l'URL de base à partir de l'URL /predict.
                // Exemple :
                // https://ecg-classifier-bys-a.onrender.com/predict
                // devient :
                // https://ecg-classifier-bys-a.onrender.com/
                var baseUrl = apiUrl.TrimEnd('/');

                if (baseUrl.EndsWith("/predict", StringComparison.OrdinalIgnoreCase))
                {
                    baseUrl = baseUrl[..^"/predict".Length];
                }

                baseUrl += "/";

                bool apiReady = false;

                const int wakeAttempts = 6;

                for (int attempt = 1; attempt <= wakeAttempts; attempt++)
                {
                    try
                    {
                        var wakeResponse = await client.GetAsync(baseUrl);

                        if (wakeResponse.IsSuccessStatusCode)
                        {
                            apiReady = true;
                            break;
                        }

                        // 405 est acceptable si l'API répond mais que
                        // la route / n'accepte pas la méthode utilisée.
                        // Dans ton cas tes logs montrent que GET / fonctionne.
                        if (wakeResponse.StatusCode == HttpStatusCode.MethodNotAllowed)
                        {
                            apiReady = true;
                            break;
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // Timeout pendant le réveil : on réessaie.
                    }
                    catch (HttpRequestException)
                    {
                        // API momentanément indisponible : on réessaie.
                    }

                    if (attempt < wakeAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                }

                if (!apiReady)
                {
                    ErrorMessage =
                        "L'API de prédiction met trop de temps à démarrer. " +
                        "Veuillez patienter quelques secondes puis réessayer.";

                    return;
                }

                // ============================================================
                // 2. LECTURE DU FICHIER
                // ============================================================

                // On lit le fichier une seule fois en mémoire afin de pouvoir
                // recréer le MultipartFormDataContent à chaque tentative.
                using var memoryStream = new MemoryStream();

                await UploadedFile.CopyToAsync(memoryStream);

                var fileBytes = memoryStream.ToArray();

                // ============================================================
                // 3. ENVOI DE LA PRÉDICTION
                // ============================================================

                HttpResponseMessage? response = null;

                const int maxAttempts = 4;

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    using var content = new MultipartFormDataContent();

                    var fileContent = new ByteArrayContent(fileBytes);

                    fileContent.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue(
                            string.IsNullOrWhiteSpace(UploadedFile.ContentType)
                                ? "image/jpeg"
                                : UploadedFile.ContentType
                        );

                    content.Add(
                        fileContent,
                        "file",
                        UploadedFile.FileName
                    );

                    try
                    {
                        response = await client.PostAsync(apiUrl, content);
                    }
                    catch (TaskCanceledException)
                    {
                        // Timeout : on considère que l'API est peut-être
                        // encore en train de se réveiller.
                        if (attempt < maxAttempts)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(10));
                            continue;
                        }

                        break;
                    }
                    catch (HttpRequestException)
                    {
                        if (attempt < maxAttempts)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(10));
                            continue;
                        }

                        break;
                    }

                    // Succès
                    if (response.IsSuccessStatusCode)
                        break;

                    // Render peut renvoyer ces codes pendant le réveil.
                    bool apiWakingUp =
                        response.StatusCode == HttpStatusCode.BadGateway ||      // 502
                        response.StatusCode == HttpStatusCode.ServiceUnavailable || // 503
                        response.StatusCode == HttpStatusCode.GatewayTimeout;    // 504

                    if (apiWakingUp && attempt < maxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                    else
                    {
                        break;
                    }
                }

                // ============================================================
                // 4. TRAITEMENT DE LA RÉPONSE
                // ============================================================

                if (response != null && response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();

                    var result =
                        System.Text.Json.JsonSerializer.Deserialize<PredictionResult>(
                            json,
                            new System.Text.Json.JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            }
                        );

                    if (!string.IsNullOrEmpty(result?.Error))
                    {
                        ErrorMessage = result.Error;
                    }
                    else
                    {
                        PredictionLabel = result?.Label;
                        Confidence = result?.Confidence;
                        Recommendation = result?.Recommendation;
                    }
                }
                else
                {
                    var body = response != null
                        ? await response.Content.ReadAsStringAsync()
                        : "";

                    if (body.Length > 300)
                        body = body.Substring(0, 300);

                    ErrorMessage =
                        $"L'API de prédiction est momentanément indisponible " +
                        $"({(int?)response?.StatusCode} {response?.ReasonPhrase}). " +
                        $"Veuillez patienter quelques secondes puis réessayer.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erreur : {ex.Message}";
            }
        }

        public class PredictionResult
        {
            public string? Label { get; set; }
            public double? Confidence { get; set; }
            public double? RawScore { get; set; }
            public string? Recommendation { get; set; }
            public string? Error { get; set; }
        }
    }
}
