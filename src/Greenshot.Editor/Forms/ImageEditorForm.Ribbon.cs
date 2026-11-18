using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greenshot.Base;
using Greenshot.Base.Controls;
using Greenshot.Base.Core;
using Greenshot.Base.Interfaces;
using Greenshot.Editor.Controls;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.Drawing.Fields;

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
        // 왕샷: the emphasis tools (spotlight, magnifying glass, click mark)
        private ToolStripDropButton _emphasisTool;
        private ToolStripButton _spotlightTool;
        private ToolStripButton _magnifyTool;
        private ToolStripButton _clickMarkTool;
        // 왕샷: the panel with the recent captures, left of the canvas
        private RecentCapturesPanel _recentCapturesPanel;
        private ToolStripButton _recentCapturesButton;

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
            int edit = _ribbon.AddTab("편집");        // 홈 → 편집 (주 작업 탭)
            int annotation = _ribbon.AddTab("주석");   // 편집 → 주석 (교재 작성 특화)
            int obj = _ribbon.AddTab("개체");          // 개체 (전문 편집)
            int view = _ribbon.AddTab("보기");         // 보기
            int export = _ribbon.AddTab("내보내기");    // 공유 → 내보내기

            // ============================================================
            // 파일 탭
            // ============================================================
            var saveAs = NewAction("다른 이름으로", "다른 이름으로 저장 (Ctrl+Shift+S)", null, (s, e) => DestinationHelper.StartExport(WellKnownDestinations.FileDialog, _surface));
            IconBinder.BindLike(saveAs, btnSave);
            AddRibbonGroup(file, "파일",
                (NewAction("새로 만들기", "새 이미지를 새 탭에 만들어요 (Ctrl+N)", "newImage.Image", (s, e) => AddNewImageTab()), null),
                (NewAction("열기", "이미지 파일 열기 (Ctrl+O)", "openFile.Image", (s, e) => OpenImageFile()), null),
                (btnSave, "저장 (Ctrl+S)"),
                (saveAs, null),
                (NewAction("PDF로 저장", "이미지를 PDF 파일로 저장해요", "pdfFile.Image", (s, e) => SaveAsPdf()), null));
            AddRibbonGroup(file, "창", (Proxy(closeToolStripMenuItem, "닫기 (Ctrl+W)"), null));

            // ============================================================
            // 편집 탭 (주 작업) - 그리기 도구 / 강조 도구 / 이미지 / 색상·속성
            // ============================================================
            var clipboardGroup = AddRibbonGroup(edit, "클립보드", (btnPaste, "붙여넣기 (Ctrl+V)"), (btnCut, "잘라내기 (Ctrl+X)"), (btnCopy, "복사 (Ctrl+C)"), (btnDelete, "삭제 (Del)"));
            foreach (ToolStripItem item in clipboardGroup.Items)
            {
                item.Padding = new Padding(2, 2, 2, 28);
            }

            // 선택/영역 도구
            CreateEmojiPresetButton();
            CreateAreaSelectButtons();
            var selectTool = new ToolStripDropButton { ToolTipText = "선택 도구 (V) - 개체 선택/이동/크기조절\n단축키: V | 드래그로 영역 선택", DropDown = BuildSelectMenu() };
            selectTool.MirrorOf(btnCursor);
            IconBinder.BindLike(selectTool, btnCursor);

            // 도형/선 도구 그룹화
            var shapeTool = new ToolStripDropButton();
            shapeTool.SetMembers((btnRect, "사각형 (R)"), (btnEllipse, "타원 (E)"));
            var lineTool = new ToolStripDropButton();
            lineTool.SetMembers((btnLine, "선 (L)"), (btnArrow, "화살표 (A)"));
            var penTool = new ToolStripDropButton();
            penTool.SetMembers((btnFreehand, "연필 (P)"), (btnHighlight, "형광펜 (H)"));

            // 왕샷: 강조 도구 (교재 작성 핵심)
            CreateEmphasisTools();

            // ---- 그리기 도구 그룹 ----
            var drawTools = AddRibbonGroup(edit, "그리기 도구",
                (selectTool, "선택 (V)"), (shapeTool, "도형"), (lineTool, "선/화살표"), (penTool, "연필/형광펜"),
                (btnText, "글자 (T)"), (btnSpeechBubble, "말풍선 (B)"));
            foreach (ToolStripItem tool in drawTools.Items)
            {
                tool.Padding = new Padding(2, 2, 2, 28);
            }

            // ---- 강조 도구 그룹 (신규, 분리) ----
            var emphasisGroup = AddRibbonGroup(edit, "강조 도구",
                (_emphasisTool, "강조 (S)"));
            foreach (ToolStripItem tool in emphasisGroup.Items)
            {
                tool.Padding = new Padding(2, 2, 2, 28);
            }

            // ---- 이미지 그룹 ----
            var effectsTool = new ToolStripDropButton { ToolTipText = "효과 (F) - 모자이크/블러/단색/그림자/흑백/반전", DropDown = BuildEffectsMenu() };
            effectsTool.MirrorOfAction(toolStripSplitButton1, () => effectsTool.ShowDropDown());
            btnCropEdge = new ToolStripButton { Text = "테두리 자르기", ToolTipText = "테두리 자르기 (K) - 이미지 가장자리를 안쪽으로 당겨 자르기 (Enter로 적용)", DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, CheckOnClick = false };
            IconBinder.Bind(btnCropEdge, IconSource.FromResource(typeof(ImageEditorForm), "btnCropEdge.Image"));
            btnCropEdge.Click += BtnCropEdgeClick;
            var cropTool = new ToolStripDropButton();
            cropTool.SetMembers((btnCropEdge, "테두리 자르기 (K)"), (btnCrop, "선택 자르기 (C)"));
            var imageGroup = AddRibbonGroup(edit, "이미지",
                (cropTool, "자르기"), (btnResize, "크기 (Ctrl+R)"), (rotateCwToolstripButton, "우회전 (Ctrl+Shift+Right)"), (rotateCcwToolstripButton, "좌회전 (Ctrl+Shift+Left)"), (effectsTool, "효과 (F)"));
            foreach (ToolStripItem item in imageGroup.Items)
            {
                item.Padding = new Padding(2, 2, 2, 28);
            }

            // 색상 팔레트 (기존 유지)
            _palette = new ColorPaletteControl();
            _palette.ThicknessChanged += value => lineThicknessUpDown.Value = value;
            _palette.ColorChosen += (color, fill) => _surface.FieldAggregator.GetField(fill ? Greenshot.Editor.Drawing.Fields.FieldType.FILL_COLOR : Greenshot.Editor.Drawing.Fields.FieldType.LINE_COLOR).Value = color;
            foreach (var colorButton in new ToolStripItem[] { btnFillColor, btnLineColor, lineThicknessLabel, lineThicknessUpDown })
            {
                colorButton.Available = false;
                colorButton.AvailableChanged += (s, e) =>
                {
                    if (colorButton.Available)
                    {
                        colorButton.Available = false;
                    }
                };
            }
            _ribbon.AddPaletteGroup(edit, "색상 · 속성", _palette, propertiesToolStrip);

            // ============================================================
            // 주석 탭 (교재 작성 특화) - 텍스트 / 강조 / 가리기 / 스탬프 / 정렬·스타일
            // ============================================================
            var emojiTool = new ToolStripDropButton { ToolTipText = "이모지 (M) - 자주 쓰는 이모지 빠른 선택\n단축키: M | 아래 화살표로 더 보기", DropDown = _emojiPresetDropDown };
            emojiTool.MirrorOf(btnEmoji);
            emojiTool.ImageScaling = ToolStripItemImageScaling.None;
            _emojiTool = emojiTool;
            emojiTool.SyncImage();

            // 텍스트 그룹
            AddRibbonGroup(annotation, "텍스트",
                (btnText, "글자 (T)"), (btnSpeechBubble, "말풍선 (B)"));

            // 강조 도구 그룹 (편집 탭에도 있지만 여기서는 프리셋 선택 중심)
            var highlightPresetTool = new ToolStripDropButton { ToolTipText = "하이라이트 프리셋", DropDown = highlightModeButton.DropDown };
            // highlightModeButton은 BindableToolStripDropDownButton이므로 MirrorOf 대신 아이콘만 복사
            if (highlightModeButton.Image != null)
            {
                highlightPresetTool.Image = (Image)highlightModeButton.Image.Clone();
            }
            AddRibbonGroup(annotation, "강조",
                (highlightPresetTool, "형광펜/스포트라이트/돋보기"), (_emphasisTool, "강조 도구 (S)"));

            // 가리기 그룹
            var obfuscateTool = new ToolStripDropButton { ToolTipText = "가리기 (Q) - 픽셀화/블러/단색/텍스트 가리기", DropDown = obfuscateModeButton.DropDown };
            if (obfuscateModeButton.Image != null)
            {
                obfuscateTool.Image = (Image)obfuscateModeButton.Image.Clone();
            }
            AddRibbonGroup(annotation, "가리기",
                (obfuscateTool, "가리기 (Q)"));

            // 스탬프 그룹 (번호/이모지/화살표 스탬프)
            AddRibbonGroup(annotation, "스탬프",
                (btnStepLabel, "번호 (N)"), (emojiTool, "이모지 (M)"));

            // 정렬·스타일 그룹
            var alignTool = new ToolStripDropButton();
            alignTool.SetMembers(
                (ProxyIcon(upToTopToolStripMenuItem, "맨 앞으로", "arrangeFront.Image"), null),
                (ProxyIcon(upOneLevelToolStripMenuItem, "앞으로", "arrangeForward.Image"), null),
                (ProxyIcon(downOneLevelToolStripMenuItem, "뒤로", "arrangeBackward.Image"), null),
                (ProxyIcon(downToBottomToolStripMenuItem, "맨 뒤로", "arrangeBack.Image"), null)
            );
            var quickStyleTool = new ToolStripDropButton { ToolTipText = "퀵 스타일 - 저장된 서식 조합 적용" };
            // 퀵 스타일 메뉴는 나중에 구현
            var formatPainter = NewAction("서식 복사", "서식 복사 (Ctrl+Shift+C) - 선택한 개체의 색/선/글꼴을 다른 개체에 적용", "formatPainter.Image", (s, e) => CopyFormat());
            AddRibbonGroup(annotation, "정렬 · 스타일",
                (alignTool, "정렬"), (quickStyleTool, "퀵 스타일"), (formatPainter, null));

            // ============================================================
            // 개체 탭 (전문 편집) - 순서 / 변형 / 그룹 / 파일
            // ============================================================
            AddRibbonGroup(obj, "순서",
                (ProxyIcon(upToTopToolStripMenuItem, "맨 앞으로 (Home)", "arrangeFront.Image"), null),
                (ProxyIcon(upOneLevelToolStripMenuItem, "앞으로 (PgUp)", "arrangeForward.Image"), null),
                (ProxyIcon(downOneLevelToolStripMenuItem, "뒤로 (PgDn)", "arrangeBackward.Image"), null),
                (ProxyIcon(downToBottomToolStripMenuItem, "맨 뒤로 (End)", "arrangeBack.Image"), null));

            var flipTool = new ToolStripDropButton();
            flipTool.SetMembers(
                (NewAction("수평 뒤집기", "수평 뒤집기 (Ctrl+H)", "flipHorizontal.Image", (s, e) => FlipSelected(true)), null),
                (NewAction("수직 뒤집기", "수직 뒤집기 (Ctrl+Shift+H)", "flipVertical.Image", (s, e) => FlipSelected(false)), null)
            );
            var transformTool = new ToolStripDropButton();
            transformTool.SetMembers(
                (NewAction("자유 변형", "자유 변형 (Ctrl+T) - 크기/회전/기울이기", "freeTransform.Image", (s, e) => FreeTransformSelected()), null),
                (NewAction("크기 지정", "정확한 크기 지정 (Ctrl+Shift+R)", "resize.Image", (s, e) => ShowResizeDialog()), null)
            );
            AddRibbonGroup(obj, "변형",
                (rotateCwToolstripButton, "우회전 (Ctrl+Shift+Right)"), (rotateCcwToolstripButton, "좌회전 (Ctrl+Shift+Left)"),
                (flipTool, "뒤집기"), (transformTool, "변형"));

            var groupTool = new ToolStripDropButton();
            groupTool.SetMembers(
                (NewAction("그룹", "선택한 개체 그룹화 (Ctrl+G)", "group.Image", (s, e) => GroupSelected()), null),
                (NewAction("그룹 해제", "그룹 해제 (Ctrl+Shift+G)", "ungroup.Image", (s, e) => UngroupSelected()), null),
                (NewAction("병합", "선택한 개체 하나로 병합", "merge.Image", (s, e) => MergeSelected()), null)
            );
            AddRibbonGroup(obj, "그룹",
                (groupTool, "그룹/해제/병합"));

            AddRibbonGroup(obj, "개체 파일",
                (ProxyIcon(saveElementsToolStripMenuItem, "개체 저장 (Ctrl+Shift+S)", "btnSave.Image"), null),
                (ProxyIcon(loadElementsToolStripMenuItem, "개체 불러오기 (Ctrl+Shift+O)", "openFile.Image"), null),
                (NewAction("템플릿으로 저장", "현재 개체들을 템플릿으로 저장", "template.Image", (s, e) => SaveAsTemplate()), null));

            // ============================================================
            // 보기 탭 - 확대/축소 / 표시 / 레이아웃
            // ============================================================
            var zoomIn = NewZoomButton("zoomInMenuItem.Image", "확대 (Ctrl++)", (s, e) => ZoomInMenuItemClick(s, e));
            var zoomOut = NewZoomButton("zoomOutMenuItem.Image", "축소 (Ctrl+-)", (s, e) => ZoomOutMenuItemClick(s, e));
            var zoomActual = NewZoomButton("zoomActualSizeMenuItem.Image", "100% (Ctrl+1)", (s, e) => ZoomSetValueMenuItemClick(zoomActualSizeMenuItem, e));
            var zoomFit = NewZoomButton("zoomBestFitMenuItem.Image", "맞춤 (Ctrl+0)", (s, e) => ZoomBestFitMenuItemClick(s, e));
            AddRibbonGroup(view, "확대/축소", (zoomIn, null), (zoomOut, null), (zoomActual, null), (zoomFit, null));

            var rulerButton = NewAction("눈금자", "가로·세로 눈금자 보이기/숨기기 (Ctrl+R)", "ruler.Image", (s, e) => SetRulersShown(((ToolStripButton)s).Checked));
            rulerButton.CheckOnClick = true;
            rulerButton.Checked = true;

            _recentCapturesButton = NewAction("최근 캡처", "왼쪽 사이드바에 최근 캡처 표시/숨기기 (Ctrl+Shift+R)", "recentCaptures.Image", (s, e) => SetRecentCapturesPanelShown(((ToolStripButton)s).Checked));
            _recentCapturesButton.CheckOnClick = true;

            var gridButton = NewAction("그리드", "그리드 표시/숨기기 (Ctrl+G)", "grid.Image", (s, e) => ToggleGrid());
            gridButton.CheckOnClick = true;

            var fullscreenButton = NewAction("전체 화면", "전체 화면 모드 (F11)", "fullscreen.Image", (s, e) => ToggleFullscreen());
            AddRibbonGroup(view, "표시", (rulerButton, null), (_recentCapturesButton, null), (gridButton, null), (fullscreenButton, null));

            var layoutTool = new ToolStripDropButton();
            layoutTool.SetMembers(
                (NewAction("캔버스 중앙", "이미지를 캔버스 중앙에 정렬", "centerCanvas.Image", (s, e) => CenterCanvas()), null),
                (NewAction("여백 설정", "캔버스 여백 설정", "margin.Image", (s, e) => SetCanvasMargin()), null)
            );
            AddRibbonGroup(view, "레이아웃", (layoutTool, "캔버스"));

            // ============================================================
            // 내보내기 탭 - 복사 / 보내기 / 자동화
            // ============================================================
            AddRibbonGroup(export, "클립보드",
                (btnClipboard, "이미지 복사 (Ctrl+C)"),
                (NewAction("개체 복사", "선택한 개체만 복사 (Ctrl+Shift+C)", "copyObjects.Image", (s, e) => CopySelectedObjectsToClipboard()), null),
                (NewAction("텍스트 복사", "OCR로 텍스트 추출 복사 (Ctrl+Shift+T)", "copyText.Image", (s, e) => CopyOcrText()), null));

            _ribbonShareStrip = AddRibbonGroup(export, "보내기");
            AddRibbonGroup(export, "자동화",
                (NewAction("스크롤 캡처", "스크롤 캡처 (Ctrl+Alt+S) - 마우스 아래 창 자동 스크롤 캡처", "scrollCapture.Image", (s, e) => SimpleServiceProvider.Current.GetInstance<IGreenshotShell>().StartScrollCapture()), null),
                (NewAction("배치 처리", "여러 이미지 일괄 처리", "batch.Image", (s, e) => ShowBatchDialog()), null));

            // 기본 탭: 편집 (주 작업)
            _ribbon.SelectTab(edit);

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

            // WangShot: the panel with the recent captures, if it was shown the last time
            if (EditorConfiguration.RecentCapturesPanelShown)
            {
                SetRecentCapturesPanelShown(true);
            }
        }

        /// <summary>
        /// 왕샷: the emphasis tools, in one button with a chevron: spotlicht (everything but the area dark), magnifying glass and click mark.
        /// The spotlight and the magnifying glass are the highlight tool with their preset, the click mark is an element of its own.
        /// </summary>
        private void CreateEmphasisTools()
        {
            _spotlightTool = NewAction("스포트라이트", "스포트라이트: 선택한 영역만 밝게 남기고 나머지는 어둡게 해요 (드래그)", "areaHighlightMenuItem.Image",
                (s, e) => StartHighlightTool(FilterContainer.PreparedFilter.SPOTLIGHT));
            _magnifyTool = NewAction("돋보기", "돋보기: 선택한 영역의 가운데를 동그란 렌즈로 확대해 보여줘요 (드래그)", "magnifyMenuItem.Image",
                (s, e) => StartHighlightTool(FilterContainer.PreparedFilter.MAGNIFY_LENS));
            _clickMarkTool = NewAction("클릭 표시", "클릭 표시: 클릭한 지점에 동그라미를 그려요 (크기는 나중에 조절할 수 있어요)", "clickMark.Image",
                (s, e) => StartDrawingMode(DrawingModes.ClickMark));
            _emphasisTool = new ToolStripDropButton();
            _emphasisTool.SetMembers((_spotlightTool, "스포트라이트"), (_magnifyTool, "돋보기"), (_clickMarkTool, "클릭 표시"));

            // The preset of the highlight tool can be changed in the properties, too: the two new presets are there as well
            var spotlightPreset = new ToolStripMenuItem("스포트라이트") { Tag = FilterContainer.PreparedFilter.SPOTLIGHT };
            IconBinder.Bind(spotlightPreset, IconSource.FromResource(typeof(ImageEditorForm), "areaHighlightMenuItem.Image"));
            var magnifyPreset = new ToolStripMenuItem("돋보기") { Tag = FilterContainer.PreparedFilter.MAGNIFY_LENS };
            IconBinder.Bind(magnifyPreset, IconSource.FromResource(typeof(ImageEditorForm), "magnifyMenuItem.Image"));
            highlightModeButton.DropDownItems.Add(spotlightPreset);
            highlightModeButton.DropDownItems.Add(magnifyPreset);

            // 왕샷: the solid block is a third way to hide something, next to the mosaic and the blur
            var solidPreset = new ToolStripMenuItem("단색") { Tag = FilterContainer.PreparedFilter.SOLID, ToolTipText = "가린 곳을 한 가지 색으로 덮어요 (색은 아래 색 팔레트에서)" };
            IconBinder.Bind(solidPreset, IconSource.FromResource(typeof(ImageEditorForm), "solidToolStripMenuItem.Image"));
            obfuscateModeButton.DropDownItems.Add(solidPreset);
        }

        private void StartHighlightTool(FilterContainer.PreparedFilter preset)
        {
            var presetField = _surface.FieldAggregator.GetField(FieldType.PREPARED_FILTER_HIGHLIGHT);
            if (presetField != null)
            {
                presetField.Value = preset;
            }

            StartDrawingMode(DrawingModes.Highlight);
        }

        /// <summary>
        /// Start a tool: the canvas draws with it, and its button is the checked one
        /// </summary>
        private void StartDrawingMode(DrawingModes drawingMode)
        {
            _surface.DrawingMode = drawingMode;
            RefreshFieldControls();
        }

        /// <summary>
        /// 왕샷: show or hide the recent captures left of the canvas, the choice is remembered
        /// </summary>
        private void SetRecentCapturesPanelShown(bool shown)
        {
            if (_recentCapturesPanel == null)
            {
                if (!shown)
                {
                    return;
                }

                _recentCapturesPanel = new RecentCapturesPanel(this);
                var content = topToolStripContainer.ContentPanel;
                content.Controls.Add(_recentCapturesPanel);
                // The tab strip is docked first (it is at the end of the collection), then the panel at the left, the canvas fills the rest
                content.Controls.SetChildIndex(_recentCapturesPanel, content.Controls.Count - 2);
            }

            _recentCapturesPanel.Visible = shown;
            EditorConfiguration.RecentCapturesPanelShown = shown;
            if (_recentCapturesButton != null)
            {
                _recentCapturesButton.Checked = shown;
            }
        }

        /// <summary>
        /// 왕샷: a capture of the recent captures panel opens in a new tab
        /// </summary>
        internal void OpenRecentCaptureInNewTab(string path)
        {
            try
            {
                var image = ImageIO.LoadImage(path);
                if (image == null)
                {
                    MessageBox.Show(this, "이 파일은 이미지로 열 수 없어요.", "왕샷", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var surface = new Surface(image);
                surface.CaptureDetails.Title = Path.GetFileNameWithoutExtension(path);
                AddTab(surface, outputMade: true, fitWindow: false);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not open the capture in a new tab: " + path, ex);
                MessageBox.Show(this, "이미지를 열지 못했어요.", "왕샷", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        // ===== 왕샷: 리본에서 호출하는 새 기능들 (ImageEditorForm.cs에 구현) =====

        /// <summary>
        /// 서식 복사: 선택한 개체의 색/선/글꼴을 다른 개체에 적용
        /// </summary>
        private void CopyFormat()
        {
            if (_surface?.HasSelectedElements == true)
            {
                _surface.CopyFormat();
            }
        }

        /// <summary>
        /// 선택한 개체 수평/수직 뒤집기
        /// </summary>
        private void FlipSelected(bool horizontal)
        {
            if (_surface?.HasSelectedElements == true)
            {
                _surface.FlipSelected(horizontal);
            }
        }

        /// <summary>
        /// 자유 변형 모드
        /// </summary>
        private void FreeTransformSelected()
        {
            if (_surface?.HasSelectedElements == true)
            {
                _surface.StartFreeTransform();
            }
        }

        /// <summary>
        /// 크기 지정 대화상자
        /// </summary>
        private void ShowResizeDialog()
        {
            if (_surface?.HasSelectedElements == true)
            {
                _surface.ShowResizeDialog();
            }
        }

        /// <summary>
        /// 선택한 개체 그룹화
        /// </summary>
        private void GroupSelected()
        {
            if (_surface?.HasSelectedElements == true)
            {
                _surface.GroupSelected();
            }
        }

        /// <summary>
        /// 그룹 해제
        /// </summary>
        private void UngroupSelected()
        {
            if (_surface?.HasSelectedElements == true)
            {
                _surface.UngroupSelected();
            }
        }

        /// <summary>
        /// 선택한 개체 병합
        /// </summary>
        private void MergeSelected()
        {
            if (_surface?.HasSelectedElements == true)
            {
                _surface.MergeSelected();
            }
        }

        /// <summary>
        /// 템플릿으로 저장
        /// </summary>
        private void SaveAsTemplate()
        {
            if (_surface?.HasSelectedElements == true)
            {
                _surface.SaveAsTemplate();
            }
        }

        /// <summary>
        /// 그리드 토글
        /// </summary>
        private void ToggleGrid()
        {
            if (_surface != null)
            {
                _surface.ShowGrid = !_surface.ShowGrid;
                _surface.Invalidate();
            }
        }

        /// <summary>
        /// 전체 화면 토글
        /// </summary>
        private void ToggleFullscreen()
        {
            if (WindowState == FormWindowState.Normal)
            {
                WindowState = FormWindowState.Maximized;
                FormBorderStyle = FormBorderStyle.None;
                topToolStripContainer.TopToolStripPanel.Visible = false;
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                WindowState = FormWindowState.Normal;
                topToolStripContainer.TopToolStripPanel.Visible = true;
            }
        }

        /// <summary>
        /// 캔버스 중앙 정렬
        /// </summary>
        private void CenterCanvas()
        {
            _surface?.CenterCanvas();
        }

        /// <summary>
        /// 캔버스 여백 설정
        /// </summary>
        private void SetCanvasMargin()
        {
            _surface?.ShowCanvasMarginDialog();
        }

        /// <summary>
        /// OCR 텍스트 복사
        /// </summary>
        private void CopyOcrText()
        {
            _surface?.CopyOcrText();
        }

        /// <summary>
        /// 배치 처리 대화상자
        /// </summary>
        private void ShowBatchDialog()
        {
            // TODO: 배치 처리 구현
            MessageBox.Show(this, "배치 처리 기능은 준비 중입니다.", "왕샷", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
