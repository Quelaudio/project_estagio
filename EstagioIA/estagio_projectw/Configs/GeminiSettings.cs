namespace WebApi.Configs
{
    public record GeminiSettings
    {
        public string ApiURL { get; set; }

  

        public string PromptColor { get; set; }

        public string PromptFace { get; set; }

        public string PromptHTML { get; set; }

        public string PromptColor_HTML { get; set; }
    }
}
