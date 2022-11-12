using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Share.Common
{
    public abstract class TestsCommon : IDisposable
    {
        private readonly IServiceScope _serviceScope;
        private readonly IList<IServiceScope> _serviceScopes;

        protected ITestOutputHelper OutputHelper { get; }

        protected IServiceProvider Services { get; }

        protected IMapper Mapper { get; }

        protected ICurrentUser CurrentUser { get; }

        protected TestsCommon(ITestOutputHelper outputHelper)
        {
            _serviceScope = GlobalObjects.ServiceProvider.CreateScope();
            _serviceScopes = new List<IServiceScope>();
            OutputHelper = outputHelper;
            Services = _serviceScope.ServiceProvider;
            Mapper = Services.GetRequiredService<IMapper>();
            CurrentUser = Services.GetRequiredService<ICurrentUser>();
        }

        protected T NewScopeService<T>()
        {
            var serviceProvider = CreateScope().ServiceProvider;
            return serviceProvider.GetRequiredService<T>();
        }

        protected (T1, T2) NewScopeService<T1, T2>()
        {
            var serviceProvider = CreateScope().ServiceProvider;
            return (serviceProvider.GetRequiredService<T1>(), serviceProvider.GetRequiredService<T2>());
        }

        protected (T1, T2, T3) NewScopeService<T1, T2, T3>()
        {
            var serviceProvider = CreateScope().ServiceProvider;
            return (serviceProvider.GetRequiredService<T1>(), serviceProvider.GetRequiredService<T2>(),
                serviceProvider.GetRequiredService<T3>());
        }

        protected (T1, T2, T3, T4) NewScopeService<T1, T2, T3, T4>()
        {
            var serviceProvider = CreateScope().ServiceProvider;
            return (serviceProvider.GetRequiredService<T1>(), serviceProvider.GetRequiredService<T2>(),
                serviceProvider.GetRequiredService<T3>(), serviceProvider.GetRequiredService<T4>());
        }

        protected (T1, T2, T3, T4, T5) NewScopeService<T1, T2, T3, T4, T5>()
        {
            var serviceProvider = CreateScope().ServiceProvider;
            return (serviceProvider.GetRequiredService<T1>(), serviceProvider.GetRequiredService<T2>(),
                serviceProvider.GetRequiredService<T3>(), serviceProvider.GetRequiredService<T4>(),
                serviceProvider.GetRequiredService<T5>());
        }

        protected (T1, T2, T3, T4, T5, T6) NewScopeService<T1, T2, T3, T4, T5, T6>()
        {
            var serviceProvider = CreateScope().ServiceProvider;
            return (serviceProvider.GetRequiredService<T1>(), serviceProvider.GetRequiredService<T2>(),
                serviceProvider.GetRequiredService<T3>(), serviceProvider.GetRequiredService<T4>(),
                serviceProvider.GetRequiredService<T5>(), serviceProvider.GetRequiredService<T6>());
        }

        protected (T1, T2, T3, T4, T5, T6, T7) NewScopeService<T1, T2, T3, T4, T5, T6, T7>()
        {
            var serviceProvider = CreateScope().ServiceProvider;
            return (serviceProvider.GetRequiredService<T1>(), serviceProvider.GetRequiredService<T2>(),
                serviceProvider.GetRequiredService<T3>(), serviceProvider.GetRequiredService<T4>(),
                serviceProvider.GetRequiredService<T5>(), serviceProvider.GetRequiredService<T6>(),
                serviceProvider.GetRequiredService<T7>());
        }

        protected (T1, T2, T3, T4, T5, T6, T7, T8) NewScopeService<T1, T2, T3, T4, T5, T6, T7, T8>()
        {
            var serviceProvider = CreateScope().ServiceProvider;
            return (serviceProvider.GetRequiredService<T1>(), serviceProvider.GetRequiredService<T2>(),
                serviceProvider.GetRequiredService<T3>(), serviceProvider.GetRequiredService<T4>(),
                serviceProvider.GetRequiredService<T5>(), serviceProvider.GetRequiredService<T6>(),
                serviceProvider.GetRequiredService<T7>(), serviceProvider.GetRequiredService<T8>());
        }

        protected (T1, T2, T3, T4, T5, T6, T7, T8, T9) NewScopeService<T1, T2, T3, T4, T5, T6, T7, T8, T9>()
        {
            var serviceProvider = CreateScope().ServiceProvider;
            return (serviceProvider.GetRequiredService<T1>(), serviceProvider.GetRequiredService<T2>(),
                serviceProvider.GetRequiredService<T3>(), serviceProvider.GetRequiredService<T4>(),
                serviceProvider.GetRequiredService<T5>(), serviceProvider.GetRequiredService<T6>(),
                serviceProvider.GetRequiredService<T7>(), serviceProvider.GetRequiredService<T8>(),
                serviceProvider.GetRequiredService<T9>());
        }

        protected (T1, T2, T3, T4, T5, T6, T7, T8, T9, T10) NewScopeService<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>()
        {
            var serviceProvider = CreateScope().ServiceProvider;
            return (serviceProvider.GetRequiredService<T1>(), serviceProvider.GetRequiredService<T2>(),
                serviceProvider.GetRequiredService<T3>(), serviceProvider.GetRequiredService<T4>(),
                serviceProvider.GetRequiredService<T5>(), serviceProvider.GetRequiredService<T6>(),
                serviceProvider.GetRequiredService<T7>(), serviceProvider.GetRequiredService<T8>(),
                serviceProvider.GetRequiredService<T9>(), serviceProvider.GetRequiredService<T10>());
        }

        private IServiceScope CreateScope()
        {
            var serviceScope = GlobalObjects.ServiceProvider.CreateScope();
            _serviceScopes.Add(serviceScope);
            return serviceScope;
        }

        public void Dispose()
        {
            _serviceScope?.Dispose();
            foreach (var serviceScope in _serviceScopes)
            {
                serviceScope.Dispose();
            }
        }
    }
}