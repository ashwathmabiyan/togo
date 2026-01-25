using Togo.Gateway.Policies;
using Yarp.ReverseProxy.LoadBalancing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ILoadBalancingPolicy, ConsistentHashLoadBalancingPolicy>();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.UseForwardedHeaders();
app.MapReverseProxy(proxyPipeline =>
    {
        proxyPipeline.UseLoadBalancing(); 
        proxyPipeline.Use(async (context, next) => { await next(); });
    }
);

app.Run();
