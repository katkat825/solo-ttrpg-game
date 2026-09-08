using Game.Tray;

namespace Game.Tests
{
    // the whole point of F1: a Godot-free helper living in game/ can be reached by a test.
    // if this file stops compiling, game/ has fallen out of the solution again and every
    // other test in this project went quiet with it
    //
    // it also draws the line. GodotSharp is on the reference path, but no engine is running
    // behind it, so a type that touches Node or Resource cannot be exercised from here -
    // that is deliberate, and a helper that needs one belongs in core/, not in a test
    // project pretending it doesn't
    public class SmokeTests
    {
        [Fact]
        public void AGodotFreeHelperInGame_IsReachableFromATestProject() =>
            Assert.Equal(3, TrayResolution.PoolSize);
    }
}
