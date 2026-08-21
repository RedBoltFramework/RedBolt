// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

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
        using var application = sample.CreateApplication();
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

internal sealed record SampleEntry(string Id, string Name, Func<RedBolt.Engine.Application> CreateApplication);

internal static class SampleCatalog
{
    internal static readonly SampleEntry[] Entries =
    [
        // <redbolt-generated name="android-sample-entries">
        new("aggregated-input", "Aggregated Input", static () => new global::RedBolt.Sample.AggregatedInput.AggregatedInputApplication()),
        new("animating-scene", "Animating Scene", static () => new global::RedBolt.Sample.AnimatingScene.AnimatingScene()),
        new("billboards", "Billboards", static () => new global::RedBolt.Sample.Billboards.BillboardsApplication()),
        new("camera-operator", "Camera Operator", static () => new global::RedBolt.Sample.CameraOperator.CameraOperatorSampleApplication()),
        new("camera-shake", "Camera Shake", static () => new global::RedBolt.Sample.CameraShake.CameraShakeApplication()),
        new("decals", "Decals", static () => new global::RedBolt.Sample.Decals.DecalsApplication()),
        new("dynamic-geometry", "Dynamic Geometry", static () => new global::RedBolt.Sample.DynamicGeometry.DynamicGeometry()),
        new("hello-gui", "Hello GUI", static () => new global::RedBolt.Sample.HelloGUI.HelloGUI()),
        new("hello-world", "Hello World", static () => new global::RedBolt.Sample.HelloWorld.HelloWorld()),
        new("huge-object-count", "Huge Object Count", static () => new global::RedBolt.Sample.HugeObjectCount.HugeObjectCountApplication()),
        new("input-logger", "Input Logger", static () => new global::RedBolt.Sample.InputLogger.InputLoggerApplication()),
        new("light-animation", "Light Animation", static () => new global::RedBolt.Sample.LightAnimation.LightAnimationApplication()),
        new("material-animation", "Material Animation", static () => new global::RedBolt.Sample.MaterialAnimation.MaterialAnimationApplication()),
        new("multiple-viewports", "Multiple Viewports", static () => new global::RedBolt.Sample.MultipleViewports.MultipleViewports()),
        new("pointer-adapter", "Pointer Adapter", static () => new global::RedBolt.Sample.PointerAdapter.PointerAdapterApplication()),
        new("render-to-texture", "Render to Texture", static () => new global::RedBolt.Sample.RenderToTexture.RenderToTexture()),
        new("skeletal-animation", "Skeletal Animation", static () => new global::RedBolt.Sample.SkeletalAnimation.SkeletalAnimationApplication()),
        new("sound-effects", "Sound Effects", static () => new global::RedBolt.Sample.SoundEffects.SoundEffects()),
        new("sound-synthesis", "Sound Synthesis", static () => new global::RedBolt.Sample.SoundSynthesis.SoundSynthesis()),
        new("static-scene", "Static Scene", static () => new global::RedBolt.Sample.StaticScene.StaticScene()),
        new("texture-formats", "Texture Formats", static () => new global::RedBolt.Sample.TextureFormats.TextureFormatsSample()),
        new("virtual-file-system", "Virtual File System", static () => new global::RedBolt.Sample.VirtualFileSystem.VirtualFileSystemApplication()),
        // </redbolt-generated name="android-sample-entries">
    ];
}
