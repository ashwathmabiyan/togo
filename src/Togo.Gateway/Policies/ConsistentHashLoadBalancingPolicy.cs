using System.Runtime.CompilerServices;
using Togo.Core.Hashing;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

namespace Togo.Gateway.Policies;

public class ConsistentHashLoadBalancingPolicy : ILoadBalancingPolicy
{
    public string Name => "ConsistentHashing";

    // Cache the ring associated with a specific list of destinations. Intelligent cache drops if not referred.
    private readonly ConditionalWeakTable<IReadOnlyList<DestinationState>, ConsistentHashRing> _rings = new();

    public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
    {
        if (availableDestinations.Count == 0)
        {
            return null;
        }

        if (availableDestinations.Count == 1)
        {
            return availableDestinations[0];
        }

        // Get the cached ring or create a new one (Thread-Safe & Lock-Free via Atomic Table)
        // YARP will return existing ring immediately if availableDestinations hasn't changed.
        var ring = _rings.GetValue(availableDestinations, CreateRing);

        // Determine the key to hash (IP, Header, or ID)
        string key = GetHashKey(context);
 
        string selectedDestinationId = ring.GetNode(key);

        return availableDestinations.FirstOrDefault(d => d.DestinationId == selectedDestinationId);
    }

    /// <summary>
    /// This method is only called, if the old list of servers held by YARP has been changed. 
    /// </summary>
    /// <param name="destinations">availableDestinations</param>
    /// <returns>new consistent hashed ring object</returns>
    private ConsistentHashRing CreateRing(IReadOnlyList<DestinationState> destinations)
    {
        var ids = destinations.Select(d => d.DestinationId);
        return new ConsistentHashRing(ids);
    }

    private string GetHashKey(HttpContext context)
    {
        // Customizable Strategy for Stickiness:
        // Priority 1: Explicit "X-User-ID" header (useful for testing or specific client control)
        // Priority 2: Remote IP Address (Standard for simple sticky sessions)
        // Priority 3: TraceIdentifier (Random fallback if IP is null)
        
        if (context.Request.Headers.TryGetValue("X-User-ID", out var headerVal) && !string.IsNullOrWhiteSpace(headerVal))
        {
            return headerVal.ToString();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? context.TraceIdentifier; 
    }
}
