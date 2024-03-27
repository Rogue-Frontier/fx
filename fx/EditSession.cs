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
	public EditSession (string path, int row = 0, int col = 0) {
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
		var save = new Label() {
			AutoSize = false,
			X = 8,
			Y = 0,
			Width = 4,
			Height = 1,
			Text = "Save",
		};

		var textView = new TextView() {
			X = 8,
			Y = 2,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			Text = File.ReadAllText(path),
		};


		save.MouseClickD(new() {
			[Button1Clicked] = _ => {
				File.WriteAllText(path, textView.Text);
			}
		});


		textView.KeyDownD(new() {
			[(int)Esc] = e => {
				if(!textView.ReadOnly) {
					textView.ReadOnly = true;
					textView.CanFocus = false;
				} else {
					return;
				}
				Done:
				e.Handled = true;
			},
		});
		root.KeyDownD(new() {
			[(int)CursorUp] = e => {
				if(!textView.CanFocus) {
					textView.TopRow -= 1;
					textView.SetNeedsDisplay();
				}
			},
			[(int)CursorDown] = e => {
				if(!textView.CanFocus) {
					textView.TopRow += 1;
					textView.SetNeedsDisplay();
				}
			},

			['S' | (uint)AltMask] = e => {
				return;
			}
		});

		var main = new Main();
		main.TermEnter += e => {
			if(main.folder.currentBody == root) {
				var cmd = e.text;
				cmd = string.Format(cmd, path);

				ExploreSession.RunCmd(cmd);
			}
		};
		textView.CursorPosition = new(col, row);
		SView.InitTree([root, save, textView]);
	}
}