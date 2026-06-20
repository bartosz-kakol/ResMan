namespace ResMan.Services
{
    public sealed class MonitorInfo
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public int RefreshRate { get; init; }  // Scaling as percentage (e.g., 100, 125)
        public int ScalingPercentage { get; init; }
    }
}
