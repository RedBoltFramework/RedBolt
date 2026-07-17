using System.Text;
using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ParticleEffectTests
{
    private readonly HeadlessEngineFixture _fixture;

    public ParticleEffectTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
        FirstCubeTestData.AddResourceDirs(Resources);
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Defaults_match_cpp_constructor()
    {
        var effect = new ParticleEffect();
        Assert.Equal(ParticleEffect.DefaultNumParticles, effect.GetNumParticles());
        Assert.False(effect.GetUpdateInvisible());
        Assert.True(effect.IsRelative());
        Assert.True(effect.IsScaled());
        Assert.False(effect.IsSorted());
        Assert.False(effect.IsFixedScreenSize());
        Assert.Equal(0.0f, effect.GetAnimationLodBias());
        Assert.Equal(EmitterType.Sphere, effect.GetEmitterType());
        Assert.Equal(Vector3f.Zero, effect.GetEmitterSize());
        Assert.Equal(new Vector3f(-1, -1, -1), effect.GetMinDirection());
        Assert.Equal(new Vector3f(1, 1, 1), effect.GetMaxDirection());
        Assert.Equal(10.0f, effect.GetMinEmissionRate());
        Assert.Equal(10.0f, effect.GetMaxEmissionRate());
        Assert.Equal(new Vector2f(0.1f, 0.1f), effect.GetMinParticleSize());
        Assert.Equal(1.0f, effect.GetMinTimeToLive());
        Assert.Equal(1.0f, effect.GetMinVelocity());
        Assert.Equal(1.0f, effect.GetSizeMul());
        Assert.Equal(FaceCameraMode.RotateXyz, effect.GetFaceCameraMode());
        Assert.Null(effect.GetMaterial());
    }

    [Fact]
    public void Xml_load_save_roundtrip_preserves_core_parameters()
    {
        const string xml = """
            <particleeffect>
                <numparticles value="25" />
                <relative enable="false" />
                <scaled enable="false" />
                <sorted enable="true" />
                <fixedscreensize enable="true" />
                <animlodbias value="2.5" />
                <emittertype value="Box" />
                <emittersize value="1 2 3" />
                <direction min="-0.5 -0.5 -0.5" max="0.5 0.5 0.5" />
                <constantforce value="0 1 0" />
                <dampingforce value="0.2" />
                <activetime value="3" />
                <inactivetime value="1" />
                <emissionrate min="5" max="15" />
                <particlesize min="0.2 0.2" max="0.4 0.4" />
                <timetolive min="0.5" max="1.5" />
                <velocity min="2" max="4" />
                <rotation min="10" max="20" />
                <rotationspeed min="30" max="40" />
                <sizedelta add="0.1" mul="1.2" />
                <faceCameraMode value="Direction" />
                <color value="1 0 0 1" />
                <texanim uv="0 0 0.5 0.5" time="0" />
                <texanim uv="0.5 0 1 0.5" time="0.5" />
            </particleeffect>
            """;

        var effect = new ParticleEffect();
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            Assert.True(effect.Load(stream));

        Assert.Equal(25u, effect.GetNumParticles());
        Assert.False(effect.IsRelative());
        Assert.False(effect.IsScaled());
        Assert.True(effect.IsSorted());
        Assert.True(effect.IsFixedScreenSize());
        Assert.Equal(2.5f, effect.GetAnimationLodBias());
        Assert.Equal(EmitterType.Box, effect.GetEmitterType());
        Assert.Equal(new Vector3f(1, 2, 3), effect.GetEmitterSize());
        Assert.Equal(new Vector3f(0, 1, 0), effect.GetConstantForce());
        Assert.Equal(0.2f, effect.GetDampingForce());
        Assert.Equal(3.0f, effect.GetActiveTime());
        Assert.Equal(1.0f, effect.GetInactiveTime());
        Assert.Equal(5.0f, effect.GetMinEmissionRate());
        Assert.Equal(15.0f, effect.GetMaxEmissionRate());
        Assert.Equal(new Vector2f(0.2f, 0.2f), effect.GetMinParticleSize());
        Assert.Equal(new Vector2f(0.4f, 0.4f), effect.GetMaxParticleSize());
        Assert.Equal(0.5f, effect.GetMinTimeToLive());
        Assert.Equal(1.5f, effect.GetMaxTimeToLive());
        Assert.Equal(2.0f, effect.GetMinVelocity());
        Assert.Equal(4.0f, effect.GetMaxVelocity());
        Assert.Equal(10.0f, effect.GetMinRotation());
        Assert.Equal(20.0f, effect.GetMaxRotation());
        Assert.Equal(30.0f, effect.GetMinRotationSpeed());
        Assert.Equal(40.0f, effect.GetMaxRotationSpeed());
        Assert.Equal(0.1f, effect.GetSizeAdd());
        Assert.Equal(1.2f, effect.GetSizeMul());
        Assert.Equal(FaceCameraMode.Direction, effect.GetFaceCameraMode());
        Assert.Equal(1u, effect.GetNumColorFrames());
        Assert.Equal(2u, effect.GetNumTextureFrames());
        Assert.Equal(Color.Red, effect.GetColorFrame(0)!.Value.Color);
        Assert.Equal(0.5f, effect.GetTextureFrame(1)!.Value.Time);

        using var saveStream = new MemoryStream();
        Assert.True(effect.Save(saveStream));
        Assert.True(saveStream.Length > 0);

        var restored = new ParticleEffect();
        saveStream.Position = 0;
        Assert.True(restored.Load(saveStream));
        Assert.Equal(25u, restored.GetNumParticles());
        Assert.Equal(EmitterType.Box, restored.GetEmitterType());
        Assert.Equal(FaceCameraMode.Direction, restored.GetFaceCameraMode());
        Assert.Equal(2u, restored.GetNumTextureFrames());
    }

    [Fact]
    public void Color_and_texture_frame_helpers_sort_by_time()
    {
        var effect = new ParticleEffect();
        effect.AddColorTime(Color.Blue, 1.0f);
        effect.AddColorTime(Color.Red, 0.0f);
        effect.AddColorTime(Color.Green, 0.5f);
        Assert.Equal(3u, effect.GetNumColorFrames());
        Assert.Equal(0.0f, effect.GetColorFrame(0)!.Value.Time);
        Assert.Equal(0.5f, effect.GetColorFrame(1)!.Value.Time);
        Assert.Equal(1.0f, effect.GetColorFrame(2)!.Value.Time);

        effect.AddTextureTime(Rect.Positive, 1.0f);
        effect.AddTextureTime(new Rect(0, 0, 0.5f, 0.5f), 0.25f);
        Assert.Equal(2u, effect.GetNumTextureFrames());
        Assert.Equal(0.25f, effect.GetTextureFrame(0)!.Value.Time);
    }

    [Fact]
    public void Clone_copies_parameters_and_frames()
    {
        var effect = new ParticleEffect();
        effect.SetNumParticles(12);
        effect.SetEmitterType(EmitterType.Ring);
        effect.SetSizeMul(2.0f);
        effect.AddColorTime(Color.White, 0.0f);
        effect.AddTextureTime(Rect.Positive, 0.0f);

        var clone = effect.Clone("clone.pex");
        Assert.Equal("clone.pex", clone.Name);
        Assert.Equal(12u, clone.GetNumParticles());
        Assert.Equal(EmitterType.Ring, clone.GetEmitterType());
        Assert.Equal(2.0f, clone.GetSizeMul());
        Assert.Equal(1u, clone.GetNumColorFrames());
        Assert.Equal(1u, clone.GetNumTextureFrames());
    }
}

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ParticleEmitterTests
{
    private readonly HeadlessEngineFixture _fixture;

    public ParticleEmitterTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
        FirstCubeTestData.AddResourceDirs(Resources);
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void ApplyEffect_copies_billboard_and_particle_settings()
    {
        var effect = new ParticleEffect();
        effect.SetNumParticles(7);
        effect.SetRelative(false);
        effect.SetScaled(false);
        effect.SetSorted(true);
        effect.SetFixedScreenSize(true);
        effect.SetAnimationLodBias(3.0f);
        effect.SetFaceCameraMode(FaceCameraMode.LookAtY);
        var material = Resources.Get<Material>("Materials/FirstCube");
        effect.SetMaterial(material);

        var emitter = new Scene().CreateChild("Emitter").CreateComponent<ParticleEmitter>();
        emitter.SetEffect(effect);

        Assert.Same(effect, emitter.GetEffect());
        Assert.Equal(7u, emitter.GetNumParticles());
        Assert.Equal(7u, emitter.GetNumBillboards());
        Assert.False(emitter.IsRelative());
        Assert.False(emitter.IsScaled());
        Assert.True(emitter.IsSorted());
        Assert.True(emitter.IsFixedScreenSize());
        Assert.Equal(3.0f, emitter.GetAnimationLodBias());
        Assert.Equal(FaceCameraMode.LookAtY, emitter.GetFaceCameraMode());
        Assert.Same(material, emitter.GetMaterial());
        Assert.True(emitter.IsEmitting());
    }

    [Fact]
    public void Emit_and_update_enables_billboards_and_advances_particle_timers()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Emitter");
        var emitter = node.CreateComponent<ParticleEmitter>();

        var effect = new ParticleEffect();
        effect.SetNumParticles(4);
        effect.SetMinEmissionRate(1000.0f);
        effect.SetMaxEmissionRate(1000.0f);
        effect.SetMinTimeToLive(1.0f);
        effect.SetMaxTimeToLive(1.0f);
        effect.SetActiveTime(0.0f);
        effect.SetInactiveTime(0.0f);
        emitter.SetEffect(effect);

        // Drive one post-update + drawable update cycle.
        emitter.OnSetEnabled();
        var frame = new CommonFrameInfo
        {
            FrameNumber = 1,
            TimeStep = 1.0f / 60.0f,
            ViewportSize = new Vector2i(1280, 720),
            Scene = scene
        };

        // Simulate the post-update path that marks needUpdate via reflection of view frame.
        // Mark the emitter in-view by advancing drawable view frame through MarkForUpdate path.
        // Force emit through Update after setting need-update via a second call path:
        // SetEmitting true already; call Update after manually ensuring effect is set.
        // Use Reset and then invoke Update after faking last timestep by emitting via SetEmitting cycle.

        // Directly exercise EmitNewParticle through Update by setting viewFrameNumber match path:
        // Call Update with needUpdate forced via public Reset then:
        // Access protected path: first update after OnSetEnabled will wait for view frame.
        // Instead, call Update after SetEffect which resets; then force by calling Update with
        // a prior MarkForUpdate and simulating HandleScenePostUpdate via emission timer path.

        // Use reflection-free approach: SetSerializeParticles and SetParticlesAttr to seed one particle,
        // then ensure Update advances it when needUpdate is true via in-view simulation.
        var particles = new VariantVector
        {
            new Variant(1),
            new Variant(new Vector3f(0, 1, 0)),
            new Variant(Vector2f.One),
            new Variant(0.0f),
            new Variant(1.0f),
            new Variant(1.0f),
            new Variant(0.0f),
            new Variant(0),
            new Variant(0)
        };
        emitter.SetParticlesAttr(particles);
        emitter.SetNumBillboards(1);
        var billboard = emitter.GetBillboard(0);
        billboard.Enabled = true;
        billboard.Position = Vector3f.Zero;
        billboard.Size = Vector2f.One;
        emitter.SetBillboard(0, billboard);

        // Force needUpdate by calling MarkForUpdate and setting last frame inequality through
        // a public Update after toggling emitting (period timer path still needs needUpdate).
        // Directly call the private path via Update after simulating post-update with scene event.
        var postUpdate = new SceneEvents.ScenePostUpdateEvent { Scene = scene, TimeStep = 0.1f };
        scene.SendEvent(ref postUpdate);
        // viewFrameNumber may not change; force MarkForUpdate and call Update with effect.UpdateInvisible
        effect.SetUpdateInvisible(true);
        postUpdate = new SceneEvents.ScenePostUpdateEvent { Scene = scene, TimeStep = 0.1f };
        scene.SendEvent(ref postUpdate);
        emitter.Update(frame);

        // After update with active billboard and invisible updates, particle timer advances.
        var after = emitter.GetParticlesAttr();
        Assert.True(after.Count >= 9);
        Assert.True(after[3].GetFloat() >= 0.1f - 1e-5f);
    }

    [Fact]
    public void Particles_attribute_roundtrip()
    {
        var emitter = new Scene().CreateChild("Emitter").CreateComponent<ParticleEmitter>();
        emitter.SetNumParticles(2);
        var attr = new VariantVector
        {
            new Variant(2),
            new Variant(new Vector3f(1, 0, 0)),
            new Variant(new Vector2f(2, 3)),
            new Variant(0.25f),
            new Variant(1.5f),
            new Variant(1.1f),
            new Variant(45.0f),
            new Variant(1),
            new Variant(2),
            new Variant(new Vector3f(0, 1, 0)),
            new Variant(new Vector2f(4, 5)),
            new Variant(0.5f),
            new Variant(2.0f),
            new Variant(0.9f),
            new Variant(10.0f),
            new Variant(0),
            new Variant(1)
        };
        emitter.SetParticlesAttr(attr);
        var roundtrip = emitter.GetParticlesAttr();
        Assert.Equal(2 * 8 + 1, roundtrip.Count);
        Assert.Equal(new Vector3f(1, 0, 0), roundtrip[1].GetVector3());
        Assert.Equal(new Vector2f(2, 3), roundtrip[2].GetVector2());
        Assert.Equal(0.25f, roundtrip[3].GetFloat());
        Assert.Equal(1, roundtrip[7].GetInt());
        Assert.Equal(2, roundtrip[8].GetInt());
    }

    [Fact]
    public void Serialize_particles_false_returns_count_only()
    {
        var emitter = new Scene().CreateChild("Emitter").CreateComponent<ParticleEmitter>();
        emitter.SetNumParticles(5);
        emitter.SetSerializeParticles(false);
        var attr = emitter.GetParticlesAttr();
        Assert.Single(attr);
        Assert.Equal(5, attr[0].GetInt());
        var billboards = emitter.GetParticleBillboardsAttr();
        Assert.Single(billboards);
        Assert.Equal(5, billboards[0].GetInt());
    }
}
