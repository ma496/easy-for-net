using System.Collections.Generic;
using Bogus;
using EasyForNet.Application.Dependencies;

namespace EasyForNet.Bogus;

public abstract class DataGenerator<T> : ITransientDependency
    where T : class
{
    protected abstract Faker<T> Faker();

    public T Generate()
    {
        return Faker().Generate();
    }

    public List<T> Generate(int count)
    {
        return Faker().Generate(count);
    }
}