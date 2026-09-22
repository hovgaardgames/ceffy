using System;

namespace Ceffy
{
    public enum LogLevel
    {
        Default = 0,
        Verbose = 1,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
        ErrorReport = 5,
        Disable = 99
    }

    public enum MouseButton
    {
        Left = 0,
        Middle = 1,
        Right = 2
    }

    /// <summary>
    /// Modifier and button-state flags attached to browser input events.
    /// </summary>
    [Flags]
    public enum EventFlags
    {
        None = 0,
        CapsLockOn = 1 << 0,
        ShiftDown = 1 << 1,
        ControlDown = 1 << 2,
        AltDown = 1 << 3,
        LeftMouseButton = 1 << 4,
        MiddleMouseButton = 1 << 5,
        RightMouseButton = 1 << 6,
        CommandDown = 1 << 7,
        NumLockOn = 1 << 8,
        IsKeyPad = 1 << 9,
        IsLeft = 1 << 10,
        IsRight = 1 << 11,
        AltGrDown = 1 << 12,
        IsRepeat = 1 << 13,
    }

    public enum KeyEventType
    {
        RawKeyDown = 0,
        KeyDown = 1,
        KeyUp = 2,
        Char = 3
    }

    /// <summary>
    /// Mirrors CEF's cef_drag_operations_mask_t. Used for HTML5 drag &amp; drop.
    /// </summary>
    [Flags]
    public enum DragOperation : uint
    {
        None    = 0,
        Copy    = 1 << 0,
        Link    = 1 << 1,
        Generic = 1 << 2,
        Private = 1 << 3,
        Move    = 1 << 4,
        Delete  = 1 << 5,
        Every   = 0xFFFFFFFF,
    }
}
