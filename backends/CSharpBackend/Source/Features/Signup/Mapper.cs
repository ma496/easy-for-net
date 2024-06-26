using Efn.Identity.Entities;

namespace Efn.Features.SignUp;

sealed class Mapper : Mapper<Request, Response, User>
{
    public override User ToEntity(Request r) => new()
    {
        Age = r.Age,
        Email = r.Email,
        Username = r.Username,
        Password = r.Password, // TODO never store clear passwords in db. always hash/salt before saving.
        Name = r.Name
    };
}