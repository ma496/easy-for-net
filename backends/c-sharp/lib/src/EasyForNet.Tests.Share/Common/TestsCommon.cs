using System;
using System.Collections.Generic;
using Autofac;
using AutoMapper;
using Xunit.Abstractions;

namespace EasyForNet.Tests.Share.Common
{
    public abstract class TestsCommon : IDisposable
    {
        private readonly ILifetimeScope _currentScope;
        private readonly IList<ILifetimeScope> _scopes;

        protected ITestOutputHelper OutputHelper { get; }

        protected ILifetimeScope Scope { get => _currentScope; }

        protected IMapper Mapper { get; }

        protected ICurrentUser CurrentUser { get; }

        protected TestsCommon(ITestOutputHelper outputHelper)
        {
            _currentScope = GlobalObjects.Container.BeginLifetimeScope();
            _scopes = new List<ILifetimeScope>();
            OutputHelper = outputHelper;
            Mapper = Scope.Resolve<IMapper>();
            CurrentUser = Scope.Resolve<ICurrentUser>();
        }

        protected T NewScopeService<T>()
        {
            var scope = CreateScope();
            return scope.Resolve<T>();
        }

        protected (T1, T2) NewScopeService<T1, T2>()
        {
            var scope = CreateScope();
            return (scope.Resolve<T1>(), scope.Resolve<T2>());
        }

        protected (T1, T2, T3) NewScopeService<T1, T2, T3>()
        {
            var scope = CreateScope();
            return (scope.Resolve<T1>(), scope.Resolve<T2>(),
                scope.Resolve<T3>());
        }

        protected (T1, T2, T3, T4) NewScopeService<T1, T2, T3, T4>()
        {
            var scope = CreateScope();
            return (scope.Resolve<T1>(), scope.Resolve<T2>(),
                scope.Resolve<T3>(), scope.Resolve<T4>());
        }

        protected (T1, T2, T3, T4, T5) NewScopeService<T1, T2, T3, T4, T5>()
        {
            var scope = CreateScope();
            return (scope.Resolve<T1>(), scope.Resolve<T2>(),
                scope.Resolve<T3>(), scope.Resolve<T4>(),
                scope.Resolve<T5>());
        }

        protected (T1, T2, T3, T4, T5, T6) NewScopeService<T1, T2, T3, T4, T5, T6>()
        {
            var scope = CreateScope();
            return (scope.Resolve<T1>(), scope.Resolve<T2>(),
                scope.Resolve<T3>(), scope.Resolve<T4>(),
                scope.Resolve<T5>(), scope.Resolve<T6>());
        }

        protected (T1, T2, T3, T4, T5, T6, T7) NewScopeService<T1, T2, T3, T4, T5, T6, T7>()
        {
            var scope = CreateScope();
            return (scope.Resolve<T1>(), scope.Resolve<T2>(),
                scope.Resolve<T3>(), scope.Resolve<T4>(),
                scope.Resolve<T5>(), scope.Resolve<T6>(),
                scope.Resolve<T7>());
        }

        protected (T1, T2, T3, T4, T5, T6, T7, T8) NewScopeService<T1, T2, T3, T4, T5, T6, T7, T8>()
        {
            var scope = CreateScope();
            return (scope.Resolve<T1>(), scope.Resolve<T2>(),
                scope.Resolve<T3>(), scope.Resolve<T4>(),
                scope.Resolve<T5>(), scope.Resolve<T6>(),
                scope.Resolve<T7>(), scope.Resolve<T8>());
        }

        protected (T1, T2, T3, T4, T5, T6, T7, T8, T9) NewScopeService<T1, T2, T3, T4, T5, T6, T7, T8, T9>()
        {
            var scope = CreateScope();
            return (scope.Resolve<T1>(), scope.Resolve<T2>(),
                scope.Resolve<T3>(), scope.Resolve<T4>(),
                scope.Resolve<T5>(), scope.Resolve<T6>(),
                scope.Resolve<T7>(), scope.Resolve<T8>(),
                scope.Resolve<T9>());
        }

        protected (T1, T2, T3, T4, T5, T6, T7, T8, T9, T10) NewScopeService<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>()
        {
            var scope = CreateScope();
            return (scope.Resolve<T1>(), scope.Resolve<T2>(),
                scope.Resolve<T3>(), scope.Resolve<T4>(),
                scope.Resolve<T5>(), scope.Resolve<T6>(),
                scope.Resolve<T7>(), scope.Resolve<T8>(),
                scope.Resolve<T9>(), scope.Resolve<T10>());
        }

        private ILifetimeScope CreateScope()
        {
            var scope = GlobalObjects.Container.BeginLifetimeScope();
            _scopes.Add(scope);
            return scope;
        }

        public void Dispose()
        {
            _currentScope?.Dispose();
            foreach (var serviceScope in _scopes)
            {
                serviceScope.Dispose();
            }
        }
    }
}