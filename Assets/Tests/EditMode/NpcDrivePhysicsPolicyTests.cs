using NUnit.Framework;
using TrafficSystem;

namespace DeliveryDriver.Tests
{
    public class NpcDrivePhysicsPolicyTests
    {
        [TestCase(true, false, false, true)]
        [TestCase(true, true, true, true)]
        [TestCase(true, true, false, false)]
        [TestCase(false, false, true, false)]
        public void NewOrUrgentDynamicBodyRequiresDecision(bool dynamicBody, bool hasPlan, bool urgent, bool expected)
        {
            Assert.That(NpcDrivePhysicsPolicy.RequiresFreshDecision(dynamicBody, hasPlan, urgent), Is.EqualTo(expected));
        }

        [TestCase(60f, true, false, 0f)]
        [TestCase(60f, false, true, 12f)]
        [TestCase(8f, false, true, 8f)]
        [TestCase(60f, false, false, 60f)]
        [TestCase(-3f, false, false, 0f)]
        [TestCase(float.NaN, false, false, 0f)]
        [TestCase(float.PositiveInfinity, false, false, 0f)]
        public void CachedForwardTarget_RespectsSafetyLimits(float target, bool mustStop, bool risk, float expected)
        {
            Assert.That(NpcDrivePhysicsPolicy.LimitForwardTarget(target, mustStop, risk), Is.EqualTo(expected));
        }

        [TestCase(10f, 1f, true, false)]
        [TestCase(10f, 1f, false, true)]
        [TestCase(-1f, -5f, false, false)]
        public void StopFlagAndTolerance_PreventAcceleration(float difference, float tolerance, bool mustStop, bool expected)
        {
            Assert.That(NpcDrivePhysicsPolicy.CanAccelerate(difference, tolerance, mustStop), Is.EqualTo(expected));
        }

        [TestCase(-0.5f, 0.3f, 0.4f, 0.5f)]
        [TestCase(-1.5f, 0.3f, 0.4f, 1f)]
        [TestCase(-0.1f, 0.3f, 0.4f, 0f)]
        [TestCase(-0.5f, 0.3f, 0f, 1f)]
        public void SuspensionTravel_MatchesBoundedPlayerFormula(float contactY, float radius, float suspension, float expected)
        {
            Assert.That(NpcDrivePhysicsPolicy.SuspensionTravel(contactY, radius, suspension), Is.EqualTo(expected).Within(0.0001f));
        }
    }
}
