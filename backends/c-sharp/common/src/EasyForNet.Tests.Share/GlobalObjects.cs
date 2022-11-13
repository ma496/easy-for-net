using Autofac;
using System;

namespace EasyForNet.Tests.Share
{
    public static class GlobalObjects
    {
        private static IContainer _container;

        public static IContainer Container
        {
            set
            {
                if (_container != null)
                    throw new Exception($"{nameof(Container)} already set");

                _container = value;
            }

            get
            {
                if (_container == null)
                    throw new NullReferenceException($"{nameof(Container)} not set");

                return _container;
            }
        }
    }
}