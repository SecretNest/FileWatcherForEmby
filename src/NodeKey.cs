namespace SecretNest.FileWatcherForEmby;

public sealed class NodeKey(int id, int parentId) : IEquatable<NodeKey>
{
    public int Id { get; } = id;
    public int ParentId { get; } = parentId;
        
    public override bool Equals(object? obj)
    {
        return Equals(obj as NodeKey);
    }
        
    public bool Equals(NodeKey? other)
    {
        if (ReferenceEquals(other, null))
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Id == other.Id && ParentId == other.ParentId;
    }
        
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + Id;
            hash = hash * 31 + ParentId;
            return hash;
        }
    }
        
    public static bool operator ==(NodeKey left, NodeKey right)
    {
        if (ReferenceEquals(left, null))
            return ReferenceEquals(right, null);
        return left.Equals(right);
    }
        
    public static bool operator !=(NodeKey left, NodeKey right)
    {
        return !(left == right);
    }
    
    public override string ToString()
    {
        return $"Id: {Id}, ParentId: {ParentId}";
    }
}