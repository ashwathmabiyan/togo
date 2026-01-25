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
var proxySection = builder.Configuration.GetSection("ReverseProxy");

var routesDict = proxySection.GetSection("Routes").Get<Dictionary<string, RouteConfig>>() ?? new();
var initialRoutes = routesDict.Select(kv => 
{ 
    if(string.IsNullOrEmpty(kv.Value.RouteId)) 
        return kv.Value with { RouteId = kv.Key };
    return kv.Value; 
}).ToList();

var clustersDict = proxySection.GetSection("Clusters").Get<Dictionary<string, ClusterConfig>>() ?? new();
var initialClusters = clustersDict.Select(kv => 
{ 
    if(string.IsNullOrEmpty(kv.Value.ClusterId)) 
        return kv.Value with { ClusterId = kv.Key };
    return kv.Value; 
}).ToList();
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
