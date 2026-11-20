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
    /// </summary>
    [Serializable]
    public class GroupContainer : DrawableContainer
    {
        private static readonly ILog LOG = LogManager.GetLogger(typeof(GroupContainer));

        private bool _updatingChildren = false;
        // 왕캡: not serialized (the clipboard and .greenshot files only allow known types)
        [NonSerialized] private NativeRect _lastChildrenBounds = NativeRect.Empty;

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
        }

        /// <summary>
        /// 왕캡: the children of a group have the surface as parent too (they are not in the element list of the surface),
        /// otherwise their changes are not invalidated and their field changes cannot be undone. Also when the group is pasted, undone or redone.
        /// </summary>
        protected override void SwitchParent(ISurface newParent)
        {
            base.SwitchParent(newParent);
            foreach (var child in Children.OfType<IDrawableContainer>())
            {
                child.Parent = newParent;
            }
        }

        public override bool HasDefaultSize => false;

        public override NativeSize DefaultSize => throw new NotSupportedException("Group size is determined by children");

        public override void ResetToDefaultSize()
        {
            UpdateBoundsFromChildren();
        }

        protected override void InitializeFields()
        {
            base.InitializeFields();
            AddField(GetType(), FieldType.SHADOW, false);
            AddField(GetType(), FieldType.LINE_THICKNESS, 0);
        }

        /// <summary>
        /// 자식 개체들의 경계를 계산해서 자신의 크기/위치 업데이트
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
                var childBounds = child.Bounds.Normalize();
                if (bounds.IsEmpty)
                {
                    bounds = childBounds;
                }
                else
                {
                    bounds = bounds.Union(childBounds);
                }
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
                UpdateBoundsFromChildren();
                Invalidate();
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
            foreach (var child in Children.OfType<DrawableContainer>())
            {
                if (child.Contains(x + Left, y + Top))
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

        public override void Invalidate()
        {
            base.Invalidate();
            foreach (var child in Children.OfType<IDrawableContainer>())
            {
                child.Invalidate();
            }
        }

        public override void Flip(bool horizontal)
        {
            _updatingChildren = true;
            try
            {
                // 왕캡: the children are mirrored in the box of the group (their places swap too), not each in its own box
                foreach (var child in Children.OfType<DrawableContainer>())
                {
                    child.Invalidate();
                    if (horizontal)
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
                UpdateBoundsFromChildren();
            }
            finally
            {
                _updatingChildren = false;
            }
            Invalidate();
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
    }
}