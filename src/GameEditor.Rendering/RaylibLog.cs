using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Raylib_cs;

namespace GameEditor.Rendering;

/// <summary>Routes raylib's log to the debugger Output window (WinExe has no console; Debug.WriteLine is stripped in Release).</summary>
public static unsafe class RaylibLog
{
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Callback(int level, sbyte* text, sbyte* args) =>
        Debug.WriteLine(Logging.GetLogMessage((nint)text, (nint)args), ((TraceLogLevel)level).ToString());

    /// <summary>Call before InitWindow so its log is captured too.</summary>
    public static void Install() => Raylib.SetTraceLogCallback(&Callback);
}
