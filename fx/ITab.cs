using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace fx {
	public interface ITab {
		string TabName { get; }
		View TabView { get; }
		TabView.Tab GetTab() => new TabView.Tab(TabName, TabView);
	}
}
