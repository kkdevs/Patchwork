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
using static Patchwork;

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
		{			ShowScrollBar(new IntPtr(scriptList.Handle.ToInt64()), 0, false);
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
						if (f.Name != "resolution")
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
		setRes.Click += (o, e) =>
		{
			s.Update("resolution", resolution.Text);
		};
		f_qualitySelect.SelectionChangeCommitted += (e, o) =>
		{
			s.qualitySelect = (byte)f_qualitySelect.SelectedIndex;
			s.Apply(false);
			settings.DoUpdateCamera(null);
			UpdateForm();
			SaveConfig();
		};
		linkUnityDoc.Click += (o, e) =>
		{
			ProcessStartInfo sInfo = new ProcessStartInfo(linkUnityDoc.Text);
			Process.Start(sInfo);
		};
		foreach (var tlabel in this.GetAll(typeof(LinkLabel)))
		{
			var label = tlabel;
			if (label.Tag == null || label.Text != "[?]") continue;
			label.Click += (o, e) =>
			{
				ProcessStartInfo sInfo = new ProcessStartInfo("https://github.com/kkdevs/Patchwork/wiki/Launcher#" + (label.Tag as string));
				Process.Start(sInfo);
			};
		}
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
		/*for (var i = 0; i < 100; i++)
		{
			var li = new ListViewItem(new[] { "a", "b", "c" });
			scriptList.Items.Add(li);
		}*/
		scriptList.ItemCheck += (o, e) =>
		{
			var lv = scriptList.Items[e.Index];
			var script = lv.Tag as ScriptEntry;
			if (e.NewValue == CheckState.Checked)
			{
				if (script.enabled)
					return;
				// enable script's dependencies
				foreach (var scr in ScriptEntry.list)
					if (script.deps.Contains(scr.name.ToLower()))
					{
						scr.enabled = true;
						if (scr.listView != null)
						{
							scr.listView.Checked = scr.enabled;
							if (scr.enabled)
								settings.scriptDisabled.Remove(scr.name.ToLower());
						}
					}
				script.enabled = true;
				if (script.enabled)
					settings.scriptDisabled.Remove(script.name.ToLower());
				SaveConfig();
			}  else
			{
				if (!script.enabled)
					return;
				// disable all scripts depending on this one
				foreach (var scr in ScriptEntry.list)
					if (scr.deps.Contains(script.name.ToLower()))
					{
						scr.enabled = false;
						if (scr.listView != null)
						{
							scr.listView.Checked = scr.enabled;
							if (!scr.enabled)
								settings.scriptDisabled.Add(scr.name.ToLower());
						}
					}
				script.enabled = false;
				if (!script.enabled)
					settings.scriptDisabled.Add(script.name.ToLower());
				SaveConfig();
			}
		};
		var sb = scriptList.GetType().GetField("h_scroll", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(scriptList) as ScrollBar;
		sb.Size = new System.Drawing.Size(0, 0);
	}

	public bool updating;
	public void UpdateForm()
	{
		updating = true;
		var enabled = s.qualitySelect == 0;
		foreach (var f in typeof(Settings).GetFields())
		{
			var val = f.GetValue(s);
			var ff = typeof(SettingsForm).GetField(f.Name, flags);
			if (ff == null) continue;
			var control = ff.GetValue(this) as Control;
			Control[] parents = { groupBox1, groupBox2, groupBox3 };
			if (parents.Contains(control.Parent))
				control.Enabled = enabled;

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
		updating = false;
		if (launched) return;
		scriptList.Items.Clear();
		foreach (var script in ScriptEntry.list)
		{
			var item = new ListViewItem(new[] { script.name, script.version, script.ass == null ? "Script" : "DLL", script.info });
			item.Tag = script;
			item.Checked = script.enabled;
			if (script.ass == null && script.info == "")
				continue;
			script.listView = item;
			scriptList.Items.Add(item);
		}
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

	private void label24_Click(object sender, EventArgs e)
	{

	}

	private void noscopeAlphaMode_SelectedIndexChanged(object sender, EventArgs e)
	{

	}

	private void enabler_noTelescope_Enter(object sender, EventArgs e)
	{

	}

	private void checkBox1_CheckedChanged_1(object sender, EventArgs e)
	{

	}

	private void label46_Click(object sender, EventArgs e)
	{

	}

	private void cam_useSunShafts_CheckedChanged(object sender, EventArgs e)
	{

	}

	private void label33_Click(object sender, EventArgs e)
	{

	}
}
