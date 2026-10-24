namespace LiveH.Contracts.Groups;

// Hierarchy navigation through public contracts only.
public static class GroupHierarchy
{
    public static bool IsDescendantOrSelf(IReadOnlyList<GroupRecord> groups,
        Guid candidateId, Guid parentId)
    {
        var seen = new HashSet<Guid>();
        Guid? cursor = candidateId;
        while (cursor is { } id)
        {
            if (!seen.Add(id)) return false;
            if (id == parentId) return true;
            cursor = groups.FirstOrDefault(g => g.Id == id)?.ParentGroupId;
        }
        return false;
    }

    public static string PathFor(IReadOnlyList<GroupRecord> groups, Guid groupId)
    {
        var parts = new List<string>();
        var seen = new HashSet<Guid>();
        Guid? cursor = groupId;
        while (cursor is { } id && seen.Add(id))
        {
            var group = groups.FirstOrDefault(g => g.Id == id);
            if (group is null) break;
            parts.Add(group.Name);
            cursor = group.ParentGroupId;
        }
        parts.Reverse();
        return string.Join(" / ", parts);
    }
}
