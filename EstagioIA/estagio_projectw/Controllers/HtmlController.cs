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


                string htmlTemplate = await System.IO.File.ReadAllTextAsync(templatePath);
                var imageBytesDict = new Dictionary<string, byte[]>();

                using var zipStream = zipFile.OpenReadStream();
                using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    if (entry.Length == 0 ||
                        (!entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                         !entry.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
                         !entry.Name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var dummyName = Path.GetFileNameWithoutExtension(entry.Name);

                    using var entryStream = entry.Open();
                    using var ms = new MemoryStream();
                    await entryStream.CopyToAsync(ms);
                    var imageBytes = ms.ToArray();
                    imageBytesDict[dummyName] = imageBytes;

                    string mimeType = entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" :
                                      entry.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                      entry.Name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" :
                                      "application/octet-stream";

                    string base64 = $"data:{mimeType};base64,{Convert.ToBase64String(imageBytes)}";
                    htmlTemplate = htmlTemplate.Replace($"{{{dummyName}}}", base64);
                }

                var extractedColors = new List<string>();

                if (extractColors && imageBytesDict.Count > 0)
                {
                    int index = 1;
                    foreach (var img in imageBytesDict.Values)
                    {
                        var result = await _gemini.SendImagesAsync(_geminiSettings.ApiURL,_geminiSettings.PromptColor_HTML,(img, "image/png"));
                        Console.WriteLine(_geminiSettings.PromptColor_HTML);

                        if (!string.IsNullOrEmpty(result))
                        {
                            var color = JsonConvert.DeserializeObject<ColoursModel>(result);
                            Console.WriteLine($"Cor {index}: {color?.ColourHex}");
                            Console.WriteLine(color?.Description);

                            string colorHex = color?.ColourHex ?? "#000000";
                            htmlTemplate = htmlTemplate.Replace($"{{cor{index}}}", colorHex);
                            extractedColors.Add(colorHex);
                        }
                        else
                        {
                            Console.WriteLine($"Não foi possível extrair a cor da imagem {index}.");
                            extractedColors.Add(null);
                        }

                        index++;
                    }
                }
                
                string htmlPath = Path.Combine(folderPath, fileName);
                await System.IO.File.WriteAllTextAsync(htmlPath, htmlTemplate);

                byte[] finalImageBytes = await PlayWrightManager.GetImage(htmlTemplate, 1920, 1080);

                if (finalImageBytes == null || finalImageBytes.Length == 0)
                    return StatusCode(500, "Falha ao gerar a imagem do HTML.");

                
                return Ok(new
                {
                    image = $"data:image/png;base64,{Convert.ToBase64String(finalImageBytes)}",
                    colors = extractedColors,
                    html = htmlTemplate 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao processar o ficheiro ZIP: {ex.Message}");
            }
        }


    }
}