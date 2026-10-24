using LiveH.Contracts.Groups;
using LiveH.Contracts.Missions;

namespace LiveH.App.Views;

// Presentation composition ONLY: this view joins two public contracts, never module internals.
// GroupsOS owns its hierarchy, MissionOS owns its mission records.
public sealed class GroupsWorkspaceView : ContentView
{
    private readonly IGroupRepository _groupsRepository;
    private readonly IMissionRepository _missionsRepository;
    private readonly Func<Guid, Task> _createMission;
    private readonly Func<Task> _changed;
    private readonly VerticalStackLayout _body = new() { Spacing = 9 };
    private IReadOnlyList<GroupRecord> _groups = Array.Empty<GroupRecord>();
    private IReadOnlyList<MissionRecord> _missions = Array.Empty<MissionRecord>();
    private Guid? _currentId;
    private bool _creating;
    private bool _editing;
    private bool _busy;
    private string? _error;

    private static readonly Color Cyan = Color.FromArgb("#20D8F2");
    private static readonly Color Text = Color.FromArgb("#EEF4FA");
    private static readonly Color Muted = Color.FromArgb("#8799A9");
    private static readonly Color Rose = Color.FromArgb("#F7A6B6");

    public GroupsWorkspaceView(IGroupRepository groupsRepository,
        IMissionRepository missionsRepository, Func<Guid, Task> createMission,
        Func<Task> changed)
    {
        _groupsRepository = groupsRepository;
        _missionsRepository = missionsRepository;
        _createMission = createMission;
        _changed = changed;
        Content = new Border
        {
            Stroke = Color.FromArgb("#335365"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
            BackgroundColor = Color.FromArgb("#08141D"),
            Padding = new Thickness(12),
            Content = _body
        };
    }

    public async Task RefreshAsync()
    {
        _groups = await _groupsRepository.ListAsync();
        _missions = await _missionsRepository.ListAsync();
        if (_currentId is { } id && !_groups.Any(g => g.Id == id))
            _currentId = null;
        Render();
    }

    private void Render()
    {
        _body.Children.Clear();
        var current = _groups.FirstOrDefault(g => g.Id == _currentId);
        _body.Children.Add(new Label
        {
            Text = "GROUP OS // PRZESTRZENIE", FontSize = 11,
            FontAttributes = FontAttributes.Bold, TextColor = Cyan
        });
        var breadcrumb = new HorizontalStackLayout { Spacing = 8 };
        if (current is not null)
        {
            var back = Button("← WRÓĆ", Muted);
            back.Clicked += (_, _) => { _currentId = current.ParentGroupId; _editing = false; _creating = false; Render(); };
            breadcrumb.Children.Add(back);
        }
        breadcrumb.Children.Add(new Label
        {
            Text = current is null ? "TWOJE GRUPY" : GroupHierarchy.PathFor(_groups, current.Id),
            FontSize = 15, FontAttributes = FontAttributes.Bold,
            TextColor = Text, VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.WordWrap
        });
        _body.Children.Add(breadcrumb);

        if (_error is not null)
            _body.Children.Add(new Label { Text = _error, FontSize = 12, TextColor = Rose });

        var ancestorsArchived = current is not null && IsArchivedEffective(current.Id);
        var create = Button(current is null ? "+ NOWA GRUPA" : "+ PODGRUPA", Cyan);
        create.IsEnabled = !ancestorsArchived && !_busy;
        create.Clicked += (_, _) => { _creating = !_creating; _editing = false; Render(); };
        var actions = new HorizontalStackLayout { Spacing = 7 };
        actions.Children.Add(create);
        if (current is not null)
        {
            var rename = Button("EDYTUJ", Muted);
            rename.Clicked += (_, _) => { _editing = !_editing; _creating = false; Render(); };
            actions.Children.Add(rename);
            var archive = Button(current.IsArchived ? "PRZYWRÓĆ" : "ARCHIWIZUJ", current.IsArchived ? Cyan : Muted);
            archive.Clicked += async (_, _) => await ExecuteAsync(async () =>
            {
                await _groupsRepository.SetArchivedAsync(current.Id, !current.IsArchived);
            });
            actions.Children.Add(archive);
        }
        _body.Children.Add(actions);
        if (_creating) RenderCreate();
        if (_editing && current is not null) RenderEdit(current);

        var children = _groups.Where(g => g.ParentGroupId == _currentId)
            .OrderBy(g => g.IsArchived).ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var child in children)
        {
            var total = _missions.Count(m => m.GroupId is { } groupId &&
                GroupHierarchy.IsDescendantOrSelf(_groups, groupId, child.Id));
            var directSubgroups = _groups.Count(g => g.ParentGroupId == child.Id);
            var row = new Border
            {
                Stroke = Color.FromArgb("#263F4F"), StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
                BackgroundColor = Color.FromArgb("#0D1C26"), Padding = new Thickness(9, 8)
            };
            var open = Button($"▣ {child.Name}  ›", child.IsArchived ? Muted : Text);
            open.HorizontalOptions = LayoutOptions.Fill;
            open.Clicked += (_, _) => { _currentId = child.Id; _editing = false; _creating = false; Render(); };
            var stack = new VerticalStackLayout { Spacing = 0 };
            stack.Children.Add(open);
            stack.Children.Add(new Label
            {
                Text = $"{total} misji · {directSubgroups} podgrup" +
                    (child.IsArchived ? " · ARCHIWUM" : ""),
                FontSize = 10, TextColor = Muted, Margin = new Thickness(9, 0, 0, 0)
            });
            row.Content = stack;
            _body.Children.Add(row);
        }
        if (children.Length == 0)
            _body.Children.Add(new Label
            {
                Text = current is null ? "Nie masz jeszcze grup. Utwórz np. STUDIA."
                    : "Brak podgrup.", TextColor = Muted, FontSize = 11
            });

        if (current is null) return;
        _body.Children.Add(new Label
        {
            Text = "MISJE W TEJ GRUPIE", FontSize = 12,
            FontAttributes = FontAttributes.Bold, TextColor = Cyan,
            Margin = new Thickness(0, 9, 0, 0)
        });
        var direct = _missions.Where(m => m.GroupId == current.Id)
            .OrderBy(m => m.Status).ThenBy(m => m.Title).ToArray();
        if (direct.Length == 0)
            _body.Children.Add(new Label { Text = "Brak misji przypisanych bezpośrednio do tej grupy.",
                FontSize = 11, TextColor = Muted });
        foreach (var mission in direct)
        {
            _body.Children.Add(new Label
            {
                Text = (mission.Status == MissionStatus.Completed ? "✓ " : "□ ") + mission.Title,
                TextColor = mission.Status == MissionStatus.Completed ? Muted : Text,
                FontSize = 12, LineBreakMode = LineBreakMode.WordWrap
            });
        }
        var addMission = Button("+ MISJA W TEJ GRUPIE", Cyan);
        addMission.IsEnabled = !ancestorsArchived;
        addMission.Clicked += async (_, _) => await ExecuteAsync(() => _createMission(current.Id));
        _body.Children.Add(addMission);
    }

    private void RenderCreate()
    {
        var name = new Entry
        {
            Placeholder = "Nazwa nowej grupy", MaxLength = 80,
            TextColor = Text, PlaceholderColor = Muted, FontSize = 13
        };
        var description = new Editor
        {
            Placeholder = "Opis (opcjonalnie)", MaxLength = 2000,
            TextColor = Text, PlaceholderColor = Muted,
            FontSize = 12, AutoSize = EditorAutoSizeOption.TextChanges,
            HeightRequest = 65
        };
        _body.Children.Add(name);
        _body.Children.Add(description);
        var save = Button("UTWÓRZ", Cyan);
        save.Clicked += async (_, _) => await ExecuteAsync(async () =>
        {
            var group = await _groupsRepository.CreateAsync(name.Text ?? "", _currentId, description.Text);
            _currentId = group.Id;
            _creating = false;
        });
        _body.Children.Add(save);
    }

    private void RenderEdit(GroupRecord group)
    {
        var name = new Entry
        {
            Text = group.Name, MaxLength = 80, TextColor = Text,
            PlaceholderColor = Muted, FontSize = 13
        };
        _body.Children.Add(name);
        var rename = Button("ZAPISZ NAZWĘ", Cyan);
        rename.Clicked += async (_, _) => await ExecuteAsync(async () =>
        {
            await _groupsRepository.RenameAsync(group.Id, name.Text ?? "");
            _editing = false;
        });
        _body.Children.Add(rename);
        _body.Children.Add(new Label { Text = "PRZENIEŚ DO", FontSize = 10, TextColor = Muted });
        var destinations = _groups.Where(g => g.Id != group.Id &&
            !GroupHierarchy.IsDescendantOrSelf(_groups, g.Id, group.Id) && !IsArchivedEffective(g.Id))
            .OrderBy(g => GroupHierarchy.PathFor(_groups, g.Id)).ToArray();
        var picker = new Picker { Title = "Wybierz grupę nadrzędną", TextColor = Text, TitleColor = Muted };
        picker.Items.Add("POZIOM GŁÓWNY");
        foreach (var dest in destinations) picker.Items.Add(GroupHierarchy.PathFor(_groups, dest.Id));
        var currentParentIndex = group.ParentGroupId is { } parentId
            ? Array.FindIndex(destinations, g => g.Id == parentId) + 1 : 0;
        picker.SelectedIndex = currentParentIndex;
        _body.Children.Add(picker);
        var move = Button("PRZENIEŚ GRUPĘ", Muted);
        move.Clicked += async (_, _) => await ExecuteAsync(async () =>
        {
            var parentId = picker.SelectedIndex > 0 ? destinations[picker.SelectedIndex - 1].Id : (Guid?)null;
            await _groupsRepository.MoveAsync(group.Id, parentId);
            _editing = false;
        });
        _body.Children.Add(move);
    }

    private bool IsArchivedEffective(Guid groupId)
    {
        var seen = new HashSet<Guid>();
        Guid? currentId = groupId;
        while (currentId is { } id && seen.Add(id))
        {
            var group = _groups.FirstOrDefault(g => g.Id == id);
            if (group is null) return true;
            if (group.IsArchived) return true;
            currentId = group.ParentGroupId;
        }
        return false;
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        _error = null;
        try
        {
            await action();
            await _changed();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            Render();
        }
        finally { _busy = false; }
    }

    private static Button Button(string title, Color color) => new()
    {
        Text = title, TextColor = color, FontSize = 10,
        BackgroundColor = Color.FromArgb("#081019"),
        Padding = new Thickness(9, 6), HeightRequest = 36
    };
}
