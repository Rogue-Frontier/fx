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

		var tree = new QuickAccessTree();
		var quickAccess = new TreeView<IFileTree>() {
			Title = "Quick Access",
			BorderStyle = LineStyle.Single,
			X = 0,
			Y = 0,
			Width = 24,
			Height = Dim.Fill(),
			TreeBuilder = tree,
			AspectGetter = tree.AspectGetter
		};

		quickAccess.AddObjects(tree.GetRoots(main));
		foreach(var t in quickAccess.Objects)
			quickAccess.Expand(t);
		var right = new View() {
			X = 24,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			BorderStyle = LineStyle.Single
		};
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
			},
		};
		quickAccess.ObjectActivated += (o, e) => {
			if(e.ActivatedObject is IFilePath { path: { } p } && File.Exists(p)) {
				path = p;
				RefreshFile();
			}
		};
		quickAccess.KeyDownD(value: new() {
			['"'] = _ => {
				if(quickAccess.SelectedObject is IFilePath item && File.Exists(item.path)) {
					ExploreSession.ShowPreview($"Preview: {item.path}", File.ReadAllText(item.path));
				}
			},
			['?'] = _ => {
				if(quickAccess.SelectedObject is IFilePath item) {
					ExploreSession.ShowProperties(main.ctx.GetPathItem(item.path, ExploreSession.GetStaticProps));
				}
			},
			['/'] = _ => {
				var rowObj = quickAccess.SelectedObject;
				var (row, col) = quickAccess.GetObjectPos(rowObj) ?? (0, 0);
				SView.ShowContext(quickAccess, [.. HomeSession.GetSpecificActions(quickAccess, main, row, rowObj)], row + 2, col + 2);
			},
			['<'] = e => {
				e.Handled = true;
				main.folder.SwitchTab(-1);
			},
			['>'] = e => {
				e.Handled = true;
				main.folder.SwitchTab(1);
			}
		});
		quickAccess.MouseEvD(new() {
			[(int)Button1Pressed] = e => {
				var y = e.MouseEvent.Y;

				var row = y + quickAccess.ScrollOffsetVertical;
				var rowObj = quickAccess.GetObjectOnRow(row);
				if(rowObj == null) {
					return;
				}
				if(rowObj == quickAccess.SelectedObject) {
					if(quickAccess.IsExpanded(rowObj)) {
						quickAccess.Collapse(rowObj);
					} else {
						quickAccess.Expand(rowObj);
					}
				}
				quickAccess.SelectedObject = rowObj;
				quickAccess.SetNeedsDisplay();
				e.Handled = true;
			},
			[(int)Button1Clicked] = e => {
				e.Handled = true;
			},
			[(int)Button1Released] = e => {

				var y = e.MouseEvent.Y;

				var row = y + quickAccess.ScrollOffsetVertical;
				var rowObj = quickAccess.GetObjectOnRow(row);
				if(rowObj != quickAccess.SelectedObject) {
					return;
				}
				if(quickAccess.IsExpanded(quickAccess.SelectedObject)) {
					quickAccess.Collapse(quickAccess.SelectedObject);
				} else {
					quickAccess.Expand(quickAccess.SelectedObject);
				}
				e.Handled = true;
			},
			[(int)Button3Pressed] = e => {
				var prevObj = quickAccess.SelectedObject;
				var y = e.MouseEvent.Y;
				var row = y + quickAccess.ScrollOffsetVertical;
				var rowObj = quickAccess.GetObjectOnRow(row);
				var c = SView.ShowContext(quickAccess, [.. HomeSession.GetSpecificActions(quickAccess, main, row, rowObj)], y, e.MouseEvent.X);
				if(row < main.ctx.fx.libraryData.Count) {
					c.MenuBar.MenuAllClosed += (a, e) => {
						if(main.ctx.fx.libraryData.Count == 0) {
							return;
						}
						if(quickAccess.GetParent(prevObj) != null) {
							quickAccess.SelectedObject = prevObj;
						} else {
							//TODO: Find suitable dest
						}
					};
				}
				e.Handled = true;
			}
		});
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
			address.Text = path;
			var data = File.ReadAllText(path);
			textView.Text = data;
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

			['F' | (int)CtrlMask] = e => {
				//Find
				return;
			},
			['R' | (int)CtrlMask] = e => {
				//Replace
				return;
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
		SView.InitTree([right, address, mode, /* save, */lineNumbers, textView]);
		SView.InitTree([root, quickAccess, right]);
	}
}