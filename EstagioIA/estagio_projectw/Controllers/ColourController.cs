using CoreAI.models;
using CoreAI.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using WebApi.Configs;


namespace WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ColourController : ControllerBase
    {
        private readonly GeminiCore _gemini;
        private readonly GeminiSettings _geminiSettings;

        public ColourController(GeminiCore gemini, IOptions<GeminiSettings> geminiSettings)
        {
            _geminiSettings = geminiSettings.Value;
            _gemini = gemini;
        }

        [HttpPost("GetColors")]
        public async Task<IActionResult> Get( IFormFile imageFile, string dropText, string provider = "gemini")
        {
            try
            {
                byte[] imageBytes;
                using (var memoryStream = new MemoryStream())
                {
                    await imageFile.CopyToAsync(memoryStream);
                    imageBytes = memoryStream.ToArray();
                }

                
                string base64Image = Convert.ToBase64String(imageBytes);

                var coloursList = new List<ColoursModel>();

                string promptBase;
                if (dropText?.ToLower() == "geral")
                {
                    promptBase = _geminiSettings.PromptColorGeral;
                }
                else
                {
                    promptBase = _geminiSettings.PromptColorFutebol;
                }

                //promptBase = promptBase.Replace("{TeamName}",teamName);
                var formattedPrompt = _geminiSettings.PromptColor.Replace("{Prompt}", promptBase);
                Console.WriteLine(formattedPrompt);

                
                
                //var formattedPrompt = _geminiSettings.PromptColor.Replace("{TeamName}", teamName);
                var result = await _gemini.GetFacePositionsJsonAsync(_geminiSettings.ApiURL, formattedPrompt, imageBytes);
                var colourModel = JsonConvert.DeserializeObject<ColoursModel>(result);

               



                if (colourModel == null)
                {
                    return BadRequest($"A resposta não pôde ser convertida para ColoursModel .");
                }

                coloursList.Add(colourModel);

                return Ok(coloursList);
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

     
        
    }
}
