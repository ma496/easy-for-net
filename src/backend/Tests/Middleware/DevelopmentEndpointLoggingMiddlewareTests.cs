namespace Backend.Tests.Middleware;

using Backend.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class DevelopmentEndpointLoggingMiddlewareTests
{
    [Fact]
    public async Task Logs_One_Line_Per_Matched_FastEndpoint_Without_Query_String()
    {
        var logger = new RecordingLogger<DevelopmentEndpointLoggingMiddleware>();
        var middleware = new DevelopmentEndpointLoggingMiddleware(
            async context =>
            {
                await Task.Delay(5);
                context.Response.StatusCode = StatusCodes.Status201Created;
            },
            logger);
        var context = CreateHttpContext(fastEndpoint: true);
        context.Request.QueryString = new QueryString("?secret=do-not-log");

        await middleware.InvokeAsync(context);

        var record = logger.Records.Should().ContainSingle().Which;
        record.Level.Should().Be(LogLevel.Information);
        record.Properties["HttpMethod"].Should().Be("POST");
        record.Properties["Path"].Should().Be("/api/test");
        record.Properties["StatusCode"].Should().Be(StatusCodes.Status201Created);
        record.Properties["ElapsedMilliseconds"].Should().BeOfType<double>().Which.Should().BeGreaterThanOrEqualTo(0);
        record.Message.Should().StartWith("POST /api/test 201 in ");
        record.Message.Should().NotContain("secret");
        record.Properties.Values.Should().NotContain("do-not-log");
    }

    [Fact]
    public async Task Does_Not_Log_Non_FastEndpoint_Requests()
    {
        var nextWasCalled = false;
        var logger = new RecordingLogger<DevelopmentEndpointLoggingMiddleware>();
        var middleware = new DevelopmentEndpointLoggingMiddleware(
            _ =>
            {
                nextWasCalled = true;
                return Task.CompletedTask;
            },
            logger);

        await middleware.InvokeAsync(CreateHttpContext(fastEndpoint: false));

        nextWasCalled.Should().BeTrue();
        logger.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task Logs_And_Rethrows_When_Endpoint_Fails()
    {
        var logger = new RecordingLogger<DevelopmentEndpointLoggingMiddleware>();
        var expectedException = new InvalidOperationException("Endpoint failed");
        var middleware = new DevelopmentEndpointLoggingMiddleware(_ => throw expectedException, logger);

        var act = () => middleware.InvokeAsync(CreateHttpContext(fastEndpoint: true));

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(expectedException);
        logger.Records.Should().ContainSingle().Which.Properties.Should().ContainKey("ElapsedMilliseconds");
    }

    private static DefaultHttpContext CreateHttpContext(bool fastEndpoint)
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "test-trace-id"
        };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/test";

        var metadata = fastEndpoint
            ? new EndpointMetadataCollection(new EndpointDefinition(typeof(object), typeof(object), typeof(object)))
            : new EndpointMetadataCollection();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "TestEndpoint"));

        return context;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogRecord> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.Where(x => x.Key != "{OriginalFormat}").ToDictionary()
                : [];
            Records.Add(new LogRecord(logLevel, formatter(state, exception), properties));
        }
    }

    private sealed record LogRecord(LogLevel Level, string Message, Dictionary<string, object?> Properties);
}
