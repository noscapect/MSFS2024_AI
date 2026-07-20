using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SayIntentions;

namespace Copilot.Tests;

[TestClass]
public sealed class SayIntentionsVoicePolicyTests
{
    [DataTestMethod]
    [DataRow("captain-engine-two")]
    [DataRow("fo-engine-two-starter")]
    [DataRow("fo-engine-two-fuel")]
    [DataRow("fo-engine-two-stable")]
    [DataRow("captain-engine-one")]
    [DataRow("fo-engine-one-starter")]
    [DataRow("fo-engine-one-fuel")]
    [DataRow("fo-engine-one-stable")]
    public void EngineStartCalloutsShareOrderedQueueWindow(string stepId)
    {
        Assert.IsTrue(SayIntentionsVoicePolicy.IsEngineStartCallout(stepId));
        Assert.AreEqual(
            TimeSpan.FromSeconds(35),
            SayIntentionsVoicePolicy.MaxQueueAge(stepId));
    }

    [TestMethod]
    public void TimeCriticalCalloutsExpireQuickly()
    {
        Assert.AreEqual(
            TimeSpan.FromSeconds(6),
            SayIntentionsVoicePolicy.MaxQueueAge("fo-v1"));
    }

    [TestMethod]
    public void RoutineCalloutsCanWaitForCabinAudio()
    {
        Assert.AreEqual(
            TimeSpan.FromSeconds(45),
            SayIntentionsVoicePolicy.MaxQueueAge("fo-flaps-one"));
    }
}
