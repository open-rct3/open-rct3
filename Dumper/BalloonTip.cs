using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Dumper.Controls;

[NativeMarshalling(typeof(BalloonTipMarshller))]
public struct BalloonTipContent {
  public string Title;
  public string Text;
  public BalloonIcon Icon;
}

public enum BalloonIcon : int {
  None = 0,
  Info = 1,
  Warning = 2,
  Error = 3,
}

public static partial class BalloonTip {
  private const int EM_SHOWBALLOONTIP = 0x1503;
  private const int EM_HIDEBALLOONTIP = 0x1504;

  [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
  private static partial IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, BalloonTipContent lParam);

  [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
  private static partial IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

  public static void Show(IntPtr hwnd, BalloonTipContent content) {
    SendMessage(hwnd, EM_SHOWBALLOONTIP, IntPtr.Zero, content);
  }

  public static void Hide(IntPtr hwnd) {
    SendMessage(hwnd, EM_HIDEBALLOONTIP, IntPtr.Zero, IntPtr.Zero);
  }
}

[CustomMarshaller(typeof(BalloonTipContent), MarshalMode.ManagedToUnmanagedIn, typeof(BalloonTipMarshller))]
internal static class BalloonTipMarshller {
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  internal struct Unmanaged {
    public int cbStruct;
    public string pszTitle;
    public string pszText;
    public int ttiIcon;
  }

  public static nint ConvertToUnmanaged(BalloonTipContent managed) {
    var unmanaged = new Unmanaged {
      cbStruct = Marshal.SizeOf(typeof(Unmanaged)),
      pszTitle = managed.Title,
      pszText = managed.Text,
      ttiIcon = Convert.ToInt32(managed.Icon)
    };

    IntPtr ptr = Marshal.AllocHGlobal(unmanaged.cbStruct);
    Marshal.StructureToPtr(unmanaged, ptr, false);
    return ptr;
  }

  public static void Free(nint ptr) => Marshal.FreeHGlobal(ptr);
}
