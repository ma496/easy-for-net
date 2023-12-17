using EasyForNet.Application.Common.Exceptions;
using EasyForNet.Application.Common.Security;
using EasyForNet.Application.TodoLists.Commands.CreateTodoList;
using EasyForNet.Application.TodoLists.Commands.PurgeTodoLists;
using EasyForNet.Domain.Entities;
using FluentAssertions;

namespace EasyForNet.Application.IntegrationTests.TodoLists.Commands;

using static Testing;

public class PurgeTodoListsTests : BaseTestFixture
{
    [Test]
    public async Task ShouldDenyAnonymousUser()
    {
        var command = new PurgeTodoListsCommand();

        command.GetType().Should().BeDecoratedWith<AuthorizeAttribute>();

        var action = () => SendAsync(command);

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task ShouldDenyNonAdmin()
    {
        await RunAsDefaultUserAsync();

        var command = new PurgeTodoListsCommand();

        var action = () => SendAsync(command);

        await action.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task ShouldAllowAdmin()
    {
        await RunAsAdminAsync();

        var command = new PurgeTodoListsCommand();

        var action = () => SendAsync(command);

        await action.Should().NotThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task ShouldDeleteAllLists()
    {
        await RunAsAdminAsync();

        await SendAsync(new CreateTodoListCommand
        {
            Title = "New List #1"
        });

        await SendAsync(new CreateTodoListCommand
        {
            Title = "New List #2"
        });

        await SendAsync(new CreateTodoListCommand
        {
            Title = "New List #3"
        });

        await SendAsync(new PurgeTodoListsCommand());

        var count = await CountAsync<TodoList>();

        count.Should().Be(0);
    }
}
