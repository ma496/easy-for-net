using System;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.Tests.Share
{
    public static class GlobalObjects
    {
        private static ServiceProvider _serviceProvider;

        public static ServiceProvider ServiceProvider
        {
            set
            {
                if (_serviceProvider != null)
                    throw new Exception($"{nameof(ServiceProvider)} already set");

                _serviceProvider = value;
            }

            get
            {
                if (_serviceProvider == null)
                    throw new Exception(
                        $"Set {nameof(GlobalObjects)}.{nameof(ServiceProvider)} before start tests.");

                return _serviceProvider;
            }
        }
    }
}