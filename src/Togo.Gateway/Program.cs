using Togo.Gateway.Policies;
using Togo.Gateway.Services;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.LoadBalancing;

var builder = WebApplication.CreateBuilder(args);

// Register Core Services
builder.Services.AddSingleton<ILoadBalancingPolicy, ConsistentHashLoadBalancingPolicy>();

// Register Dynamic Configuration Services
builder.Services.AddHttpClient<RemoteConfigClient>();

// Initial Fallback Config
var initialRoutes = new List<RouteConfig>();
var initialClusters = new List<ClusterConfig>();
var configProvider = new Togo.Gateway.Configuration.InMemoryConfigProvider(initialRoutes, initialClusters);

builder.Services.AddSingleton(configProvider);
builder.Services.AddSingleton<IProxyConfigProvider>(configProvider);
builder.Services.AddHostedService<ConfigPollingWorker>();

builder.Services.AddReverseProxy(); 

var app = builder.Build();
app.UseForwardedHeaders();
app.MapReverseProxy(proxyPipeline =>
    {
        proxyPipeline.UseLoadBalancing(); 
        proxyPipeline.Use(async (context, next) => { await next(); });
    }
);

app.Run();
