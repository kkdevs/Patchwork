using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ParadoxNotion.Serialization.FullSerializer;
using UnityEngine;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Patchwork
{
	public partial class SettingsForm : Form
	{
		const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
		Settings s;
		public SettingsForm(Settings _s)
		{
			InitializeComponent();
			s = _s;

			// Generate automatic data binding for class Settings
			foreach (var f in typeof(Settings).GetFields())
			{
				var ff = typeof(SettingsForm).GetField(f.Name, flags);
				if (ff == null) continue;
				var control = ff.GetValue(this) as Control;
				var t = control as TextBox; if (t != null) t.TextChanged += (o, e) =>
				{
					try
					{
						s.Update(f.Name, float.Parse(t.Text));
					}
					catch { };
				};
				var l = control as ComboBox;
				if (l != null)
				{
					if (f.FieldType == typeof(byte))
					{
						l.SelectionChangeCommitted += (o, e) =>
						{
							s.Update(f.Name, (byte)l.SelectedIndex);
						};
					}
					else
					{
						l.TextChanged += (e, o) =>
						{
							s.Update(f.Name, l.Text);
						};
					}
				}
				var c = control as CheckBox; if (c != null) c.CheckedChanged += (o, e) =>
				{
					s.Update(f.Name, c.Checked);
				};
				var b = control as Button; if (b != null)
				{
					b.KeyDown += (o, e) => {
						if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
						{
							e.SuppressKeyPress = true;
							b.PerformClick();
						}
					};
				}
			}
			f_qualitySelect.SelectionChangeCommitted += (e, o) =>
			{
				s.qualitySelect = (byte)f_qualitySelect.SelectedIndex;
				s.Apply(false);
				UpdateForm();
				Program.SaveConfig();
			};
			linkUnityDoc.Click += (o, e) =>
			{
				ProcessStartInfo sInfo = new ProcessStartInfo(linkUnityDoc.Text);
				Process.Start(sInfo);
			};
		}

		public void UpdateForm()
		{
			var enabled = s.qualitySelect == 0;
			foreach (var f in typeof(Settings).GetFields())
			{
				var val = f.GetValue(s);
				var ff = typeof(SettingsForm).GetField(f.Name, flags);
				if (ff == null) continue;
				var control = ff.GetValue(this) as Control;
				if (f.Name != "fullscreen" && f.Name != "resolution")
				{
					Control[] parents = { groupBox1, groupBox2, groupBox3 };
					if (parents.Contains(control.Parent))
						control.Enabled = enabled;
				}
				//Trace.Log($"UpdateForm(): Setting {f.Name} to {val}");
				var t = control as TextBox;
				if (t != null)
				{
					t.Text = val.ToString();
				}
				var l = control as ComboBox;
				if (l != null)
				{
					if (f.FieldType == typeof(byte))
					{
						var bv = (int)(byte)val;
						l.SelectedIndex = bv;
					}
					else
					{
						l.Text = (string)val;
					}
				}
				var b = control as CheckBox;
				if (b != null)
				{
					b.Checked = (bool)val;
				}
			}
			f_qualitySelect.SelectedIndex = s.qualitySelect;
		}
	}
}