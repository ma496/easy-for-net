using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ardalis.GuardClauses;
using AutoMapper;
using EasyForNet.Application.Dependencies;
using EasyForNet.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.Modules
{
    public static class AppInitializer
    {
        private static readonly List<string> InitModules = new();

        public static IServiceProvider Init<TModule>(IConfiguration configuration = null, bool isValidateMapper = true)
            where TModule : ModuleBase
        {
            var services = new ServiceCollection();

            var moduleName = typeof(TModule).FullName;

            var mapperConfigurationExpression = new MapperConfigurationExpression();

            if (InitModules.SingleOrDefault(m => m == moduleName) == null)
            {
                var modulesInfo = GetUniqueAndOrderModulesInfo(GetModulesInfo(typeof(TModule), 0));
                foreach (var moduleInfo in modulesInfo)
                {
                    DependencyThroughInterfaces(moduleInfo.Module, services);
                    moduleInfo.Module.Dependencies(services, configuration);

                    mapperConfigurationExpression.AddMaps(moduleInfo.Module.GetType().Assembly);
                    moduleInfo.Module.Mapping(mapperConfigurationExpression, configuration);
                }

                var mapperConfiguration = new MapperConfiguration(mapperConfigurationExpression);
                if (isValidateMapper)
                    mapperConfiguration.AssertConfigurationIsValid();
                var mapper = mapperConfiguration.CreateMapper();
                services.AddSingleton(mapper);

                InitModules.Add(moduleName);
            }
            else
            {
                throw new Exception($"{moduleName} module already initialized");
            }
            
            return services.BuildServiceProvider();
        }

        private static void CheckModuleType(Type type)
        {
            Guard.Against.Null(type, nameof(type));

            if (!typeof(ModuleBase).IsAssignableFrom(type))
                throw new Exception($"{type.FullName} must be inherit from {nameof(ModuleBase)} class");
            if (!type.IsConcreteClass())
                throw new Exception($"{type.FullName} must be concrete class");
            if (!type.IsDefaultConstructor())
                throw new Exception($"{type.FullName} must have default constructor");
        }

        private static List<ModuleInfo> GetModulesInfo(Type type, int level)
        {
            var modulesInfo = new List<ModuleInfo>();

            CheckModuleType(type);
            modulesInfo.Add(new ModuleInfo(level, (ModuleBase) Activator.CreateInstance(type)));

            var dependOnAttributes = type.GetCustomAttributes<DependOnAttribute>();
            foreach (var dependOnAttribute in dependOnAttributes)
                modulesInfo.AddRange(GetModulesInfo(dependOnAttribute.ModuleType, level + 1));

            return modulesInfo;
        }

        private static List<ModuleInfo> GetUniqueAndOrderModulesInfo(List<ModuleInfo> modulesInfo)
        {
            var uniqueAndOrderModulesInfo = new List<ModuleInfo>();
            foreach (var module in modulesInfo.OrderByDescending(m => m.Level))
                if (!uniqueAndOrderModulesInfo.Exists(m =>
                    m.Module.GetType().FullName == module.Module.GetType().FullName))
                    uniqueAndOrderModulesInfo.Add(module);

            return uniqueAndOrderModulesInfo;
        }

        private static void DependencyThroughInterfaces(ModuleBase module, IServiceCollection services)
        {
            var types = module.GetType().Assembly.GetConcreteTypes()
                .Where(t =>
                    typeof(IScopedDependency).IsAssignableFrom(t) || typeof(ITransientDependency).IsAssignableFrom(t)
                                                                  || typeof(ISingletonDependency).IsAssignableFrom(t))
                .ToList();
            foreach (var type in types)
                if (typeof(IScopedDependency).IsAssignableFrom(type))
                {
                    services.AddScoped(type);
                    var serviceInterfaces = GetServiceInterfaces(type);
                    foreach (var serviceInterface in serviceInterfaces) services.AddScoped(serviceInterface, type);
                }
                else if (typeof(ITransientDependency).IsAssignableFrom(type))
                {
                    services.AddTransient(type);
                    var serviceInterfaces = GetServiceInterfaces(type);
                    foreach (var serviceInterface in serviceInterfaces) services.AddTransient(serviceInterface, type);
                }
                else if (typeof(ISingletonDependency).IsAssignableFrom(type))
                {
                    services.AddSingleton(type);
                    var serviceInterfaces = GetServiceInterfaces(type);
                    foreach (var serviceInterface in serviceInterfaces) services.AddSingleton(serviceInterface, type);
                }
        }

        private static List<Type> GetServiceInterfaces(Type type)
        {
            return type.GetInterfaces()
                .Where(t => t != typeof(IScopedDependency) && t != typeof(ITransientDependency) &&
                            t != typeof(ISingletonDependency))
                .ToList();
        }
    }
}