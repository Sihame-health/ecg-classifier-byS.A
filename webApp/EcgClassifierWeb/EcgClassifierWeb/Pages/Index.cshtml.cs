using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EcgClassifierWeb.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public IndexModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
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
                using var stream = UploadedFile.OpenReadStream();
                using var streamContent = new StreamContent(stream);
                streamContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(UploadedFile.ContentType);

                content.Add(streamContent, "file", UploadedFile.FileName);

                var response = await client.PostAsync("http://127.0.0.1:8000/predict", content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = System.Text.Json.JsonSerializer.Deserialize<PredictionResult>(
                        json,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    PredictionLabel = result?.Label;
                    Confidence = result?.Confidence;
                    Recommendation = result?.Recommendation;
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
        }
    }
}