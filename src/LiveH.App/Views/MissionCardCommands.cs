using LiveH.Contracts.Groups;
using LiveH.Contracts.Missions;

namespace LiveH.App.Views;

public enum MissionCardCommand { Track, Edit, Postpone, Complete, Assign, Delete }

// A single application-layer router owns mission-card commands in all surfaces.
// Neither GroupsOS nor TodayOS directly mutates the MissionOS data store.
public sealed class MissionCardCommands
{
    private readonly ContentPage _host;
    private readonly IMissionRepository _missions;
    private readonly IGroupRepository _groups;
    private readonly Func<MissionRecord, Task> _track;

    public MissionCardCommands(ContentPage host, IMissionRepository missions,
        IGroupRepository groups, Func<MissionRecord, Task> track)
    {
        _host = host;
        _missions = missions;
        _groups = groups;
        _track = track;
    }

    public async Task ExecuteAsync(MissionRecord mission, MissionCardCommand action, Func<Task> reload)
    {
        try
        {
            var changed = false;
            switch (action)
            {
                case MissionCardCommand.Track:
                    await _track(mission);
                    return;
                case MissionCardCommand.Edit:
                {
                    var editor = new MissionEditorPage(mission, _missions,
                        await _groups.ListAsync());
                    await _host.Navigation.PushModalAsync(editor);
                    changed = await editor.Completion;
                    break;
                }
                case MissionCardCommand.Postpone:
                {
                    var picked = await _host.DisplayActionSheetAsync("Odroczyć misję", "ANULUJ", null,
                        "JUTRO", "WYBIERZ DATĘ");
                    DateOnly? day = picked switch
                    {
                        "JUTRO" => DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                        "WYBIERZ DATĘ" => await PickDateAsync(),
                        _ => null
                    };
                    if (day is { } target)
                    {
                        await _missions.RescheduleAsync(mission.Id, target);
                        changed = true;
                    }
                    break;
                }
                case MissionCardCommand.Complete:
                    await _missions.CompleteAsync(mission.Id);
                    changed = true;
                    break;
                case MissionCardCommand.Assign:
                {
                    var catalog = await _groups.ListAsync();
                    var groups = catalog.Where(g => IsAssignable(catalog, g.Id))
                        .OrderBy(g => GroupHierarchy.PathFor(catalog, g.Id)).ToArray();
                    var values = groups.Select(g => GroupHierarchy.PathFor(catalog, g.Id)).ToArray();
                    var labels = new[] { "BEZ GRUPY" }.Concat(values).ToArray();
                    var selected = await _host.DisplayActionSheetAsync("Przypisz grupę", "ANULUJ", null, labels);
                    if (selected is null || selected == "ANULUJ") break;
                    var index = Array.IndexOf(labels, selected);
                    if (index < 0) break;
                    var group = index == 0 ? null : groups[index - 1];
                    await _missions.AssignGroupAsync(mission.Id, group?.Id, group?.Name);
                    changed = true;
                    break;
                }
                case MissionCardCommand.Delete:
                    if (await _host.DisplayAlertAsync("Usunąć misję?", mission.Title, "USUŃ", "ANULUJ"))
                    {
                        await _missions.DeleteAsync(mission.Id);
                        changed = true;
                    }
                    break;
            }
            if (changed) await reload();
        }
        catch (Exception ex)
        {
            await _host.DisplayAlertAsync("MISSION OS", "Nie udało się wykonać operacji: " + ex.Message, "OK");
        }
    }

    private static bool IsAssignable(IReadOnlyList<GroupRecord> groups, Guid id)
    {
        var visited = new HashSet<Guid>();
        Guid? parent = id;
        while (parent is { } groupId && visited.Add(groupId))
        {
            var group = groups.FirstOrDefault(g => g.Id == groupId);
            if (group is null || group.IsArchived) return false;
            parent = group.ParentGroupId;
        }
        return parent is null;
    }

    private async Task<DateOnly?> PickDateAsync()
    {
        var picker = new DatePicker { Date = DateTime.Today.AddDays(1),
            MinimumDate = DateTime.Today, Format = "dd.MM.yyyy",
            TextColor = Color.FromArgb("#EEF4FA") };
        var completion = new TaskCompletionSource<DateOnly?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new ContentPage { BackgroundColor = Color.FromArgb("#05090E"), Title = "ODROCZ MISJĘ" };
        page.Disappearing += (_, _) => completion.TrySetResult(null);
        var stack = new VerticalStackLayout { Padding = new Thickness(22, 38), Spacing = 20 };
        stack.Children.Add(new Label { Text = "WYBIERZ NOWY TERMIN", FontSize = 18,
            TextColor = Color.FromArgb("#20D8F2"), FontAttributes = FontAttributes.Bold });
        stack.Children.Add(picker);
        var confirm = new Button { Text = "ODROCZ NA TEN DZIEŃ" };
        confirm.Clicked += async (_, _) =>
        {
            var day = DateOnly.FromDateTime(picker.Date ?? DateTime.Today);
            completion.TrySetResult(day);
            await page.Navigation.PopModalAsync();
        };
        var cancel = new Button { Text = "ANULUJ" };
        cancel.Clicked += async (_, _) =>
        {
            completion.TrySetResult(null);
            await page.Navigation.PopModalAsync();
        };
        stack.Children.Add(confirm);
        stack.Children.Add(cancel);
        page.Content = stack;
        await _host.Navigation.PushModalAsync(page);
        return await completion.Task;
    }
}
