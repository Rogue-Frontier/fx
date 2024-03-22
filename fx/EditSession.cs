using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace fx;
public class EditSession : ITab {
	public string TabName => "Edit";
	public View TabView => root;

	public View root;
	public EditSession (string path) {
		root = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		var addressBar = new TextField(path) {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = 1
		};
		var textView = new TextView() {
			X = 0,
			Y = 2,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		SView.InitTree([root, addressBar, textView]);
	}
}