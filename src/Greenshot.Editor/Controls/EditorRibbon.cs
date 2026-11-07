using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Greenshot.Editor.Controls
{
    /// <summary>
    /// 왕샷: the look of the buttons of the ribbon: flat, rounded, with the accent colour for the pressed and checked state
    /// </summary>
    internal sealed class RibbonRenderer : ToolStripProfessionalRenderer
    {
        public RibbonRenderer()
        {
            RoundedEdges = false;
        }

        private static Color Blend(Color background, Color color, double amount) => Color.FromArgb(
            (int)(background.R + (color.R - background.R) * amount),
            (int)(background.G + (color.G - background.G) * amount),
            (int)(background.B + (color.B - background.B) * amount));

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(e.ToolStrip.BackColor);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void DrawButton(Graphics graphics, ToolStripItem item, bool isChecked)
        {
            if (item is ToolStripDropButton dropButton)
            {
                DrawDropButton(graphics, dropButton, isChecked);
                return;
            }

            DrawShape(graphics, item, new Rectangle(1, 1, item.Width - 3, item.Height - 3), isChecked, item.Selected && item.Enabled);
        }

        /// <summary>
        /// The upper part is the tool, the chevron under it has its own highlight
        /// </summary>
        private static void DrawDropButton(Graphics graphics, ToolStripDropButton item, bool isChecked)
        {
            var theme = EditorTheme.Current;
            var chevron = item.ChevronBounds;
            bool hotChevron = item.Selected && item.Enabled && item.ChevronHot;
            bool hotMain = item.Selected && item.Enabled && !item.ChevronHot;
            DrawShape(graphics, item, new Rectangle(1, 1, item.Width - 3, item.Height - chevron.Height - 1), isChecked, hotMain);
            if (hotChevron)
            {
                DrawShape(graphics, item, new Rectangle(1, item.Height - chevron.Height + 1, item.Width - 3, chevron.Height - 3), false, true);
            }

            using var pen = new Pen(item.Enabled ? theme.WindowText : theme.Muted, 1.5f);
            int cx = item.Width / 2;
            int cy = item.Height - chevron.Height / 2 - 1;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.DrawLines(pen, new[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
        }

        private static void DrawShape(Graphics graphics, ToolStripItem item, Rectangle bounds, bool isChecked, bool hot)
        {
            var theme = EditorTheme.Current;
            bool pressed = item.Pressed;
            if (!isChecked && !pressed && !hot)
            {
                return;
            }

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = pressed ? Blend(theme.Window, theme.Highlight, 0.30) : isChecked ? Blend(theme.Window, theme.Highlight, 0.18) : Blend(theme.Window, theme.Highlight, 0.10);
            using var path = Rounded(bounds, 5);
            using var brush = new SolidBrush(fill);
            graphics.FillPath(brush, path);
            if (isChecked || pressed)
            {
                using var pen = new Pen(Blend(theme.Window, theme.Highlight, 0.65));
                graphics.DrawPath(pen, path);
            }
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e) => DrawButton(e.Graphics, e.Item, (e.Item as ToolStripButton)?.Checked == true);

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e) => DrawButton(e.Graphics, e.Item, false);

        protected override void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs e)
        {
            DrawButton(e.Graphics, e.Item, false);
            if (e.Item is ToolStripSplitButton split)
            {
                var arrow = split.DropDownButtonBounds;
                using var pen = new Pen(split.Enabled ? EditorTheme.Current.WindowText : EditorTheme.Current.Muted, 1.5f);
                int cx = arrow.Left + arrow.Width / 2;
                int cy = arrow.Top + arrow.Height / 2 + 1;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawLines(pen, new[] { new Point(cx - 3, cy - 1), new Point(cx, cy + 2), new Point(cx + 3, cy - 1) });
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item.Enabled ? EditorTheme.Current.WindowText : EditorTheme.Current.Muted;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(EditorTheme.Current.Border);
            if (e.Vertical)
            {
                int x = e.Item.Width / 2;
                e.Graphics.DrawLine(pen, x, 4, x, e.Item.Height - 4);
            }
            else
            {
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 4, y, e.Item.Width - 4, y);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? EditorTheme.Current.WindowText : EditorTheme.Current.Muted;
            base.OnRenderItemText(e);
        }
    }

    /// <summary>
    /// 왕샷: a ribbon for the editor: a row for the quick access tools, a row of tabs, and under it the groups of the selected tab,
    /// each with a caption and a tool strip with large buttons. The buttons are the existing tool strip items of the editor, they are
    /// moved here (or have a twin which clicks them), so every handler keeps working.
    /// </summary>
    internal sealed class EditorRibbon : Panel
    {
        private sealed class RibbonTab
        {
            public string Title;
            public bool IsFile;
            public FlowLayoutPanel Body;
            public Rectangle Bounds;
        }

        private readonly List<RibbonTab> _tabs = new();
        private readonly List<ToolStrip> _strips = new();
        private readonly List<ToolStrip> _smallStrips = new();
        private readonly List<Panel> _groups = new();
        private readonly Dictionary<Panel, (ToolStrip Strip, Label Label)> _stripGroups = new();
        private readonly ToolStrip _headerRight;
        private readonly ToolStrip _quickAccess;
        private int _selected;
        private int _hot = -1;
        private MenuStrip _menu;
        private bool _collapsed;
        private bool _scrollVisible;
        private bool _collapseHot;
        private Rectangle _collapseBounds;

        public EditorRibbon()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            _headerRight = NewHeaderStrip();
            _quickAccess = NewHeaderStrip();
            _headerRight.SizeChanged += (s, e) => PlaceHeaderStrips();
            _quickAccess.SizeChanged += (s, e) => PlaceHeaderStrips();
            Controls.Add(_headerRight);
            Controls.Add(_quickAccess);
            _smallStrips.Add(_headerRight);
            _smallStrips.Add(_quickAccess);
        }

        private static ToolStrip NewHeaderStrip() => new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            CanOverflow = false,
            AutoSize = true,
            Dock = DockStyle.None,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        /// <summary>
        /// The tool strips of the groups, for the icon size
        /// </summary>
        public IReadOnlyList<ToolStrip> Strips => _strips;

        /// <summary>
        /// The tool strips with small icons: the quick access tools and the buttons on the right of the first row
        /// </summary>
        public IReadOnlyList<ToolStrip> SmallStrips => _smallStrips;

        /// <summary>
        /// The buttons on the right of the first row (settings, help)
        /// </summary>
        public ToolStrip HeaderRight => _headerRight;

        /// <summary>
        /// The quick access tools on the left of the first row
        /// </summary>
        public ToolStrip QuickAccess => _quickAccess;

        private int Scale(int value) => LogicalToDeviceUnits(value);

        private int QuickRowHeight => Scale(30);

        private int TabRowHeight => Scale(32);

        private int HeaderHeight => QuickRowHeight + TabRowHeight;

        /// <summary>
        /// Add a tab
        /// </summary>
        /// <returns>The index of the tab</returns>
        public int AddTab(string title, bool isFile = false)
        {
            // The groups lie in a panel which is as wide as they need, in a viewport: a narrow window doesn't get a scroll bar (it would
            // make the ribbon higher), the mouse wheel moves the groups instead
            var viewport = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, Visible = _tabs.Count == 0 };
            var body = new FlowLayoutPanel
            {
                Dock = DockStyle.None,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Location = Point.Empty,
                Margin = Padding.Empty,
                Padding = new Padding(Scale(4), Scale(2), 0, 0)
            };
            viewport.Controls.Add(body);
            viewport.Resize += (s, e) => ClampScroll(body);
            body.SizeChanged += (s, e) => ClampScroll(body);
            Controls.Add(viewport);
            _tabs.Add(new RibbonTab { Title = title, Body = body, IsFile = isFile });
            Padding = new Padding(Scale(8), HeaderHeight + Scale(2), Scale(8), Scale(8));
            _headerRight.BringToFront();
            _quickAccess.BringToFront();
            Invalidate();
            return _tabs.Count - 1;
        }

        /// <summary>
        /// Add a group with a caption to a tab
        /// </summary>
        /// <returns>The tool strip of the group: more items can be added to it later</returns>
        public ToolStrip AddGroup(int tabIndex, string caption)
        {
            var strip = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                CanOverflow = false,
                AutoSize = true,
                Dock = DockStyle.None,
                LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                Location = new Point(Scale(2), Scale(2))
            };
            _strips.Add(strip);
            BuildGroup(tabIndex, caption, strip);
            return strip;
        }

        /// <summary>
        /// Add a group which shows an existing tool strip (it leaves its old place). The group is hidden while no item of the strip is shown.
        /// </summary>
        public void AddStripGroup(int tabIndex, string caption, ToolStrip strip)
        {
            strip.Parent?.Controls.Remove(strip);
            strip.Dock = DockStyle.None;
            strip.Stretch = false;
            strip.AutoSize = true;
            strip.GripStyle = ToolStripGripStyle.Hidden;
            strip.CanOverflow = false;
            strip.Location = new Point(Scale(2), Scale(2));
            BuildGroup(tabIndex, caption, strip);
        }

        /// <summary>
        /// Add a group which holds a control of its own (the palette)
        /// </summary>
        public void AddControlGroup(int tabIndex, string caption, Control content)
        {
            var label = new Label { Text = caption, AutoSize = false, Dock = DockStyle.Bottom, Height = Scale(20), TextAlign = ContentAlignment.MiddleCenter, UseMnemonic = false };
            var group = new Panel { Margin = Padding.Empty };
            content.Location = new Point(Scale(2), Scale(2));
            group.Controls.Add(content);
            group.Controls.Add(label);
            group.Width = content.Width + Scale(8);
            group.Tag = content.Height + label.Height + Scale(8);
            group.Height = (int)group.Tag;
            group.Paint += (s, e) =>
            {
                using var pen = new Pen(EditorTheme.Current.Border);
                e.Graphics.DrawLine(pen, group.Width - 1, Scale(8), group.Width - 1, group.Height - Scale(8));
            };
            _tabs[tabIndex].Body.Controls.Add(group);
            _groups.Add(group);
            ApplyTheme(EditorTheme.Current);
            RecalculateHeight();
        }

        /// <summary>
        /// Add one group with the colour palette and, on its right, the options of the tool (the tool strip wraps into rows): they belong together
        /// </summary>
        public void AddPaletteGroup(int tabIndex, string caption, Control palette, ToolStrip options)
        {
            options.Parent?.Controls.Remove(options);
            options.Dock = DockStyle.None;
            options.GripStyle = ToolStripGripStyle.Hidden;
            options.CanOverflow = false;
            options.Stretch = false;
            options.AutoSize = false;
            options.LayoutStyle = ToolStripLayoutStyle.Flow;
            var label = new Label { Text = caption, AutoSize = false, Dock = DockStyle.Bottom, Height = Scale(20), TextAlign = ContentAlignment.MiddleCenter, UseMnemonic = false };
            var group = new Panel { Margin = Padding.Empty };
            palette.Location = new Point(Scale(2), Scale(2));
            options.Size = new Size(Scale(330), palette.Height);
            // Tidy rows like PicPick/Snagit: the same spacing everywhere, a label sits close to its field and the pairs keep a gap between them
            options.Padding = new Padding(Scale(2), Scale(2), 0, 0);
            void Tidy(ToolStripItem item)
            {
                item.Margin = item is ToolStripLabel && !(item is ToolStripButton) && !(item is ToolStripDropDownItem)
                    ? new Padding(Scale(8), Scale(3), Scale(2), Scale(3))
                    : new Padding(Scale(2), Scale(2), Scale(2), Scale(2));
                if (item is ToolStripLabel label2 && !(item is ToolStripButton))
                {
                    label2.TextAlign = ContentAlignment.MiddleLeft;
                }
            }

            foreach (ToolStripItem item in options.Items)
            {
                Tidy(item);
            }

            options.ItemAdded += (s, e) => Tidy(e.Item);
            options.Location = new Point(palette.Right + Scale(12), Scale(2));
            group.Controls.Add(palette);
            group.Controls.Add(options);
            group.Controls.Add(label);
            void Fit()
            {
                bool any = options.Items.Cast<ToolStripItem>().Any(i => i.Available);
                if (options.Visible != any)
                {
                    options.Visible = any;
                }

                int width = palette.Right + Scale(8) + (any ? Scale(4) + options.Width : 0);
                if (group.Width != width)
                {
                    group.Width = width;
                }

                group.Invalidate();
            }

            options.LayoutCompleted += (s, e) => Fit();
            group.Paint += (s, e) =>
            {
                using var pen = new Pen(EditorTheme.Current.Border);
                e.Graphics.DrawLine(pen, group.Width - 1, Scale(8), group.Width - 1, group.Height - Scale(8));
                if (options.Visible)
                {
                    // A thin line between the palette and the options
                    e.Graphics.DrawLine(pen, options.Left - Scale(7), Scale(10), options.Left - Scale(7), palette.Height - Scale(6));
                }
            };
            group.Tag = palette.Height + label.Height + Scale(8);
            group.Height = (int)group.Tag;
            Fit();
            _tabs[tabIndex].Body.Controls.Add(group);
            _groups.Add(group);
            ApplyTheme(EditorTheme.Current);
            RecalculateHeight();
        }
        private void BuildGroup(int tabIndex, string caption, ToolStrip strip)
        {
            var label = new Label
            {
                Text = caption,
                AutoSize = false,
                Dock = DockStyle.Bottom,
                Height = Scale(20),
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false
            };
            var group = new Panel { Margin = Padding.Empty, Height = Scale(96), Width = Scale(40) };
            group.Controls.Add(strip);
            group.Controls.Add(label);
            // The separator line on the right of the group
            group.Paint += (s, e) =>
            {
                using var pen = new Pen(EditorTheme.Current.Border);
                e.Graphics.DrawLine(pen, group.Width - 1, Scale(8), group.Width - 1, group.Height - Scale(8));
            };
            strip.LayoutCompleted += (s, e) => FitGroup(group, strip, label);
            strip.SizeChanged += (s, e) => FitGroup(group, strip, label);
            _tabs[tabIndex].Body.Controls.Add(group);
            _groups.Add(group);
            _stripGroups[group] = (strip, label);
            ApplyTheme(EditorTheme.Current);
        }

        private void FitGroup(Panel group, ToolStrip strip, Label label)
        {
            // A group without a shown item (e.g. the properties while nothing is selected) takes no room
            bool anyShown = strip.Items.Cast<ToolStripItem>().Any(i => i.Available);
            if (group.Visible != anyShown)
            {
                group.Visible = anyShown;
            }

            int width = Math.Max(strip.Width + Scale(6), TextRenderer.MeasureText(label.Text ?? string.Empty, label.Font).Width + Scale(12));
            int height = Math.Max(Scale(60), strip.Height + label.Height + Scale(6));
            if (group.Width != width)
            {
                group.Width = width;
            }

            group.Tag = height;
            RecalculateHeight();
        }

        /// <summary>
        /// The ribbon is as high as its tallest group (and room for the scroll bar)
        /// </summary>
        private void RecalculateHeight()
        {
            // The natural height of every group (also those on tabs which were never shown, they have no layout yet): the same on every tab
            int tallest = Scale(80);
            foreach (var group in _groups)
            {
                if (_stripGroups.TryGetValue(group, out var parts))
                {
                    int stripHeight = parts.Strip.GetPreferredSize(Size.Empty).Height;
                    foreach (ToolStripItem item in parts.Strip.Items)
                    {
                        // Items measure themselves also on a tab which is not shown
                        if (item.Available)
                        {
                            stripHeight = Math.Max(stripHeight, item.GetPreferredSize(Size.Empty).Height + item.Margin.Vertical + parts.Strip.Padding.Vertical);
                        }
                    }

                    group.Tag = Math.Max(Scale(60), stripHeight + parts.Label.Height + Scale(6));
                }

                tallest = Math.Max(tallest, group.Tag is int natural ? natural : group.Height);
            }

            foreach (var group in _groups)
            {
                // The same height: the captions of all groups stand in one line at the bottom
                if (group.Height != tallest)
                {
                    group.Height = tallest;
                }
            }

            int wanted = _collapsed ? HeaderHeight + Scale(6) : HeaderHeight + Scale(2) + tallest + Scale(12) + (_scrollVisible ? Scale(18) : 0);
            if (Height != wanted)
            {
                Height = wanted;
            }
        }

        private void PlaceHeaderStrips()
        {
            _quickAccess.Location = new Point(Scale(8), Math.Max(0, (QuickRowHeight - _quickAccess.Height) / 2));
            _headerRight.Location = new Point(Math.Max(0, Width - _headerRight.Width - Scale(8)), Math.Max(0, (QuickRowHeight - _headerRight.Height) / 2));
        }

        /// <summary>
        /// The menu bar of the editor stays in the editor (its shortcut keys work only while it is shown) but out of sight
        /// </summary>
        public void HostMenu(MenuStrip menu)
        {
            _menu = menu;
            menu.Parent?.Controls.Remove(menu);
            menu.Dock = DockStyle.None;
            menu.AutoSize = true;
            menu.GripStyle = ToolStripGripStyle.Hidden;
            menu.Location = new Point(-4000, 0);
            menu.TabStop = false;
            Controls.Add(menu);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            PlaceHeaderStrips();
        }

        private static void ClampScroll(FlowLayoutPanel body)
        {
            int min = Math.Min(0, body.Parent.ClientSize.Width - body.Width);
            int left = Math.Max(min, Math.Min(0, body.Left));
            if (left != body.Left)
            {
                body.Left = left;
            }
        }

        private sealed class WheelFilter : IMessageFilter
        {
            private readonly EditorRibbon _owner;

            public WheelFilter(EditorRibbon owner) => _owner = owner;

            public bool PreFilterMessage(ref Message m)
            {
                // WM_MOUSEWHEEL, WM_MOUSEHWHEEL
                if ((m.Msg != 0x020A && m.Msg != 0x020E) || _owner.IsDisposed || !_owner.Visible || _owner._collapsed)
                {
                    return false;
                }

                if (!_owner.RectangleToScreen(_owner.ClientRectangle).Contains(Cursor.Position))
                {
                    return false;
                }

                var body = _owner._tabs[_owner._selected].Body;
                if (body.Width <= body.Parent.ClientSize.Width)
                {
                    return false;
                }

                int delta = (short)(((long)m.WParam >> 16) & 0xFFFF);
                body.Left += delta > 0 ? _owner.Scale(60) : -_owner.Scale(60);
                ClampScroll(body);
                return true;
            }
        }

        private WheelFilter _wheelFilter;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _wheelFilter ??= new WheelFilter(this);
            Application.AddMessageFilter(_wheelFilter);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_wheelFilter != null)
            {
                Application.RemoveMessageFilter(_wheelFilter);
            }

            base.OnHandleDestroyed(e);
        }

        public void SelectTab(int index)
        {
            if (index < 0 || index >= _tabs.Count)
            {
                return;
            }

            _selected = index;
            if (_collapsed)
            {
                // A click on a tab opens the ribbon again
                _collapsed = false;
            }

            UpdateBodies();
            _scrollVisible = !_collapsed && _tabs[_selected].Body.HorizontalScroll.Visible;
            RecalculateHeight();
            Invalidate();
        }

        private void UpdateBodies()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].Body.Parent.Visible = !_collapsed && i == _selected;
            }
        }

        /// <summary>
        /// Hide the groups and keep the tab names (the chevron on the right)
        /// </summary>
        public void ToggleCollapsed()
        {
            _collapsed = !_collapsed;
            UpdateBodies();
            RecalculateHeight();
            Invalidate();
        }

        /// <summary>
        /// The colors of the editor theme
        /// </summary>
        public void ApplyTheme(EditorTheme theme)
        {
            BackColor = theme.Background;
            ForeColor = theme.Foreground;
            foreach (var tab in _tabs)
            {
                tab.Body.BackColor = theme.Window;
                tab.Body.Parent.BackColor = theme.Window;
            }

            foreach (var group in _groups)
            {
                group.BackColor = theme.Window;
                foreach (Control child in group.Controls)
                {
                    if (child is Label label)
                    {
                        label.ForeColor = theme.Muted;
                        label.BackColor = theme.Window;
                    }
                    else if (child is ColorPaletteControl palette)
                    {
                        palette.BackColor = theme.Window;
                        palette.ForeColor = theme.WindowText;
                        palette.Invalidate();
                    }
                    else if (child is ToolStrip strip)
                    {
                        strip.BackColor = theme.Window;
                        strip.ForeColor = theme.WindowText;
                        if (strip.Renderer is not RibbonRenderer)
                        {
                            strip.Renderer = new RibbonRenderer();
                        }
                    }
                }

                group.Invalidate();
            }

            foreach (var strip in _smallStrips)
            {
                strip.BackColor = theme.Background;
                strip.ForeColor = theme.Foreground;
                if (strip.Renderer is not RibbonRenderer)
                {
                    strip.Renderer = new RibbonRenderer();
                }
            }

            if (_menu != null)
            {
                _menu.BackColor = theme.Background;
                _menu.ForeColor = theme.Foreground;
            }

            Invalidate(true);
        }

        private void MeasureTabs()
        {
            int x = Scale(8);
            using var font = new Font(Font.FontFamily, Font.Size + 0.5f, FontStyle.Regular);
            foreach (var tab in _tabs)
            {
                int width = TextRenderer.MeasureText(tab.Title, font).Width + Scale(30);
                tab.Bounds = new Rectangle(x, QuickRowHeight + Scale(2), width, TabRowHeight - Scale(2));
                x += width + Scale(2);
            }
        }

        private static GraphicsPath TopRounded(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddLine(bounds.Right, bounds.Bottom, bounds.Left, bounds.Bottom);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = EditorTheme.Current;
            var graphics = e.Graphics;
            graphics.Clear(theme.Background);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            MeasureTabs();
            _collapseBounds = new Rectangle(Width - Scale(34), QuickRowHeight + Scale(5), Scale(26), TabRowHeight - Scale(8));
            // The groups sit on a rounded card
            if (!_collapsed)
            {
                using var cardPath = RoundedCard(new Rectangle(Scale(4), HeaderHeight - 1, Width - Scale(9), Height - HeaderHeight - Scale(3)), Scale(7));
                using var cardBrush = new SolidBrush(theme.Window);
                graphics.FillPath(cardBrush, cardPath);
                using var cardPen = new Pen(theme.Border);
                graphics.DrawPath(cardPen, cardPath);
            }

            using var font = new Font(Font.FontFamily, Font.Size + 0.5f, FontStyle.Regular);
            using var selectedFont = new Font(Font.FontFamily, Font.Size + 0.5f, FontStyle.Bold);
            using var border = new Pen(theme.Border);
            int lineY = HeaderHeight - 1;
            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                bool selected = i == _selected;
                if (selected)
                {
                    using var path = TopRounded(new Rectangle(tab.Bounds.Left, tab.Bounds.Top, tab.Bounds.Width, tab.Bounds.Height + 1), Scale(6));
                    using var brush = new SolidBrush(theme.Window);
                    graphics.FillPath(brush, path);
                    graphics.DrawPath(border, path);
                    using var accent = new Pen(theme.Highlight, Math.Max(2, Scale(2)));
                    graphics.DrawLine(accent, tab.Bounds.Left + Scale(8), tab.Bounds.Top + 1, tab.Bounds.Right - Scale(8), tab.Bounds.Top + 1);
                }
                else if (i == _hot)
                {
                    using var path = TopRounded(new Rectangle(tab.Bounds.Left + Scale(1), tab.Bounds.Top + Scale(3), tab.Bounds.Width - Scale(2), tab.Bounds.Height - Scale(3)), Scale(6));
                    using var brush = new SolidBrush(theme.HotTab);
                    graphics.FillPath(brush, path);
                }

                Color textColor = tab.IsFile ? theme.Highlight : selected ? theme.WindowText : theme.Foreground;
                TextRenderer.DrawText(graphics, tab.Title, selected || tab.IsFile ? selectedFont : font, tab.Bounds, textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }

            // The card's top line is open under the selected tab
            if (!_collapsed && _selected >= 0 && _selected < _tabs.Count)
            {
                var selectedBounds = _tabs[_selected].Bounds;
                using var openBrush = new SolidBrush(theme.Window);
                graphics.FillRectangle(openBrush, selectedBounds.Left + 1, lineY - 1, selectedBounds.Width - 1, 3);
            }
            else if (_collapsed)
            {
                graphics.DrawLine(border, 0, lineY, Width, lineY);
            }

            // The chevron which closes or opens the ribbon
            if (_collapseHot)
            {
                using var hotBrush = new SolidBrush(theme.HotTab);
                graphics.FillRectangle(hotBrush, _collapseBounds);
            }

            using var chevronPen = new Pen(theme.Foreground, Math.Max(1.5f, Scale(1)));
            int cx = _collapseBounds.Left + _collapseBounds.Width / 2;
            int cy = _collapseBounds.Top + _collapseBounds.Height / 2;
            if (_collapsed)
            {
                graphics.DrawLines(chevronPen, new[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
            }
            else
            {
                graphics.DrawLines(chevronPen, new[] { new Point(cx - 4, cy + 2), new Point(cx, cy - 2), new Point(cx + 4, cy + 2) });
            }
        }

        private static GraphicsPath RoundedCard(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hot = _tabs.FindIndex(t => t.Bounds.Contains(e.Location));
            bool collapseHot = _collapseBounds.Contains(e.Location);
            if (hot != _hot || collapseHot != _collapseHot)
            {
                _hot = hot;
                _collapseHot = collapseHot;
                Invalidate(new Rectangle(0, 0, Width, HeaderHeight));
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hot != -1 || _collapseHot)
            {
                _hot = -1;
                _collapseHot = false;
                Invalidate(new Rectangle(0, 0, Width, HeaderHeight));
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_collapseBounds.Contains(e.Location))
                {
                    ToggleCollapsed();
                    return;
                }

                int index = _tabs.FindIndex(t => t.Bounds.Contains(e.Location));
                if (index >= 0)
                {
                    SelectTab(index);
                }
            }
        }
    }
}
