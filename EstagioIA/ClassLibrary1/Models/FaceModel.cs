namespace CoreAI.models
{
    public record FaceModel
    {
        public int Xmin { get; set; }
        public int Ymin { get; set; }
        public int Xmax { get; set; }
        public int Ymax { get; set; }
    }
}
