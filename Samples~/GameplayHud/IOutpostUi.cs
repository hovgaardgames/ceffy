using Ceffy.Bridge;

namespace Ceffy.Demos.GameplayHud
{
    [UiMethods]
    public interface IOutpostUi
    {
        void UpdateState(OutpostSnapshot state);
        void ShowNotification(string message);
    }
}
