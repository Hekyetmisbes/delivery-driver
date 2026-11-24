using DeliveryDriver.Quest;
using NUnit.Framework;

namespace DeliveryDriver.Tests
{
    public class PlayerBalanceReadPolicyTests
    {
        [TestCase(0, 500, 1000, 0)]
        [TestCase(50, 200, 500, 50)]
        [TestCase(null, 10, 500, 10)]
        [TestCase(null, 0, 500, 0)]
        [TestCase(null, null, 500, 500)]
        [TestCase(null, null, -20, 0)]
        public void ResolvesBySourceNotByLargestBalance(int? database, int? preferences, int snapshot, int expected)
        {
            Assert.That(PlayerBalanceReadPolicy.Resolve(database, preferences, snapshot), Is.EqualTo(expected));
        }
    }
}
