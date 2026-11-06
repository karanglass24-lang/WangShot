using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Greenshot.Base.Controls;

namespace Greenshot.Editor.Controls
{
    /// <summary>
    /// 왕샷: a large ribbon button with a small chevron under it. A click on the upper part uses the tool, a click on the chevron opens a
    /// drop-down with more choices. It can mirror one button, or hold a group of tools (e.g. pencil and highlighter): the button shows the tool
    /// which was used last.
    /// </summary>
    internal sealed class ToolStripDropButton : ToolStripButton
    {
        private sealed class Member
        {
            public ToolStripButton Real;
            public string Label;
        }

        private readonly List<Member> _members = new();
        private int _active;
        private ToolStripItem _mirror;
        private Action _clickAction;
        private bool _chevronDown;

        /// <summary>
        /// The choices under the chevron
        /// </summary>
        public ToolStripDropDown DropDown { get; set; }

        /// <summary>
        /// True while the mouse is over the chevron
        /// </summary>
        public bool ChevronHot { get; private set; }

        /// <summary>
        /// The area of the chevron: the bottom of the button
        /// </summary>
        public Rectangle ChevronBounds
        {
            get
            {
                int height = (Owner?.DeviceDpi ?? 96) * 16 / 96;
                return new Rectangle(0, Math.Max(0, Height - height), Width, Math.Min(Height, height));
            }
        }

        /// <summary>
        /// Show what another button shows (its checked state, its icon), and click it when this one is clicked
        /// </summary>
        public void MirrorOf(ToolStripButton real)
        {
            _mirror = real;
            Checked = real.Checked;
            Enabled = real.Enabled;
            real.CheckedChanged += (s, e) => Checked = real.Checked;
            real.EnabledChanged += (s, e) => Enabled = real.Enabled;
            _clickAction = real.PerformClick;
            Click += (s, e) => _clickAction();
        }

        /// <summary>
        /// Show the icon of another item (a split button, which has no checked state) and run the action when this one is clicked
        /// </summary>
        public void MirrorOfAction(ToolStripItem real, Action click)
        {
            _mirror = real;
            Enabled = real.Enabled;
            real.EnabledChanged += (s, e) => Enabled = real.Enabled;
            _clickAction = click;
            Click += (s, e) => _clickAction();
            IconBinder.BindLike(this, real);
        }

        /// <summary>
        /// A group of tools in one button: it shows the tool used last, the chevron lists all of them
        /// </summary>
        public void SetMembers(params (ToolStripButton Real, string Label)[] members)
        {
            for (int i = 0; i < members.Length; i++)
            {
                int index = i;
                var member = new Member { Real = members[i].Real, Label = members[i].Label };
                _members.Add(member);
                member.Real.CheckedChanged += (s, e) =>
                {
                    if (member.Real.Checked)
                    {
                        Activate(index, false);
                    }

                    Checked = _members.Any(m => m.Real.Checked);
                };
            }

            var menu = new ToolStripDropDownMenu { ShowImageMargin = true };
            menu.Opening += (s, e) => RebuildMenu(menu);
            DropDown = menu;
            _clickAction = () => _members[_active].Real.PerformClick();
            Click += (s, e) => _clickAction();
            Activate(0, false);
            Checked = _members.Any(m => m.Real.Checked);
        }

        private void Activate(int index, bool click)
        {
            _active = index;
            var member = _members[index];
            Text = member.Label;
            ToolTipText = member.Real.ToolTipText;
            if (!IconBinder.BindLike(this, member.Real))
            {
                Image = member.Real.Image;
            }

            Invalidate();
            if (click)
            {
                member.Real.PerformClick();
            }
        }

        private void RebuildMenu(ToolStripDropDownMenu menu)
        {
            var theme = EditorTheme.Current;
            menu.Items.Clear();
            for (int i = 0; i < _members.Count; i++)
            {
                int index = i;
                var member = _members[i];
                var item = new ToolStripMenuItem(member.Label, member.Real.Image != null ? (Image)member.Real.Image.Clone() : null) { Checked = member.Real.Checked };
                item.Click += (s, e) => Activate(index, true);
                menu.Items.Add(item);
            }

            EditorTheme.ApplyRenderer(menu);
            menu.BackColor = theme.MenuBackground;
            menu.ForeColor = theme.Foreground;
        }

        /// <summary>
        /// Take the icon of the mirrored button (for an icon which is made later, like the emoji)
        /// </summary>
        public void SyncImage()
        {
            if (_mirror?.Image != null)
            {
                Image = _mirror.Image;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hot = DropDown != null && ChevronBounds.Contains(e.Location);
            if (hot != ChevronHot)
            {
                ChevronHot = hot;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (ChevronHot)
            {
                ChevronHot = false;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _chevronDown = e.Button == MouseButtons.Left && DropDown != null && ChevronBounds.Contains(e.Location);
            base.OnMouseDown(e);
        }

        protected override void OnClick(EventArgs e)
        {
            if (_chevronDown)
            {
                _chevronDown = false;
                ShowDropDown();
                return;
            }

            base.OnClick(e);
        }

        /// <summary>
        /// Open the choices under the button
        /// </summary>
        public void ShowDropDown()
        {
            if (Owner != null && DropDown != null)
            {
                DropDown.Show(Owner.PointToScreen(new Point(Bounds.Left, Bounds.Bottom)));
            }
        }
    }
}
