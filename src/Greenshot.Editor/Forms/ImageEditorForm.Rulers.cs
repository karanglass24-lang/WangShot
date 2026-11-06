using System;
using System.Drawing;
using System.Windows.Forms;
using Greenshot.Editor.Controls;

namespace Greenshot.Editor.Forms
{
    /// <summary>
    /// 왕샷: the rulers on the top and the left of the canvas
    /// </summary>
    public partial class ImageEditorForm
    {
        private EditorRuler _rulerTop;
        private EditorRuler _rulerLeft;
        private Panel _rulerCorner;
        private Timer _rulerTimer;
        private bool _rulersShown = true;

        /// <summary>
        /// The table around the canvas gets a row and a column for the rulers
        /// </summary>
        private void BuildRulers()
        {
            if (_rulerTop != null)
            {
                return;
            }

            int size = LogicalToDeviceUnits(22);
            _rulerTop = new EditorRuler(horizontal: true) { Dock = DockStyle.Fill, Margin = Padding.Empty };
            _rulerLeft = new EditorRuler(horizontal: false) { Dock = DockStyle.Fill, Margin = Padding.Empty };
            _rulerCorner = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            var table = tableLayoutPanel1;
            table.SuspendLayout();
            table.ColumnStyles.Clear();
            table.RowStyles.Clear();
            table.ColumnCount = 2;
            table.RowCount = 2;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, size));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, size));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            table.SetCellPosition(panel1, new TableLayoutPanelCellPosition(1, 1));
            panel1.Margin = Padding.Empty;
            table.Controls.Add(_rulerCorner, 0, 0);
            table.Controls.Add(_rulerTop, 1, 0);
            table.Controls.Add(_rulerLeft, 0, 1);
            table.ResumeLayout(true);
            ApplyRulerTheme();

            _rulerTimer = new Timer { Interval = 50 };
            _rulerTimer.Tick += (s, e) => UpdateRulers();
            _rulerTimer.Start();
            FormClosed += (s, e) =>
            {
                _rulerTimer.Stop();
                _rulerTimer.Dispose();
            };
        }

        private void ApplyRulerTheme()
        {
            if (_rulerCorner != null)
            {
                _rulerCorner.BackColor = EditorTheme.Current.Background;
            }
        }

        /// <summary>
        /// Show or hide the rulers
        /// </summary>
        private void SetRulersShown(bool shown)
        {
            if (_rulerTop == null || shown == _rulersShown)
            {
                return;
            }

            _rulersShown = shown;
            int size = shown ? LogicalToDeviceUnits(22) : 0;
            var table = tableLayoutPanel1;
            table.SuspendLayout();
            table.ColumnStyles[0] = new ColumnStyle(SizeType.Absolute, size);
            table.RowStyles[0] = new RowStyle(SizeType.Absolute, size);
            _rulerTop.Visible = shown;
            _rulerLeft.Visible = shown;
            _rulerCorner.Visible = shown;
            table.ResumeLayout(true);
        }

        /// <summary>
        /// The rulers follow the canvas: scroll, zoom and the mouse
        /// </summary>
        private void UpdateRulers()
        {
            if (_palette != null && _surface != null && !IsDisposed)
            {
                var lineField = _surface.FieldAggregator.GetField(Greenshot.Editor.Drawing.Fields.FieldType.LINE_COLOR);
                var fillField = _surface.FieldAggregator.GetField(Greenshot.Editor.Drawing.Fields.FieldType.FILL_COLOR);
                if (lineField?.Value is Color line && fillField?.Value is Color fill)
                {
                    _palette.SetColors(line, fill);
                }
            }

            if (!_rulersShown || _rulerTop == null || _surface == null || IsDisposed || !Visible || _surface.Image == null)
            {
                return;
            }

            var image = _surface.Image;
            double zoom = image.Width > 0 ? _surface.Width / (double)image.Width : 1;
            var origin = _surface.PointToScreen(Point.Empty);
            var cursor = Cursor.Position;
            bool over = panel1.RectangleToScreen(panel1.ClientRectangle).Contains(cursor);
            _rulerTop.Set(_rulerTop.PointToClient(origin).X, zoom, image.Width, over ? _rulerTop.PointToClient(cursor).X : -1);
            _rulerLeft.Set(_rulerLeft.PointToClient(origin).Y, zoom, image.Height, over ? _rulerLeft.PointToClient(cursor).Y : -1);
        }
    }
}
