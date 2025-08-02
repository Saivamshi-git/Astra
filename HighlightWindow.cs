// HighlightWindow.cs
using System.Drawing;
using System.Windows.Forms;

namespace DesktopElementInspector
{
    /// <summary>
    /// A transparent, screen-wide, top-most window used to draw highlight rectangles.
    /// This version is multi-monitor aware.
    /// </summary>
    public class HighlightWindow : Form
    {
        private readonly List<Rectangle> _rectanglesToDraw = new();

        public HighlightWindow(List<Rectangle> rectangles)
        {
            _rectanglesToDraw.AddRange(rectangles);

            // --- FIX 1: Cover all monitors ---
            // Get the bounding box of the entire virtual screen (all monitors combined).
            Rectangle virtualScreen = SystemInformation.VirtualScreen;

            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = virtualScreen; // Force the form to span all displays.
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.BackColor = Color.Magenta;
            this.TransparencyKey = this.BackColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_rectanglesToDraw.Count == 0) return;
            
            using (var redPen = new Pen(Color.Red, 3))
            {
                foreach (var screenRect in _rectanglesToDraw)
                {
                    // --- FIX 2: Translate screen coordinates to window-local coordinates ---
                    // This ensures drawing is correct even if the primary monitor is not the top-left one.
                    var clientRect = new Rectangle(
                        screenRect.X - this.Left,
                        screenRect.Y - this.Top,
                        screenRect.Width,
                        screenRect.Height
                    );

                    e.Graphics.DrawRectangle(redPen, clientRect);
                }
            }
        }
    }
}