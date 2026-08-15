namespace RedBolt.Sample.Android;

using System;
using System.Linq;

using global::Android.App;
using global::Android.Content;
using global::Android.Content.PM;
using global::Android.OS;
using global::Android.Widget;

using RedBolt.IO;

[Activity(Label = "RedBolt samples", MainLauncher = true, Exported = true)]
public sealed class SamplePickerActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var requestedSample = Intent?.GetStringExtra(SampleActivity.SampleExtra);
        if (requestedSample is not null)
        {
            Launch(requestedSample);
            return;
        }

        var list = new ListView(this)
        {
            Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleListItem1,
                SampleCatalog.Entries.Select(static entry => entry.Name).ToArray())
        };
        list.ItemClick += (_, eventArgs) => Launch(SampleCatalog.Entries[eventArgs.Position].Id);
        SetContentView(list);
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (Intent?.GetStringExtra(SampleActivity.SampleExtra) is not null)
            Finish();
    }

    private void Launch(string sampleId)
    {
        var intent = new Intent(this, typeof(SampleActivity));
        intent.PutExtra(SampleActivity.SampleExtra, sampleId);
        StartActivity(intent);
    }
}

[Activity(Label = "RedBolt samples", Exported = false,
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.KeyboardHidden | ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class SampleActivity : Org.Libsdl.App.SDLActivity
{
    internal const string SampleExtra = "sample";
    internal const string ScreenshotExtra = "screenshot";

    protected override void RunMain()
    {
        var sampleId = Intent?.GetStringExtra(SampleExtra)
            ?? throw new InvalidOperationException("A RedBolt sample id is required.");
        var sample = SampleCatalog.Entries.FirstOrDefault(entry => entry.Id == sampleId)
            ?? throw new InvalidOperationException($"Unknown RedBolt sample: {sampleId}");

        global::Android.Util.Log.Info("RedBolt", $"Starting sample {sample.Id}");
        VerifyPackagedAssets();
        var applicationType = typeof(SampleActivity).Assembly.GetType(sample.ApplicationType, true)!;
        using var application = (RedBolt.Engine.Application?)Activator.CreateInstance(applicationType, true)
            ?? throw new InvalidOperationException($"Cannot create RedBolt sample application: {sample.ApplicationType}");

        try
        {
            var exitCode = application.Run([]);
            global::Android.Util.Log.Info("RedBolt", $"Sample {sample.Id} exited with code {exitCode}");
        }
        catch (Exception exception)
        {
            global::Android.Util.Log.Error("RedBolt", exception.ToString());
        }
    }

    private static void VerifyPackagedAssets()
    {
        using var assets = new MountedDirectory("/apk/CoreData");
        using var plane = assets.OpenFile(new FileIdentifier("Models/Plane.mdl"), RedBolt.IO.FileMode.Read)
            ?? throw new InvalidOperationException("Packaged CoreData assets are unavailable.");
        global::Android.Util.Log.Info("RedBolt", $"Verified packaged CoreData asset ({plane.Size} bytes)");
    }
}

internal sealed record SampleEntry(string Id, string Name, string ApplicationType);

internal static class SampleCatalog
{
    internal static readonly SampleEntry[] Entries =
    [
        new("advanced-ui", "Advanced UI", "RedBolt.Sample.AdvancedUI.AdvancedUIApplication"),
        new("aggregated-input", "Aggregated Input", "RedBolt.Sample.AggregatedInput.AggregatedInputApplication"),
        new("actions", "Actions", "RedBolt.Sample.Actions.ActionsApplication"),
        new("animating-scene", "Animating Scene", "RedBolt.Sample.AnimatingScene.AnimatingScene"),
        new("baked-lighting", "Baked Lighting", "RedBolt.Sample.BakedLighting.BakedLighting"),
        new("billboards", "Billboards", "RedBolt.Sample.Billboards.BillboardsApplication"),
        new("camera-operator", "Camera Operator", "RedBolt.Sample.CameraOperator.CameraOperatorSampleApplication"),
        new("camera-shake", "Camera Shake", "RedBolt.Sample.CameraShake.CameraShakeApplication"),
        new("chat", "Chat", "RedBolt.Sample.Chat.Chat"),
        new("crowd-navigation", "Crowd Navigation", "RedBolt.Sample.CrowdNavigation.CrowdNavigationSample"),
        new("decals", "Decals", "RedBolt.Sample.Decals.DecalsApplication"),
        new("dynamic-geometry", "Dynamic Geometry", "RedBolt.Sample.DynamicGeometry.DynamicGeometry"),
        new("hello-gui", "Hello GUI", "RedBolt.Sample.HelloGUI.HelloGUI"),
        new("hello-rmlui", "Hello RmlUI", "RedBolt.Sample.HelloRmlUI.HelloRmlUI"),
        new("hello-world", "Hello World", "RedBolt.Sample.HelloWorld.HelloWorld"),
        new("huge-object-count", "Huge Object Count", "RedBolt.Sample.HugeObjectCount.HugeObjectCountApplication"),
        new("input-logger", "Input Logger", "RedBolt.Sample.InputLogger.InputLoggerApplication"),
        new("inverse-kinematics", "Inverse Kinematics", "RedBolt.Sample.InverseKinematics.InverseKinematicsApplication"),
        new("light-animation", "Light Animation", "RedBolt.Sample.LightAnimation.LightAnimationApplication"),
        new("material-animation", "Material Animation", "RedBolt.Sample.MaterialAnimation.MaterialAnimationApplication"),
        new("multiple-viewports", "Multiple Viewports", "RedBolt.Sample.MultipleViewports.MultipleViewports"),
        new("navigation", "Navigation", "RedBolt.Sample.Navigation.NavigationSample"),
        new("particles", "Particles", "RedBolt.Sample.Particles.ParticlesApplication"),
        new("pointer-adapter", "Pointer Adapter", "RedBolt.Sample.PointerAdapter.PointerAdapterApplication"),
        new("render-to-texture", "Render to Texture", "RedBolt.Sample.RenderToTexture.RenderToTexture"),
        new("skeletal-animation", "Skeletal Animation", "RedBolt.Sample.SkeletalAnimation.SkeletalAnimationApplication"),
        new("sound-effects", "Sound Effects", "RedBolt.Sample.SoundEffects.SoundEffects"),
        new("sound-synthesis", "Sound Synthesis", "RedBolt.Sample.SoundSynthesis.SoundSynthesis"),
        new("static-scene", "Static Scene", "RedBolt.Sample.StaticScene.StaticScene"),
        new("texture-formats", "Texture Formats", "RedBolt.Sample.TextureFormats.TextureFormatsSample"),
        new("virtual-file-system", "Virtual File System", "RedBolt.Sample.VirtualFileSystem.VirtualFileSystemApplication")
    ];
}
