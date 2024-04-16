using Efn.Identity.Services;

namespace Efn.Features.SignUp;

sealed class Endpoint : Endpoint<Request, Response, Mapper>
{
    private readonly IUserManager _userManger;

    public Endpoint(IUserManager userManger)
    {
        _userManger = userManger;
    }

    public override void Configure()
    {
        Post("sign-up");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request r, CancellationToken c)
    {
        var user = Map.ToEntity(r);
        await _userManger.Create(user);

        if (user.Id == default)
            ThrowError("User creation failed!");

        Response.Message = $"The user [{r.Name}] has been created with ID: {user.Id}";
    }
}