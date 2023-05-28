using System.Threading.Tasks;
using EasyForNet.Application.Dependencies;

namespace EasyForNet.Data;

public abstract class DataSeeder : IDataSeeder, ITransientDependency
{
    public abstract Task SeedAsync();
}