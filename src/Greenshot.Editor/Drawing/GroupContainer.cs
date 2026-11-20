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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Dapplo.Windows.Common.Extensions;
using Dapplo.Windows.Common.Structs;
using Greenshot.Base.Interfaces;
using Greenshot.Base.Interfaces.Drawing;
using Greenshot.Base.Interfaces.Drawing.Adorners;
using Greenshot.Editor.Drawing.Adorners;
using Greenshot.Editor.Drawing.Fields;
using Greenshot.Editor.Helpers;
using log4net;

namespace Greenshot.Editor.Drawing
{
    /// <summary>
    /// 왕샷: 여러 개체를 하나로 묶는 그룹 컨테이너.
    /// 자식 개체들의 경계를 따라 움직이며, 그룹 전체를 하나의 개체처럼 다룰 수 있다.
    /// 자식들의 Parent는 여전히 Surface이며, GroupContainer는 논리적 그룹만 관리한다.
    /// 왕캡: the children have coordinates relative to the group (Left and Top of the group are their origin).
    /// </summary>
    [Serializable]
    public class GroupContainer : DrawableContainer
    {
        private static readonly ILog LOG = LogManager.GetLogger(typeof(GroupContainer));

        /// <summary>
        /// 왕캡: deepest nesting of groups which is accepted from the clipboard and from files
        /// </summary>
        internal const int MaximumDepth = 8;

        private const int MaximumNodes = 10000;

        private bool _updatingChildren = false;
        // 왕캡: not serialized (the clipboard and .greenshot files only allow known types)
        [NonSerialized] private NativeRect _lastChildrenBounds = NativeRect.Empty;
        [NonSerialized] private bool _switchingParent;
        [NonSerialized] private bool _disposed;

        public GroupContainer(ISurface parent) : base(parent)
        {
            Init();
        }

        protected override void OnDeserialized(StreamingContext streamingContext)
        {
            base.OnDeserialized(streamingContext);
            Init();
        }

        private void Init()
        {
            CreateDefaultAdorners();
            // 왕캡: the events are not serialized, a pasted group listens to its children again
            foreach (var child in Children.OfType<IDrawableContainer>())
            {
                child.PropertyChanged -= Child_PropertyChanged;
                child.PropertyChanged += Child_PropertyChanged;
            }

            // 왕캡: a field change of a child (the children invalidate their own, group relative, area only) redraws the area of the group
            FieldChanged -= Group_FieldChanged;
            FieldChanged += Group_FieldChanged;
        }

        private void Group_FieldChanged(object sender, FieldChangedEventArgs e)
        {
            Invalidate();
        }

        /// <summary>
        /// 왕캡: the children of a group have the surface as parent too (they are not in the element list of the surface),
        /// otherwise their changes are not invalidated and their field changes cannot be undone. Also when the group is pasted, undone or redone.
        /// </summary>
        protected override void SwitchParent(ISurface newParent)
        {
            // A group which (wrongly) contains itself must not run in circles
            if (_switchingParent)
            {
                return;
            }

            _switchingParent = true;
            try
            {
                base.SwitchParent(newParent);
                foreach (var child in Children.OfType<IDrawableContainer>())
                {
                    child.Parent = newParent;
                }
            }
            finally
            {
                _switchingParent = false;
            }
        }

        /// <summary>
        /// 왕캡: false for the elements which cannot be in a group: the filters work on the image below them at their place on the surface,
        /// and the speech bubble, the torn edge, the cut mark and the crop have coordinates of the surface (tail, image) which a group does not move.
        /// </summary>
        internal static bool CanBeChild(IDrawableContainer element)
        {
            return element is DrawableContainer container
                   && container is not FilterContainer
                   && container is not SpeechbubbleContainer
                   && container is not TornEdgeContainer
                   && container is not CutMarkContainer
                   && container is not CropContainer
                   && !container.HasFilters;
        }

        /// <summary>
        /// 왕캡: checks elements which come from the clipboard or from a file: no group in a group in a circle, not deeper than
        /// <see cref="MaximumDepth"/>, only elements which can be in a group. One wrong element makes the whole list invalid.
        /// </summary>
        internal static bool IsValidTree(IEnumerable<IDrawableContainer> elements)
        {
            if (elements == null)
            {
                return false;
            }

            foreach (var element in elements)
            {
                int nodes = 0;
                if (!IsValidNode(element, new HashSet<object>(ReferenceComparer.Instance), 0, ref nodes))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 왕캡: how deep groups are nested in the element: 0 for an element, 1 for a group of elements, 2 for a group with a group in it...
        /// </summary>
        internal static int NestingOf(IDrawableContainer element, int level = 0)
        {
            if (element is not GroupContainer group || level > MaximumDepth + 1)
            {
                return 0;
            }

            return 1 + group.Children.OfType<IDrawableContainer>().Select(child => NestingOf(child, level + 1)).DefaultIfEmpty(0).Max();
        }

        private static bool IsValidNode(object node, HashSet<object> path, int depth, ref int nodes)
        {
            if (node is not DrawableContainer container || depth > MaximumDepth || ++nodes > MaximumNodes)
            {
                return false;
            }

            if (!path.Add(container))
            {
                return false;
            }

            try
            {
                foreach (var child in container.Children)
                {
                    if (container is GroupContainer)
                    {
                        if (child is not IDrawableContainer drawable || !CanBeChild(drawable) || !IsValidNode(child, path, depth + 1, ref nodes))
                        {
                            return false;
                        }
                    }
                    else if (child is IDrawableContainer)
                    {
                        // Other elements only have filters as children
                        return false;
                    }
                }
            }
            finally
            {
                path.Remove(container);
            }

            return true;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object x, object y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>
        /// 왕캡: all elements in the group and in the groups in it, without the groups themselves
        /// </summary>
        internal IEnumerable<DrawableContainer> Leaves()
        {
            foreach (var child in Children.OfType<DrawableContainer>())
            {
                if (child is GroupContainer group)
                {
                    foreach (var leaf in group.Leaves())
                    {
                        yield return leaf;
                    }
                }
                else
                {
                    yield return child;
                }
            }
        }

        /// <summary>
        /// 왕캡: true when the element is a child of this group or of a group in it
        /// </summary>
        internal bool ContainsDescendant(IDrawableContainer element)
        {
            foreach (var child in Children.OfType<IDrawableContainer>())
            {
                if (ReferenceEquals(child, element) || (child is GroupContainer group && group.ContainsDescendant(element)))
                {
                    return true;
                }
            }

            return false;
        }

        public override bool HasDefaultSize => false;

        public override NativeSize DefaultSize => throw new NotSupportedException("Group size is determined by children");

        public override void ResetToDefaultSize()
        {
            RecomputeLocalBounds();
        }

        protected override void InitializeFields()
        {
            base.InitializeFields();
            AddField(GetType(), FieldType.SHADOW, false);
            AddField(GetType(), FieldType.LINE_THICKNESS, 0);
        }

        /// <summary>
        /// 왕캡: the bounds of a child in the coordinates of its parent. A freehand line has the size of the image as bounds,
        /// its path is used instead.
        /// </summary>
        private static NativeRect BoundsOf(IDrawableContainer child)
        {
            if (child is FreehandContainer freehand)
            {
                return freehand.PathBounds;
            }

            return child.Bounds.Normalize();
        }

        /// <summary>
        /// 자식 개체들의 경계를 계산해서 자신의 크기/위치 업데이트.
        /// 왕캡: only for children which still have the coordinates of the surface (when they are put into the group): they are made relative to the group.
        /// </summary>
        public void UpdateBoundsFromChildren()
        {
            if (Children.Count == 0)
            {
                Width = 0;
                Height = 0;
                return;
            }

            var bounds = NativeRect.Empty;
            foreach (var child in Children.OfType<IDrawableContainer>())
            {
                var childBounds = BoundsOf(child);
                bounds = bounds.IsEmpty ? childBounds : bounds.Union(childBounds);
            }

            // 자식 위치를 그룹 로컬 좌표로 변환
            int dx = bounds.Left;
            int dy = bounds.Top;

            Left = dx;
            Top = dy;
            Width = bounds.Width;
            Height = bounds.Height;

            // 자식들을 그룹 로컬 좌표로 이동
            foreach (var child in Children.OfType<IDrawableContainer>())
            {
                child.Left -= dx;
                child.Top -= dy;
            }

            _lastChildrenBounds = new NativeRect(0, 0, Width, Height);
        }

        /// <summary>
        /// 왕캡: the group fits its children again, they already have coordinates relative to the group: the group moves by the
        /// offset of the box of its children and the children move back by it, so nothing moves on the surface.
        /// </summary>
        private void RecomputeLocalBounds()
        {
            var bounds = NativeRect.Empty;
            foreach (var child in Children.OfType<IDrawableContainer>())
            {
                var childBounds = BoundsOf(child);
                bounds = bounds.IsEmpty ? childBounds : bounds.Union(childBounds);
            }

            if (bounds.IsEmpty)
            {
                return;
            }

            int dx = bounds.Left;
            int dy = bounds.Top;
            Left += dx;
            Top += dy;
            Width = bounds.Width;
            Height = bounds.Height;
            if (dx != 0 || dy != 0)
            {
                foreach (var child in Children.OfType<IDrawableContainer>())
                {
                    child.Left -= dx;
                    child.Top -= dy;
                }
            }

            _lastChildrenBounds = new NativeRect(0, 0, Width, Height);
        }

        /// <summary>
        /// 그룹에 자식 추가 (Parent는 변경하지 않음 - 여전히 Surface)
        /// 경계 업데이트는 호출자가 UpdateBoundsFromChildren()으로 직접 호출
        /// </summary>
        public void AddChildToGroup(IDrawableContainer child)
        {
            AddChild((IFieldHolder)child);
            child.Parent = Parent;
            if (!_updatingChildren)
            {
                child.PropertyChanged += Child_PropertyChanged;
            }
        }

        /// <summary>
        /// 그룹에서 자식 제거
        /// 경계 업데이트는 호출자가 UpdateBoundsFromChildren()으로 직접 호출
        /// </summary>
        public void RemoveChildFromGroup(IDrawableContainer child)
        {
            child.PropertyChanged -= Child_PropertyChanged;
            RemoveChild((IFieldHolder)child);
        }

        private void Child_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!_updatingChildren && (e.PropertyName == nameof(Left) || e.PropertyName == nameof(Top) ||
                e.PropertyName == nameof(Width) || e.PropertyName == nameof(Height) || e.PropertyName == "Bounds"))
            {
                RecomputeLocalBounds();
                Invalidate();
            }
        }

        /// <summary>
        /// 왕캡: the area to redraw in the coordinates of the surface: the areas of the children moved by the position of the group
        /// </summary>
        public override NativeRect DrawingBounds
        {
            get
            {
                var bounds = NativeRect.Empty;
                foreach (var child in Children.OfType<DrawableContainer>())
                {
                    var childBounds = child.DrawingBounds;
                    bounds = bounds.IsEmpty ? childBounds : bounds.Union(childBounds);
                }

                if (bounds.IsEmpty)
                {
                    return base.DrawingBounds;
                }

                return new NativeRect(bounds.Left + Left, bounds.Top + Top, bounds.Width, bounds.Height);
            }
        }

        public override void Draw(Graphics graphics, RenderMode renderMode)
        {
            // 그룹 자체의 테두리는 그리지 않음, 선택 시 아도너만 표시됨.
            // 왕캡: 자식들은 표면의 개체 목록에서 빠지므로 그룹이 그려 줘야 한다 (그렇지 않으면 그룹으로 묶는 순간 사라졌다).
            // 자식 좌표는 그룹 기준이므로 그룹 위치만큼 옮겨서 그린다.
            foreach (var child in Children.OfType<DrawableContainer>())
            {
                var state = graphics.Save();
                try
                {
                    graphics.TranslateTransform(Left, Top);
                    child.Draw(graphics, renderMode);
                }
                finally
                {
                    graphics.Restore(state);
                }
            }
        }

        public override bool Contains(int x, int y)
        {
            // 왕캡: the point is in the coordinates of the surface, the children have coordinates relative to the group
            foreach (var child in Children.OfType<DrawableContainer>())
            {
                if (child.Contains(x - Left, y - Top))
                {
                    return true;
                }
            }
            return false;
        }

        public override bool ClickableAt(int x, int y)
        {
            var rect = new NativeRect(Left, Top, Width, Height).Normalize();
            return rect.Contains(x, y);
        }

        public override bool HandleMouseDown(int mouseX, int mouseY)
        {
            foreach (var child in Children.OfType<DrawableContainer>())
            {
                if (child.ClickableAt(mouseX - Left, mouseY - Top))
                {
                    return child.HandleMouseDown(mouseX - Left, mouseY - Top);
                }
            }
            return base.HandleMouseDown(mouseX, mouseY);
        }

        public override bool HandleMouseMove(int x, int y)
        {
            if (Status == EditStatus.MOVING)
            {
                _updatingChildren = true;
                try
                {
                    base.HandleMouseMove(x, y);
                }
                finally
                {
                    _updatingChildren = false;
                }
                return true;
            }
            return base.HandleMouseMove(x, y);
        }

        public override void Flip(bool horizontal)
        {
            _updatingChildren = true;
            try
            {
                Invalidate();
                // 왕캡: the children are mirrored in the box of the group (their places swap too), not each in its own box
                foreach (var child in Children.OfType<DrawableContainer>())
                {
                    if (child is FreehandContainer freehand)
                    {
                        // The width of a freehand line is the size of the image: its points are mirrored in the box of the group
                        freehand.MirrorInParent(horizontal, horizontal ? Width : Height);
                    }
                    else if (child is GroupContainer inner)
                    {
                        // A group has no negative size: it moves to the mirrored place and mirrors its own children
                        if (horizontal)
                        {
                            inner.Left = Width - (inner.Left + inner.Width);
                        }
                        else
                        {
                            inner.Top = Height - (inner.Top + inner.Height);
                        }

                        inner.Flip(horizontal);
                    }
                    else if (horizontal)
                    {
                        child.Left = Width - child.Left;
                        child.Width = -child.Width;
                    }
                    else
                    {
                        child.Top = Height - child.Top;
                        child.Height = -child.Height;
                    }
                }

                // 왕캡: local, the box of the group does not move (the surface coordinates would send the group to 0,0)
                RecomputeLocalBounds();
            }
            finally
            {
                _updatingChildren = false;
            }
            Invalidate();
        }

        /// <summary>
        /// 왕캡: image resize or rotation: the group gets the new box, the children are transformed in the coordinates of the group
        /// </summary>
        public override void Transform(Matrix matrix)
        {
            if (matrix == null)
            {
                return;
            }

            Point[] corners =
            {
                new Point(Left, Top), new Point(Left + Width, Top + Height)
            };
            matrix.TransformPoints(corners);
            var newBox = new NativeRect(corners[0].X, corners[0].Y, corners[1].X - corners[0].X, corners[1].Y - corners[0].Y).Normalize();

            _updatingChildren = true;
            try
            {
                // children: from the group to the surface, the matrix of the transformation, from the surface to the new group
                using var childMatrix = new Matrix();
                childMatrix.Translate(Left, Top, MatrixOrder.Append);
                childMatrix.Multiply(matrix, MatrixOrder.Append);
                childMatrix.Translate(-newBox.Left, -newBox.Top, MatrixOrder.Append);
                foreach (var child in Children.OfType<DrawableContainer>())
                {
                    if (child is FreehandContainer freehand)
                    {
                        freehand.TransformInParent(childMatrix);
                    }
                    else
                    {
                        child.Transform(childMatrix);
                    }
                }

                Left = newBox.Left;
                Top = newBox.Top;
                Width = newBox.Width;
                Height = newBox.Height;
            }
            finally
            {
                _updatingChildren = false;
            }
        }

        public override void ApplyFormat(FormatSnapshot format)
        {
            base.ApplyFormat(format);
            foreach (var child in Children.OfType<DrawableContainer>())
            {
                child.ApplyFormat(format);
            }
            Invalidate();
        }

        public ScaleOptions GetScaleOptions() => ScaleOptions.Rational;

        /// <summary>
        /// 그룹 해제: 자식들을 반환하고 그룹은 비움
        /// </summary>
        public IEnumerable<IDrawableContainer> Ungroup()
        {
            _updatingChildren = true;
            try
            {
                var children = Children.OfType<IDrawableContainer>().ToList();
                foreach (var child in children)
                {
                    RemoveChildFromGroup(child);
                    // 자식 위치를 전역 좌표로 변환
                    child.Left += Left;
                    child.Top += Top;
                }
                return children;
            }
            finally
            {
                _updatingChildren = false;
            }
        }

        /// <summary>
        /// 왕캡: the children are disposed with the group (bitmaps of images). After an ungroup the group has no children any more,
        /// they belong to the surface then.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                foreach (var child in Children.OfType<IDrawableContainer>().ToList())
                {
                    child.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
