using AutoMapper;
using EasyForNet.Application.Common.Mappings;
using EasyForNet.Application.Common.Models;
using EasyForNet.Application.TodoLists.Queries.GetTodos;
using EasyForNet.Domain.Entities;

namespace EasyForNet.Application.UnitTests.Common.Mappings;

public class MappingTests
{
    private readonly IConfigurationProvider _configuration;
    private readonly IMapper _mapper;

    public MappingTests()
    {
        _configuration = new MapperConfiguration(config =>
            config.AddProfile<MappingProfile>());

        _mapper = _configuration.CreateMapper();
    }

    [Test]
    public void ShouldHaveValidConfiguration()
    {
        _configuration.AssertConfigurationIsValid();
    }

    [Test]
    [TestCase(typeof(TodoList), typeof(TodoListDto))]
    [TestCase(typeof(TodoItem), typeof(TodoItemDto))]
    [TestCase(typeof(TodoList), typeof(LookupDto))]
    [TestCase(typeof(TodoItem), typeof(LookupDto))]
    public void ShouldSupportMappingFromSourceToDestination(Type source, Type destination)
    {
        var instance = GetInstanceOf(source);

        _mapper.Map(instance, source, destination);
    }

    private object? GetInstanceOf(Type type)
    {
        if (type.GetConstructor(Type.EmptyTypes) != null)
            return Activator.CreateInstance(type)!;

        throw new Exception($"Unable to create an instance of '{type}'. " +
                            $"Ensure the type has a parameterless constructor.");
    }
}
