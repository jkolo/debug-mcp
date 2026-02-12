namespace DebugMcp.Models.Inspection;

/// <summary>
/// Classification of recognized .NET collection types.
/// </summary>
public enum CollectionKind
{
    /// <summary>T[] or multidimensional arrays.</summary>
    Array,

    /// <summary>List&lt;T&gt;, ImmutableList&lt;T&gt;, ImmutableArray&lt;T&gt;.</summary>
    List,

    /// <summary>Dictionary&lt;K,V&gt;, SortedDictionary&lt;K,V&gt;, ConcurrentDictionary&lt;K,V&gt;.</summary>
    Dictionary,

    /// <summary>HashSet&lt;T&gt;, SortedSet&lt;T&gt;.</summary>
    Set,

    /// <summary>Queue&lt;T&gt;, Stack&lt;T&gt;.</summary>
    Queue,

    /// <summary>LinkedList&lt;T&gt;.</summary>
    Stack,

    /// <summary>Any type with a Count property not matching a known pattern.</summary>
    Other
}
