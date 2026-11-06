/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026  Thomas Braun, Jens Klingen, Robin Krom
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

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Dapplo.Windows.Common.Extensions;
using Dapplo.Windows.Common.Structs;
using Greenshot.Base.Interfaces;
using Greenshot.Base.Interfaces.Drawing;
using Greenshot.Editor.Drawing.Fields;

namespace Greenshot.Editor.Drawing
{
    /// <summary>
    /// This is an enumerated label, every single StepLabelContainer shows the number of the order it was created.
    /// To make sure that deleting recalculates, we check the location before every draw.
    /// </summary>
    /// <summary>
    /// The shape of a step label (the numbered marker)
    /// </summary>
    public enum StepLabelShape
    {
        Circle,
        Rectangle,
        Triangle
    }

    [Serializable]
    public sealed class StepLabelContainer : DrawableContainer, IHaveScaleOptions
    {
        [NonSerialized] private StringFormat _stringFormat = new StringFormat();

        private readonly bool _drawAsRectangle = false;

        public StepLabelContainer(ISurface parent) : base(parent)
        {
            InternalParent?.AddStepLabel(this);
            InitContent();
            Init();
        }

        private void Init()
        {
            CreateDefaultAdorners();
        }

        // Used to store the number of this label, so when deserializing it can be placed back to the StepLabels list in the right location
        private int _number;

        // Used to store the counter start of the Surface, as the surface is NOT stored.
        private int _counterStart = 1;

        public int Number
        {
            get { return _number; }
            set { _number = value; }
        }

        /// <summary>
        /// Retrieve the counter before serializing
        /// </summary>
        /// <param name="context"></param>
        [OnSerializing]
        private void SetValuesOnSerializing(StreamingContext context)
        {
            if (InternalParent == null) return;

            Number = InternalParent.CountStepLabels(this);
            _counterStart = InternalParent.CounterStart;
        }

        /// <summary>
        /// Restore values that don't serialize
        /// </summary>
        /// <param name="context"></param>
        protected override void OnDeserialized(StreamingContext context)
        {
            Init();
            _stringFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            // Fix old data where thickness and shadow were not set (backwards compatibility)
            if (!HasField(FieldType.LINE_THICKNESS))
            {
                AddField(GetType(), FieldType.LINE_THICKNESS, 0);
                // actively set because AddField above uses 0 as default but overrides it with current value from config file
                SetFieldValue(FieldType.LINE_THICKNESS, 0);
            }
            if (!HasField(FieldType.STEP_SHAPE))
            {
                AddField(GetType(), FieldType.STEP_SHAPE, StepLabelShape.Circle);
                SetFieldValue(FieldType.STEP_SHAPE, StepLabelShape.Circle);
            }
            if (!HasField(FieldType.SHADOW))
            {
                AddField(GetType(), FieldType.SHADOW, false);
                // actively set because AddField above uses false as default but overrides it with current value from config file
                SetFieldValue(FieldType.SHADOW, false);
            }
        }

        /// <summary>
        /// Add the StepLabel to the parent
        /// </summary>
        /// <param name="newParent"></param>
        protected override void SwitchParent(ISurface newParent)
        {
            if (newParent == Parent)
            {
                return;
            }

            if (newParent is not Surface newParentSurface)
            {
                return;
            }
            InternalParent?.RemoveStepLabel(this);
            base.SwitchParent(newParent);

            // Make sure the counter start is restored (this unfortunately happens multiple times... -> hack)
            newParentSurface.CounterStart = _counterStart;
            newParentSurface.AddStepLabel(this);
        }

        public override NativeSize DefaultSize => new NativeSize(36, 36);

        public override bool InitContent()
        {
            _defaultEditMode = EditStatus.IDLE;
            _stringFormat.Alignment = StringAlignment.Center;
            _stringFormat.LineAlignment = StringAlignment.Center;

            // Set defaults
            Width = DefaultSize.Width;
            Height = DefaultSize.Height;

            return true;
        }

        /// <summary>
        /// This makes it possible for the label to be placed exactly in the middle of the pointer.
        /// </summary>
        public override bool HandleMouseDown(int mouseX, int mouseY)
        {
            return base.HandleMouseDown(mouseX - Width / 2, mouseY - Height / 2);
        }

        /// <summary>
        /// We set our own field values
        /// </summary>
        protected override void InitializeFields()
        {
            AddField(GetType(), FieldType.FILL_COLOR, Color.FromArgb(0xE5, 0x39, 0x35));
            AddField(GetType(), FieldType.LINE_COLOR, Color.White);
            AddField(GetType(), FieldType.LINE_THICKNESS, 0);
            AddField(GetType(), FieldType.SHADOW, false);
            AddField(GetType(), FieldType.FLAGS, FieldFlag.COUNTER);
            AddField(GetType(), FieldType.STEP_SHAPE, StepLabelShape.Circle);
        }

        /// <summary>
        /// Make sure this element is no longer referenced from the surface
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
            {
                return;
            }

            ((Surface) Parent)?.RemoveStepLabel(this);
            if (_stringFormat == null)
            {
                return;
            }

            _stringFormat.Dispose();
            _stringFormat = null;
        }

        public override bool HandleMouseMove(int x, int y)
        {
            Invalidate();
            Left = x - Width / 2;
            Top = y - Height / 2;
            Invalidate();
            return true;
        }

        /// <summary>
        /// Override the parent, calculate the label number, than draw
        /// </summary>
        /// <param name="graphics"></param>
        /// <param name="rm"></param>
        public override void Draw(Graphics graphics, RenderMode rm)
        {
            if (Width == 0 || Height == 0) { return; }

            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.None;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            string text = ((Surface) Parent).CountStepLabels(this).ToString();
            var rect = new NativeRect(Left, Top, Width, Height).Normalize();
            Color fillColor = GetFieldValueAsColor(FieldType.FILL_COLOR);
            Color lineColor = GetFieldValueAsColor(FieldType.LINE_COLOR);
            int lineThickness = GetFieldValueAsInt(FieldType.LINE_THICKNESS);
            bool shadow = GetFieldValueAsBool(FieldType.SHADOW);

            switch (Shape)
            {
                case StepLabelShape.Rectangle:
                    RectangleContainer.DrawRectangle(rect, graphics, rm, lineThickness, lineColor, fillColor, shadow);
                    break;
                case StepLabelShape.Triangle:
                    DrawTriangle(rect, graphics, lineThickness, lineColor, fillColor);
                    break;
                default:
                    EllipseContainer.DrawEllipse(rect, graphics, rm, lineThickness, lineColor, fillColor, shadow);
                    break;
            }

            using FontFamily fam = new(FontFamily.GenericSansSerif.Name);
            using Font font = new(fam, NumberFontSize(text), FontStyle.Bold, GraphicsUnit.Pixel);
            // The number of a triangle sits in its lower part, where the triangle is wide
            var textRect = Shape == StepLabelShape.Triangle
                ? new NativeRect(rect.Left, rect.Top + rect.Height * 2 / 5, rect.Width, rect.Height * 3 / 5)
                : rect;
            TextContainer.DrawText(graphics, textRect, 0, lineColor, false, _stringFormat, text, font);
        }

        /// <summary>
        /// The shape of the label: a circle, a rectangle or a triangle
        /// </summary>
        public StepLabelShape Shape
        {
            get
            {
                if (_drawAsRectangle)
                {
                    return StepLabelShape.Rectangle;
                }
                return HasField(FieldType.STEP_SHAPE) && GetFieldValue(FieldType.STEP_SHAPE) is StepLabelShape shape ? shape : StepLabelShape.Circle;
            }
        }

        private static PointF[] TrianglePoints(NativeRect rect) => new[]
        {
            new PointF(rect.Left + rect.Width / 2f, rect.Top),
            new PointF(rect.Left + rect.Width, rect.Top + rect.Height),
            new PointF(rect.Left, rect.Top + rect.Height)
        };

        private static void DrawTriangle(NativeRect rect, Graphics graphics, int lineThickness, Color lineColor, Color fillColor)
        {
            var points = TrianglePoints(rect);
            using var brush = new SolidBrush(fillColor);
            graphics.FillPolygon(brush, points);
            if (lineThickness > 0)
            {
                using var pen = new Pen(lineColor, lineThickness) { LineJoin = LineJoin.Round };
                graphics.DrawPolygon(pen, points);
            }
        }

        /// <summary>
        /// The number of this label, as drawn
        /// </summary>
        internal string NumberText => ((Surface) Parent).CountStepLabels(this).ToString();

        /// <summary>
        /// A rectangle instead of the circle
        /// </summary>
        internal bool DrawAsRectangle => _drawAsRectangle;

        /// <summary>
        /// The size in pixels of the bold number in the generic sans serif family, so it fits the label
        /// </summary>
        internal float NumberFontSize(string text)
        {
            using FontFamily fam = new(FontFamily.GenericSansSerif.Name);
            //calculate new font size based on ratio from text height and text width
            float initialFontSize = Math.Min(Math.Abs(Width), Math.Abs(Height));
            using Font measureFont = new(fam, initialFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            var measured = TextRenderer.MeasureText(text, measureFont);
            //static scale for optimal fit
            return initialFontSize * measured.Height / measured.Width * 0.7f;
        }

        public override bool ClickableAt(int x, int y)
        {
            var rect = new NativeRect(Left, Top, Width, Height).Normalize();
            Color fillColor = GetFieldValueAsColor(FieldType.FILL_COLOR);
            switch (Shape)
            {
                case StepLabelShape.Rectangle:
                case StepLabelShape.Triangle:
                    return RectangleContainer.RectangleClickableAt(rect, 0, fillColor, x, y);
                default:
                    return EllipseContainer.EllipseClickableAt(rect, 0, fillColor, x, y);
            }
        }

        public ScaleOptions GetScaleOptions() => ScaleOptions.Rational;
    }
}