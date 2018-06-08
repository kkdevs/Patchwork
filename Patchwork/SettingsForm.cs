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
			CenterToScreen();

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
						if (f.FieldType == typeof(string))
						{
							s.Update(f.Name, t.Text);
							return;
						}
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
					foreach (var en in Controls.Find("enabler_" + f.Name, true))
						en.Enabled = c.Checked;
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
				Program.settings.DoUpdateCamera(null);
				UpdateForm();
				Program.SaveConfig();
			};
			linkUnityDoc.Click += (o, e) =>
			{
				ProcessStartInfo sInfo = new ProcessStartInfo(linkUnityDoc.Text);
				Process.Start(sInfo);
			};
			tabPage1.Enter += (o, e) =>
			{
				launchButton.Focus();
			};
			/*tabPage6.Enter += (o, e) =>
			{
				replInput.Focus();
			};*/
			tabControl1.SelectedIndexChanged += (o, e) => {
				if (tabControl1.SelectedTab == tabPage6)
					replInput.Focus();
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
					foreach (var en in Controls.Find("enabler_" + f.Name, true))
						en.Enabled = (bool)val;
					b.Checked = (bool)val;
				}
			}
			f_qualitySelect.SelectedIndex = s.qualitySelect;
		}

		private void tabPage2_Click(object sender, EventArgs e)
		{

		}

		private void runChara_Click(object sender, EventArgs e)
		{

		}

		private void hideMoz_CheckedChanged(object sender, EventArgs e)
		{

		}

		private void checkBox1_CheckedChanged(object sender, EventArgs e)
		{

		}

		public void ConnectRepl()
		{
			LinkedList<string> history = new LinkedList<string>();
			string histCurrent = null;
			bool tabMode = false;
			string currentLine = null;

			replInput.KeyDown += (o, e) => {
				if (!tabMode && e.KeyCode == Keys.Enter)
				{
					var t = replInput.Text;
					try
					{
						var ret = Script.eval(replInput.Text);
						if (ret != typeof(Script.Sentinel))
							Script.pp(ret);
					}
					catch (Exception ex) { Script.print(ex); };
					history.Remove(t);
					history.AddLast(t);
					replInput.Text = "";
					histCurrent = null;
				}
				else if (!tabMode && e.KeyCode == Keys.Up)
				{
					if (histCurrent == null)
						histCurrent = history.Last();
					else
						histCurrent = history.Find(histCurrent).Previous?.Value ?? histCurrent;
					if (histCurrent != null)
					{
						replInput.Text = histCurrent;
						replInput.SelectionStart = histCurrent.Length;
					}
				}
				else if (!tabMode && e.KeyCode == Keys.Down)
				{
					if (histCurrent != null)
					{
						histCurrent = history.Find(histCurrent).Previous?.Value ?? "";
						replInput.Text = histCurrent;
						replInput.SelectionStart = histCurrent.Length;
					}
				}
				else if (e.KeyCode == Keys.Tab)
				{
					tabMode = !tabMode;
					if (!tabMode)
					{
						replInput.Items.Clear();
						replInput.DroppedDown = false;
					}
					else if (LoadSuggestions(replInput.Text))
					{
						replInput.DroppedDown = true;
					}
					else tabMode = false;
					e.Handled = true;
				}
			};
			replInput.PreviewKeyDown += (o, e) =>
			{
				if (e.KeyCode == Keys.Tab)
					e.IsInputKey = true;
			};
			replInput.TextChanged += (o, e) =>
			{
				if (tabMode)
					replInput.Text = "x";
			};
		}

		public bool LoadSuggestions(string t)
		{
			string pfx;
			var cmps = Script.Evaluator.GetCompletions(t, out pfx);
			if (cmps != null)
			{
				foreach (var v in cmps)
					replInput.Items.Add(pfx + v);
			}
			return cmps != null;
		}

		private void replInput_SelectedIndexChanged(object sender, EventArgs e)
		{

		}
	}
}