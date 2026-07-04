namespace RedBolt;

public sealed class ShakeComponent : LogicComponent
{
    public const float DefaultTimeScale = 16.0f;

    private readonly PerlinNoise perlinNoise = new(RandomEngine.GetDefaultEngine());
    private float time;
    private float trauma;
    private float traumaPower = 2.0f;
    private float traumaFalloff = 1.0f;
    private float timeScale = DefaultTimeScale;
    private Vector3 shiftRange = Vector3.Zero;
    private Vector3 rotationRange = Vector3.Zero;

    public ShakeComponent()
    {
        SetUpdateEventMask(UpdateEvent.UseNoEvent);
        RegisterAttribute("Trauma", typeof(float), self => ((ShakeComponent)self).GetTrauma(), (self, value) => ((ShakeComponent)self).SetTrauma(value is float typed ? typed : 0.0f), 0.0f);
        RegisterAttribute("Trauma Power", typeof(float), self => ((ShakeComponent)self).GetTraumaPower(), (self, value) => ((ShakeComponent)self).SetTraumaPower(value is float typed ? typed : 2.0f), 2.0f);
        RegisterAttribute("Trauma Falloff", typeof(float), self => ((ShakeComponent)self).GetTraumaFalloff(), (self, value) => ((ShakeComponent)self).SetTraumaFalloff(value is float typed ? typed : 1.0f), 1.0f);
        RegisterAttribute("Time Scale", typeof(float), self => ((ShakeComponent)self).GetTimeScale(), (self, value) => ((ShakeComponent)self).SetTimeScale(value is float typed ? typed : DefaultTimeScale), DefaultTimeScale);
        RegisterAttribute("Shift Range", typeof(Vector3), self => ((ShakeComponent)self).GetShiftRange(), (self, value) => ((ShakeComponent)self).SetShiftRange(value is Vector3 typed ? typed : Vector3.Zero), Vector3.Zero);
        RegisterAttribute("Rotation Range", typeof(Vector3), self => ((ShakeComponent)self).GetRotationRange(), (self, value) => ((ShakeComponent)self).SetRotationRange(value is Vector3 typed ? typed : Vector3.Zero), Vector3.Zero);
    }

    public void SetTimeScale(float value)
    {
        timeScale = value;
    }

    public float GetTimeScale() => timeScale;

    public void AddTrauma(float value)
    {
        SetTrauma(trauma + value);
    }

    public void SetTrauma(float value)
    {
        trauma = Math.Max(value, 0.0f);
        if (trauma > 0.0f && (GetUpdateEventMask() & UpdateEvent.UseUpdate) == 0)
            SetUpdateEventMask(UpdateEvent.UseUpdate);
    }

    public float GetTrauma() => trauma;

    public void SetTraumaPower(float value)
    {
        traumaPower = value;
    }

    public float GetTraumaPower() => traumaPower;

    public void SetTraumaFalloff(float value)
    {
        traumaFalloff = value;
    }

    public float GetTraumaFalloff() => traumaFalloff;

    public void SetShiftRange(Vector3 value)
    {
        shiftRange = value;
    }

    public Vector3 GetShiftRange() => shiftRange;

    public void SetRotationRange(Vector3 value)
    {
        rotationRange = value;
    }

    public Vector3 GetRotationRange() => rotationRange;

    public override void Update(float timeStep)
    {
        if (!IsAttached)
            return;

        time += timeStep * timeScale;
        trauma = Math.Max(0.0f, trauma - traumaFalloff * timeStep);
        if (MathDefs.ApproximatelyEquivalent(trauma, 0.0f))
        {
            Node.SetPosition(Vector3.Zero);
            Node.SetRotation(Quaternion.Identity);
            SetUpdateEventMask(UpdateEvent.UseNoEvent);
            return;
        }

        var scale = MathDefs.Pow(trauma, traumaPower);
        var offset = Vector3.Zero;
        if (shiftRange != Vector3.Zero)
        {
            offset = (new Vector3(perlinNoise.Get(time), perlinNoise.Get(time + 1.0f), perlinNoise.Get(time + 2.0f)) * 2.0f - Vector3.One)
                * shiftRange * scale;
        }
        Node.SetPosition(offset);

        var rotation = Vector3.Zero;
        if (rotationRange != Vector3.Zero)
        {
            rotation = (new Vector3(perlinNoise.Get(time + 3.0f), perlinNoise.Get(time + 4.0f), perlinNoise.Get(time + 5.0f)) * 2.0f - Vector3.One)
                * rotationRange * scale;
        }
        Node.SetRotation(new Quaternion(rotation));
    }
}
