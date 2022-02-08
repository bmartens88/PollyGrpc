using System.Net;
using Grpc.Core;
using GrpcServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

var services = new ServiceCollection();
var loggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});

var serverErrors = new HttpStatusCode[]
{
    HttpStatusCode.BadGateway,
    HttpStatusCode.GatewayTimeout,
    HttpStatusCode.ServiceUnavailable,
    HttpStatusCode.InternalServerError,
    HttpStatusCode.TooManyRequests,
    HttpStatusCode.RequestTimeout
};

var grpcErrors = new StatusCode[]
{
    StatusCode.DeadlineExceeded,
    StatusCode.Internal,
    StatusCode.NotFound,
    StatusCode.ResourceExhausted,
    StatusCode.Unavailable,
    StatusCode.Unknown
};

Func<HttpRequestMessage, IAsyncPolicy<HttpResponseMessage>> retryFunc = (request) =>
{
    return Policy.HandleResult<HttpResponseMessage>(r =>
        {
            var grpcStatus = StatusManager.GetStatusCode(r);
            var httpStatusCode = r.StatusCode;

            return grpcStatus == null && serverErrors.Contains(httpStatusCode) ||
                   httpStatusCode == HttpStatusCode.OK && grpcErrors.Contains(grpcStatus.Value);
        })
        .WaitAndRetryAsync(3, (input) => TimeSpan.FromSeconds(3 + input), (result, timeSpan, retryCount, context) =>
        {
            var grpcStatus = StatusManager.GetStatusCode(result.Result);
            Console.WriteLine($"Request failed with {grpcStatus}. Retry");
        });
};

services.AddGrpcClient<Greeter.GreeterClient>(o => { o.Address = new Uri("https://localhost:7234"); })
    .AddPolicyHandler(retryFunc);

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<Greeter.GreeterClient>();

try
{
    var reply = await client.SayHelloAsync(new HelloRequest {Name = "Bas"});
    Console.WriteLine($"Received: {reply.Message}");
}
catch (RpcException ex)
{
    Console.WriteLine(ex.Message);
}

internal static class StatusManager
{
    public static StatusCode? GetStatusCode(HttpResponseMessage response)
    {
        var headers = response.Headers;

        if (!headers.Contains("grpc-status") && response.StatusCode == HttpStatusCode.OK)
            return StatusCode.OK;

        if (headers.Contains("grpc-status"))
            return (StatusCode) int.Parse(headers.GetValues("grpc-status").First());

        return null;
    }
}