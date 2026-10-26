using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace GameEditor.Editor;

/// <summary>Default panel layout. ImGui.NET has no DockBuilder wrapper; the bundled cimgui exports it.</summary>
public static class DockLayout
{
    private const ImGuiDockNodeFlags DockSpaceFlag = (ImGuiDockNodeFlags)(1 << 10); // ImGuiDockNodeFlags_DockSpace (internal)

    /// <summary>Assets left, Hierarchy/Inspector stacked right, Scene in the central node.</summary>
    public static void Build(uint dockspaceId, Vector2 size)
    {
        igDockBuilderRemoveNode(dockspaceId);
        igDockBuilderAddNode(dockspaceId, DockSpaceFlag | ImGuiDockNodeFlags.PassthruCentralNode);
        igDockBuilderSetNodeSize(dockspaceId, size);
        // the opposite side of each split keeps the central-node flag
        igDockBuilderSplitNode(dockspaceId, ImGuiDir.Left, 0.20f, out uint left, out uint rest);
        igDockBuilderSplitNode(rest, ImGuiDir.Right, 0.22f / 0.80f, out uint right, out uint center);
        igDockBuilderSplitNode(right, ImGuiDir.Up, 0.5f, out uint top, out uint bottom);
        igDockBuilderDockWindow("Assets", left);
        igDockBuilderDockWindow("Hierarchy", top);
        igDockBuilderDockWindow("Inspector", bottom);
        igDockBuilderDockWindow("Scene", center);
        igDockBuilderFinish(dockspaceId);
    }

    /// <summary>No split yet: no imgui.ini, or one without this dockspace's layout. Valid after the first NewFrame (ini loads there).</summary>
    public static bool IsEmpty(uint dockspaceId)
    {
        var node = igDockBuilderGetNode(dockspaceId);
        return node == 0 || Marshal.ReadIntPtr(node, 24 + IntPtr.Size) == 0; // ImGuiDockNode.ChildNodes[0]: after 6 ints + ParentNode
    }

    [DllImport("cimgui")] private static extern void igDockBuilderRemoveNode(uint nodeId);
    [DllImport("cimgui")] private static extern uint igDockBuilderAddNode(uint nodeId, ImGuiDockNodeFlags flags);
    [DllImport("cimgui")] private static extern void igDockBuilderSetNodeSize(uint nodeId, Vector2 size);
    [DllImport("cimgui")] private static extern uint igDockBuilderSplitNode(uint nodeId, ImGuiDir splitDir, float sizeRatioForNodeAtDir, out uint outIdAtDir, out uint outIdAtOppositeDir);
    [DllImport("cimgui")] private static extern void igDockBuilderDockWindow(string windowName, uint nodeId);
    [DllImport("cimgui")] private static extern void igDockBuilderFinish(uint nodeId);
    [DllImport("cimgui")] private static extern nint igDockBuilderGetNode(uint nodeId);
}
