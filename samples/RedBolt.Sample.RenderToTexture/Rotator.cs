using RedBolt;

sealed class Rotator : LogicComponent
{
    private Vector3f _rotationSpeed;

    public Rotator()
    {
        SetUpdateEventMask(UpdateEvent.UseUpdate);
    }

    public void SetRotationSpeed(Vector3f speed)
    {
        _rotationSpeed = speed;
    }

    public override void Update(float timeStep)
    {
        Node.Rotate(Quaternion.FromEulerAngles(
            _rotationSpeed.X * timeStep,
            _rotationSpeed.Y * timeStep,
            _rotationSpeed.Z * timeStep));
    }
}
