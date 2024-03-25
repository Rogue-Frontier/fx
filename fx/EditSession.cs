using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;
namespace fx;
public class EditSession {
	public View root;
	public EditSession (string path, int row = 0, int col = 0) {
		var content = File.ReadAllText(path);
		root = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		var editList = new FrameView("Opened", new() { Effect3D = false, BorderStyle = BorderStyle.Single, DrawMarginFrame = true }) {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		var addressBar = new TextField(path) {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = 1,
			ReadOnly = true,
			CanFocus=false,

		};
		var textView = new TextView() {
			X = 0,
			Y = 2,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			Text = content,
		};
		textView.CursorPosition = new(col, row);
		SView.InitTree([root, addressBar, textView]);
	}
}