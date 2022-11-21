using Xunit;

namespace EasyForNet.Tests.Base;

[CollectionDefinition(nameof(EasyForNetTestsCollectionFixture))]
public class EasyForNetTestsCollectionFixture : ICollectionFixture<EasyForNetTestsFixture>
{
}