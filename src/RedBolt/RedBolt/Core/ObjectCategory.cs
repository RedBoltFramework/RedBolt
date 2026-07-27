using RedBolt.Audio;
using RedBolt.Graphics;
using RedBolt.RenderPipeline;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Core;

public static class ObjectCategory
{
    /// <summary>
    /// Categories for built-in components.
    /// </summary>
    public const string Category_Audio = "Component/Audio";
    public const string Category_Geometry = "Component/Geometry";
    public const string Category_IK = "Component/Inverse Kinematics";
    public const string Category_Logic = "Component/Logic";
    public const string Category_Navigation = "Component/Navigation";
    public const string Category_Network = "Component/Network";
    public const string Category_Physics = "Component/Physics";
    public const string Category_Physics2D = "Component/Physics2D";
    public const string Category_RmlUI = "Component/RmlUI";
    public const string Category_Scene = "Component/Scene";
    public const string Category_Subsystem = "Component/Subsystem";
    public const string Category_Urho2D = "Component/Urho2D";

    /// <summary>
    /// 'Plugin' and 'User' categories are displayed separately in Editor.
    /// - 'Component/Plugin/PluginName' is intended for components registered by plugins.
    /// - 'Component/User' is intended for application-specific components.
    /// - Components that don't belong to 'Plugin' and 'User' categories are displayed together with built-in components.
    /// </summary>
    public const string Category_Plugin = "Component/Plugin";
    public const string Category_User = "Component/User";

    /// <summary>
    /// Category for objects derived from RenderPass.
    /// </summary>
    public const string Category_RenderPass = "RenderPass";

    /// <summary>
    /// Category for objects derived from AssetTransformer.
    /// </summary>
    public const string Category_Transformer = "Transformer";

    /// <summary>
    /// Category for objects derived from UIElement.
    /// </summary>
    public const string Category_UI = "UI";
}
