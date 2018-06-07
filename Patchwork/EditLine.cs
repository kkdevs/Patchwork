using System;
using System.Windows.Forms;
using System.Linq;
using System.Collections.Generic;

public class EditLine : ComboBox
{
	string histCurrent = null;
	string currentLine = null;

	// User supplied
	public Func<string, object> Eval = (s) => null;
	public Type Sentinel;
	public Action<object> Print = (s) => { };
	public LinkedList<string> history = new LinkedList<string>();
	public Func<string, IEnumerable<string>> GetCompletions = (s) => null;
	public string prehistory;
	public bool escaped;
	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (!DroppedDown)
		{
			if (e.KeyCode == Keys.Enter)
			{
				var t = Text;
				try
				{
					var ret = Eval(Text);
					if (ret != Sentinel)
						Print(ret);
				}
				catch (Exception ex) { Print(ex); };
				history.Remove(t);
				history.AddLast(t);
				Text = "";
				histCurrent = null;
				e.Handled = true;
				return;
			}
			else if (e.KeyCode == Keys.Down)
			{
				if (histCurrent != null)
					histCurrent = history.Find(histCurrent)?.Next?.Value;
				Text = (histCurrent ?? prehistory) ?? currentLine;
				Select(Text.Length, 0);
				e.Handled = true;
				return;
			}
			else if (e.KeyCode == Keys.Up)
			{
				if (histCurrent == null)
				{
					try
					{
						histCurrent = history.LastOrDefault();
						prehistory = Text;
					}
					catch { };
				}
				else
					histCurrent = history.Find(histCurrent)?.Previous?.Value ?? histCurrent;
				if (histCurrent != null)
				{
					Text = histCurrent;
					Select(Text.Length, 0);
				}
				e.Handled = true;
				return;
			}
		}
		else
		{
			if (e.KeyCode == Keys.Escape)
				escaped = true;
		}

		if (e.KeyCode == Keys.Tab)
		{
			if (!DroppedDown)
			{
				if (LoadSuggestions())
					DroppedDown = true;
			}
			else
			{
				try
				{
					SelectedIndex++;
				}
				catch { };
			}
			e.Handled = true;
			return;
		}
		base.OnKeyDown(e);
	}
	protected override void OnTextChanged(EventArgs e)
	{
		base.OnTextChanged(e);
	}

	public bool LoadSuggestions()
	{
		escaped = false;
		Items.Clear();
		var sug = GetCompletions(Text);
		if (sug.Count() == 0)
		{
			return false;
		}
		if (sug.Count() == 1)
		{
			currentLine = Text = sug.First();
			base.Select(Text.Length, 0);
			return false;
		}
		if (sug != null)
			foreach (var s in sug)
				Items.Add(s);
		SelectedIndex = 0;
		return true;
	}

	/*	protected override void OnSelectionChangeCommitted(EventArgs e)
		{
			//base.OnSelectionChangeCommitted(e);
			currentLine = Text;
			base.Select(Text.Length, 0);
		}*/

	protected override void OnTextUpdate(EventArgs e)
	{
		base.OnTextUpdate(e);
		currentLine = Text;
		//base.Select(Text.Length, 0);
		if (DroppedDown)
			if (!LoadSuggestions())
				DroppedDown = false;
	}

	protected override void OnSelectedItemChanged(EventArgs e)
	{
		base.OnSelectedItemChanged(e);
		base.Select(Text.Length, 0);
	}
	protected override void OnDropDownClosed(EventArgs e)
	{
		base.OnDropDownClosed(e);
		if (escaped)
			Text = currentLine;
		base.Select(Text.Length, 0);
	}

	protected override void OnKeyUp(KeyEventArgs e)
	{
		base.OnKeyUp(e);
		if (DroppedDown)
		{
			if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Escape || e.KeyCode == Keys.Tab)
				base.Select(Text.Length, 0);
		}
		else
		{
			if (e.KeyCode != Keys.Escape && e.KeyCode != Keys.Tab)
			{
				currentLine = Text;
			}
		}
	}

	protected override bool IsInputKey(Keys keyData)
	{
		if (keyData == Keys.Tab)
			return true;
		return base.IsInputKey(keyData);
	}
}

