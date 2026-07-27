using RedBolt.Core;
using RedBolt.IK;
using RedBolt.Math;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class IKSolverTests
{
    private readonly HeadlessEngineFixture _fixture;

    public IKSolverTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Constructors_preserve_cpp_defaults()
    {
        var solver = new IKSolver();
        var identity = new IKIdentitySolver();
        var rotateTo = new IKRotateTo();
        var limb = new IKLimbSolver();
        var leg = new IKLegSolver();
        var arm = new IKArmSolver();
        var head = new IKHeadSolver();
        var spine = new IKSpineSolver();
        var stickTargets = new IKStickTargets();

        Assert.False(solver.SolveWhenPaused);
        Assert.True(solver.SolveFromOriginal);
        Assert.False(solver.ContinuousRotation);
        Assert.Equal(Quaternion.ZERO, identity.RotationOffset);
        Assert.Equal(1.0f, rotateTo.Weight);
        Assert.Equal(1.0f, limb.PositionWeight);
        Assert.Equal(0.0f, limb.RotationWeight);
        Assert.Equal(1.0f, limb.BendWeight);
        Assert.Equal(180.0f, limb.MaxAngle);
        Assert.Equal(-1.0f, leg.HeelGroundOffset);
        Assert.Equal(1.0f, leg.HeelWeight);
        Assert.Equal(0.0f, arm.RotationWeight);
        Assert.Equal(1.0f, head.DirectionWeight);
        Assert.Equal(0.5f, head.NeckWeight);
        Assert.Equal(90.0f, spine.MaxAngle);
        Assert.True(stickTargets.IsActive);
        Assert.True(stickTargets.IsPositionSticky);
        Assert.True(stickTargets.IsRotationSticky);
        Assert.Equal(IKStickTargets.DefaultRecoverTime, stickTargets.RecoverTime);
    }

    [Fact]
    public void Registration_registers_the_complete_cpp_ik_library()
    {
        RedBolt.IK.IK.RegisterIKLibrary(_fixture.Context);

        var solverReflection = _fixture.Context.GetReflection<IKSolver>();
        var solverComponentReflection = _fixture.Context.GetReflection<IKSolverComponent>();
        var identityReflection = _fixture.Context.GetReflection<IKIdentitySolver>();
        var rotateToReflection = _fixture.Context.GetReflection<IKRotateTo>();
        var legReflection = _fixture.Context.GetReflection<IKLegSolver>();
        var limbReflection = _fixture.Context.GetReflection<IKLimbSolver>();
        var spineReflection = _fixture.Context.GetReflection<IKSpineSolver>();
        var armReflection = _fixture.Context.GetReflection<IKArmSolver>();
        var chainReflection = _fixture.Context.GetReflection<IKChainSolver>();
        var headReflection = _fixture.Context.GetReflection<IKHeadSolver>();
        var stickTargetsReflection = _fixture.Context.GetReflection<IKStickTargets>();

        Assert.NotNull(solverReflection);
        Assert.NotNull(solverComponentReflection);
        Assert.NotNull(identityReflection);
        Assert.NotNull(rotateToReflection);
        Assert.NotNull(legReflection);
        Assert.NotNull(limbReflection);
        Assert.NotNull(spineReflection);
        Assert.NotNull(armReflection);
        Assert.NotNull(chainReflection);
        Assert.NotNull(headReflection);
        Assert.NotNull(stickTargetsReflection);
        Assert.Equal(ObjectCategory.Category_IK, solverReflection.Category);
        Assert.NotNull(solverReflection.GetAttribute("Continuous Rotation"));
        Assert.NotNull(identityReflection.GetAttribute("Rotation Offset"));
        Assert.NotNull(rotateToReflection.GetAttribute("Weight"));
        Assert.NotNull(legReflection.GetAttribute("Heel Ground Offset"));
        Assert.NotNull(limbReflection.GetAttribute("Bend Direction"));
        Assert.NotNull(spineReflection.GetAttribute("Twist Rotation Offset"));
        Assert.NotNull(armReflection.GetAttribute("Hand Bone Name"));
        Assert.NotNull(chainReflection.GetAttribute("Bone Names"));
        Assert.NotNull(headReflection.GetAttribute("Look At Target Name"));
        Assert.NotNull(stickTargetsReflection.GetAttribute("Base World Velocity"));
    }

    [Fact]
    public void Limb_solver_reaches_target_and_sends_cpp_events()
    {
        using var scene = new SceneType();
        var rig = scene.CreateChild("Rig");
        var host = rig.CreateComponent<IKSolver>();
        var first = rig.CreateChild("First");
        var second = first.CreateChild("Second");
        second.SetPosition(Vector3f.Right);
        var third = second.CreateChild("Third");
        third.SetPosition(Vector3f.Right);
        var target = rig.CreateChild("Target");
        target.SetPosition(new Vector3f(1.0f, 1.0f, 0.0f));
        var limb = rig.CreateComponent<IKLimbSolver>();
        limb.FirstBoneName = "First";
        limb.SecondBoneName = "Second";
        limb.ThirdBoneName = "Third";
        limb.TargetName = "Target";

        var preSolveCount = 0;
        var postSolveCount = 0;
        host.SubscribeToEvent<IKEvents.IKPreSolveEvent>(rig, (ref IKEvents.IKPreSolveEvent eventData) =>
        {
            Assert.Same(rig, eventData.Node);
            Assert.Same(host, eventData.IKSolver);
            ++preSolveCount;
        });
        host.SubscribeToEvent<IKEvents.IKPostSolveEvent>(rig, (ref IKEvents.IKPostSolveEvent eventData) =>
        {
            Assert.Same(rig, eventData.Node);
            Assert.Same(host, eventData.IKSolver);
            ++postSolveCount;
        });

        host.Solve(1.0f / 60.0f);

        Assert.True((third.WorldPosition - target.WorldPosition).Length < 0.01f);
        Assert.Equal(1, preSolveCount);
        Assert.Equal(1, postSolveCount);
    }

    [Fact]
    public void Identity_solver_copies_target_transform()
    {
        using var scene = new SceneType();
        var rig = scene.CreateChild("Rig");
        var host = rig.CreateComponent<IKSolver>();
        var bone = rig.CreateChild("Bone");
        var target = rig.CreateChild("Target");
        target.SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));
        target.SetRotation(new Quaternion(20.0f, Vector3f.Up));
        var identity = rig.CreateComponent<IKIdentitySolver>();
        identity.BoneName = "Bone";
        identity.TargetName = "Target";

        host.Solve(1.0f / 60.0f);

        Assert.True(bone.WorldPosition.ApproximatelyEquivalent(target.WorldPosition, 0.001f));
        Assert.True(bone.WorldRotation.ApproximatelyEquivalent(target.WorldRotation, 0.001f));
    }

    [Fact]
    public void Stick_targets_recover_toward_repeated_desired_transform()
    {
        using var scene = new SceneType();
        var rig = scene.CreateChild("Rig");
        var host = rig.CreateComponent<IKSolver>();
        var target = rig.CreateChild("Target");
        var stickTargets = rig.CreateComponent<IKStickTargets>();
        stickTargets.SetTargetNames(new StringVector(["Target"]));

        host.Solve(0.1f);
        target.SetPosition(Vector3f.Right);
        host.Solve(0.1f);
        Assert.True(target.Position.ApproximatelyEquivalent(Vector3f.Zero, 0.001f));

        target.SetPosition(Vector3f.Right);
        host.Solve(0.1f);

        Assert.True(target.Position.ApproximatelyEquivalent(new Vector3f(0.5f, 0.0f, 0.0f), 0.001f));
    }
}
