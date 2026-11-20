using System.Collections.Generic;
using System.Linq;
using Greenshot.Base.Interfaces.Drawing;
using Greenshot.Editor.Drawing;

namespace Greenshot.Editor.Memento
{
    /// <summary>
    /// 왕캡: undo of "뒤집기". A flip is its own inverse: the elements are flipped once more.
    /// </summary>
    internal sealed class FlipMemento : IMemento
    {
        private readonly List<DrawableContainer> _elements;
        private readonly bool _horizontal;

        internal FlipMemento(IEnumerable<DrawableContainer> elements, bool horizontal)
        {
            _elements = elements.ToList();
            _horizontal = horizontal;
        }

        public IMemento Restore()
        {
            foreach (var element in _elements)
            {
                element.Flip(_horizontal);
            }

            return new FlipMemento(_elements, _horizontal);
        }

        public bool Merge(IMemento otherMemento) => false;

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// 왕캡: undo of "서식 적용": all field values of the elements (and of their children) as they were before, in one step
    /// </summary>
    internal sealed class FieldValuesMemento : IMemento
    {
        private readonly List<(DrawableContainer Element, IField Field, object Value)> _values;

        internal FieldValuesMemento(IEnumerable<DrawableContainer> elements)
            : this(elements.SelectMany(element => element.GetFields().Select(field => (element, field, field.Value))).ToList())
        {
        }

        private FieldValuesMemento(List<(DrawableContainer Element, IField Field, object Value)> values)
        {
            _values = values;
        }

        public IMemento Restore()
        {
            var inverse = new FieldValuesMemento(_values.Select(entry => (entry.Element, entry.Field, entry.Field.Value)).ToList());
            foreach (var (element, field, value) in _values)
            {
                element.Invalidate();
                field.Value = value;
                element.Invalidate();
            }

            return inverse;
        }

        public bool Merge(IMemento otherMemento) => false;

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// 왕캡: undo of grouping (Ctrl+Alt+G) and ungrouping (Ctrl+Shift+G) in one step. Before, every removed and added element was a step
    /// of its own: an undo put the children back with the coordinates inside the group, or brought back an empty group.
    /// The places in the element list are kept, so the z-order is the same after the undo.
    /// </summary>
    internal sealed class GroupMemento : IMemento
    {
        /// <summary>
        /// One group of the operation: the group with its children, its place in the element list of the surface when it is on it
        /// and the places of the children when they are on the surface (taken one step after the other, as the operation ran)
        /// </summary>
        internal sealed class Step
        {
            internal Step(GroupContainer group, List<IDrawableContainer> children, int groupIndex, List<int> childIndices)
            {
                Group = group;
                Children = children;
                GroupIndex = groupIndex;
                ChildIndices = childIndices;
            }

            internal GroupContainer Group { get; }
            internal List<IDrawableContainer> Children { get; }
            internal int GroupIndex { get; }
            internal List<int> ChildIndices { get; }
        }

        private readonly Surface _surface;
        private readonly List<Step> _steps;
        private readonly bool _originalIsGroup;
        private readonly bool _redo;

        /// <param name="surface">Surface</param>
        /// <param name="steps">the groups, in the order the operation did them</param>
        /// <param name="originalIsGroup">true when the operation was grouping, false when it was ungrouping</param>
        /// <param name="redo">true when this memento repeats the operation, false when it reverts it</param>
        internal GroupMemento(Surface surface, List<Step> steps, bool originalIsGroup, bool redo)
        {
            _surface = surface;
            _steps = steps;
            _originalIsGroup = originalIsGroup;
            _redo = redo;
        }

        public IMemento Restore()
        {
            _surface.DeselectAllElements();
            // Reverting runs the steps backwards, as the places of the elements depend on the steps before
            IEnumerable<Step> steps = _redo ? _steps : Enumerable.Reverse(_steps);
            bool group = _originalIsGroup == _redo;
            foreach (var step in steps)
            {
                if (group)
                {
                    _surface.GroupElements(step.Children, step.Group, step.GroupIndex);
                }
                else
                {
                    _surface.UngroupElement(step.Group, step.ChildIndices);
                }
            }

            return new GroupMemento(_surface, _steps, _originalIsGroup, !_redo);
        }

        public bool Merge(IMemento otherMemento) => false;

        public void Dispose()
        {
        }
    }
}
