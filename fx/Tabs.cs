using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace fx;
public class Tabs {
	public TabView root;
	public Tabs (Main m) {
		root = new TabView() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(3),
			Border = new() { Effect3D = false, DrawMarginFrame = false, BorderStyle = BorderStyle.None }
		};
		root.SelectedTabChanged += (a, e) => {
			m.readProc = e.NewTab.Text == "Term";
		};
	}
}