using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.Gsx;

namespace Msfs2024Ai.Copilot.Tests;

[TestClass]
public sealed class GsxOwnershipLeaseTests
{
    [TestMethod]
    public void LeaseCanRecoverAndClearsCleanly()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "lease");
        var lease = new GsxOwnershipLease(path);
        var now = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

        try
        {
            Assert.IsFalse(lease.CanRecover(now));
            lease.MarkOwned(now);
            Assert.IsTrue(lease.CanRecover(now.AddHours(2)));
            Assert.IsFalse(lease.CanRecover(now.AddHours(25)));
            lease.Clear();
            Assert.IsFalse(lease.CanRecover(now));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
