using Togo.Gateway.Policies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddLoadBalancingPolicy<ConsistentHashLoadBalancingPolicy>();

var app = builder.Build();
app.UseForwardedHeaders();
app.MapReverseProxy(proxyPipeline =>
    {
        proxyPipeline.UseLoadBalancing(); 
        proxyPipeline.Use(async (context, next) => { await next(); });
    }
);

app.Run();
