using NUnit.Framework;
using Unity.Platforms.IOS;

class BasicTests
{
	[Test]
	public void VerifyCanReferenceIOSBuildTarget()
	{
		Assert.IsNotNull(typeof(IOSBuildTarget));
	}
}
