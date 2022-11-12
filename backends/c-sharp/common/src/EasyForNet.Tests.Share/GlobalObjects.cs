using System;

namespace EasyForNet.Tests.Share
{
    public static class GlobalObjects
    {
        private static IServiceProvider _serviceProvider;

        public static IServiceProvider ServiceProvider
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
                    throw new NullReferenceException($"{nameof(ServiceProvider)} not set");

                return _serviceProvider;
            }
        }
    }
}