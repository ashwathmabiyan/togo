using System.Runtime.CompilerServices;
using Togo.Core.Hashing;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

namespace Togo.Gateway.Policies;

public class ConsistentHashLoadBalancingPolicy : ILoadBalancingPolicy
{
    public string Name => "ConsistentHashing";

    // Cache the ring associated with a specific list of destinations.
    // Using ConditionalWeakTable ensures that when YARP drops the list of destinations (e.g. config reload),
    // the associated Ring is also eligible for Garbage Collection.
    private readonly ConditionalWeakTable<IReadOnlyList<DestinationState>, ConsistentHashRing> _rings = new();

    public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
    {
        if (availableDestinations.Count == 0)
        {
            return null;
        }

        // Optimization: Single destination doesn't need hashing
        if (availableDestinations.Count == 1)
        {
            return availableDestinations[0];
        }

        // Get the cached ring or create a new one (Thread-Safe & Lock-Free via Atomic Table)
        var ring = _rings.GetValue(availableDestinations, CreateRing);

        // Determine the key to hash (IP, Header, or ID)
        string key = GetHashKey(context);

        // Map key to a destination ID using the ring
        string selectedDestinationId = ring.GetNode(key);

        // Find the destination object. 
        // Note: availableDestinations is usually small (<100), so O(N) linear scan is acceptable here.
        // If thousands of backends, we would optimize this with a dictionary in the Ring or separate cache.
        return availableDestinations.FirstOrDefault(d => d.DestinationId == selectedDestinationId);
    }

    private ConsistentHashRing CreateRing(IReadOnlyList<DestinationState> destinations)
    {
        var ids = destinations.Select(d => d.DestinationId);
        return new ConsistentHashRing(ids);
    }

    private string GetHashKey(HttpContext context)
    {
        // Customizable Strategy for Stickiness:
        // Priority 1: Explicit "X-Hash-Key" header (useful for testing or specific client control)
        // Priority 2: Remote IP Address (Standard for simple sticky sessions)
        // Priority 3: TraceIdentifier (Random fallback if IP is null)
        
        if (context.Request.Headers.TryGetValue("X-Hash-Key", out var headerVal) && !string.IsNullOrWhiteSpace(headerVal))
        {
            return headerVal.ToString();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? context.TraceIdentifier; 
    }
}
