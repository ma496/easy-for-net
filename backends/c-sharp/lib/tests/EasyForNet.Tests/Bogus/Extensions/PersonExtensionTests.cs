using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Autofac;
using Bogus;
using EasyForNet.Bogus;
using EasyForNet.Bogus.Extensions;
using EasyForNet.Extensions;
using EasyForNet.Tests.Base;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Bogus.Extensions;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class PersonExtensionTests : TestsBase
{
    public PersonExtensionTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public void IdCard()
    {
        var userGenerator = Scope.Resolve<UserGenerator>();
        var user = userGenerator.Generate();

        Assert.NotNull(user);
        Assert.NotEmpty(user.IdCard);

        var splitIdCard = user.IdCard.Split('-').ToList();

        Assert.Equal(4, splitIdCard.Count);
        Assert.Equal(4, splitIdCard[0].Length);
        Assert.Equal(4, splitIdCard[1].Length);
        Assert.Equal(4, splitIdCard[2].Length);
        Assert.Equal(4, splitIdCard[3].Length);
    }

    public class User
    {
        public string IdCard { get; set; }
    }

    public class UserGenerator : DataGenerator<User>
    {
        protected override Faker<User> Faker()
        {
            return new Faker<User>()
                .RuleFor(u => u.IdCard, f => f.Person.IdCard());
        }
    }
}