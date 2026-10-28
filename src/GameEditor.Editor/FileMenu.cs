using System.Diagnostics;
using System.Numerics;
using GameEditor.Application;
using GameEditor.Infrastructure;
using ImGuiNET;

namespace GameEditor.Editor;

/// <param name="docs">Documents/GameEditor: holds maps/ (saves) and export/.</param>
public sealed class FileMenu(MapEditingService service, string docs)
{
    private const string SaveAsPopup = "Save As";
    private const string DiscardPopup = "Unsaved changes";
    private const string NewPopup = "New Map";
    private const string ResizePopup = "Resize Map";
    private const string OpenPopup = "Open Map";
    private static readonly string SizeHint = $"1-{MapEditingService.MaxSize} tiles per side"; // built once, not per frame
    private bool _openSaveAs;
    private bool _openNew;
    private bool _openResize;
    private bool _openOpen;
    private int _width, _height;
    private bool _openDiscard;
    private Action? _onDiscard;
    private string _name = "";
    private IReadOnlyList<string> _existing = [];
    private Result? _status;
    private bool _exportAfterSave;
    private (Result Status, string Folder)? _exported; // "Open folder" shows while _status is still this export's

    /// <summary>Set by View > Reset Layout; Program.cs rebuilds before the next DockSpaceOverViewport and clears it.</summary>
    public bool ResetLayout { get; set; }

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

    // shared by the menu and the Ctrl shortcuts in Program.cs
    public void New() => _openNew = true;
    public void Open() => _openOpen = true;
    public void SaveAs() => _openSaveAs = true;
    public void Save()
    {
        if (service.CurrentName is { } n) _status = service.Save(n);
        else _openSaveAs = true;
    }

    /// <summary>Call between BeginMainMenuBar/EndMainMenuBar.</summary>
    public void DrawMenu(ScenePanel scene, bool play)
    {
        ImGui.BeginDisabled(play); // edits would land on the editor map, invisible until Stop
        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.MenuItem("New...", "Ctrl+N")) New();
            if (ImGui.MenuItem("Open...", "Ctrl+O")) Open();
            if (ImGui.MenuItem("Save", "Ctrl+S")) Save();
            if (ImGui.MenuItem("Save As...", "Ctrl+Shift+S")) SaveAs();
            ImGui.Separator();
            if (ImGui.MenuItem("Export Game...")) ExportGame();
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("Edit"))
        {
            if (ImGui.MenuItem("Undo", "Ctrl+Z", false, service.CanUndo)) service.Undo();
            if (ImGui.MenuItem("Redo", "Ctrl+Y", false, service.CanRedo)) service.Redo();
            ImGui.Separator();
            if (ImGui.MenuItem("Resize Map...")) _openResize = true;
            ImGui.EndMenu();
        }
        ImGui.EndDisabled();
        if (ImGui.BeginMenu("View")) // usable during play: touches no map
        {
            if (ImGui.MenuItem("Grid", "", scene.Grid)) scene.Grid = !scene.Grid; // no shortcut: G is Fill
            if (ImGui.MenuItem("Rotate Left", "Q")) scene.Rotate(-1);
            if (ImGui.MenuItem("Rotate Right", "E")) scene.Rotate(1);
            ImGui.TextDisabled($"View angle {scene.Rotation * 90}°");
            if (ImGui.MenuItem("Reset Layout")) ResetLayout = true;
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
        if (_exported is { } e && _status == e.Status && ImGui.SmallButton("Open folder"))
        {
            try { Process.Start(new ProcessStartInfo(e.Folder) { UseShellExecute = true }); }
            catch (Exception ex) { _status = new(false, $"Open folder failed: {ex.Message}"); } // folder deleted meanwhile
        }
    }

    /// <summary>Exports the saved file, so unsaved or dirty maps are saved first (Save As when unnamed).</summary>
    private void ExportGame()
    {
        if (service.CurrentName is not { } name)
        {
            _exportAfterSave = true;
            _openSaveAs = true;
            return;
        }
        if (service.IsDirty && (_status = service.Save(name)) is { Success: false }) return;

        var outDir = Path.Combine(docs, "export");
        _status = GameExporter.Export(Path.Combine(AppContext.BaseDirectory, "player", "GameEditor.Player.exe"),
            Path.Combine(docs, "maps", name + ".json"), outDir, name);
        _exported = _status.Value.Success ? (_status.Value, Path.Combine(outDir, name)) : null;
    }

    /// <summary>Call outside the menu bar (popup IDs must match where OpenPopup runs).</summary>
    public void DrawPopups()
    {
        DrawSaveAs();
        DrawOpen();
        DrawSize(NewPopup, ref _openNew, resize: false);
        DrawSize(ResizePopup, ref _openResize, resize: true);
        DrawDiscard(); // after DrawSize: New's confirm opens from there
    }

    /// <summary>Width/height modal, defaults to the current map size.</summary>
    private void DrawSize(string id, ref bool openRequest, bool resize)
    {
        if (openRequest)
        {
            openRequest = false;
            (_width, _height) = (service.Map.Width, service.Map.Height);
            ImGui.OpenPopup(id);
        }

        bool open = true;
        if (!ImGui.BeginPopupModal(id, ref open, ImGuiWindowFlags.AlwaysAutoResize)) return;

        ImGui.InputInt("Width", ref _width);
        ImGui.InputInt("Height", ref _height);
        _width = Math.Clamp(_width, 1, MapEditingService.MaxSize);
        _height = Math.Clamp(_height, 1, MapEditingService.MaxSize);
        ImGui.TextDisabled(SizeHint);

        if (ImGui.Button("OK"))
        {
            // branch here, not a delegate parameter: DrawPopups runs every frame and must not allocate
            if (resize) service.Resize(_width, _height);
            else
            {
                (int w, int h) = (_width, _height);
                ConfirmDiscard(() => { service.New(w, h); _status = null; });
            }
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private void DrawOpen()
    {
        if (_openOpen)
        {
            _openOpen = false;
            _existing = service.ListMaps(); // disk I/O once per open, not per frame
            ImGui.OpenPopup(OpenPopup);
        }

        bool open = true;
        if (!ImGui.BeginPopupModal(OpenPopup, ref open, ImGuiWindowFlags.AlwaysAutoResize)) return;

        if (_existing.Count == 0) ImGui.TextDisabled("(no saved maps)");
        foreach (var m in _existing)
            if (ImGui.Selectable(m, m == service.CurrentName))
            {
                ConfirmDiscard(() => _status = service.Load(m));
                ImGui.CloseCurrentPopup();
            }
        if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
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
        if (!ImGui.BeginPopupModal(SaveAsPopup, ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            _exportAfterSave = false; // closed (Cancel or X) without saving: a later plain Save As must not export
            return;
        }

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
            if (_exportAfterSave && _status.Value.Success) ExportGame();
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
