using Bogus;
using Efn.Features.Signup;

namespace Efn.Tests.Features.Signup;

public class RequestFaker : Faker<Request>
{
    public RequestFaker()
    {
        RuleFor(r => r.Name, f => f.Name.FullName());
        RuleFor(r => r.Username, f => $"user_{f.UniqueIndex}");
        RuleFor(r => r.Password, f => f.Internet.Password());
        RuleFor(r => r.Email, (f, u) => $"email_{f.UniqueIndex}@gmail.com");
        RuleFor(r => r.Age, f => f.Random.Number(10, 100));
    }
}
