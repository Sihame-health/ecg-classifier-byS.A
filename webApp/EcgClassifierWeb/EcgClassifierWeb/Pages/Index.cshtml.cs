using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EcgClassifierWeb.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public IndexModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
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
                client.Timeout = TimeSpan.FromMinutes(4);

                var apiUrl = _configuration["ApiSettings:PredictUrl"];

                // On lit le fichier une seule fois en mémoire pour pouvoir le renvoyer à chaque essai
                using var memoryStream = new MemoryStream();
                await UploadedFile.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();

                HttpResponseMessage? response = null;
                const int maxAttempts = 5;

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    // Le contenu ne peut être envoyé qu'une fois : on le recrée à chaque essai
                    using var content = new MultipartFormDataContent();
                    var fileContent = new ByteArrayContent(fileBytes);
                    fileContent.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue(UploadedFile.ContentType);
                    content.Add(fileContent, "file", UploadedFile.FileName);

                    response = await client.PostAsync(apiUrl, content);

                    if (response.IsSuccessStatusCode)
                        break;

                    // 502/503/504 = l'API est en train de se réveiller : on attend puis on réessaie
                    var code = (int)response.StatusCode;
                    bool apiWakingUp = code == 502 || code == 503 || code == 504;

                    if (apiWakingUp && attempt < maxAttempts)
                        await Task.Delay(TimeSpan.FromSeconds(20));
                    else
                        break;
                }

                if (response != null && response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = System.Text.Json.JsonSerializer.Deserialize<PredictionResult>(
                        json,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
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
                    var body = response != null ? await response.Content.ReadAsStringAsync() : "";
                    if (body.Length > 200) body = body.Substring(0, 200);
                    ErrorMessage = $"Erreur API : {(int?)response?.StatusCode} {response?.ReasonPhrase} - {body}";
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