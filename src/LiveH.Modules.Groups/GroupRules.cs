using LiveH.Contracts.Groups;

namespace LiveH.Modules.Groups;

// No MAUI, MissionOS, CampusOS or storage dependencies in these rules.
public static class GroupRules
{
    public static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80)
            throw new ArgumentException("Nazwa grupy musi mieć od 1 do 80 znaków.");
        return name.Trim();
    }

    public static void ValidatePlacement(IReadOnlyList<GroupRecord> groups,
        Guid? movingId, Guid? newParentId, string name)
    {
        name = ValidateName(name);
        if (groups.Any(g => g.Id != movingId && g.ParentGroupId == newParentId &&
                string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Grupa o tej nazwie już istnieje w tym miejscu.");
        if (newParentId is not { } parentId) return;
        if (!groups.Any(g => g.Id == parentId))
            throw new KeyNotFoundException("Nie znaleziono grupy nadrzędnej.");
        var seen = new HashSet<Guid>();
        Guid? cursor = parentId;
        while (cursor is { } id)
        {
            if (!seen.Add(id) || movingId == id)
                throw new InvalidOperationException("Grupa nie może zawierać samej siebie ani tworzyć cyklu.");
            cursor = groups.First(g => g.Id == id).ParentGroupId;
        }
    }

}
