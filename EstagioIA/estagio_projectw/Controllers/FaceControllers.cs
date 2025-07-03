using System.Text.Json;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using CoreAI.Providers;
using CoreAI.models;
using WebApi.Configs;
using Microsoft.Extensions.Options;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FaceControllers : ControllerBase
    {
        private readonly GeminiSettings _geminiSettings;
        private readonly ApiSettings _apiSettings;
        private readonly GeminiCore _gemini;

        public FaceControllers(GeminiCore gemini, IOptions<GeminiSettings> geminiSettings, IOptions<ApiSettings> apiSettings)
        {
            _gemini = gemini;
            _geminiSettings = geminiSettings.Value;
            _apiSettings = apiSettings.Value;
        }

        [HttpPost("GetFaces")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Get(IFormFile imageFile, string provider = "gemini")
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return BadRequest("Nenhuma imagem foi enviada.");
            }

            try
            {
                using var ms = new MemoryStream();
                await imageFile.CopyToAsync(ms);
                byte[] imageBytes = ms.ToArray();


                
                Image image;
               
                try
                {
                    image = Image.FromStream(new MemoryStream(imageBytes));
                   
                }
                catch
                {
                    return BadRequest("Arquivo de imagem inválido ou corrompido.");
                }

                

                string faceJson = await _gemini.GetFacePositionsJsonAsync(_geminiSettings.ApiURL, _geminiSettings.PromptFace, imageBytes);

                var faces = JsonConvert.DeserializeObject<List<FaceModel>>(faceJson);

                if (faces == null || faces.Count == 0)
                {
                    return BadRequest("Nenhuma face detectada na imagem.");
                }

            
                string outputDirectory = _apiSettings.FacesDirectory;
                Directory.CreateDirectory(outputDirectory);

                List<string> croppedImagePaths = new List<string>();

                foreach (var face in faces)
                {
                    try
                    {
                        int newX = face.Xmin;
                        int newY = face.Ymin;
                        int width = face.Xmax - newX;
                        int height = face.Ymax - newY;

                     
                       

                  
                        if (newX < 0) newX = 0;
                        if (newY < 0) newY = 0;

                        if (newX + width > image.Width)
                        {
                            width = image.Width - newX;
                        }
                        if (newY + height > image.Height)
                        {
                            height = image.Height - newY;
                        }

                        if (width <= 0 || height <= 0)
                        {
                            return StatusCode(500, $"coordenadas não válidas após ajuste");
                        }

                        using var cropped = Crop(image, width, height, newX, newY);

                        using var msCropped = new MemoryStream();
                        cropped.Save(msCropped, ImageFormat.Jpeg);
                        string base64 = Convert.ToBase64String(msCropped.ToArray());
                        string dataUrl = $"data:image/jpeg;base64,{base64}";
                        croppedImagePaths.Add(dataUrl);


                     
                    
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, $"Erro ao processar a imagem: {ex.Message}");
                    }
                }

                image.Dispose();

                return Ok(new
                {
                    message = "Caras detectadas com sucesso",
                    faces_cut = croppedImagePaths,
                    datacut = JsonDocument.Parse(faceJson).RootElement
                });

            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao processar a imagem: {ex.Message}");
            }
        }


        // Lógica de recorte da imagem
        private Image Crop(Image image, int width, int height, int x, int y)
        {
            try
            {
                Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                bmp.SetResolution(image.HorizontalResolution, image.VerticalResolution);

                using (Graphics gfx = Graphics.FromImage(bmp))
                {
                    gfx.SmoothingMode = SmoothingMode.AntiAlias;
                    gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    gfx.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    gfx.DrawImage(image, new Rectangle(0, 0, width, height), x, y, width, height, GraphicsUnit.Pixel);
                }

                return bmp;
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao recortar imagem: {ex.Message}");
            }
        }

       
      
    }
}
