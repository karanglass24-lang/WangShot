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
            public bool Visible = true; // 컨텍스트 탭 표시/숨김용
            public string KeyTip; // 왕샷: KeyTip 문자열 (예: "H" for Home)
        }

        private sealed class RibbonGroup
        {
            public Panel Panel;
            public string KeyTip;
        }

        private readonly List<RibbonTab> _tabs = new();
        private readonly List<ToolStrip> _strips = new();
        private readonly List<ToolStrip> _smallStrips = new();
        private readonly List<Panel> _groups = new();
        private readonly Dictionary<Panel, (ToolStrip Strip, Label Label)> _stripGroups = new();
        private readonly List<RibbonGroup> _ribbonGroups = new(); // 왕샷: KeyTip용 그룹 정보
        private readonly ToolStrip _headerRight;
        private readonly ToolStrip _quickAccess;
        private int _selected;
        private int _hot = -1;
        private MenuStrip _menu;
        private bool _collapsed;
        private bool _scrollVisible;
        private bool _collapseHot;
        private Rectangle _collapseBounds;

        // 왕샷: KeyTip 관련 필드
        private bool _keyTipMode;
        private readonly Dictionary<string, (string Type, int Index)> _keyTipMap = new(); // KeyTip 문자열 -> (타입, 인덱스)
        private readonly HashSet<string> _usedKeyTips = new(); // 이미 사용된 KeyTip 문자들

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
        /// 왕샷: 탭 표시/숨김 설정 (컨텍스트 탭용)
        /// </summary>
        public void SetTabVisible(int tabIndex, bool visible)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return;
            if (_tabs[tabIndex].Visible == visible) return;

            _tabs[tabIndex].Visible = visible;
            // 숨기는 탭이 현재 선택된 탭이면 첫 번째 보이는 탭으로 이동
            if (!visible && _selected == tabIndex)
            {
                int newTab = _tabs.FindIndex(t => t.Visible);
                if (newTab >= 0) SelectTab(newTab);
            }
            Invalidate();
        }

        /// <summary>
        /// 왕샷: 탭 표시 여부 확인
        /// </summary>
        public bool IsTabVisible(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return false;
            return _tabs[tabIndex].Visible;
        }

        /// <summary>
        /// 왕샷: 탭의 KeyTip 설정
        /// </summary>
        public void SetTabKeyTip(int tabIndex, string keyTip)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return;
            _tabs[tabIndex].KeyTip = keyTip?.ToUpperInvariant();
        }

        /// <summary>
        /// 왕샷: 그룹의 KeyTip 설정 (탭 인덱스, 그룹 인덱스 순서)
        /// </summary>
        public void SetGroupKeyTip(int tabIndex, int groupIndex, string keyTip)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return;
            var tab = _tabs[tabIndex];
            int groupCount = 0;
            foreach (var control in tab.Body.Controls)
            {
                if (control is Panel panel && _stripGroups.ContainsKey(panel))
                {
                    if (groupCount == groupIndex)
                    {
                        // _ribbonGroups에서 해당 그룹 찾기
                        foreach (var rg in _ribbonGroups)
                        {
                            if (rg.Panel == panel)
                            {
                                rg.KeyTip = keyTip?.ToUpperInvariant();
                                break;
                            }
                        }
                        break;
                    }
                    groupCount++;
                }
            }
        }

        /// <summary>
        /// 왕샷: KeyTip 모드 진입/종료
        /// </summary>
        public void SetKeyTipMode(bool enabled)
        {
            if (_keyTipMode == enabled) return;
            _keyTipMode = enabled;
            if (enabled)
            {
                BuildKeyTipMap();
            }
            else
            {
                _keyTipMap.Clear();
                _usedKeyTips.Clear();
            }
            Invalidate();
        }

        /// <summary>
        /// 왕샷: KeyTip 맵 구성 (탭, 그룹, 버튼에 문자 할당)
        /// </summary>
        private void BuildKeyTipMap()
        {
            _keyTipMap.Clear();
            _usedKeyTips.Clear();

            // 탭 KeyTip 할당
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (!_tabs[i].Visible) continue;
                string kt = _tabs[i].KeyTip ?? AutoAssignKeyTip(_tabs[i].Title);
                _keyTipMap[kt] = ("Tab", i);
            }

            // 현재 선택된 탭의 그룹 KeyTip 할당
            if (_selected >= 0 && _selected < _tabs.Count)
            {
                var tab = _tabs[_selected];
                int groupIdx = 0;
                foreach (var control in tab.Body.Controls)
                {
                    if (control is Panel panel && _stripGroups.ContainsKey(panel))
                    {
                        // _ribbonGroups에서 찾기
                        foreach (var rg in _ribbonGroups)
                        {
                            if (rg.Panel == panel)
                            {
                                string kt = rg.KeyTip ?? AutoAssignKeyTip(rg.Panel.Controls.OfType<Label>().FirstOrDefault()?.Text ?? $"G{groupIdx}");
                                _keyTipMap[kt] = ("Group", _ribbonGroups.IndexOf(rg));
                                break;
                            }
                        }
                        groupIdx++;
                    }
                }
            }
        }

        /// <summary>
        /// 왕샷: 자동 KeyTip 문자 할당 (첫 글자부터 사용 안 된 것 찾기)
        /// </summary>
        private string AutoAssignKeyTip(string text)
        {
            if (string.IsNullOrEmpty(text)) return "?";
            foreach (char c in text.ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(c) && !_usedKeyTips.Contains(c.ToString()))
                {
                    _usedKeyTips.Add(c.ToString());
                    return c.ToString();
                }
            }
            // 모두 사용됐으면 숫자 시도
            for (int i = 1; i <= 99; i++)
            {
                string s = i.ToString();
                if (!_usedKeyTips.Contains(s))
                {
                    _usedKeyTips.Add(s);
                    return s;
                }
            }
            return "?";
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
            // 왕샷: KeyTip용 그룹 정보 저장
            _ribbonGroups.Add(new RibbonGroup { Panel = group, KeyTip = null });
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
                if (!tab.Visible) continue; // 숨겨진 탭은 레이아웃에서 제외
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
                if (!tab.Visible) continue; // 숨겨진 탭은 그리지 않음
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

            // 왕샷: KeyTip 배지 그리기
            if (_keyTipMode)
            {
                DrawKeyTips(graphics, theme);
            }
        }

        /// <summary>
        /// 왕샷: KeyTip 배지 렌더링
        /// </summary>
        private void DrawKeyTips(Graphics graphics, EditorTheme theme)
        {
            using var keyTipFont = new Font(Font.FontFamily, Font.Size - 1f, FontStyle.Bold);
            using var keyTipBrush = new SolidBrush(theme.Highlight);
            using var keyTipBackBrush = new SolidBrush(Color.FromArgb(220, theme.Background.R, theme.Background.G, theme.Background.B));
            using var keyTipBorderPen = new Pen(theme.Highlight, 1.5f);

            // 탭 KeyTips
            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                if (!tab.Visible || string.IsNullOrEmpty(tab.KeyTip)) continue;

                var bounds = tab.Bounds;
                int badgeWidth = Scale(24);
                int badgeHeight = Scale(18);
                int x = bounds.Left + (bounds.Width - badgeWidth) / 2;
                int y = bounds.Bottom + Scale(2);

                var badgeRect = new Rectangle(x, y, badgeWidth, badgeHeight);
                using var path = Rounded(badgeRect, Scale(3));
                graphics.FillPath(keyTipBackBrush, path);
                graphics.DrawPath(keyTipBorderPen, path);

                TextRenderer.DrawText(graphics, tab.KeyTip, keyTipFont, badgeRect, theme.Highlight,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }

            // 현재 탭의 그룹 KeyTips
            if (_selected >= 0 && _selected < _tabs.Count && !_collapsed)
            {
                var tab = _tabs[_selected];
                foreach (var control in tab.Body.Controls)
                {
                    if (control is Panel panel && _stripGroups.ContainsKey(panel))
                    {
                        var label = panel.Controls.OfType<Label>().FirstOrDefault();
                        string keyTip = null;
                        foreach (var rg in _ribbonGroups)
                        {
                            if (rg.Panel == panel)
                            {
                                keyTip = rg.KeyTip;
                                break;
                            }
                        }
                        if (string.IsNullOrEmpty(keyTip)) continue;

                        int badgeWidth = Scale(24);
                        int badgeHeight = Scale(18);
                        int x = panel.Left + (panel.Width - badgeWidth) / 2;
                        int y = panel.Top + Scale(2);

                        var badgeRect = new Rectangle(x, y, badgeWidth, badgeHeight);
                        using var path = Rounded(badgeRect, Scale(3));
                        graphics.FillPath(keyTipBackBrush, path);
                        graphics.DrawPath(keyTipBorderPen, path);

                        TextRenderer.DrawText(graphics, keyTip, keyTipFont, badgeRect, theme.Highlight,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                    }
                }
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

        /// <summary>
        /// 왕샷: 작은 배지용 둥근 사각형 (KeyTip용)
        /// </summary>
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

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hot = _tabs.FindIndex(t => t.Visible && t.Bounds.Contains(e.Location));
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

                int index = _tabs.FindIndex(t => t.Visible && t.Bounds.Contains(e.Location));
                if (index >= 0)
                {
                    SelectTab(index);
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // 왕샷: KeyTip 모드 처리
            if (_keyTipMode)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    SetKeyTipMode(false);
                    e.Handled = true;
                    return;
                }

                // KeyTip 문자열 매칭 (단일 문자 또는 숫자)
                string keyChar = null;
                if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z)
                {
                    keyChar = ((char)('A' + (e.KeyCode - Keys.A))).ToString();
                }
                else if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)
                {
                    keyChar = ((char)('0' + (e.KeyCode - Keys.D0))).ToString();
                }
                else if (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9)
                {
                    keyChar = ((char)('0' + (e.KeyCode - Keys.NumPad0))).ToString();
                }

                if (!string.IsNullOrEmpty(keyChar) && _keyTipMap.TryGetValue(keyChar, out var action))
                {
                    switch (action.Type)
                    {
                        case "Tab":
                            SelectTab(action.Index);
                            // 탭 선택 후 해당 탭의 그룹 KeyTips 표시 위해 맵 재구성
                            BuildKeyTipMap();
                            Invalidate();
                            break;
                        case "Group":
                            // 그룹 선택 시 해당 그룹의 첫 번째 버튼 포커스 또는 클릭 시뮬레이션
                            if (action.Index >= 0 && action.Index < _ribbonGroups.Count)
                            {
                                var group = _ribbonGroups[action.Index];
                                if (_stripGroups.TryGetValue(group.Panel, out var stripInfo))
                                {
                                    var strip = stripInfo.Strip;
                                    if (strip != null && strip.Items.Count > 0)
                                    {
                                        var firstItem = strip.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Available && i.Enabled);
                                        if (firstItem != null)
                                        {
                                            // 버튼 클릭 시뮬레이션
                                            if (firstItem is ToolStripButton btn)
                                            {
                                                btn.PerformClick();
                                            }
                                            else if (firstItem is ToolStripDropButton dropBtn)
                                            {
                                                dropBtn.ShowDropDown();
                                            }
                                            else if (firstItem is ToolStripSplitButton splitBtn)
                                            {
                                                splitBtn.PerformButtonClick();
                                            }
                                        }
                                    }
                                }
                            }
                            break;
                    }
                    // KeyTip 액션 후 모드 종료 (Office 스타일: 한 번 누르면 끝)
                    SetKeyTipMode(false);
                    e.Handled = true;
                }
            }
            else if (e.KeyCode == Keys.Menu && !e.Alt) // Alt 키 단독 눌림
            {
                SetKeyTipMode(true);
                e.Handled = true;
            }
        }
    }
}
