using Ceffy.Bridge;

namespace Ceffy.Demos.GameplayHud
{
    [UnityMethods]
    public interface IOutpostCommands
    {
        OutpostSnapshot GetState();
        string BuildTurret();
        string BuildTurretAt(int index);
        string UpgradeTurret(int index);
        string RepairGenerator();
        string HealPlayer();
        void StartWave();
        void SetPaused(bool isPaused);
        void Restart();
        void SetInputRegions(OutpostInputRegion[] regions);
        void SetHudZoomPercent(float percent);
        void SetTheme(string value);
    }
}
