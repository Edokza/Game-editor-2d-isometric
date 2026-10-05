using System.Numerics;
using GameEditor.Application;
using ImGuiNET;

namespace GameEditor.Editor;

public sealed class FileMenu(MapEditingService service)
{
    private const string SaveAsPopup = "Save As";
    private bool _openSaveAs;
    private string _name = "";
    private IReadOnlyList<string> _existing = [];
    private Result? _status;

    /// <summary>Call between BeginMainMenuBar/EndMainMenuBar.</summary>
    public void DrawMenu()
    {
        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.BeginMenu("Open"))
            {
                var maps = service.ListMaps();
                if (maps.Count == 0) ImGui.TextDisabled("(no saved maps)");
                foreach (var m in maps)
                    if (ImGui.MenuItem(m, "", m == service.CurrentName)) _status = service.Load(m);
                ImGui.EndMenu();
            }
            if (ImGui.MenuItem("Save"))
            {
                if (service.CurrentName is { } n) _status = service.Save(n);
                else _openSaveAs = true;
            }
            if (ImGui.MenuItem("Save As...")) _openSaveAs = true;
            ImGui.EndMenu();
        }

        ImGui.TextDisabled(service.CurrentName ?? "(unsaved)");
        if (_status is { } s)
            ImGui.TextColored(s.Success ? new Vector4(0.5f, 1, 0.5f, 1) : new Vector4(1, 0.4f, 0.4f, 1), s.Message);
    }

    /// <summary>Call outside the menu bar (popup IDs must match where OpenPopup runs).</summary>
    public void DrawPopups()
    {
        if (_openSaveAs)
        {
            _openSaveAs = false;
            _name = service.CurrentName ?? "";
            _existing = service.ListMaps();
            ImGui.OpenPopup(SaveAsPopup);
        }

        bool open = true;
        if (!ImGui.BeginPopupModal(SaveAsPopup, ref open, ImGuiWindowFlags.AlwaysAutoResize)) return;

        if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
        bool submit = ImGui.InputText("Name", ref _name, 64, ImGuiInputTextFlags.EnterReturnsTrue);
        var name = _name.Trim();
        bool exists = _existing.Contains(name);
        if (exists) ImGui.TextColored(new Vector4(1, 0.8f, 0.3f, 1), $"'{name}' already exists. Overwrite?");

        submit |= ImGui.Button(exists ? "Overwrite" : "Save");
        ImGui.SameLine();
        if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();

        if (submit && name.Length > 0)
        {
            _status = service.Save(name);
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }
}
