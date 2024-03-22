namespace Efn.Features.CheckOne;

sealed class Request
{
    public string Name { get; set; }
}

sealed class Validator : Validator<Request>
{
    public Validator()
    {

    }
}

sealed class Response
{
    public string Message => "This endpoint hasn't been implemented yet!";
}
