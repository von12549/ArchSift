// Compile this NEW file into the test project; original test files remain unchanged.
using System.Reflection;
using ArchSift.Testing;
using Xunit;
using Xunit.v3;

[assembly: ArchSiftCaseIdentity]

public sealed class ArchSiftCaseIdentityAttribute : BeforeAfterTestAttribute
{
    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        var projection = new CaseProjection(test.UniqueID, test.TestCase.UniqueID,
            CaseIdentities.Create(methodUnderTest, test.TestMethodArguments ?? []));
        (TestContext.Current.TestOutputHelper ?? throw new InvalidOperationException("Missing test output."))
            .WriteLine(CaseIdentities.Marker + CaseIdentities.Serialize(projection));
    }
}
