namespace Efn.Features.CheckOne;

sealed class Endpoint : Endpoint<Request, Response, Mapper>
{
    public Endpoint()
    {
        
    }

    public override void Configure()
    {
        Get("checkone");
        //AllowAnonymous();
        Permissions("DeleteUser");
    }

    public override async Task HandleAsync(Request r, CancellationToken c)
    {
        await SendAsync(new Response());
    }
}