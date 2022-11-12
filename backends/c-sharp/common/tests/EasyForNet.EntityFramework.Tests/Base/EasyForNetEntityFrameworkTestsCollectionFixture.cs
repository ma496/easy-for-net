using Xunit;

namespace EasyForNet.EntityFramework.Tests.Base
{
    [CollectionDefinition(nameof(EasyForNetEntityFrameworkTestsCollectionFixture))]
    public class
        EasyForNetEntityFrameworkTestsCollectionFixture : ICollectionFixture<EasyForNetEntityFrameworkTestsFixture>
    {
    }
}