namespace ArenaBackend.Configs
{
    public class PdlCalculationSettings
    {
        public int FactorBase { get; set; } = 50;
        public int FactorNewPlayer { get; set; } = 80;
        public int FactorMax { get; set; } = 140;
        public int MinMatchesStable { get; set; } = 10;
        public int DefaultPdl { get; set; } = 1000;
        
        public Dictionary<int, float> PlacementMultipliers { get; set; } = new Dictionary<int, float>
        {
            [1] = 1.3f,
            [2] = 1.1f,
            [3] = 0.8f,
            [4] = 0.6f,
            [5] = -0.4f,
            [6] = -0.6f,
            [7] = -1.0f,
            [8] = -1.7f
        };
    }
}
