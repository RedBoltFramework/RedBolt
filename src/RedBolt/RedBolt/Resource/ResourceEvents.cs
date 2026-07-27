using RedBolt.Container;
namespace RedBolt.Resource;

public static class ResourceEvents
{
    /// <summary>Resource reloading started.</summary>
    public struct ReloadStarted
    {
    }

    /// <summary>Resource reloading finished successfully.</summary>
    public struct ReloadFinished
    {
    }

    /// <summary>Resource reloading failed.</summary>
    public struct ReloadFailed
    {
    }

    /// <summary>Tracked file changed in the resource directories.</summary>
    public struct FileChanged
    {
        public string FileName;
        public string ResourceName;
    }

    /// <summary>Resource loading failed.</summary>
    public struct LoadFailed
    {
        public string ResourceName;
    }

    /// <summary>Resource not found.</summary>
    public struct ResourceNotFound
    {
        public string ResourceName;
    }

    /// <summary>Unknown resource type.</summary>
    public struct UnknownResourceType
    {
        public StringHash ResourceType;
    }

    /// <summary>Resource background loading finished.</summary>
    public struct ResourceBackgroundLoaded
    {
        public string ResourceName;
        public bool Success;
        public Resource? Resource;
    }

    /// <summary>Language changed.</summary>
    public struct ChangeLanguage
    {
    }
}
