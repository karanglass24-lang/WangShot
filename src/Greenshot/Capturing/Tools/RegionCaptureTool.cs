/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 *
 * For more information see: https://getgreenshot.org/
 * The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.Windows.Input;
using Dapplo.Windows.Common.Extensions;
using Dapplo.Windows.Common.Structs;
using Dapplo.Windows.Desktop;
using Greenshot.Base.Interfaces.Capture;
using Greenshot.Base.Languages;
using CaptureMode = Greenshot.Base.Interfaces.CaptureMode;

namespace Greenshot.Capturing.Tools
{
    /// <summary>
    /// Drag a rectangle with the mouse, or start and end it with Enter
    /// </summary>
    public class RegionCaptureTool : CaptureTool
    {
        /// <summary>
        /// Smaller selections are taken as a click
        /// </summary>
        private const int MinimumSize = 3;

        private NativePoint _start;

        public override CaptureMode Mode => CaptureMode.Region;

        public override bool ShowsCrosshair => !IsSelecting;

        /// <summary>
        /// True while the rectangle is being dragged
        /// </summary>
        protected bool IsSelecting { get; private set; }

        /// <summary>
        /// The rectangle from the start to the cursor, without the pixel under the cursor
        /// </summary>
        protected NativeRect Selection { get; private set; } = NativeRect.Empty;

        public override void Activate(ICaptureToolHost host)
        {
            base.Activate(host);
            IsSelecting = false;
            Selection = NativeRect.Empty;
            _hoverWindow = null;
            _hoverRect = NativeRect.Empty;
            _dragged = false;
        }

        public override void Deactivate()
        {
            if (IsSelecting)
            {
                IsSelecting = false;
                Host.HideSelection();
                Host.ClearLabels();
            }
        }

        public override void OnMouseDown() => StartSelection();

        public override void OnMouseUp()
        {
            if (IsSelecting)
            {
                EndSelection();
            }
        }

        /// <summary>
        /// Enter starts and finishes the selection, only while this tool is active
        /// </summary>
        public override void Attach(ICaptureToolHost host)
        {
            base.Attach(host);
            host.RegisterToolKey(this, Key.Return, ModifierKeys.None, () => Texts.Core.CaptureKeyRegionSelect, ToggleSelection);
        }

        private void ToggleSelection()
        {
            if (IsSelecting)
            {
                EndSelection();
            }
            else
            {
                StartSelection();
            }
        }

        /// <summary>
        /// True when the plain region tool also follows the window under the cursor: a click selects that window,
        /// dragging selects a rectangle. Tools derived for other modes keep the plain rectangle.
        /// </summary>
        private bool HoverSelectsWindow => Mode == CaptureMode.Region;

        // The window under the cursor and its visible part in capture coordinates, before the mouse button is pressed
        private IInteropWindow _hoverWindow;
        private NativeRect _hoverRect = NativeRect.Empty;
        // True once the mouse moved far enough to draw a rectangle, then a release is not a click on the window
        private bool _dragged;

        public override void OnMouseMove()
        {
            if (IsSelecting)
            {
                var cursor = Host.CursorPosition;
                Selection = new NativeRect(cursor.X, cursor.Y, _start.X - cursor.X, _start.Y - cursor.Y).Normalize();
                if (Selection.Width > MinimumSize || Selection.Height > MinimumSize)
                {
                    _dragged = true;
                }
                if (HoverSelectsWindow && !_dragged && !_hoverRect.IsEmpty)
                {
                    // Still a click: keep the window highlighted
                    return;
                }
                Host.ShowSelection(Selection);
                // The selection includes the pixel under the cursor
                Host.ShowLabels(Selection, new NativeSize(Selection.Width + 1, Selection.Height + 1));
            }
            else if (HoverSelectsWindow)
            {
                UpdateHoverWindow();
            }
            else if (Host.IsSelectionVisible && !Host.IsSelectionAnimating)
            {
                // e.g. what is left of the window selection
                Host.HideSelection();
                Host.ClearLabels();
            }
        }

        /// <summary>
        /// Highlight the (child) window under the cursor, like the window mode does
        /// </summary>
        private void UpdateHoverWindow()
        {
            var window = Host.FindWindowUnderCursor(true);
            if (window == null)
            {
                _hoverWindow = null;
                _hoverRect = NativeRect.Empty;
                if (Host.IsSelectionVisible && !Host.IsSelectionAnimating)
                {
                    Host.HideSelection();
                    Host.ClearLabels();
                }
                return;
            }
            if (window.Equals(_hoverWindow))
            {
                return;
            }
            var screenBounds = Host.ScreenBounds;
            var rect = window.GetInfo().Bounds
                .Offset(-screenBounds.X, -screenBounds.Y)
                .Intersect(new NativeRect(0, 0, screenBounds.Width, screenBounds.Height));
            _hoverWindow = window;
            _hoverRect = rect;
            if (rect.IsEmpty)
            {
                return;
            }
            Host.ShowSelection(rect, true);
            Host.ShowLabels(rect, new NativeSize(rect.Width, rect.Height));
        }

        private void StartSelection()
        {
            _start = Host.CursorPosition;
            IsSelecting = true;
            _dragged = false;
            OnMouseMove();
        }

        private void EndSelection()
        {
            IsSelecting = false;
            if (Selection.Width > MinimumSize && Selection.Height > MinimumSize)
            {
                // The selection includes the pixel under the cursor
                AcceptSelection(new NativeRect(Selection.Left, Selection.Top, Selection.Width + 1, Selection.Height + 1));
            }
            else if (!OnClick())
            {
                OnMouseMove();
            }
        }

        /// <summary>
        /// The mouse was released without selecting a region
        /// </summary>
        /// <returns>true when the click was handled</returns>
        protected virtual bool OnClick()
        {
            if (!HoverSelectsWindow || _dragged || _hoverWindow == null || _hoverRect.IsEmpty)
            {
                return false;
            }
            var title = _hoverWindow.GetCaption();
            Host.Capture.CaptureDetails.Title = title;
            Host.Capture.CaptureDetails.AddMetaData("windowtitle", title);
            Host.Accept(_hoverRect, _hoverWindow);
            return true;
        }

        /// <summary>
        /// Close the window with the selected rectangle
        /// </summary>
        protected virtual void AcceptSelection(NativeRect rect)
        {
            // The fixed size capture offers the sizes and places of the last region captures
            if (Mode == CaptureMode.Region)
            {
                FixedSizeCaptureTool.RememberArea(rect.Offset(Host.ScreenBounds.X, Host.ScreenBounds.Y), Host.Windows);
            }
            Host.Accept(rect);
        }
    }
}
