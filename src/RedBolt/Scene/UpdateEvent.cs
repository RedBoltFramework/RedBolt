namespace RedBolt;

[Flags]
public enum UpdateEvent : uint
{
    UseNoEvent = 0x0,
    UseUpdate = 0x1,
    UsePostUpdate = 0x2,
    UseFixedUpdate = 0x4,
    UseFixedPostUpdate = 0x8
}
