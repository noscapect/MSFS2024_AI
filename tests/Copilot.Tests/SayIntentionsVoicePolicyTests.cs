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

    [DataTestMethod]
    [DataRow("fo-ground-spoilers")]
    [DataRow("fo-flaps-takeoff")]
    [DataRow("fo-flaps-one")]
    [DataRow("fo-flaps-full")]
    [DataRow("fo-flaps-up")]
    public void ConfigurationCalloutsExpireBeforeTheyBecomeMisleading(string stepId)
    {
        Assert.AreEqual(
            TimeSpan.FromSeconds(8),
            SayIntentionsVoicePolicy.MaxQueueAge(stepId));
    }

    [DataTestMethod]
    [DataRow("thrust-set")]
    [DataRow("fo-100-knots")]
    [DataRow("fo-v1")]
    [DataRow("fo-rotate")]
    public void TakeoffCalloutsBypassOlderVoiceQueueWork(string stepId)
    {
        Assert.IsTrue(SayIntentionsVoicePolicy.BypassesQueue(stepId));
    }

    [TestMethod]
    public void ConfigurationCalloutsRemainOrdered()
    {
        Assert.IsFalse(SayIntentionsVoicePolicy.BypassesQueue("fo-flaps-one"));
    }
}
