using System;

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
        RedBolt.IK.IK.RegisterIKLibrary(_fixture.Context);
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
    public void Generated_registration_registers_the_complete_cpp_ik_library()
    {
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
    public void All_solver_steady_state_paths_do_not_allocate()
    {
        using var scene = new SceneType();
        var rig = scene.CreateChild("Rig");
        var host = rig.CreateComponent<IKSolver>();

        var identityBone = rig.CreateChild("IdentityBone");
        var identityTarget = rig.CreateChild("IdentityTarget");
        identityTarget.SetPosition(new Vector3f(-1.0f, 1.0f, 0.0f));
        var identity = rig.CreateComponent<IKIdentitySolver>();
        identity.BoneName = "IdentityBone";
        identity.TargetName = "IdentityTarget";

        var rotateFirst = rig.CreateChild("RotateFirst");
        var rotateSecond = rotateFirst.CreateChild("RotateSecond");
        rotateSecond.SetPosition(Vector3f.Right);
        var rotateTarget = rig.CreateChild("RotateTarget");
        rotateTarget.SetPosition(Vector3f.Up);
        var rotateTo = rig.CreateComponent<IKRotateTo>();
        rotateTo.FirstBoneName = "RotateFirst";
        rotateTo.SecondBoneName = "RotateSecond";
        rotateTo.TargetName = "RotateTarget";

        var limbFirst = rig.CreateChild("LimbFirst");
        var limbSecond = limbFirst.CreateChild("LimbSecond");
        limbSecond.SetPosition(Vector3f.Right);
        var limbThird = limbSecond.CreateChild("LimbThird");
        limbThird.SetPosition(Vector3f.Right);
        var limbTarget = rig.CreateChild("LimbTarget");
        limbTarget.SetPosition(new Vector3f(1.0f, 1.0f, 0.0f));
        var limb = rig.CreateComponent<IKLimbSolver>();
        limb.FirstBoneName = "LimbFirst";
        limb.SecondBoneName = "LimbSecond";
        limb.ThirdBoneName = "LimbThird";
        limb.TargetName = "LimbTarget";

        var armBone = rig.CreateChild("ArmBone");
        var forearmBone = armBone.CreateChild("ForearmBone");
        forearmBone.SetPosition(Vector3f.Right);
        var handBone = forearmBone.CreateChild("HandBone");
        handBone.SetPosition(Vector3f.Right);
        var armTarget = rig.CreateChild("ArmTarget");
        armTarget.SetPosition(new Vector3f(1.0f, 1.0f, 0.5f));
        var arm = rig.CreateComponent<IKArmSolver>();
        arm.ArmBoneName = "ArmBone";
        arm.ForearmBoneName = "ForearmBone";
        arm.HandBoneName = "HandBone";
        arm.TargetName = "ArmTarget";

        var thighBone = rig.CreateChild("ThighBone");
        var calfBone = thighBone.CreateChild("CalfBone");
        calfBone.SetPosition(new Vector3f(0.0f, -1.0f, 0.0f));
        var heelBone = calfBone.CreateChild("HeelBone");
        heelBone.SetPosition(new Vector3f(0.0f, -1.0f, 0.0f));
        var toeBone = heelBone.CreateChild("ToeBone");
        toeBone.SetPosition(Vector3f.Forward);
        var legTarget = rig.CreateChild("LegTarget");
        legTarget.SetPosition(new Vector3f(0.5f, -1.5f, 0.5f));
        var leg = rig.CreateComponent<IKLegSolver>();
        leg.ThighBoneName = "ThighBone";
        leg.CalfBoneName = "CalfBone";
        leg.HeelBoneName = "HeelBone";
        leg.ToeBoneName = "ToeBone";
        leg.TargetName = "LegTarget";

        var chainFirst = rig.CreateChild("ChainFirst");
        var chainSecond = chainFirst.CreateChild("ChainSecond");
        chainSecond.SetPosition(Vector3f.Right);
        var chainTarget = rig.CreateChild("ChainTarget");
        chainTarget.SetPosition(new Vector3f(0.5f, 1.0f, 0.0f));
        var chain = rig.CreateComponent<IKChainSolver>();
        chain.SetBoneNames(new StringVector(["ChainFirst", "ChainSecond"]));
        chain.TargetName = "ChainTarget";

        var spineFirst = rig.CreateChild("SpineFirst");
        var spineSecond = spineFirst.CreateChild("SpineSecond");
        spineSecond.SetPosition(Vector3f.Up);
        var spineTarget = rig.CreateChild("SpineTarget");
        spineTarget.SetPosition(new Vector3f(0.5f, 1.5f, 0.0f));
        var spine = rig.CreateComponent<IKSpineSolver>();
        spine.SetBoneNames(new StringVector(["SpineFirst", "SpineSecond"]));
        spine.TargetName = "SpineTarget";

        var neckBone = rig.CreateChild("NeckBone");
        var headBone = neckBone.CreateChild("HeadBone");
        headBone.SetPosition(Vector3f.Up);
        var headTarget = rig.CreateChild("HeadTarget");
        headTarget.SetPosition(new Vector3f(0.0f, 2.0f, 1.0f));
        var head = rig.CreateComponent<IKHeadSolver>();
        head.NeckBoneName = "NeckBone";
        head.HeadBoneName = "HeadBone";
        head.TargetName = "HeadTarget";

        var stickyTarget = rig.CreateChild("StickyTarget");
        stickyTarget.SetPosition(Vector3f.Forward);
        var stickTargets = rig.CreateComponent<IKStickTargets>();
        stickTargets.SetTargetNames(new StringVector(["StickyTarget"]));

        var preSolveCount = 0;
        var postSolveCount = 0;
        host.SubscribeToEvent<IKEvents.IKPreSolveEvent>(rig, (ref IKEvents.IKPreSolveEvent _) =>
        {
            ++preSolveCount;
        });
        host.SubscribeToEvent<IKEvents.IKPostSolveEvent>(rig, (ref IKEvents.IKPostSolveEvent _) =>
        {
            ++postSolveCount;
        });

        var warmupIterations = 64;
        for (var i = 0; i < warmupIterations; ++i)
            host.Solve(1.0f / 60.0f);

        var measuredIterations = 1024;
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < measuredIterations; ++i)
            host.Solve(1.0f / 60.0f);
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal(0L, allocatedBytes);
        Assert.Equal(warmupIterations + measuredIterations, preSolveCount);
        Assert.Equal(warmupIterations + measuredIterations, postSolveCount);
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
