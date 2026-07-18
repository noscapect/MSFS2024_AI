using Microsoft.VisualStudio.TestTools.UnitTesting;
using Msfs2024Ai.Copilot.SayIntentions;

namespace Copilot.Tests;

[TestClass]
public sealed class SayIntentionsCopilotActionMapTests
{
    [TestMethod]
    public void IfrAndPushbackUseTheirDistinctNativeActions()
    {
        Assert.IsTrue(SayIntentionsCopilotActionMap.TryGetActionName(
            "captain-ifr-clearance",
            out var ifrAction));
        Assert.IsTrue(SayIntentionsCopilotActionMap.TryGetActionName(
            "captain-pushback-clearance",
            out var pushbackAction));

        Assert.AreEqual("preflight_request_clearance_ifr", ifrAction);
        Assert.AreEqual("preflight_request_push_and_start", pushbackAction);
        Assert.AreNotEqual(ifrAction, pushbackAction);
    }

    [TestMethod]
    public void UnknownStepsDoNotGuessNativeActionNames()
    {
        Assert.IsFalse(SayIntentionsCopilotActionMap.TryGetActionName(
            "fo-taxi-clearance",
            out var actionName));
        Assert.AreEqual("", actionName);
    }
}
