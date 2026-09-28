namespace Backend.Features.Tenancy.Endpoints.Editions;

/// <summary>
/// This route group that prefixes all edition administration endpoints - the plans the platform
/// sells - with the <c>editions</c> segment. Endpoints in this area declare only the remainder of
/// their route.
/// </summary>
sealed class EditionsGroup : Group
{
    public EditionsGroup()
    {
        Configure("editions", ep => {});
    }
}
