using System.Collections;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Megastructure.Tests
{
    /// <summary>Part 7.6 / 9: the new Input System provides a keyboard and a mouse at runtime (the game never uses UnityEngine.Input).</summary>
    public class InputDeviceTests
    {
        [UnityTest]
        public IEnumerator KeyboardAndMouseExist()
        {
            yield return null;
            Assert.IsNotNull(Keyboard.current, "Keyboard.current");
            Assert.IsNotNull(Mouse.current, "Mouse.current");
        }
    }
}
