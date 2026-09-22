using UnityEngine;

namespace Ceffy
{
    /// <summary>
    /// Provides mouse and modifier-key state used by browser display components.
    /// </summary>
    public interface IWebBrowserInput
    {
        Vector2 GetMousePosition();
        bool GetMouseButton(int button);
        bool GetMouseButtonDown(int button);
        bool GetMouseButtonUp(int button);
        Vector2 GetMouseScrollDelta();
        bool GetShift();
        bool GetControl();
        bool GetAlt();
        bool GetRightAlt();
    }
}
