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
                using var content = new MultipartFormDataContent();

                using var memoryStream = new MemoryStream();
                await UploadedFile.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                var streamContent = new ByteArrayContent(memoryStream.ToArray());
                streamContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(UploadedFile.ContentType);

                content.Add(streamContent, "file", UploadedFile.FileName);

                var apiUrl = _configuration["ApiSettings:PredictUrl"];
                var response = await client.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
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
                    ErrorMessage = "Erreur lors de l'appel à l'API de prédiction.";
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