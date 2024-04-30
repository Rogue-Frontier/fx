using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

using static Terminal.Gui.MouseFlags;
using static Terminal.Gui.KeyCode;
namespace fx;
public class EditSession {
	public View root;

	public event Action Close;
	public EditSession (Main main, string path, int row = 0, int col = 0) {

		main.TermEnter += e => {
			if(main.folder.currentBody == root) {
				var cmd = e.text;
				cmd = string.Format(cmd, path);
				ExploreSession.RunCmd(cmd, Path.GetDirectoryName(path));
				e.term.Text = "";
			}
		};

		root = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		var editList = new FrameView() {
			Title = "Opened",
			BorderStyle = LineStyle.Single,
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		var save = new Button() {
			AutoSize = false,
			X = 0,
			Y = 0,
			Width = 6,
			Height = 1,
			Text = "Save",
			NoDecorations = false,
			NoPadding = true
		};

		var mode = new Label() {
			AutoSize = false,
			X = 0,
			Y = 1,
			Width = 8,
			Height = 1,
			Text = "EDIT",
		};
		var textView = new TextView() {
			X = 0,
			Y = 2,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			Text = File.ReadAllText(path),

			ColorScheme = Application.Top.ColorScheme with {
				Focus = new(Color.White, new Color(25,25,25))
			}
		};

		void RefreshMode () {
			mode.Text = textView.ReadOnly ? "READ" : "EDIT";
		}
		void Save () {
			File.WriteAllText(path, textView.Text);
		}
		save.MouseClickD(new() {
			[Button1Clicked] = _ => {
				Save();
			}
		});

		textView.KeyDownD(new() {
			[(int)Esc] = e => {
				e.Handled = true;
				if(!textView.ReadOnly) {
					textView.ReadOnly = true;
					RefreshMode();
				}
			},
			[(int)Enter] = e => {
				e.Handled = textView.ReadOnly;
				if(textView.ReadOnly) {
					textView.ReadOnly = false;
					RefreshMode();
				}
			},
			[(int)(Enter | ShiftMask)] = e => {
				e.Handled = !textView.ReadOnly;
				if(e.Handled) {
					textView.ReadOnly = true;
					RefreshMode();
				}
			},
			['S'] = e => {
				e.Handled = textView.ReadOnly;
				if(e.Handled)
					Save();
			},
			['G' | (int)CtrlMask] = e => {
				return;
			},
			['R'] = e => {
				e.Handled = textView.ReadOnly;
				if(!e.Handled) {
					return;
				}
				var line = new string(textView.GetLine(textView.CurrentRow).Select(r => (char)r.Rune.Value).ToArray());
				line = line.TrimStart('/');
				var cmd = string.Format(line, path);
				ExploreSession.RunCmd(cmd, Path.GetDirectoryName(path));
				return;
			},
			[':'] = e => {
				e.Handled = textView.ReadOnly;
				if(e.Handled) {
					main.FocusTerm();
				}
			},
			[(int)Delete] = e => {
				e.Handled = textView.ReadOnly;
				if(e.Handled)
					main.folder.RemoveTab();
			},
			['<'] = e => {
				e.Handled = textView.ReadOnly;
				if(e.Handled) {
					main.folder.SwitchTab(-1);
				}
			},
			['>'] = e => {
				e.Handled = textView.ReadOnly;
				if(e.Handled) {
					main.folder.SwitchTab(1);
				}
			}

		});
		root.KeyDownD(new() {
			[(int)CursorUp] = e => {
				e.Handled = textView.ReadOnly;
				if(e.Handled) {
					textView.TopRow -= 1;
					textView.SetNeedsDisplay();
				}
			},
			[(int)CursorDown] = e => {
				e.Handled = textView.ReadOnly;
				if(e.Handled) {
					textView.TopRow += 1;
					textView.SetNeedsDisplay();
				}
			},

			['G' | (uint)CtrlMask] = e => {
				return;
			},
		});
		textView.CursorPosition = new(col, row);
		SView.InitTree([root, save, mode, textView]);
	}
}