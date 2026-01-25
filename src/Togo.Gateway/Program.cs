using Togo.Core.Hashing;

var builder = WebApplication.CreateBuilder();

var serverList = new[]{""};

builder.Services.AddSingleton(new ConsistentHashRing(serverList, 100));

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
