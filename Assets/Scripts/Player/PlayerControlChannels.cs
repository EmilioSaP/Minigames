
using System;

[Flags]
public enum PlayerControlChannels
{
    None = 0,

    Movement = 1 << 0,
    Look = 1 << 1,
    Interaction = 1 << 2,
    CameraToggle = 1 << 3,

    All = Movement | Look | Interaction | CameraToggle
}
