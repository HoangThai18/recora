using Recora.Core;

namespace Recora.Core.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void RecoraException_KeepsCode_ForFriendlyMessages()
        {
            var error = new RecoraException("region_too_small", "region 10x10 is below 160x90");

            Assert.Equal("region_too_small", error.Code);
        }
    }
}
