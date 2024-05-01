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

		var w = new FileSystemWatcher(Path.GetDirectoryName(path));
		w.Renamed += (a, e) => {
			if(e.OldFullPath == path) {
				path = e.FullPath;
			}
		};
		w.EnableRaisingEvents = true;

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

		/*
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
		*/

		var mode = new Label() {
			AutoSize = false,
			X = Pos.AnchorEnd(4),
			Y = 0,
			Width = 4,
			Height = 1,
			Text = "EDIT",
		};

		var address = new Label() {
			AutoSize = false,
			X = 0,
			Y = 0,
			Width = Dim.Fill() - 16,
			Height = 1,
		};
		address.Text = path;

		var lineNumbers = new Label() {
			AutoSize = false,
			X = 0,
			Y = Pos.Bottom(address),
			Width = 5,
			Height = Dim.Fill(),
		};
		
		var textView = new TextView() {
			X = 6,
			Y = Pos.Bottom(address),
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			ReadOnly = true,

			ColorScheme = Application.Top.ColorScheme with {
				Focus = new(Color.White, new Color(25,25,25))
			}
		};

		textView.TextChanged += (a, e) => RefreshLines();
		var prevTopRow = 0;

		textView.DrawContentComplete += (a, e) => {
			if(textView.TopRow == prevTopRow) {
				return;
			}
			prevTopRow = textView.TopRow;
			RefreshLines();
		};
		textView.UnwrappedCursorPosition += (a, e) => {
			if(textView.TopRow == prevTopRow) {
				return;
			}
			prevTopRow = textView.TopRow;
			RefreshLines();
		};


		main.TermEnter += e => {
			if(main.folder.currentBody == root) {
				var replace = new {
					file = path,
					filename = Path.GetFileName(path),
					select = textView.SelectedText
				};
				var cmd = e.text;
				foreach(var f in replace.GetType().GetFields()) {
					cmd = cmd.Replace($"%{f.Name}%", (string)f.GetValue(replace), StringComparison.OrdinalIgnoreCase);
				}
				cmd = string.Format(cmd, textView.Text.Split("\n"));
				ExploreSession.RunCmd(cmd, Path.GetDirectoryName(path));
				e.term.Text = "";
			}
		};
		RefreshFile();
		RefreshMode();
		RefreshLines();
		void RefreshLines () {
			lineNumbers.Text = string.Join("\n", Enumerable.Range(textView.TopRow, textView.Frame.Height).Select(l => $"{l,4}| "));
		}
		void RefreshFile() {
			textView.Text = File.ReadAllText(path);
		}
		void RefreshMode () {
			mode.Text = textView.ReadOnly ? "READ" : "EDIT";
		}
		void Save () {
			File.WriteAllText(path, textView.Text);
		}
		/*
		save.MouseClickD(new() {
			[Button1Clicked] = _ => {
				Save();
			}
		});
		*/

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
			/*
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
			*/
			[':'] = e => {
				e.Handled = textView.ReadOnly;
				if(e.Handled) {
					main.FocusTerm(textView);
				}
			},
			['/'] = e => {
				e.Handled = textView.ReadOnly;
				if(e.Handled) {
					SView.ShowContext(textView, [
						new MenuItem("Cancel", null, () => { }),
					new MenuItem("Save", null, Save),
					new MenuItem("Refresh", null, RefreshFile)
					], 0, 0);
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
			/*
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
			*/
			['G' | (uint)CtrlMask] = e => {
				return;
			},
		});
		textView.CursorPosition = new(col, row);
		SView.InitTree([root, address, mode, /* save, */lineNumbers, textView]);
	}
}