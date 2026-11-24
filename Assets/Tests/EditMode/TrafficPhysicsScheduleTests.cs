using DeliveryDriver.Optimization;
using NUnit.Framework;

namespace DeliveryDriver.Tests
{
    public class TrafficPhysicsScheduleTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(8)]
        [TestCase(16)]
        public void EachPhaseRunsOncePerPeriod(int interval)
        {
            for (uint phase = 0; phase < (uint)interval; phase++)
            {
                int runs = 0;
                for (uint tick = 0; tick < (uint)(interval * 10); tick++)
                    if (TrafficSimulationOptimizer.IsScheduledPhysicsTick(tick, phase, interval))
                        runs++;

                Assert.That(runs, Is.EqualTo(10));
            }
        }

        [Test]
        public void PhasesDistributeWorkAcrossTicks()
        {
            Assert.That(TrafficSimulationOptimizer.IsScheduledPhysicsTick(4, 0, 4), Is.True);
            Assert.That(TrafficSimulationOptimizer.IsScheduledPhysicsTick(4, 1, 4), Is.False);
            Assert.That(TrafficSimulationOptimizer.IsScheduledPhysicsTick(7, 1, 4), Is.True);
        }

        [Test]
        public void InvalidIntervalDoesNotDivideByZero()
        {
            Assert.That(TrafficSimulationOptimizer.IsScheduledPhysicsTick(1, 1, 0), Is.True);
        }
    }
}
