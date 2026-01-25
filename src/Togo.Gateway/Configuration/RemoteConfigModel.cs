using Yarp.ReverseProxy.Configuration;

namespace Togo.Gateway.Configuration;

public class RemoteConfigModel
{
    public List<RouteConfig> Routes { get; set; } = new();
    public List<ClusterConfig> Clusters { get; set; } = new();
}
