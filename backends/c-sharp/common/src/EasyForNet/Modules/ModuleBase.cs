using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.Modules
{
    public abstract class ModuleBase
    {
        public virtual void Dependencies(IServiceCollection services, IConfiguration configuration)
        {
        }

        public virtual void Mapping(IMapperConfigurationExpression mapperConfiguration, IConfiguration configuration)
        {
        }
    }
}