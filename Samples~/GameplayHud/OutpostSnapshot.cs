using System;

namespace Ceffy.Demos.GameplayHud
{
    [Serializable]
    public class OutpostSnapshot
    {
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int GeneratorHealth { get; set; }
        public int MaxGeneratorHealth { get; set; }
        public int Energy { get; set; }
        public int Score { get; set; }
        public int Wave { get; set; }
        public int Enemies { get; set; }
        public int Countdown { get; set; }
        public int Turrets { get; set; }
        public int BuildCost { get; set; }
        public int UpgradeCost { get; set; }
        public int RepairCost { get; set; }
        public int HealCost { get; set; }
        public bool IsPaused { get; set; }
        public bool IsDefeated { get; set; }
        public bool IsWaveActive { get; set; }
        public float HudZoomPercent { get; set; }
        public string Theme { get; set; }
        public int MatchId { get; set; }
        public int CollectedEnergy { get; set; }
    }
}
