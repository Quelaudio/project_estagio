using System.Net.Http.Json;
using System.Text.Json;
using CoreAI.models;
using Newtonsoft.Json;
using System.Text.RegularExpressions;


namespace CoreAI.Providers
{
    public class GeminiCore
    {
        private readonly HttpClient _httpClient;


        public GeminiCore(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(600);
        }

        public async Task<string> GenerateContentAsync(string apiUrl, string prompt)
        {
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(apiUrl, requestBody);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(responseString);

            using var jsonDoc = JsonDocument.Parse(responseString);
            var textContent = jsonDoc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            string cleanedJson = CleanJson(textContent);

            return cleanedJson;
        }

        public async Task<string> GetFacePositionsJsonAsync(string apiUrl, string prompt, byte[] imageBytes)
        {
            string base64Image = Convert.ToBase64String(imageBytes);

            var requestBody = new
            {
                contents = new[]
                {
            new
            {
                parts = new object[]
                {
                    new { text = prompt },
                    new
                    {
                        inlineData = new
                        {
                            mimeType = "image/png",
                            data = base64Image
                        }
                    }
                }
            }
        }
            };

            var response = await _httpClient.PostAsJsonAsync(apiUrl, requestBody);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(responseString);

            using var jsonDoc = JsonDocument.Parse(responseString);

            var content = jsonDoc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            // Se o Gemini devolver markdown ou tags como ```json ... ```
            string cleanedJson = CleanJson(content);

            return cleanedJson;
        }

        public async Task<string> GenerateHtmlTemplateAsync(string apiUrl, string prompt, byte[] imageBytes)
        {
            string base64Image = Convert.ToBase64String(imageBytes);

            var requestBody = new
            {
                contents = new[] {
            new {
                parts = new object[] {
                    new { text = prompt },
                    new {
                        inlineData = new {
                            mimeType = "image/png",
                            data = base64Image
                        }
                    }
                }
            }
        }
            };

            var response = await _httpClient.PostAsJsonAsync(apiUrl, requestBody);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(responseString);

            using var jsonDoc = JsonDocument.Parse(responseString);

            var content = jsonDoc.RootElement
              .GetProperty("candidates")[0]
              .GetProperty("content")
              .GetProperty("parts")[0]
              .GetProperty("text")
              .GetString();


            return CleanJson(content);
        }


        public async Task<string> SendImagesAsync(string apiUrl, string prompt, params (byte[] Data, string MimeType)[] images)
        {
            var parts = new List<object>();

            if (!string.IsNullOrEmpty(prompt))
            {
                parts.Add(new { text = prompt });
            }

            foreach (var image in images)
            {
                parts.Add(new
                {
                    inline_data = new
                    {
                        mime_type = image.MimeType,
                        data = Convert.ToBase64String(image.Data)
                    }
                });
            }

            var requestBody = new
            {
                contents = new[]
                {
            new
            {
                parts = parts.ToArray()
            }
        }
            };

            var response = await _httpClient.PostAsJsonAsync(apiUrl, requestBody);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"API error: {responseString}");

            using var jsonDoc = JsonDocument.Parse(responseString);
            var content = jsonDoc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return CleanJson(content);
        }

        private static string CleanJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            raw = raw.Trim();

            // Remove markdown code fences (```html, ```json, etc.)
            if (raw.StartsWith("```"))
            {
                int firstNewLine = raw.IndexOf('\n');
                if (firstNewLine >= 0)
                {
                    raw = raw.Substring(firstNewLine + 1).Trim();
                }

                if (raw.EndsWith("```"))
                {
                    raw = raw.Substring(0, raw.Length - 3).Trim();
                }
            }

            return raw;
        }

    }
}


