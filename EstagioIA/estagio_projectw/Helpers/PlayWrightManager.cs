using Microsoft.Playwright;

namespace WebApi.Helpers
{
    public class PlayWrightManager
    {

        public static async Task<byte[]> GetImage(string html, int width, int height)
        {
            // Inicializa o Playwright e abre uma instância do navegador
            using var playwright = await Playwright.CreateAsync();

            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

            // Cria uma nova página no navegador
            var page = await browser.NewPageAsync();

            

            // Define o conteúdo HTML da página diretamente
            await page.SetContentAsync(html);

            // Screenshot da página completa e retorna o resultado como byte[]
            byte[] screenshotBytes = await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Clip = new Clip
                {
                    Width = width,
                    Height = height,
                    X = 0,
                    Y = 0
                },
                OmitBackground = true,
                Type = ScreenshotType.Png
            });

            return screenshotBytes;
        }
    }
}
