using Grpc.Core;

namespace GrpcServer.Services;

public class Service : Greeter.GreeterBase
{
    private readonly ILogger<Service> _logger;

    public Service(ILogger<Service> logger)
    {
        _logger = logger;
    }

    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        var rnd = new Random();
        var res = rnd.NextDouble() * 10;
        return res > 5
            ? Task.FromResult(new HelloReply
            {
                Message = $"Hello {request.Name}"
            })
            : throw new RpcException(new Status(StatusCode.Internal, "Internal server error"), new Metadata());
    }
}