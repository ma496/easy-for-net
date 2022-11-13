using Autofac;
using AutoMapper;
using Microsoft.Extensions.Configuration;

namespace EasyForNet.Modules
{
    public abstract class ModuleBase
    {
        public virtual void Dependencies(ContainerBuilder builder, IConfiguration configuration)
        {
        }

        public virtual void Mapping(IMapperConfigurationExpression mapperConfiguration, IConfiguration configuration)
        {
        }
    }
}