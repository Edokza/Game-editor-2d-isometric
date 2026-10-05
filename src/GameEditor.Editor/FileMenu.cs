using System.Numerics;
using GameEditor.Application;
using ImGuiNET;

namespace GameEditor.Editor;

public sealed class FileMenu(MapEditingService service)
{
    private const string SaveAsPopup = "Save As";
    private const string DiscardPopup = "Unsaved changes";
    private bool _openSaveAs;
    private bool _openDiscard;
    private Action? _onDiscard;
    private string _name = "";
    private IReadOnlyList<string> _existing = [];
    private Result? _status;

    /// <summary>Runs <paramref name="onDiscard"/> now if clean, else after the user confirms.</summary>
    public void ConfirmDiscard(Action onDiscard)
    {
        if (!service.IsDirty)
        {
            onDiscard();
            return;
        }
        _onDiscard = onDiscard;
        _openDiscard = true;
    }

    /// <summary>Call between BeginMainMenuBar/EndMainMenuBar.</summary>
    public void DrawMenu()
    {
        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.BeginMenu("Open"))
            {
                if (ImGui.IsWindowAppearing()) _existing = service.ListMaps(); // disk I/O once per open, not per frame
                if (_existing.Count == 0) ImGui.TextDisabled("(no saved maps)");
                foreach (var m in _existing)
                    if (ImGui.MenuItem(m, "", m == service.CurrentName)) ConfirmDiscard(() => _status = service.Load(m));
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

        ImGui.TextDisabled(Esc(service.CurrentName ?? "(unsaved)"));
        if (service.IsDirty)
        {
            ImGui.SameLine(0, 0);
            ImGui.TextDisabled("*");
        }
        if (_status is { } s)
            ImGui.TextColored(s.Success ? new Vector4(0.5f, 1, 0.5f, 1) : new Vector4(1, 0.4f, 0.4f, 1), Esc(s.Message));
    }

    /// <summary>Call outside the menu bar (popup IDs must match where OpenPopup runs).</summary>
    public void DrawPopups()
    {
        DrawSaveAs();
        DrawDiscard();
    }

    private void DrawSaveAs()
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
        bool exists = _existing.Contains(name, StringComparer.OrdinalIgnoreCase); // Windows file names ignore case
        if (exists) ImGui.TextColored(new Vector4(1, 0.8f, 0.3f, 1), Esc($"'{name}' already exists. Overwrite?"));

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

    private void DrawDiscard()
    {
        if (_openDiscard)
        {
            _openDiscard = false;
            ImGui.OpenPopup(DiscardPopup);
        }

        bool open = true;
        if (!ImGui.BeginPopupModal(DiscardPopup, ref open, ImGuiWindowFlags.AlwaysAutoResize)) return;

        ImGui.TextUnformatted("Discard unsaved changes?");
        if (ImGui.Button("Discard"))
        {
            _onDiscard?.Invoke();
            _onDiscard = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            _onDiscard = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    /// <summary>ImGui Text* treat the string as a printf format; user names may contain '%'.</summary>
    private static string Esc(string s) => s.Replace("%", "%%");
}
