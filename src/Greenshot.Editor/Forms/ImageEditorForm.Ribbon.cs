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
using Greenshot.Editor.Drawing.Fields.Binding;

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
        private ToolStripButton _fullscreenButton;
        // 왕샷: 서식 컨텍스트 탭 인덱스
        private int _formatTabIndex = -1;
        // 왕샷: 서식 탭의 컨트롤들 (표면이 연결될 때 바인딩한다 - 폼 생성 시에는 _surface가 아직 없다)
        private FontFamilyComboBox _formatFontFamilyComboBox;
        private ToolStripNumericUpDown _formatFontSizeUpDown;
        private BindableToolStripButton _formatFontBoldButton;
        private BindableToolStripButton _formatFontItalicButton;
        private BindableToolStripDropDownButton _formatTextHorizontalAlignmentButton;
        private BindableToolStripDropDownButton _formatTextVerticalAlignmentButton;
        private BindableToolStripButton _formatShadowButton;
        private ToolStripColorButton _formatLineColorButton;
        private ToolStripColorButton _formatFillColorButton;
        private ToolStripNumericUpDown _formatLineThicknessUpDown;

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
            // 왕샷: 컨텍스트 탭 - 텍스트 편집 시에만 표시되는 "서식" 탭
            _formatTabIndex = _ribbon.AddTab("서식");
            _ribbon.SetTabVisible(_formatTabIndex, false); // 기본적으로 숨김

            // 왕샷: KeyTip 설정 (Alt 키로 활성화되는 단축키)
            _ribbon.SetTabKeyTip(file, "F");      // 파일
            _ribbon.SetTabKeyTip(edit, "H");      // 편집 (Home)
            _ribbon.SetTabKeyTip(annotation, "A"); // 주석 (Annotation)
            _ribbon.SetTabKeyTip(obj, "O");       // 개체 (Object)
            _ribbon.SetTabKeyTip(view, "V");      // 보기 (View)
            _ribbon.SetTabKeyTip(export, "E");    // 내보내기 (Export)
            _ribbon.SetTabKeyTip(_formatTabIndex, "T"); // 서식 (Text format)

            // ============================================================
            // 파일 탭
            // ============================================================
            var saveAs = NewAction("다른 이름으로", "다른 이름으로 저장 (Ctrl+Shift+S)", null, (s, e) => DestinationHelper.StartExport(WellKnownDestinations.FileDialog, _surface));
            IconBinder.BindLike(saveAs, btnSave);
            AddRibbonGroup(file, "파일",
                (NewAction("새로 만들기", "새 이미지를 새 탭에 만들어요 (Ctrl+N)", "newImage.Image", (s, e) => AddNewImageTab()), null),
                (NewAction("열기", "이미지 파일 열기 (Ctrl+O)", "openFile.Image", (s, e) => OpenImageFile()), null),
                (btnSave, "저장 (Ctrl+S)"),
                (saveAs, null));
            AddRibbonGroup(file, "캡처",
                (NewAction("스크롤 캡처", "스크롤 캡처: 누르고 3초 안에 캡처할 창 위에 마우스를 올려 두세요.\n창을 끝까지 스크롤하며 한 장으로 이어 붙여요 (Esc로 멈춤)", "scrollCapture.Image", (s, e) => StartScrollCaptureFromRibbon()), null));
            AddRibbonGroup(file, "창", (Proxy(closeToolStripMenuItem, "창 닫기"), null));

            // 왕샷: 파일 탭 그룹 KeyTips
            _ribbon.SetGroupKeyTip(file, 0, "F"); // 파일
            _ribbon.SetGroupKeyTip(file, 1, "C"); // 캡처
            _ribbon.SetGroupKeyTip(file, 2, "W"); // 창

            // ============================================================
            // 편집 탭 (주 작업) - 그리기 도구 / 강조 도구 / 이미지 / 색상·속성
            // ============================================================
            var clipboardGroup = AddRibbonGroup(edit, "클립보드", (btnPaste, "붙여넣기 (Ctrl+V)"), (btnCut, "잘라내기 (Ctrl+X)"), (btnCopy, "복사 (Ctrl+C)"), (btnDelete, "삭제 (Del)"));
            foreach (ToolStripItem item in clipboardGroup.Items)
            {
                UseChevronRow(item);
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
            penTool.SetMembers((btnFreehand, "연필 (F)"), (btnHighlight, "형광펜 (H)"));

            // 왕샷: 강조 도구 (교재 작성 핵심)
            CreateEmphasisTools();
            var editText = new ToolStripDropButton();
            editText.MirrorOf(btnText);
            IconBinder.BindLike(editText, btnText);
            var editSpeech = new ToolStripDropButton();
            editSpeech.MirrorOf(btnSpeechBubble);
            IconBinder.BindLike(editSpeech, btnSpeechBubble);
            var editEmphasis = new ToolStripDropButton();
            editEmphasis.SetMembers((_spotlightTool, "스포트라이트"), (_magnifyTool, "돋보기"), (_clickMarkTool, "클릭 표시"));

            // ---- 그리기 도구 그룹 ----
            var drawTools = AddRibbonGroup(edit, "그리기 도구",
                (selectTool, "선택 (V)"), (shapeTool, "도형"), (lineTool, "선/화살표"), (penTool, "연필/형광펜"),
                (editText, "글자"), (editSpeech, "말풍선"));
            foreach (ToolStripItem tool in drawTools.Items)
            {
                UseChevronRow(tool);
            }

            // ---- 강조 도구 그룹 (신규, 분리) ----
            var emphasisGroup = AddRibbonGroup(edit, "강조 도구",
                (editEmphasis, "강조"));
            foreach (ToolStripItem tool in emphasisGroup.Items)
            {
                UseChevronRow(tool);
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
                (cropTool, "자르기"), (btnResize, "이미지 크기"), (rotateCwToolstripButton, "우회전"), (rotateCcwToolstripButton, "좌회전"),
                (NewAction("여백 추가", "캔버스의 각 방향에 여백 추가", "CropHorizontal.Image", (s, e) => SetCanvasMargin()), null),
                (effectsTool, "효과"));
            foreach (ToolStripItem item in imageGroup.Items)
            {
                UseChevronRow(item);
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
            // Keep related options together instead of letting crop and text controls
            // wrap at arbitrary positions when the selected tool changes.
            if (propertiesToolStrip.LayoutSettings is FlowLayoutSettings propertyRows)
            {
                propertyRows.SetFlowBreak(fontSizeUpDown, true);
                propertyRows.SetFlowBreak(btnCancel, true);
                propertyRows.SetFlowBreak(btnCropAuto, true);
                propertyRows.SetFlowBreak(cutMarkStyleButton, true);
                propertyRows.SetFlowBreak(toothHeightUpDown, true);
            }

            // 왕샷: 편집 탭 그룹 KeyTips
            _ribbon.SetGroupKeyTip(edit, 0, "C"); // 클립보드
            _ribbon.SetGroupKeyTip(edit, 1, "D"); // 그리기 도구
            _ribbon.SetGroupKeyTip(edit, 2, "E"); // 강조 도구
            _ribbon.SetGroupKeyTip(edit, 3, "I"); // 이미지
            _ribbon.SetGroupKeyTip(edit, 4, "P"); // 색상 · 속성

            // ============================================================
            // 주석 탭 (교재 작성 특화) - 텍스트 / 강조 / 가리기 / 스탬프 / 정렬·스타일
            // ============================================================
            var emojiTool = new ToolStripDropButton { ToolTipText = "이모지 (M) - 자주 쓰는 이모지 빠른 선택\n단축키: M | 아래 화살표로 더 보기", DropDown = _emojiPresetDropDown };
            emojiTool.MirrorOf(btnEmoji);
            _emojiTool = emojiTool;
            IconBinder.Bind(emojiTool, IconSource.FromResource(typeof(ImageEditorForm), "emojiBoss.Image"));

            // 텍스트 그룹
            AddRibbonGroup(annotation, "텍스트",
                (btnText, "글자"), (btnSpeechBubble, "말풍선"));

            // 강조 도구 그룹 (편집 탭에도 있지만 여기서는 프리셋 선택 중심)
            var highlightPresetTool = new ToolStripDropButton { ToolTipText = "형광펜, 스포트라이트, 돋보기 설정", DropDown = highlightModeButton.DropDown };
            IconBinder.Bind(highlightPresetTool, IconSource.FromResource(typeof(ImageEditorForm), "highlightModeButton.Image"));
            AddRibbonGroup(annotation, "강조",
                (highlightPresetTool, "강조 설정"), (_emphasisTool, "강조 도구"));

            // 가리기 그룹
            var obfuscateTool = new ToolStripDropButton { ToolTipText = "가리기 (O) - 픽셀화/블러/단색/텍스트 가리기", DropDown = obfuscateModeButton.DropDown };
            IconBinder.Bind(obfuscateTool, IconSource.FromResource(typeof(ImageEditorForm), "obfuscateModeButton.Image"));
            AddRibbonGroup(annotation, "가리기",
                (obfuscateTool, "가리기"),
                (NewAction("민감정보 자동", "민감정보 자동 가리기: 전화번호·이메일·주민등록번호·카드번호를 글자 인식(OCR)으로 찾아 모자이크로 가려요.\n결과는 아래 상태 표시줄에 나와요. 놓친 곳이 없는지 꼭 확인하세요 (Ctrl+Z로 되돌리기)", "obfuscateModeButton.Image", (s, e) => ObfuscateSensitiveInfo()), null));

            // 스탬프 그룹 (번호/이모지/화살표 스탬프)
            AddRibbonGroup(annotation, "스탬프",
                (btnStepLabel, "번호"), (emojiTool, "이모지"));

            // 왕샷: 주석 탭 그룹 KeyTips
            _ribbon.SetGroupKeyTip(annotation, 0, "T"); // 텍스트
            _ribbon.SetGroupKeyTip(annotation, 1, "H"); // 강조
            _ribbon.SetGroupKeyTip(annotation, 2, "Q"); // 가리기
            _ribbon.SetGroupKeyTip(annotation, 3, "S"); // 스탬프

            // ============================================================
            // 개체 탭 (전문 편집) - 순서 / 변형 / 파일
            // ============================================================
            AddRibbonGroup(obj, "쌓기 순서",
                (ProxyIcon(upToTopToolStripMenuItem, "맨 앞으로", "arrangeFront.Image"), null),
                (ProxyIcon(upOneLevelToolStripMenuItem, "앞으로", "arrangeForward.Image"), null),
                (ProxyIcon(downOneLevelToolStripMenuItem, "뒤로", "arrangeBackward.Image"), null),
                (ProxyIcon(downToBottomToolStripMenuItem, "맨 뒤로", "arrangeBack.Image"), null));

            var flipTool = new ToolStripDropButton();
            flipTool.SetMembers(
                (NewAction("수평 뒤집기", "선택한 개체 수평 뒤집기", "flipHorizontal.Image", (s, e) => FlipSelected(true)), null),
                (NewAction("수직 뒤집기", "선택한 개체 수직 뒤집기", "flipVertical.Image", (s, e) => FlipSelected(false)), null)
            );
            var transformTool = new ToolStripDropButton();
            transformTool.SetMembers(
                (NewAction("자유 변형", "선택한 개체 변형", "freeTransform.Image", (s, e) => FreeTransformSelected()), null),
                (NewAction("크기 지정", "정확한 크기 지정", "freeTransform.Image", (s, e) => ShowResizeDialog()), null)
            );
            AddRibbonGroup(obj, "변형",
                (flipTool, "뒤집기"), (transformTool, "변형"));

            AddRibbonGroup(obj, "복사 · 서식",
                (NewAction("개체 복사", "선택한 개체를 편집기에 붙여넣을 수 있도록 복사 (Ctrl+Shift+C)", "copyToolStripMenuItem.Image", (s, e) => CopySelectedObjectsToClipboard()), null),
                (NewAction("서식 복사", "선택한 개체의 서식 저장", "btnCopy.Image", (s, e) => CopyFormat()), null),
                (NewAction("서식 적용", "복사한 서식을 선택한 개체에 적용", "btnPaste.Image", (s, e) => _surface?.PasteFormat()), null));

            AddRibbonGroup(obj, "개체 파일",
                (ProxyIcon(saveElementsToolStripMenuItem, "개체 저장", "btnSave.Image"), null),
                (ProxyIcon(loadElementsToolStripMenuItem, "개체 불러오기", "openFile.Image"), null));

            // 왕샷: 개체 탭 그룹 KeyTips
            _ribbon.SetGroupKeyTip(obj, 0, "O"); // 순서
            _ribbon.SetGroupKeyTip(obj, 1, "T"); // 변형
            _ribbon.SetGroupKeyTip(obj, 2, "C"); // 복사 · 서식
            _ribbon.SetGroupKeyTip(obj, 3, "F"); // 개체 파일

            // ============================================================
            // 보기 탭 - 확대/축소 / 표시 / 레이아웃
            // ============================================================
            // 왕캡: short names under the icons, the shortcut keys in the tool tips (like every other button)
            var zoomIn = NewAction("확대", "확대 (Ctrl++)", "zoomInMenuItem.Image", (s, e) => ZoomInMenuItemClick(s, e));
            var zoomOut = NewAction("축소", "축소 (Ctrl+-)", "zoomOutMenuItem.Image", (s, e) => ZoomOutMenuItemClick(s, e));
            var zoomActual = NewAction("100%", "실제 크기 100% (Ctrl+0)", "zoomActualSizeMenuItem.Image", (s, e) => ZoomSetValueMenuItemClick(zoomActualSizeMenuItem, e));
            var zoomFit = NewAction("창에 맞춤", "창에 맞춤 (Ctrl+9)", "zoomBestFitMenuItem.Image", (s, e) => ZoomBestFitMenuItemClick(s, e));
            AddRibbonGroup(view, "확대/축소", (zoomIn, null), (zoomOut, null), (zoomActual, null), (zoomFit, null));

            var rulerButton = NewAction("눈금자", "가로·세로 눈금자 보이기/숨기기", "ruler.Image", (s, e) => SetRulersShown(((ToolStripButton)s).Checked));
            rulerButton.CheckOnClick = true;
            rulerButton.Checked = true;

            _recentCapturesButton = NewAction("최근 캡처", "왼쪽 사이드바에 최근 캡처 표시/숨기기", "recentCaptures.Image", (s, e) => SetRecentCapturesPanelShown(((ToolStripButton)s).Checked));
            _recentCapturesButton.CheckOnClick = true;

            var gridButton = NewAction("그리드", "격자(그리드) 보이기/숨기기", "grid.Image", (s, e) => ToggleGrid());
            gridButton.CheckOnClick = true;

            _fullscreenButton = NewAction("전체 화면", FullscreenTip, "fullscreen.Image", (s, e) => ToggleFullscreen());
            AddRibbonGroup(view, "표시", (rulerButton, null), (_recentCapturesButton, null), (gridButton, null), (_fullscreenButton, null));

            AddRibbonGroup(view, "화면 위치",
                (NewAction("화면 중앙 보기", "이미지의 가운데로 화면 이동", "zoomBestFitMenuItem.Image", (s, e) => CenterCanvas()), null));

            // 왕샷: 보기 탭 그룹 KeyTips
            _ribbon.SetGroupKeyTip(view, 0, "Z"); // 확대/축소
            _ribbon.SetGroupKeyTip(view, 1, "D"); // 표시
            _ribbon.SetGroupKeyTip(view, 2, "L"); // 레이아웃

            // ============================================================
            // 내보내기 탭 - 복사 / 저장 / 보내기 / 자동화
            // ============================================================
            AddRibbonGroup(export, "클립보드",
                (btnClipboard, "전체 이미지 복사"),
                (NewAction("텍스트 복사", "이미지 속 글자를 인식(OCR)해 클립보드에 복사 (Ctrl+Shift+T)", "copyText.Image", (s, e) => CopyOcrText()), null));

            AddRibbonGroup(export, "저장",
                (NewAction("PDF로 저장", "이미지를 PDF 파일로 저장해요 (용지·여백은 PDF 설정을 따라요)", "pdfFile.Image", (s, e) => SaveAsPdf()), null));
            _ribbonShareStrip = AddRibbonGroup(export, "보내기");

            // 왕샷: 내보내기 탭 그룹 KeyTips
            _ribbon.SetGroupKeyTip(export, 0, "C"); // 클립보드
            _ribbon.SetGroupKeyTip(export, 1, "S"); // 저장
            _ribbon.SetGroupKeyTip(export, 2, "D"); // 보내기

            // 기본 탭: 편집 (주 작업)
            _ribbon.SelectTab(edit);

            // ---- Quick access tools, and the settings and the help on the right of the same row
            var quick = _ribbon.QuickAccess;
            foreach (var item in new ToolStripItem[]
                     {
                         NewAction(null, "새로 만들기 (Ctrl+N)", "newImage.Image", (s, e) => AddNewImageTab()),
                         NewAction(null, "열기 (Ctrl+O)", "openFile.Image", (s, e) => OpenImageFile()),
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

            // ============================================================
            // 서식 탭 (컨텍스트 탭) - 텍스트 편집 시에만 표시
            // ============================================================
            CreateFormatTab(_formatTabIndex);

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
            // 왕캡: the editor opens wide enough for the whole ribbon, but it may be made narrower (e.g. half of the screen next to the
            // document): arrows on the edges of the ribbon then show and move the groups which do not fit. Never wider than the screen.
            int workingWidth = Screen.FromControl(this).WorkingArea.Width;
            MinimumSize = new Size(Math.Min(Math.Max(MinimumSize.Width, LogicalToDeviceUnits(600)), workingWidth), MinimumSize.Height);
            int preferredWidth = Math.Min(LogicalToDeviceUnits(1280), workingWidth);
            if (Width < preferredWidth && WindowState == FormWindowState.Normal)
            {
                Width = preferredWidth;
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
        /// 왕샷: 텍스트 서식 컨텍스트 탭 생성
        /// </summary>
        private void CreateFormatTab(int tabIndex)
        {
            // 글꼴 그룹
            var fontGroup = _ribbon.AddGroup(tabIndex, "글꼴");

            // 글꼴 패밀리 콤보박스 (ToolStripComboBox 상속)
            _formatFontFamilyComboBox = new FontFamilyComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                MaxDropDownItems = 20,
                Width = 180,
                Padding = new Padding(2, 0, 0, 2)
            };
            _formatFontFamilyComboBox.GotFocus += ToolBarFocusableElementGotFocus;
            _formatFontFamilyComboBox.LostFocus += ToolBarFocusableElementLostFocus;
            _formatFontFamilyComboBox.PropertyChanged += FontPropertyChanged;
            fontGroup.Items.Add(_formatFontFamilyComboBox);

            // 글꼴 크기
            _formatFontSizeUpDown = new ToolStripNumericUpDown
            {
                DecimalPlaces = 0,
                Minimum = 1,
                Maximum = 200,
                Width = 60,
                ToolTipText = "글꼴 크기"
            };
            _formatFontSizeUpDown.GotFocus += ToolBarFocusableElementGotFocus;
            _formatFontSizeUpDown.LostFocus += ToolBarFocusableElementLostFocus;
            fontGroup.Items.Add(_formatFontSizeUpDown);

            // 굵게/기울임꼴
            _formatFontBoldButton = new BindableToolStripButton
            {
                CheckOnClick = true,
                ToolTipText = "굵게 (Ctrl+B)",
                DisplayStyle = ToolStripItemDisplayStyle.Image
            };
            IconBinder.Bind(_formatFontBoldButton, IconSource.FromResource(typeof(ImageEditorForm), "fontBoldButton.Image"));
            fontGroup.Items.Add(_formatFontBoldButton);

            _formatFontItalicButton = new BindableToolStripButton
            {
                CheckOnClick = true,
                ToolTipText = "기울임꼴 (Ctrl+I)",
                DisplayStyle = ToolStripItemDisplayStyle.Image
            };
            IconBinder.Bind(_formatFontItalicButton, IconSource.FromResource(typeof(ImageEditorForm), "fontItalicButton.Image"));
            fontGroup.Items.Add(_formatFontItalicButton);

            // 정렬 그룹 (드롭다운 버튼 사용)
            var alignmentGroup = _ribbon.AddGroup(tabIndex, "정렬");

            _formatTextHorizontalAlignmentButton = new BindableToolStripDropDownButton
            {
                ToolTipText = "가로 정렬",
                DisplayStyle = ToolStripItemDisplayStyle.Image
            };
            IconBinder.Bind(_formatTextHorizontalAlignmentButton, IconSource.FromResource(typeof(ImageEditorForm), "btnAlignCenter.Image"));
            // 드롭다운 메뉴 구성
            _formatTextHorizontalAlignmentButton.DropDownItems.Add(
                CreateAlignmentMenuItem("왼쪽", StringAlignment.Near, _formatTextHorizontalAlignmentButton));
            _formatTextHorizontalAlignmentButton.DropDownItems.Add(
                CreateAlignmentMenuItem("가운데", StringAlignment.Center, _formatTextHorizontalAlignmentButton));
            _formatTextHorizontalAlignmentButton.DropDownItems.Add(
                CreateAlignmentMenuItem("오른쪽", StringAlignment.Far, _formatTextHorizontalAlignmentButton));
            alignmentGroup.Items.Add(_formatTextHorizontalAlignmentButton);

            _formatTextVerticalAlignmentButton = new BindableToolStripDropDownButton
            {
                ToolTipText = "세로 정렬",
                DisplayStyle = ToolStripItemDisplayStyle.Image
            };
            IconBinder.Bind(_formatTextVerticalAlignmentButton, IconSource.FromResource(typeof(ImageEditorForm), "btnAlignMiddle.Image"));
            _formatTextVerticalAlignmentButton.DropDownItems.Add(
                CreateAlignmentMenuItem("위쪽", StringAlignment.Near, _formatTextVerticalAlignmentButton));
            _formatTextVerticalAlignmentButton.DropDownItems.Add(
                CreateAlignmentMenuItem("가운데", StringAlignment.Center, _formatTextVerticalAlignmentButton));
            _formatTextVerticalAlignmentButton.DropDownItems.Add(
                CreateAlignmentMenuItem("아래쪽", StringAlignment.Far, _formatTextVerticalAlignmentButton));
            alignmentGroup.Items.Add(_formatTextVerticalAlignmentButton);

            // 스타일 그룹
            var styleGroup = _ribbon.AddGroup(tabIndex, "스타일");

            _formatShadowButton = new BindableToolStripButton
            {
                CheckOnClick = true,
                ToolTipText = "그림자",
                DisplayStyle = ToolStripItemDisplayStyle.Image
            };
            IconBinder.Bind(_formatShadowButton, IconSource.FromResource(typeof(ImageEditorForm), "shadowButton.Image"));
            styleGroup.Items.Add(_formatShadowButton);

            // 테두리/채우기 그룹
            var borderFillGroup = _ribbon.AddGroup(tabIndex, "테두리/채우기");

            _formatLineColorButton = new ToolStripColorButton { ToolTipText = "테두리 색" };
            IconBinder.Bind(_formatLineColorButton, IconSource.FromResource(typeof(ImageEditorForm), "btnLineColor.Image"));
            borderFillGroup.Items.Add(_formatLineColorButton);

            _formatFillColorButton = new ToolStripColorButton { ToolTipText = "채우기 색" };
            IconBinder.Bind(_formatFillColorButton, IconSource.FromResource(typeof(ImageEditorForm), "btnFillColor.Image"));
            borderFillGroup.Items.Add(_formatFillColorButton);

            _formatLineThicknessUpDown = new ToolStripNumericUpDown
            {
                DecimalPlaces = 0,
                Minimum = 0,
                Maximum = 50,
                Width = 60,
                ToolTipText = "테두리 두께"
            };
            _formatLineThicknessUpDown.GotFocus += ToolBarFocusableElementGotFocus;
            _formatLineThicknessUpDown.LostFocus += ToolBarFocusableElementLostFocus;
            borderFillGroup.Items.Add(_formatLineThicknessUpDown);

            // 왕샷: 서식 탭 그룹 KeyTips
            _ribbon.SetGroupKeyTip(tabIndex, 0, "F"); // 글꼴
            _ribbon.SetGroupKeyTip(tabIndex, 1, "A"); // 정렬
            _ribbon.SetGroupKeyTip(tabIndex, 2, "S"); // 스타일
            _ribbon.SetGroupKeyTip(tabIndex, 3, "B"); // 테두리/채우기
        }

        /// <summary>
        /// 왕샷: 서식 탭의 컨트롤을 현재 표면의 필드에 연결한다.
        /// 폼을 만들 때는 표면이 아직 없으므로, 표면이 연결될 때(BindFieldControls) 호출한다.
        /// </summary>
        private void BindFormatFieldControls()
        {
            if (_surface == null || _formatFontFamilyComboBox == null || _formatLineThicknessUpDown == null)
            {
                return;
            }

            var fields = _surface.FieldAggregator;
            Bind(_formatFontFamilyComboBox, "Text", fields.GetField(FieldType.FONT_FAMILY), "Value", NotNullValidator.GetInstance());
            Bind(_formatFontSizeUpDown, "Value", fields.GetField(FieldType.FONT_SIZE), "Value", DecimalFloatConverter.GetInstance(), NotNullValidator.GetInstance());
            Bind(_formatFontBoldButton, "Checked", fields.GetField(FieldType.FONT_BOLD), "Value", NotNullValidator.GetInstance());
            Bind(_formatFontItalicButton, "Checked", fields.GetField(FieldType.FONT_ITALIC), "Value", NotNullValidator.GetInstance());
            Bind(_formatTextHorizontalAlignmentButton, "SelectedTag", fields.GetField(FieldType.TEXT_HORIZONTAL_ALIGNMENT), "Value", NotNullValidator.GetInstance());
            Bind(_formatTextVerticalAlignmentButton, "SelectedTag", fields.GetField(FieldType.TEXT_VERTICAL_ALIGNMENT), "Value", NotNullValidator.GetInstance());
            Bind(_formatShadowButton, "Checked", fields.GetField(FieldType.SHADOW), "Value", NotNullValidator.GetInstance());
            Bind(_formatLineColorButton, "SelectedColor", fields.GetField(FieldType.LINE_COLOR), "Value", NotNullValidator.GetInstance());
            Bind(_formatFillColorButton, "SelectedColor", fields.GetField(FieldType.FILL_COLOR), "Value", NotNullValidator.GetInstance());
            Bind(_formatLineThicknessUpDown, "Value", fields.GetField(FieldType.LINE_THICKNESS), "Value", DecimalIntConverter.GetInstance(), NotNullValidator.GetInstance());
        }

        /// <summary>
        /// 왕샷: 정렬 메뉴 항목 생성
        /// </summary>
        private ToolStripMenuItem CreateAlignmentMenuItem(string text, StringAlignment alignment, BindableToolStripDropDownButton owner)
        {
            var item = new ToolStripMenuItem(text)
            {
                Tag = alignment,
                CheckOnClick = true
            };
            item.Click += (s, e) =>
            {
                owner.SelectedTag = alignment;
                // 바인딩을 통해 필드 업데이트됨
            };
            return item;
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
                    MessageBox.Show(this, "이 파일은 이미지로 열 수 없어요.", "왕캡", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var surface = new Surface(image);
                surface.CaptureDetails.Title = Path.GetFileNameWithoutExtension(path);
                AddTab(surface, outputMade: true, fitWindow: false);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not open the capture in a new tab: " + path, ex);
                MessageBox.Show(this, "이미지를 열지 못했어요.", "왕캡", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                // The PDF plugin writes it with its own settings (page size, margins, scaling), the same as the PDF destination
                try
                {
                    ImageIO.Save(_surface, dialog.FileName, true, new Greenshot.Base.Interfaces.Plugin.SurfaceOutputSettings("pdf"), false);
                }
                catch (Exception pluginException)
                {
                    Log.Warn("The PDF plugin could not save " + dialog.FileName + ", the built in PDF export is used", pluginException);
                    using var image = _surface.GetImageForExport();
                    PdfExport.Save(image, dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not save the PDF " + dialog.FileName, ex);
                MessageBox.Show(this, "PDF로 저장하지 못했어요.\n" + ex.Message, "왕캡", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 왕캡: no dialog for a success, the status bar says where the file is
            ShowRibbonStatus("PDF로 저장했어요: " + dialog.FileName);
        }

        /// <summary>
        /// 왕캡: a short notice in the status bar of the editor (it does not block like a message box; a screen reader reads it)
        /// </summary>
        private void ShowRibbonStatus(string text, bool isError = false)
        {
            _surface?.SendMessageEvent(this, isError ? SurfaceMessageTyp.Error : SurfaceMessageTyp.Info, text);
        }

        /// <summary>
        /// 왕캡: the scroll capture starts after a delay of a few seconds, the status bar says what to do meanwhile
        /// </summary>
        private void StartScrollCaptureFromRibbon()
        {
            ShowRibbonStatus("스크롤 캡처: 3초 안에 캡처할 창 위에 마우스를 올려 두세요. Esc로 멈출 수 있어요.");
            SimpleServiceProvider.Current.GetInstance<IGreenshotShell>().StartScrollCapture();
        }

        // 왕캡: the large buttons which keep a row for a chevron under their name
        private readonly HashSet<ToolStripItem> _chevronRowItems = new();

        private static int ChevronRowHeight(int dpi) => Math.Max(28, (int)Math.Round(28 * dpi / 96.0));

        private void UseChevronRow(ToolStripItem item)
        {
            _chevronRowItems.Add(item);
            item.Padding = new Padding(2, 2, 2, ChevronRowHeight(DeviceDpi));
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

            // 왕캡: the row for the chevron grows with the DPI, like the chevron itself (see ToolStripDropButton.ChevronBounds)
            foreach (var item in _chevronRowItems)
            {
                var wanted = new Padding(2, 2, 2, ChevronRowHeight(dpi));
                if (item.Padding != wanted)
                {
                    item.Padding = wanted;
                }
            }

            foreach (var strip in _ribbon.Strips)
            {
                IconBinder.ApplyForDpi(strip, dpi, "the editor ribbon");
            }

            // The quick access tools and the buttons in the corner are smaller
            int small = IconSizing.DisplaySize(Math.Max(16, (int)Math.Round(20 * dpi / 96.0)));
            foreach (var strip in _ribbon.SmallStrips)
            {
                IconBinder.Apply(strip, small);
            }
        }

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
            // 왕캡: the shortcut key of the menu item belongs in the tool tip, like on the other buttons
            if (source is ToolStripMenuItem menuItem && !tip.Contains("("))
            {
                string shortcut = !string.IsNullOrEmpty(menuItem.ShortcutKeyDisplayString) ? menuItem.ShortcutKeyDisplayString
                    : menuItem.ShortcutKeys != Keys.None ? new KeysConverter().ConvertToString(menuItem.ShortcutKeys) : null;
                if (!string.IsNullOrEmpty(shortcut))
                {
                    tip += " (" + shortcut + ")";
                }
            }

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

            // 왕캡: a button with a chevron needs the row under its name (else the chevron is drawn over the name); the other buttons
            // of the group get the same height, so the icons stay in one line
            if (items.Any(entry => entry.Item is ToolStripDropButton { DropDown: not null }))
            {
                foreach (var (item, _) in items)
                {
                    UseChevronRow(item);
                }
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
                // 왕캡: one rule for every button - a short name under the icon, the shortcut key only in the tool tip
                string name = ToolStripDropButton.SplitShortcut(label, out string shortcut);
                item.Text = name;
                item.ToolTipText = ToolStripDropButton.WithShortcut(item.ToolTipText, shortcut);
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
                    MessageBox.Show(this, "이 파일은 이미지로 열 수 없어요.", "왕캡", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var surface = new Surface(image);
                surface.CaptureDetails.Title = Path.GetFileNameWithoutExtension(dialog.FileName);
                AddTab(surface, outputMade: true, fitWindow: false);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not open the image " + dialog.FileName, ex);
                MessageBox.Show(this, "이미지를 열지 못했어요.", "왕캡", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                var destination = (Greenshot.Base.Interfaces.IDestination)item.Tag;
                if (string.Equals(destination.Designation, "Windows10OCR", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(destination.Designation, "Windows10Share", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                MoveItem(item, _ribbonShareStrip);
                item.Visible = true;
                string text = item.ToolTipText ?? item.Text ?? string.Empty;
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
        private const string FullscreenTip = "전체 화면 (F11) - 리본과 상태 표시줄만 남기고 화면을 꽉 채워요";
        private bool _fullscreen;
        private FormWindowState _beforeFullscreenState;
        private FormBorderStyle _beforeFullscreenBorder;
        private void ToggleFullscreen()
        {
            if (!_fullscreen)
            {
                _beforeFullscreenState = WindowState;
                _beforeFullscreenBorder = FormBorderStyle;
                _fullscreen = true;
                // 왕캡: a window which is maximized already keeps the room of the task bar, unless it is made normal first
                if (WindowState == FormWindowState.Maximized)
                {
                    WindowState = FormWindowState.Normal;
                }

                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
                _fullscreenButton.Text = "전체 화면 종료";
                _fullscreenButton.ToolTipText = "전체 화면 종료 (F11)";
                ShowRibbonStatus("전체 화면이에요. F11 또는 [보기] 탭의 '전체 화면 종료'로 돌아갈 수 있어요.");
            }
            else
            {
                _fullscreen = false;
                // Normal first, so the window gets its border and its old size back
                WindowState = FormWindowState.Normal;
                FormBorderStyle = _beforeFullscreenBorder;
                WindowState = _beforeFullscreenState;
                _fullscreenButton.Text = "전체 화면";
                _fullscreenButton.ToolTipText = FullscreenTip;
            }
        }

        /// <summary>
        /// 캔버스 중앙 정렬
        /// </summary>
        private void CenterCanvas()
        {
            if (_surface == null) return;
            panel1.AutoScrollPosition = new Point(
                Math.Max(0, (_surface.Width - panel1.ClientSize.Width) / 2),
                Math.Max(0, (_surface.Height - panel1.ClientSize.Height) / 2));
        }

        /// <summary>
        /// 캔버스 여백 설정
        /// </summary>
        private void SetCanvasMargin()
        {
            if (_surface == null) return;
            using var dialog = new Form { Text = "캔버스 여백 추가", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(270, 125), MaximizeBox = false, MinimizeBox = false };
            var margin = new NumericUpDown { Left = 130, Top = 16, Width = 110, Minimum = 0, Maximum = 5000, Value = 25 };
            var ok = new Button { Text = "확인", Left = 70, Top = 75, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "취소", Left = 155, Top = 75, DialogResult = DialogResult.Cancel };
            dialog.Controls.AddRange(new Control[] { new Label { Text = "각 방향 여백 (px)", Left = 12, Top = 18, Width = 120 }, margin, ok, cancel });
            dialog.AcceptButton = ok;
            dialog.CancelButton = cancel;
            if (dialog.ShowDialog(this) == DialogResult.OK && margin.Value > 0)
            {
                int pixels = (int)margin.Value;
                ApplyEffect(new Greenshot.Base.Effects.ResizeCanvasEffect(pixels, pixels, pixels, pixels));
            }
        }

        /// <summary>
        /// OCR 텍스트 복사
        /// </summary>
        private void CopyOcrText() { _ = CopyOcrTextAsync(); }

        private async System.Threading.Tasks.Task CopyOcrTextAsync()
        {
            if (_surface == null) return;
            try
            {
                var provider = SimpleServiceProvider.Current.GetInstance<Greenshot.Base.Interfaces.Ocr.IOcrProvider>(isOptional: true);
                if (provider == null)
                {
                    MessageBox.Show(this, "사용 가능한 글자 인식 엔진이 없습니다.", "왕캡", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                List<Greenshot.Base.Interfaces.Plugin.IOcrLineFeature> lines;
                ShowRibbonStatus("글자를 인식하는 중이에요…");
                Cursor = Cursors.WaitCursor;
                try
                {
                    using var image = _surface.GetImageForExport();
                    lines = await provider.DoOcrAsync(image);
                }
                finally
                {
                    Cursor = Cursors.Default;
                }

                var recognized = (lines ?? new List<Greenshot.Base.Interfaces.Plugin.IOcrLineFeature>()).Where(line => !string.IsNullOrWhiteSpace(line.Text)).ToList();
                var text = string.Join(Environment.NewLine, recognized.Select(line => line.Text));
                if (string.IsNullOrWhiteSpace(text))
                {
                    // 왕캡: a notice in the status bar is enough, nothing changed
                    ShowRibbonStatus("인식된 글자가 없어요. Windows 글자 인식 언어(한국어) 설정을 확인해 주세요.");
                    return;
                }
                Clipboard.SetText(text);
                ShowRibbonStatus($"글자 {recognized.Count}줄을 클립보드에 복사했어요.");
            }
            catch (Exception ex)
            {
                Log.Warn("Could not copy OCR text", ex);
                MessageBox.Show(this, "글자를 인식하거나 복사하지 못했습니다.", "왕캡", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// 배치 처리 대화상자
        /// </summary>
        private void ShowBatchDialog()
        {
            // TODO: 배치 처리 구현
            MessageBox.Show(this, "배치 처리 기능은 준비 중입니다.", "왕캡", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
