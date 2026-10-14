using System;
using System.Drawing;
using System.Windows.Forms;

namespace MySFformat
{
    internal static class EditorWindowPlacement
    {
        internal static void ShowCentered(Form window, Form owner)
        {
            if (window.Visible) return; // Preserve the user's position while already open.
            window.StartPosition = FormStartPosition.Manual;
            // Load runs after WinForms autoscaling; Show(owner) alone does not center
            // modeless windows. Also handle subsequent Show calls after Hide.
            EventHandler placeOnLoad = (s, e) => Center(window, owner);
            window.Load += placeOnLoad;
            try
            {
                Center(window, owner);
                window.Show(owner);
                Center(window, owner);
            }
            finally
            {
                window.Load -= placeOnLoad;
            }
        }

        private static void Center(Form window, Form owner)
        {
            Rectangle anchor = owner.WindowState == FormWindowState.Minimized ? owner.RestoreBounds : owner.Bounds;
            Rectangle area = Screen.FromRectangle(anchor).WorkingArea;
            // An oversized MinimumSize would otherwise prevent fitting a small display.
            window.MinimumSize = new Size(Math.Min(window.MinimumSize.Width, area.Width),
                Math.Min(window.MinimumSize.Height, area.Height));
            window.Bounds = CalculateBounds(anchor, window.Size, area);
        }

        internal static Rectangle CalculateBounds(Rectangle anchor, Size size, Rectangle area)
        {
            int width = Math.Min(size.Width, area.Width);
            int height = Math.Min(size.Height, area.Height);
            int x = anchor.Left + (anchor.Width - width) / 2;
            int y = anchor.Top + (anchor.Height - height) / 2;
            x = Math.Max(area.Left, Math.Min(x, area.Right - width));
            y = Math.Max(area.Top, Math.Min(y, area.Bottom - height));
            return new Rectangle(x, y, width, height);
        }
    }
}
