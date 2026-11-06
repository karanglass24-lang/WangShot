using System;
using System.Drawing;
using System.Windows.Forms;
using Greenshot.Base.Controls;
using Greenshot.Base.Core;
using Greenshot.Base.Interfaces;
using Greenshot.Editor.Controls;

namespace Greenshot.Editor.Forms
{
    /// <summary>
    /// 왕샷: select a part of the image (under the chevron of the select tool): copy it, crop to it, or let it go
    /// </summary>
    public partial class ImageEditorForm
    {
        private ToolStripButton _areaCopyButton;
        private ToolStripButton _areaCropButton;
        private ToolStripButton _areaClearButton;

        private void CreateAreaSelectButtons()
        {
            if (_areaCopyButton != null)
            {
                return;
            }

            _areaCopyButton = NewAreaButton("영역 복사", "선택한 부분을 클립보드로 복사해요 (Ctrl+C)", (s, e) => CopyAreaSelection());
            _areaCropButton = NewAreaButton("영역으로 자르기", "선택한 부분만 남기고 자르기", (s, e) =>
            {
                _surface.ConfirmCrop(true);
                _surface.AreaSelectMode = false;
                RefreshFieldControls();
            });
            _areaClearButton = NewAreaButton("선택 해제", "선택을 없애요", (s, e) =>
            {
                _surface.ConfirmCrop(false);
                _surface.AreaSelectMode = false;
                RefreshFieldControls();
            });
        }

        private ToolStripButton NewAreaButton(string text, string tip, EventHandler click)
        {
            var button = new ToolStripButton { Text = text, ToolTipText = tip, DisplayStyle = ToolStripItemDisplayStyle.Text, Visible = false };
            button.Click += click;
            propertiesToolStrip.Items.Add(button);
            return button;
        }

        /// <summary>
        /// Select a part of the image with the mouse
        /// </summary>
        private void StartAreaSelect()
        {
            CreateAreaSelectButtons();
            _surface.AreaSelectMode = true;
            if (_surface.DrawingMode != DrawingModes.Crop)
            {
                _surface.DrawingMode = DrawingModes.Crop;
                InitCropMode(Greenshot.Editor.Drawing.CropContainer.CropModes.Default);
            }

            RefreshFieldControls();
        }

        private void CopyAreaSelection()
        {
            if (!_surface.TryGetAreaSelection(out var area))
            {
                return;
            }

            try
            {
                using var image = _surface.GetAreaImage(area);
                Clipboard.SetImage(image);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not copy the selected area", ex);
            }
        }

        /// <summary>
        /// The choices under the chevron of the select tool
        /// </summary>
        private ToolStripDropDown BuildSelectMenu()
        {
            var menu = new ToolStripDropDownMenu { ShowImageMargin = false };
            menu.Items.Add(new ToolStripMenuItem("개체 선택", null, (s, e) =>
            {
                _surface.AreaSelectMode = false;
                btnCursor.PerformClick();
            }));
            menu.Items.Add(new ToolStripMenuItem("영역 선택 (이미지 일부)", null, (s, e) => StartAreaSelect()));
            menu.Items.Add(new ToolStripMenuItem("모두 선택", null, (s, e) => selectAllToolStripMenuItem.PerformClick()));
            var theme = EditorTheme.Current;
            EditorTheme.ApplyRenderer(menu);
            menu.BackColor = theme.MenuBackground;
            menu.ForeColor = theme.Foreground;
            return menu;
        }
    }
}
