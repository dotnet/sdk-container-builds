// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;

namespace Test.Microsoft.NET.Build.Containers.Resources
{
    [TestClass]
    public class ResourceTests
    {
        [TestMethod]
        public void GetString_ReturnsValueFromResources()
        {
            Assert.AreEqual(Resource.GetString(nameof(Strings._Test)), "Value for unit test {0}");
        }

        [TestMethod]
        public void FormatString_ReturnsValueFromResources()
        {
            Assert.AreEqual(Resource.FormatString(nameof(Strings._Test), 1), "Value for unit test 1");
        }
    }
}
