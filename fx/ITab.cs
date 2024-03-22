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

	}
	public static class STab {
		public static TabView.Tab GetTab(this ITab session) => new TabView.Tab(session.TabName, session.TabView);
	}
}
