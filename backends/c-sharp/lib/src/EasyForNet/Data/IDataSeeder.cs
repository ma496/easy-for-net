using System.Threading.Tasks;

namespace EasyForNet.Data;

public interface IDataSeeder
{
    Task SeedAsync();
}