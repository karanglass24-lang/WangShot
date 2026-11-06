using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greenshot.Base;
using Greenshot.Base.Controls;
using Greenshot.Base.Core;
using Greenshot.Editor.Controls;
using Greenshot.Editor.Drawing;

namespace Greenshot.Editor.Forms
{
    /// <summary>
    /// 왕샷: the ribbon of the editor. The buttons of the old tool bars and menus move into the groups of the ribbon.
    /// Order of the tabs: File, Home, Edit, Object, View, Share.
    /// </summary>
    public partial class ImageEditorForm
    {
        private EditorRibbon _ribbon;
        private ToolStrip _ribbonShareStrip;
        private ToolStripControlHost _ribbonHost;
        private ToolStripDropButton _emojiTool;
        private ColorPaletteControl _palette;

        /// <summary>
        /// Put the buttons into groups, make the ribbon the only tool bar above the canvas
        /// </summary>
        private void BuildRibbon()
        {
            if (_ribbon != null)
            {
                return;
            }

            _ribbon = new EditorRibbon { Name = "editorRibbon", Font = Font };
            int file = _ribbon.AddTab("파일", isFile: true);
            int home = _ribbon.AddTab("홈");
            int edit = _ribbon.AddTab("편집");
            int obj = _ribbon.AddTab("개체");
            int view = _ribbon.AddTab("보기");
            int share = _ribbon.AddTab("공유");

            // ---- File
            var saveAs = NewAction("다른 이름으로", "다른 이름으로 저장", null, (s, e) => DestinationHelper.StartExport(WellKnownDestinations.FileDialog, _surface));
            IconBinder.BindLike(saveAs, btnSave);
            AddRibbonGroup(file, "파일",
                (NewAction("새로 만들기", "새 이미지를 새 탭에 만들어요", "newImage.Image", (s, e) => AddNewImageTab()), null),
                (NewAction("열기", "이미지 파일 열기", "openFile.Image", (s, e) => OpenImageFile()), null),
                (btnSave, "저장"),
                (saveAs, null),
                (NewAction("PDF로 저장", "이미지를 PDF 파일로 저장해요", "pdfFile.Image", (s, e) => SaveAsPdf()), null));
            AddRibbonGroup(file, "창", (Proxy(closeToolStripMenuItem, "닫기"), null));

            // ---- Home
            var clipboardGroup = AddRibbonGroup(home, "클립보드", (btnPaste, "붙여넣기"), (btnCut, "잘라내기"), (btnCopy, "복사"), (btnDelete, "삭제"));
            foreach (ToolStripItem item in clipboardGroup.Items)
            {
                item.Padding = new Padding(2, 2, 2, 15);
            }

            // The select tool and the emoji tool have a chevron under them: a drop-down with more choices
            CreateEmojiPresetButton();
            CreateAreaSelectButtons();
            var selectTool = new ToolStripDropButton { ToolTipText = !string.IsNullOrEmpty(btnCursor.ToolTipText) ? btnCursor.ToolTipText : btnCursor.Text, DropDown = BuildSelectMenu() };
            selectTool.MirrorOf(btnCursor);
            IconBinder.BindLike(selectTool, btnCursor);
            var emojiTool = new ToolStripDropButton { ToolTipText = "이모지 (M), 아래 화살표로 다른 이모지 고르기", DropDown = _emojiPresetDropDown };
            emojiTool.MirrorOf(btnEmoji);
            emojiTool.ImageScaling = ToolStripItemImageScaling.None;
            _emojiTool = emojiTool;
            emojiTool.SyncImage();
            // Tools which belong together share a button: the chevron lists them (rectangle and ellipse, line and arrow, pencil and highlighter)
            var shapeTool = new ToolStripDropButton();
            shapeTool.SetMembers((btnRect, "사각형"), (btnEllipse, "타원"));
            var lineTool = new ToolStripDropButton();
            lineTool.SetMembers((btnLine, "선"), (btnArrow, "화살표"));
            var penTool = new ToolStripDropButton();
            penTool.SetMembers((btnFreehand, "연필"), (btnHighlight, "형광펜"));
            var tools = AddRibbonGroup(home, "도구",
                (selectTool, "선택"), (shapeTool, null), (lineTool, null), (penTool, null),
                (btnText, "글자"), (btnSpeechBubble, "말풍선"), (btnStepLabel, "번호"), (emojiTool, "이모지"), (btnObfuscate, "가리기"));
            // Room under every tool for the chevron, so the names stand in one line
            foreach (ToolStripItem tool in tools.Items)
            {
                tool.Padding = new Padding(2, 2, 2, 15);
            }
            var effectsTool = new ToolStripDropButton { ToolTipText = "효과", DropDown = BuildEffectsMenu() };
            effectsTool.MirrorOfAction(toolStripSplitButton1, () => effectsTool.ShowDropDown());
            var imageGroup = AddRibbonGroup(home, "이미지", (btnCrop, "자르기"), (btnResize, "크기"), (rotateCwToolstripButton, "우회전"), (rotateCcwToolstripButton, "좌회전"), (effectsTool, "효과"));
            foreach (ToolStripItem item in imageGroup.Items)
            {
                item.Padding = new Padding(2, 2, 2, 15);
            }

            // The colours: line colour, fill colour and a palette
            _palette = new ColorPaletteControl();
            _palette.ColorChosen += (color, fill) => _surface.FieldAggregator.GetField(fill ? Greenshot.Editor.Drawing.Fields.FieldType.FILL_COLOR : Greenshot.Editor.Drawing.Fields.FieldType.LINE_COLOR).Value = color;
            _ribbon.AddControlGroup(home, "색상", _palette);
            // The options of the tool or of the selected element (colour, thickness, font ...): shown only when there are some
            _ribbon.AddStripGroup(home, "속성", propertiesToolStrip);

            // ---- Edit
            AddRibbonGroup(edit, "실행", (Proxy(undoToolStripMenuItem, "되돌리기"), null), (Proxy(redoToolStripMenuItem, "다시실행"), null));
            AddRibbonGroup(edit, "클립보드", (Proxy(cutToolStripMenuItem, "잘라내기"), null), (Proxy(copyToolStripMenuItem, "복사"), null),
                (Proxy(pasteToolStripMenuItem, "붙여넣기"), null), (ProxyIcon(duplicateToolStripMenuItem, "복제", "duplicate.Image"), null));
            AddRibbonGroup(edit, "선택", (ProxyIcon(selectAllToolStripMenuItem, "모두 선택", "selectAll.Image"), null), (Proxy(removeObjectToolStripMenuItem, "개체 삭제"), null));

            // ---- Object
            AddRibbonGroup(obj, "순서", (ProxyIcon(upToTopToolStripMenuItem, "맨 앞으로", "arrangeFront.Image"), null), (ProxyIcon(upOneLevelToolStripMenuItem, "앞으로", "arrangeForward.Image"), null),
                (ProxyIcon(downOneLevelToolStripMenuItem, "뒤로", "arrangeBackward.Image"), null), (ProxyIcon(downToBottomToolStripMenuItem, "맨 뒤로", "arrangeBack.Image"), null));
            AddRibbonGroup(obj, "개체 파일", (ProxyIcon(saveElementsToolStripMenuItem, "개체 저장", "btnSave.Image"), null), (ProxyIcon(loadElementsToolStripMenuItem, "개체 불러오기", "openFile.Image"), null));

            // ---- View
            var zoomIn = NewZoomButton("zoomInMenuItem.Image", "확대", (s, e) => ZoomInMenuItemClick(s, e));
            var zoomOut = NewZoomButton("zoomOutMenuItem.Image", "축소", (s, e) => ZoomOutMenuItemClick(s, e));
            var zoomActual = NewZoomButton("zoomActualSizeMenuItem.Image", "100%", (s, e) => ZoomSetValueMenuItemClick(zoomActualSizeMenuItem, e));
            var zoomFit = NewZoomButton("zoomBestFitMenuItem.Image", "맞춤", (s, e) => ZoomBestFitMenuItemClick(s, e));
            AddRibbonGroup(view, "확대/축소", (zoomIn, null), (zoomOut, null), (zoomActual, null), (zoomFit, null));
            var rulerButton = NewAction("눈금자", "가로·세로 눈금자 보이기/숨기기", "ruler.Image", (s, e) => SetRulersShown(((ToolStripButton)s).Checked));
            rulerButton.CheckOnClick = true;
            rulerButton.Checked = true;
            AddRibbonGroup(view, "표시", (rulerButton, null));

            // ---- Share
            AddRibbonGroup(share, "복사", (btnClipboard, "클립보드"));
            _ribbonShareStrip = AddRibbonGroup(share, "보내기");

            _ribbon.SelectTab(home);

            // ---- Quick access tools, and the settings and the help on the right of the same row
            var quick = _ribbon.QuickAccess;
            foreach (var item in new ToolStripItem[]
                     {
                         NewAction(null, "새로 만들기", "newImage.Image", (s, e) => AddNewImageTab()),
                         NewAction(null, "열기", "openFile.Image", (s, e) => OpenImageFile()),
                         Proxy(btnSave, null),
                         new ToolStripSeparator(),
                         Proxy(undoToolStripMenuItem, null),
                         Proxy(redoToolStripMenuItem, null)
                     })
            {
                item.DisplayStyle = ToolStripItemDisplayStyle.Image;
                item.Margin = new Padding(1, 1, 1, 1);
                quick.Items.Add(item);
            }

            foreach (var (item, tip) in new[] { ((ToolStripItem)btnSettings, "설정"), ((ToolStripItem)btnHelp, "도움말") })
            {
                MoveItem(item, _ribbon.HeaderRight);
                item.ToolTipText = tip;
                item.DisplayStyle = ToolStripItemDisplayStyle.Image;
                item.Margin = new Padding(1, 1, 1, 1);
            }

            // The menu bar stays (shortcut keys) but out of sight
            _ribbon.HostMenu(menuStrip1);

            // The old tool bars: the left one is empty now, the top one holds the ribbon (and the buttons of the destinations, see below)
            toolsToolStrip.Visible = false;
            foreach (ToolStripItem old in destinationsToolStrip.Items.Cast<ToolStripItem>().ToList())
            {
                if (!ReferenceEquals(old, toolStripSeparator16))
                {
                    old.Visible = false;
                }
            }

            toolStripSeparator16.Visible = false;
            destinationsToolStrip.CanOverflow = false;
            _ribbonHost = new ToolStripControlHost(_ribbon) { AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty };
            destinationsToolStrip.Items.Insert(0, _ribbonHost);
            destinationsToolStrip.SizeChanged += (s, e) => FitRibbonHost();
            _ribbon.SizeChanged += (s, e) => FitRibbonHost();
            FitRibbonHost();
            _ribbon.ApplyTheme(EditorTheme.Current);
            ApplyRibbonDpi(DeviceDpi);
            BuildTabOverview();
            BuildRulers();
            // The ribbon needs room: a narrow editor would hide most buttons
            MinimumSize = new Size(Math.Max(MinimumSize.Width, LogicalToDeviceUnits(1280)), MinimumSize.Height);
            if (Width < MinimumSize.Width)
            {
                Width = MinimumSize.Width;
            }
        }

        /// <summary>
        /// The menu under the chevron of the effects: the items of the old effects button
        /// </summary>
        private ToolStripDropDown BuildEffectsMenu()
        {
            var menu = new ToolStripDropDownMenu { ShowImageMargin = true };
            menu.Opening += (s, e) =>
            {
                menu.Items.Clear();
                foreach (ToolStripItem source in toolStripSplitButton1.DropDownItems)
                {
                    if (source is ToolStripSeparator)
                    {
                        menu.Items.Add(new ToolStripSeparator());
                        continue;
                    }

                    var item = new ToolStripMenuItem(source.Text) { Enabled = source.Enabled, Image = source.Image != null ? (Image)source.Image.Clone() : null };
                    var target = source;
                    item.Click += (s2, e2) =>
                    {
                        // The drop shadow and the torn edges start on the mouse up of their menu item
                        if (ReferenceEquals(target, addDropshadowToolStripMenuItem))
                        {
                            AddDropshadowToolStripMenuItemMouseUp(target, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                        }
                        else if (ReferenceEquals(target, tornEdgesToolStripMenuItem))
                        {
                            TornEdgesToolStripMenuItemMouseUp(target, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
                        }
                        else
                        {
                            target.PerformClick();
                        }
                    };
                    menu.Items.Add(item);
                }

                var theme = EditorTheme.Current;
                EditorTheme.ApplyRenderer(menu);
                menu.BackColor = theme.MenuBackground;
                menu.ForeColor = theme.Foreground;
            };
            return menu;
        }

        /// <summary>
        /// Save the image as a PDF file
        /// </summary>
        private void SaveAsPdf()
        {
            using var dialog = new SaveFileDialog
            {
                Title = "PDF로 저장",
                Filter = "PDF 파일|*.pdf",
                DefaultExt = "pdf",
                AddExtension = true,
                FileName = string.IsNullOrWhiteSpace(_surface.CaptureDetails?.Title) ? "이미지" : _surface.CaptureDetails.Title
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                using var image = _surface.GetImageForExport();
                PdfExport.Save(image, dialog.FileName);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not save the PDF " + dialog.FileName, ex);
                MessageBox.Show(this, "PDF로 저장하지 못했어요.", "왕샷", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void FitRibbonHost()
        {
            if (_ribbonHost == null || destinationsToolStrip == null)
            {
                return;
            }

            var wanted = new Size(Math.Max(100, destinationsToolStrip.ClientSize.Width - 4), _ribbon.Height);
            if (_ribbonHost.Size != wanted)
            {
                _ribbonHost.Size = wanted;
            }
        }

        private void ApplyRibbonDpi(int dpi)
        {
            if (_ribbon == null)
            {
                return;
            }

            foreach (var strip in _ribbon.Strips)
            {
                IconBinder.ApplyForDpi(strip, dpi, "the editor ribbon");
            }

            // The quick access tools and the buttons in the corner are smaller
            int small = Math.Max(16, (int)Math.Round(20 * dpi / 96.0));
            foreach (var strip in _ribbon.SmallStrips)
            {
                IconBinder.Apply(strip, small);
            }
        }

        private ToolStripButton NewZoomButton(string iconResource, string text, EventHandler onClick) => NewAction(text, text, iconResource, onClick);

        /// <summary>
        /// A new button with an icon from the resources of the editor
        /// </summary>
        private ToolStripButton NewAction(string text, string tip, string iconResource, EventHandler onClick)
        {
            var button = new ToolStripButton { Text = text ?? string.Empty, ToolTipText = tip };
            if (iconResource != null)
            {
                IconBinder.Bind(button, IconSource.FromResource(typeof(ImageEditorForm), iconResource));
            }

            button.Click += onClick;
            return button;
        }

        /// <summary>
        /// A twin of a menu item or a button, it has the same icon and state and clicks the original
        /// </summary>
        private static ToolStripButton Proxy(ToolStripItem source, string label)
        {
            string tip = !string.IsNullOrEmpty(source.ToolTipText) ? source.ToolTipText : (source.Text ?? string.Empty).Replace("&", string.Empty);
            var button = new ToolStripButton { Text = label ?? string.Empty, ToolTipText = tip, Enabled = source.Enabled };
            if (!IconBinder.BindLike(button, source))
            {
                button.Image = source.Image;
            }

            source.EnabledChanged += (s, e) => button.Enabled = source.Enabled;
            button.Click += (s, e) => source.PerformClick();
            return button;
        }

        /// <summary>
        /// A twin whose icon comes from the resources of the editor (for the menu items which have none)
        /// </summary>
        private static ToolStripButton ProxyIcon(ToolStripItem source, string label, string iconResource)
        {
            var button = Proxy(source, label);
            button.Image = null;
            IconBinder.Bind(button, IconSource.FromResource(typeof(ImageEditorForm), iconResource));
            return button;
        }

        private ToolStrip AddRibbonGroup(int tab, string caption, params (ToolStripItem Item, string Label)[] items)
        {
            var strip = _ribbon.AddGroup(tab, caption);
            foreach (var (item, label) in items)
            {
                MoveItem(item, strip);
                MakeLarge(item, label);
            }

            return strip;
        }

        /// <summary>
        /// The item leaves its old tool bar and joins the group
        /// </summary>
        private static void MoveItem(ToolStripItem item, ToolStrip target)
        {
            item.Owner?.Items.Remove(item);
            target.Items.Add(item);
        }

        /// <summary>
        /// A large button: the icon with a short text below, the old text stays as the tool tip
        /// </summary>
        private static void MakeLarge(ToolStripItem item, string label)
        {
            if (string.IsNullOrEmpty(item.ToolTipText))
            {
                item.ToolTipText = item.Text;
            }

            if (!string.IsNullOrEmpty(label))
            {
                item.Text = label;
            }

            item.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            item.TextImageRelation = TextImageRelation.ImageAboveText;
            item.TextAlign = ContentAlignment.MiddleCenter;
            item.ImageAlign = ContentAlignment.MiddleCenter;
            item.AutoSize = true;
            item.Margin = new Padding(0, 2, 0, 2);
            item.Padding = new Padding(2, 2, 2, 2);
        }

        /// <summary>
        /// Open an image file in a new tab
        /// </summary>
        private void OpenImageFile()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "이미지 열기",
                Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|모든 파일|*.*"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                var image = ImageIO.LoadImage(dialog.FileName);
                if (image == null)
                {
                    MessageBox.Show(this, "이 파일은 이미지로 열 수 없어요.", "왕샷", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var surface = new Surface(image);
                surface.CaptureDetails.Title = Path.GetFileNameWithoutExtension(dialog.FileName);
                AddTab(surface, outputMade: true, fitWindow: false);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not open the image " + dialog.FileName, ex);
                MessageBox.Show(this, "이미지를 열지 못했어요.", "왕샷", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// The buttons of the destinations (they are added after the editor is shown) go to the group "보내기"
        /// </summary>
        private void MoveDestinationButtonsIntoRibbon()
        {
            if (_ribbonShareStrip == null)
            {
                return;
            }

            foreach (ToolStripItem item in destinationsToolStrip.Items.Cast<ToolStripItem>().Where(i => i.Tag is Greenshot.Base.Interfaces.IDestination).ToList())
            {
                MoveItem(item, _ribbonShareStrip);
                item.Visible = true;
                string text = item.ToolTipText ?? item.Text ?? string.Empty;
                if (text.IndexOf("OCR", StringComparison.OrdinalIgnoreCase) >= 0) { text = "글자 인식"; }
                else if (text.IndexOf("share", StringComparison.OrdinalIgnoreCase) >= 0) { text = "공유"; }
                MakeLarge(item, text);
            }

            ApplyRibbonDpi(DeviceDpi);
        }

        private IEnumerable<ToolStripItem> DestinationButtons()
        {
            var items = destinationsToolStrip.Items.Cast<ToolStripItem>();
            if (_ribbonShareStrip != null)
            {
                items = items.Concat(_ribbonShareStrip.Items.Cast<ToolStripItem>());
            }

            return items;
        }
    }
}
