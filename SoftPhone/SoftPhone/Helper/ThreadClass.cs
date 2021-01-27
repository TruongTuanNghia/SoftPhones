using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace SoftPhone.Helper
{
    public class ThreadClass
    {
        public bool ControlInvokeRequired(Control c, Action a)
        {
            if (c.InvokeRequired) c.Invoke(new MethodInvoker(delegate { a(); }));
            else return false;
            return true;
        }
        public void ChangeText(Control ctl, string text)
        {
            if (ControlInvokeRequired(ctl, () => ChangeText(ctl, text))) return;
            ctl.Text = text;
        }
        public void ChangeVisible(Control ctl, bool check)
        {
            if (ControlInvokeRequired(ctl, () => ChangeVisible(ctl, check))) return;
            ctl.Visible=check;
        }
        public void ChangeLocation(Control ctl, int x, int y)
        {
            if (ControlInvokeRequired(ctl, () => ChangeLocation(ctl, x,y))) return;
            ctl.Location = new System.Drawing.Point(x,y);
        }
        public void ChangeLocationGroup(Control control, int left, int top)
        {
            if (ControlInvokeRequired(control, () => ChangeLocationGroup(control, left, top))) return;
            control.Left = left;
            control.Top = top;
        }
        public void ChangeSizeControl(Control ctl,int with,int height)
        {
            if (ControlInvokeRequired(ctl, () => ChangeSizeControl(ctl, with, height))) return;
            ctl.Width = with;
            ctl.Height = height;
        }
        public void ChangSizeText(Control ctl, float size)
        {
            if (ControlInvokeRequired(ctl, () => ChangSizeText(ctl, size))) return;
            ctl.Font = new System.Drawing.Font("Microsoft Sans Serif",size,System.Drawing.FontStyle.Bold);
        }
        public void ChangeSizeFrom(Form frm, int width, int height)
        {
            if (ControlInvokeRequired(frm, () => ChangeSizeFrom(frm, width, height))) return;
            frm.Width = width;
            frm.Height = height;
        }
        public void FocusControl(Control control)
        {
            if (ControlInvokeRequired(control, () => FocusControl(control))) return;
            control.Focus();
        }
    }
}
