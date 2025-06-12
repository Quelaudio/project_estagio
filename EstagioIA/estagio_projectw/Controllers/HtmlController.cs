using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Drawing;
using WebApi.Helpers;
using CoreAI.Providers;
using CoreAI.models;
using WebApi.Configs;
using Microsoft.Extensions.Options;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HtmlController : ControllerBase
    {
        private readonly GeminiCore _gemini;
        private readonly GeminiSettings _geminiSettings;
        private readonly ApiSettings _apiSettings;

        public HtmlController(GeminiCore gemini, IOptions<GeminiSettings> geminiSettings, IOptions<ApiSettings> apiSettings)
        {
            _gemini = gemini;
            _geminiSettings = geminiSettings.Value;
            _apiSettings = apiSettings.Value;
        }



        [HttpPost("GetImage")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> GetImage( IFormFile imageFile, [FromForm] string Prompt, [FromForm] string provider = "gemini")
        {
            string folderPath = _apiSettings.TemplatesDirectory;
            Directory.CreateDirectory(folderPath);

            if (imageFile == null || imageFile.Length == 0)
                return BadRequest("Nenhuma imagem foi enviada.");

            try
            {
                using var ms = new MemoryStream();
                await imageFile.CopyToAsync(ms);
                byte[] imageBytes = ms.ToArray();

                try { Image.FromStream(new MemoryStream(imageBytes)); }
                catch { return BadRequest("Arquivo de imagem inválido ou corrompido."); }

                string formattedPrompt = _geminiSettings.PromptHTML.Replace("{Prompt}", Prompt);
                var htmlresult = await _gemini.GetFacePositionsJsonAsync(_geminiSettings.ApiURL, formattedPrompt, imageBytes);

                string fileName = $"template_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                string filePath = Path.Combine(folderPath, fileName);
                await System.IO.File.WriteAllTextAsync(filePath, htmlresult);

                Console.WriteLine(formattedPrompt);
                Console.WriteLine(htmlresult);
                
                return Ok(htmlresult);
            }
            catch (TaskCanceledException)
            {
                return StatusCode(504, "A requisição demorou muito tempo para responder.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro: {ex.Message}");
            }
        }

        [HttpPost("GenerateTemplateFromZip")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> GenerateTemplateFromZip( string templateName, IFormFile zipFile, string provider = "gemini", bool extractColors = false)  
        {
            try
            {
                if (zipFile == null || zipFile.Length == 0)
                    return BadRequest("O ficheiro ZIP está vazio.");

                var folderPath = _apiSettings.TemplatesDirectory;
                string fileName = $"generated_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                var templatePath = Path.Combine(folderPath, $"{templateName}.html");

                Console.WriteLine($"File Name: {zipFile.FileName}, Size: {zipFile.Length}");

                string htmlTemplate = await System.IO.File.ReadAllTextAsync(templatePath);

                var imageBytesDict = new Dictionary<string, byte[]>();

                using var zipStream = zipFile.OpenReadStream();
                using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    if (entry.Length == 0 || !entry.Name.EndsWith(".png") && !entry.Name.EndsWith(".jpg") && !entry.Name.EndsWith(".jpeg"))
                        continue;

                    var dummyName = Path.GetFileNameWithoutExtension(entry.Name);

                    using var entryStream = entry.Open();
                    using var ms = new MemoryStream();
                    await entryStream.CopyToAsync(ms);
                    imageBytesDict[dummyName] = ms.ToArray();

                    string mimeType = entry.Name.EndsWith(".png") ? "image/png" :
                                      entry.Name.EndsWith(".jpg") || entry.Name.EndsWith(".jpeg") ? "image/jpeg" : "application/octet-stream";

                    string base64 = $"data:{mimeType};base64,{Convert.ToBase64String(ms.ToArray())}";

                    htmlTemplate = htmlTemplate.Replace($"{{{dummyName}}}", base64);
                }

                var imageBytesList = imageBytesDict.Values.ToList();

                if (extractColors && imageBytesList.Count > 0)  
                {
                    for (int i = 0; i < imageBytesList.Count; i++)
                    {
                        var result = await _gemini.SendImagesAsync(_geminiSettings.ApiURL, _geminiSettings.PromptColor_HTML, (imageBytesList[i], "image/png"));

                        if (!string.IsNullOrEmpty(result))
                        {
                            var color = JsonConvert.DeserializeObject<ColoursModel>(result);
                            Console.WriteLine($"Cor {i + 1}: {color.ColourHex}");

                            if (!string.IsNullOrEmpty(color?.ColourHex))
                            {
                                htmlTemplate = htmlTemplate.Replace($"{{cor{i + 1}}}", color.ColourHex);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Não foi possível extrair a cor da imagem {i + 1}.");
                        }
                    }
                }

                string htmlPath = Path.Combine(folderPath, fileName);
                await System.IO.File.WriteAllTextAsync(htmlPath, htmlTemplate);

                byte[] finalImageBytes = await PlayWrightManager.GetImage(htmlTemplate, 1920, 1080);
                if (finalImageBytes == null || finalImageBytes.Length == 0)
                    return StatusCode(500, "Falha ao gerar a imagem do HTML.");

                return File(finalImageBytes, "image/png");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao processar o ficheiro ZIP: {ex.Message}");
            }
        }

    }
}