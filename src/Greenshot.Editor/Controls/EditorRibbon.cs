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
        /// <summary>
        /// 왕캡: an arrow on the left or right edge of a narrow ribbon: it shows that there are more groups, a click moves them
        /// </summary>
        private sealed class ScrollArrow : Control
        {
            private readonly bool _left;
            private bool _hot;

            public ScrollArrow(bool left)
            {
                _left = left;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                SetStyle(ControlStyles.Selectable, false);
                Cursor = Cursors.Hand;
                Dock = left ? DockStyle.Left : DockStyle.Right;
                Visible = false;
                TabStop = false;
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                _hot = true;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hot = false;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var theme = EditorTheme.Current;
                e.Graphics.Clear(_hot ? theme.HotTab : theme.Window);
                using var border = new Pen(theme.Border);
                int edge = _left ? Width - 1 : 0;
                e.Graphics.DrawLine(border, edge, 0, edge, Height);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(theme.WindowText, Math.Max(1.5f, DeviceDpi / 64f));
                int cx = Width / 2;
                int cy = Height / 2;
                int d = Math.Max(3, DeviceDpi * 4 / 96);
                var points = _left
                    ? new[] { new Point(cx + d / 2, cy - d), new Point(cx - d / 2, cy), new Point(cx + d / 2, cy + d) }
                    : new[] { new Point(cx - d / 2, cy - d), new Point(cx + d / 2, cy), new Point(cx - d / 2, cy + d) };
                e.Graphics.DrawLines(pen, points);
            }
        }

        private sealed class RibbonTab
        {
            public string Title;
            public bool IsFile;
            public FlowLayoutPanel Body;
            public Rectangle Bounds;
            public bool Visible = true; // 컨텍스트 탭 표시/숨김용
            public string KeyTip; // 왕캡: KeyTip 문자열 (예: "H" for Home)
            public ScrollArrow ScrollLeft;
            public ScrollArrow ScrollRight;
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
        private readonly List<RibbonGroup> _ribbonGroups = new(); // 왕캡: KeyTip용 그룹 정보
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
        private bool _keyTipGroupLevel;
        private bool _altPending;
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
            // 왕캡: arrows on the edges when the groups do not fit (a narrow window or a high DPI)
            var tab = new RibbonTab { Title = title, Body = body, IsFile = isFile, ScrollLeft = new ScrollArrow(true), ScrollRight = new ScrollArrow(false) };
            foreach (var arrow in new[] { tab.ScrollLeft, tab.ScrollRight })
            {
                arrow.Width = Scale(18);
                arrow.MouseDown += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        ScrollBody(body, ReferenceEquals(s, tab.ScrollLeft) ? Math.Max(Scale(120), viewport.ClientSize.Width / 2) : -Math.Max(Scale(120), viewport.ClientSize.Width / 2));
                    }
                };
                viewport.Controls.Add(arrow);
                arrow.BringToFront();
            }

            viewport.Resize += (s, e) => ClampScroll(body);
            body.SizeChanged += (s, e) => ClampScroll(body);
            Controls.Add(viewport);
            _tabs.Add(tab);
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
            // 왕캡: Office처럼 두 단계 - 먼저 탭 글자, 탭을 고르면 그 탭의 그룹 글자
            _keyTipGroupLevel = false;
            if (enabled)
            {
                BuildKeyTipMap();
                ShowKeyTipBadges();
            }
            else
            {
                _keyTipMap.Clear();
                _usedKeyTips.Clear();
                ClearKeyTipBadges();
            }
            Invalidate();
        }

        /// <summary>
        /// 왕캡: KeyTip 모드가 켜져 있는지 (테스트와 폼에서 확인용)
        /// </summary>
        public bool KeyTipMode => _keyTipMode;

        /// <summary>
        /// 왕샷: KeyTip 맵 구성. 첫 단계는 탭만, 탭을 고른 뒤에는 그 탭의 그룹만 글자를 받는다
        /// (탭 글자와 그룹 글자가 겹쳐도 서로 가리지 않는다).
        /// </summary>
        private void BuildKeyTipMap()
        {
            _keyTipMap.Clear();
            _usedKeyTips.Clear();

            // 탭 KeyTip 할당
            for (int i = 0; i < _tabs.Count && !_keyTipGroupLevel; i++)
            {
                if (!_tabs[i].Visible) continue;
                string kt = _tabs[i].KeyTip ?? AutoAssignKeyTip(_tabs[i].Title);
                _keyTipMap[kt] = ("Tab", i);
                _usedKeyTips.Add(kt);
            }

            // 현재 선택된 탭의 그룹 KeyTip 할당
            if (_keyTipGroupLevel && _selected >= 0 && _selected < _tabs.Count)
            {
                var tab = _tabs[_selected];
                int groupIdx = 0;
                foreach (var control in tab.Body.Controls)
                {
                    if (control is Panel panel && _stripGroups.TryGetValue(panel, out var groupParts))
                    {
                        if (!groupParts.Strip.Items.Cast<ToolStripItem>().Any(i => i.Available))
                        {
                            // An empty group is hidden, it gets no letter
                            groupIdx++;
                            continue;
                        }

                        // _ribbonGroups에서 찾기
                        foreach (var rg in _ribbonGroups)
                        {
                            if (rg.Panel == panel)
                            {
                                string kt = rg.KeyTip ?? AutoAssignKeyTip(rg.Panel.Controls.OfType<Label>().FirstOrDefault()?.Text ?? $"G{groupIdx}");
                                if (_keyTipMap.ContainsKey(kt))
                                {
                                    kt = AutoAssignKeyTip("123456789");
                                }
                                _usedKeyTips.Add(kt);
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
            // 왕캡: ToolStripEx - the keyboard can go into the group (KeyTips, F6), and a click works also while the editor is not active
            var strip = new ToolStripEx
            {
                ClickThrough = true,
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
            // Office-style compact property rows, beside the three colour controls.
            options.Size = new Size(Scale(250), palette.Height);
            options.Padding = new Padding(Scale(4), Scale(2), 0, 0);
            void Tidy(ToolStripItem item)
            {
                item.Margin = item is ToolStripLabel && !(item is ToolStripButton) && !(item is ToolStripDropDownItem)
                    ? new Padding(Scale(5), Scale(2), Scale(2), Scale(2))
                    : new Padding(Scale(2), Scale(1), Scale(2), Scale(1));
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

        private void ClampScroll(FlowLayoutPanel body)
        {
            int min = Math.Min(0, body.Parent.ClientSize.Width - body.Width);
            int left = Math.Max(min, Math.Min(0, body.Left));
            if (left != body.Left)
            {
                body.Left = left;
            }

            // 왕캡: the arrows show where more groups are
            var tab = _tabs.FirstOrDefault(t => ReferenceEquals(t.Body, body));
            if (tab?.ScrollLeft != null)
            {
                bool showLeft = left < 0;
                bool showRight = left > min;
                if (tab.ScrollLeft.Visible != showLeft)
                {
                    tab.ScrollLeft.Visible = showLeft;
                }

                if (tab.ScrollRight.Visible != showRight)
                {
                    tab.ScrollRight.Visible = showRight;
                }
            }
        }

        /// <summary>
        /// 왕캡: move the groups of a tab by the given pixels (positive: to the right, the groups on the left come into view)
        /// </summary>
        private void ScrollBody(FlowLayoutPanel body, int delta)
        {
            body.Left += delta;
            ClampScroll(body);
        }

        private sealed class WheelFilter : IMessageFilter
        {
            private readonly EditorRibbon _owner;

            public WheelFilter(EditorRibbon owner) => _owner = owner;

            public bool PreFilterMessage(ref Message m)
            {
                if (_owner.IsDisposed)
                {
                    return false;
                }

                // 왕캡: Alt and the letters of the KeyTips
                if (_owner.PreFilterKeyTipMessage(ref m))
                {
                    return true;
                }

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
                _owner.ScrollBody(body, delta > 0 ? _owner.Scale(60) : -_owner.Scale(60));
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

        // ============================================================
        // 왕캡: KeyTips - 왼쪽 Alt를 눌렀다 떼면 탭 글자가 뜨고, 탭 글자를 누르면 그 탭의 그룹 글자가 뜬다.
        // 그룹 글자를 누르면 그 그룹의 첫 버튼으로 키보드 포커스가 간다 (화살표로 이동, Enter/Space로 실행, 아래 화살표로 더 보기).
        // Esc는 한 단계 뒤로, 마우스를 누르거나 창을 떠나면 끝난다.
        // ============================================================
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_NCLBUTTONDOWN = 0x00A1;

        [System.Runtime.InteropServices.DllImport("imm32.dll")]
        private static extern int ImmGetVirtualKey(IntPtr hWnd);

        private readonly List<Label> _keyTipBadges = new();

        /// <summary>
        /// The keys of the editor window go through here first (see <see cref="WheelFilter"/>)
        /// </summary>
        /// <returns>true when the key was used for the KeyTips and must not go on</returns>
        private bool PreFilterKeyTipMessage(ref Message m)
        {
            bool mouseDown = m.Msg is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_NCLBUTTONDOWN;
            if (m.Msg is not (WM_KEYDOWN or WM_SYSKEYDOWN or WM_SYSKEYUP) && !mouseDown)
            {
                return false;
            }

            var form = FindForm();
            var target = FromChildHandle(m.HWnd);
            if (form == null || target == null || target.FindForm() != form)
            {
                // Another window (a menu, a dialog): the KeyTips end
                _altPending = false;
                if (_keyTipMode && (mouseDown || form == null || !form.ContainsFocus))
                {
                    SetKeyTipMode(false);
                }

                return false;
            }

            if (mouseDown)
            {
                _altPending = false;
                SetKeyTipMode(false);
                return false;
            }

            var key = (Keys)(int)((long)m.WParam & 0xFFFF);
            long lParam = (long)m.LParam;
            if (key == Keys.ProcessKey)
            {
                // The Korean input method holds the letter, ask it which key it was
                int virtualKey = ImmGetVirtualKey(m.HWnd);
                if (virtualKey != 0)
                {
                    key = (Keys)virtualKey;
                }
            }

            switch (m.Msg)
            {
                case WM_SYSKEYDOWN when key == Keys.Menu:
                    // Only the left Alt: on Korean keyboards the right Alt can be the Hangul key. Bit 30: the key repeats.
                    bool rightAlt = (lParam & (1L << 24)) != 0;
                    bool repeat = (lParam & (1L << 30)) != 0;
                    if (!repeat)
                    {
                        _altPending = !rightAlt && Visible;
                    }

                    return false;
                case WM_SYSKEYUP when key == Keys.Menu:
                    if (!_altPending)
                    {
                        return false;
                    }

                    _altPending = false;
                    SetKeyTipMode(!_keyTipMode);
                    // Swallowed: the hidden menu bar would take the keyboard otherwise
                    return true;
                case WM_SYSKEYUP:
                    return false;
                default:
                    _altPending = false;
                    return _keyTipMode && HandleKeyTipKey(key);
            }
        }

        /// <summary>
        /// A key while the KeyTips are shown
        /// </summary>
        /// <returns>true when the key was used</returns>
        private bool HandleKeyTipKey(Keys key)
        {
            if (key is Keys.ShiftKey or Keys.ControlKey or Keys.Menu)
            {
                return false;
            }

            if (key == Keys.Escape)
            {
                if (_keyTipGroupLevel)
                {
                    // One level back: the letters of the tabs
                    _keyTipGroupLevel = false;
                    BuildKeyTipMap();
                    ShowKeyTipBadges();
                }
                else
                {
                    SetKeyTipMode(false);
                }

                return true;
            }

            string keyChar = null;
            if (key is >= Keys.A and <= Keys.Z)
            {
                keyChar = ((char)('A' + (key - Keys.A))).ToString();
            }
            else if (key is >= Keys.D0 and <= Keys.D9)
            {
                keyChar = ((char)('0' + (key - Keys.D0))).ToString();
            }
            else if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
            {
                keyChar = ((char)('0' + (key - Keys.NumPad0))).ToString();
            }

            if (keyChar == null || !_keyTipMap.TryGetValue(keyChar, out var action))
            {
                // Any other key ends the KeyTips and does what it always does
                SetKeyTipMode(false);
                return false;
            }

            if (action.Type == "Tab")
            {
                SelectTab(action.Index);
                _keyTipGroupLevel = true;
                BuildKeyTipMap();
                ShowKeyTipBadges();
                Invalidate();
                return true;
            }

            SetKeyTipMode(false);
            if (action.Index >= 0 && action.Index < _ribbonGroups.Count && _stripGroups.TryGetValue(_ribbonGroups[action.Index].Panel, out var parts))
            {
                FocusStrip(parts.Strip);
            }

            return true;
        }

        /// <summary>
        /// The keyboard goes into a group: its first button is selected, the arrows move on, Enter uses it
        /// </summary>
        private bool FocusStrip(ToolStrip strip)
        {
            if (strip == null || !strip.Visible || !strip.Items.Cast<ToolStripItem>().Any(i => i.Available && i.Enabled && i.CanSelect))
            {
                return false;
            }

            // A group outside of a narrow ribbon comes into view first
            if (strip.Parent is Panel group && group.Parent is FlowLayoutPanel body && body.Parent != null)
            {
                int x = body.Left + group.Left;
                int width = body.Parent.ClientSize.Width;
                if (x < 0)
                {
                    ScrollBody(body, -x);
                }
                else if (x + group.Width > width)
                {
                    ScrollBody(body, width - (x + group.Width));
                }
            }

            if (strip is ToolStripEx toolStripEx)
            {
                toolStripEx.SelectFirstItem();
            }
            else
            {
                strip.Focus();
            }

            return true;
        }

        /// <summary>
        /// 왕캡: F6 moves the keyboard into the first group of the shown tab
        /// </summary>
        /// <returns>false when no group of the tab has a button to go to</returns>
        public bool FocusFirstGroup()
        {
            if (_collapsed || _selected < 0 || _selected >= _tabs.Count)
            {
                return false;
            }

            foreach (Control control in _tabs[_selected].Body.Controls)
            {
                if (control is Panel panel && panel.Visible && _stripGroups.TryGetValue(panel, out var parts) && FocusStrip(parts.Strip))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The letters are small labels over the tabs and the captions of the groups (the groups are child controls, painting on the
        /// ribbon itself would be hidden under them)
        /// </summary>
        private void ShowKeyTipBadges()
        {
            ClearKeyTipBadges();
            if (!_keyTipMode)
            {
                return;
            }

            MeasureTabs();
            int size = Scale(18);
            foreach (var entry in _keyTipMap)
            {
                if (entry.Value.Type == "Tab")
                {
                    var bounds = _tabs[entry.Value.Index].Bounds;
                    AddKeyTipBadge(this, entry.Key, new Point(bounds.Left + (bounds.Width - size) / 2, bounds.Bottom - size / 3));
                }
                else if (entry.Value.Index >= 0 && entry.Value.Index < _ribbonGroups.Count)
                {
                    var panel = _ribbonGroups[entry.Value.Index].Panel;
                    if (!_stripGroups.TryGetValue(panel, out var parts) || !panel.Visible)
                    {
                        continue;
                    }

                    // On the left of the caption, so the name of the group stays readable
                    var caption = parts.Label;
                    int textWidth = TextRenderer.MeasureText(caption.Text ?? string.Empty, caption.Font).Width;
                    int x = Math.Max(Scale(2), (panel.Width - textWidth) / 2 - size - Scale(2));
                    AddKeyTipBadge(panel, entry.Key, new Point(x, caption.Top + Math.Max(0, (caption.Height - size) / 2)));
                }
            }
        }

        private void AddKeyTipBadge(Control parent, string text, Point location)
        {
            var theme = EditorTheme.Current;
            var badge = new Label
            {
                Text = text,
                AutoSize = false,
                Size = new Size(Scale(18), Scale(18)),
                Location = location,
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false,
                Font = new Font(Font.FontFamily, Math.Max(6f, Font.Size - 1f), FontStyle.Bold),
                BackColor = theme.Highlight,
                ForeColor = theme.Window
            };
            parent.Controls.Add(badge);
            badge.BringToFront();
            _keyTipBadges.Add(badge);
        }

        private void ClearKeyTipBadges()
        {
            foreach (var badge in _keyTipBadges)
            {
                var font = badge.Font;
                badge.Parent?.Controls.Remove(badge);
                badge.Dispose();
                font.Dispose();
            }

            _keyTipBadges.Clear();
        }
    }
}
