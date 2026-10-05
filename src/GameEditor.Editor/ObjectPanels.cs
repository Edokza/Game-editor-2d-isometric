using System.Numerics;
using GameEditor.Application;
using GameEditor.Domain;
using GameEditor.Rendering;
using ImGuiNET;

namespace GameEditor.Editor;

/// <summary>Hierarchy + Inspector share the selection.</summary>
public sealed class ObjectPanels(MapEditingService service)
{
    private int? _selectedId;
    private TileMap? _selectedIn;
    private int _editToken;

    /// <summary>Null after New/Load (ids repeat across maps). May point at an object undo removed; look it up, never index by it.</summary>
    public int? SelectedId
    {
        get => _selectedIn == service.Map ? _selectedId : null;
        set => (_selectedId, _selectedIn) = (value, service.Map);
    }

    public void DrawHierarchy()
    {
        ImGui.SetNextWindowSize(new(220, 300), ImGuiCond.FirstUseEver);
        ImGui.Begin("Hierarchy");
        var map = service.Map;
        if (ImGui.Button("Add"))
            SelectedId = service.AddObject($"Object {map.NextObjectId}", map.Width / 2f, map.Height / 2f).Id;
        ImGui.SameLine();
        ImGui.BeginDisabled(SelectedId is not { } id || map.IndexOf(id) < 0);
        if (ImGui.Button("Delete") && SelectedId is { } del) service.RemoveObject(del);
        ImGui.EndDisabled();

        for (int k = 0; k < map.Objects.Count; k++) // not foreach: IReadOnlyList boxes its enumerator every frame
        {
            var o = map.Objects[k];
            ImGui.PushID(o.Id);
            // Selectable is not printf-formatted, '%' is safe; empty label would be unclickable
            if (ImGui.Selectable(o.Name.Length > 0 ? o.Name : "(unnamed)", o.Id == SelectedId)) SelectedId = o.Id;
            ImGui.PopID();
        }
        ImGui.End();
    }

    public void DrawInspector()
    {
        ImGui.SetNextWindowSize(new(260, 120), ImGuiCond.FirstUseEver);
        ImGui.Begin("Inspector");
        var map = service.Map;
        int i = SelectedId is { } id ? map.IndexOf(id) : -1;
        if (i < 0) ImGui.TextDisabled("(none)");
        else
        {
            var o = map.Objects[i];
            var name = o.Name;
            bool changed = ImGui.InputText("Name", ref name, 64);
            Track(o.Id, changed ? o with { Name = name } : null);

            o = map.Objects[i]; // re-read: the field above may have just changed it
            var pos = new Vector2(o.X, o.Y);
            changed = ImGui.DragFloat2("Position", ref pos, 0.05f);
            Track(o.Id, changed ? o with { X = pos.X, Y = pos.Y } : null);

            o = map.Objects[i];
            if (ImGui.BeginCombo("Sprite", o.Sprite ?? "(box)"))
            {
                var pick = o.Sprite;
                if (ImGui.Selectable("(box)", o.Sprite is null)) pick = null;
                foreach (var s in Sprites.Objects.Keys)
                    if (ImGui.Selectable(s, s == o.Sprite)) pick = s;
                ImGui.EndCombo();
                if (pick != o.Sprite)
                {
                    // a pick is instant, not a drag: one Begin/Update/End = one undo step
                    int token = service.BeginObjectEdit(o.Id);
                    service.UpdateObject(o with { Sprite = pick });
                    service.EndObjectEdit(token);
                }
            }
        }
        ImGui.End();
    }

    /// <summary>Call right after the widget: one undo step per activate → deactivate (whole drag / typing session).
    /// <paramref name="edited"/> null when unchanged, so idle frames don't allocate.</summary>
    private void Track(int id, MapObject? edited)
    {
        if (ImGui.IsItemActivated()) _editToken = service.BeginObjectEdit(id);
        if (edited is not null) service.UpdateObject(edited);
        if (ImGui.IsItemDeactivated()) service.EndObjectEdit(_editToken);
    }
}
