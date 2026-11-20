using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Greenshot.Base.Interfaces.Drawing;
using Greenshot.Editor.Drawing;

namespace Greenshot.Editor.Memento
{
    /// <summary>
    /// 왕캡: the places (and the points of freehand lines) of elements and of the elements in groups, for the undo of a flip.
    /// A flip of a group or of a freehand line is not exactly its own inverse (rounding of the smoothed path), so the state is restored.
    /// </summary>
    internal sealed class PlaceSnapshot
    {
        private readonly List<(DrawableContainer Element, int Left, int Top, int Width, int Height, (List<Point> Points, int Left, int Top)? Line)> _places;

        private PlaceSnapshot(List<(DrawableContainer, int, int, int, int, (List<Point>, int, int)?)> places)
        {
            _places = places;
        }

        internal static PlaceSnapshot Take(IEnumerable<DrawableContainer> elements)
        {
            var places = new List<(DrawableContainer, int, int, int, int, (List<Point>, int, int)?)>();
            foreach (var element in elements)
            {
                Collect(element, places);
            }

            return new PlaceSnapshot(places);
        }

        private static void Collect(DrawableContainer element, List<(DrawableContainer, int, int, int, int, (List<Point>, int, int)?)> places)
        {
            (List<Point>, int, int)? line = element is FreehandContainer freehand ? freehand.TakeState() : null;
            places.Add((element, element.Left, element.Top, element.Width, element.Height, line));
            if (element is GroupContainer group)
            {
                foreach (var child in group.Children.OfType<DrawableContainer>())
                {
                    Collect(child, places);
                }
            }
        }

        internal void Apply()
        {
            foreach (var (element, left, top, width, height, line) in _places)
            {
                element.Invalidate();
                element.Left = left;
                element.Top = top;
                element.Width = width;
                element.Height = height;
                if (line.HasValue && element is FreehandContainer freehand)
                {
                    freehand.RestoreState(line.Value);
                }

                element.Invalidate();
            }
        }
    }

    /// <summary>
    /// 왕캡: undo of "뒤집기": the places before the flip come back (and the places after it are kept for the redo)
    /// </summary>
    internal sealed class FlipMemento : IMemento
    {
        private readonly List<DrawableContainer> _elements;
        private readonly PlaceSnapshot _state;

        /// <param name="elements">the flipped elements</param>
        /// <param name="state">the places the undo brings back</param>
        internal FlipMemento(IEnumerable<DrawableContainer> elements, PlaceSnapshot state)
        {
            _elements = elements.ToList();
            _state = state;
        }

        public IMemento Restore()
        {
            var current = PlaceSnapshot.Take(_elements);
            _state.Apply();
            return new FlipMemento(_elements, current);
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
            : this(elements.SelectMany(Targets).SelectMany(element => element.GetFields().Select(field => (element, field, field.Value))).ToList())
        {
        }

        /// <summary>
        /// The element, or all elements of a group (also in groups in the group): GetFields of a group reaches one level only
        /// </summary>
        private static IEnumerable<DrawableContainer> Targets(DrawableContainer element) => element is GroupContainer group ? group.Leaves() : new[] { element };

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
