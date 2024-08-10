using Terminal.Gui;
using View = Terminal.Gui.View;
using static SView;

using static Terminal.Gui.MouseFlags;
using System.Diagnostics.CodeAnalysis;
namespace fx;
//TO DO: Move tabs to side
public record Folder {
	public View root, head, body;
	public View? currentBody => body.Subviews.SingleOrDefault();
	public Dictionary<View, Tab> tabs = [];
	public Tab currentTab => tabs[currentBody];
	private Dictionary<Tab, View> prevView = [];
	public Folder(View root, params(string name, View view)[] tabs) {
		var head = new View {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = 1,
			//Height = 3,
			//BorderStyle = LineStyle.Single
		};
		var body = new View {
			X = 0,
			Y = Pos.Bottom(head),
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			//BorderStyle = LineStyle.Single
		};
		Init(root, head, body, tabs);
	}
	public Folder(View root, View head, View body, params (string name, View view)[] tabs) {
		Init(root, head, body, tabs);
	}
	private void Init(View root, View head, View body, params (string name, View view)[] tabs) {
		this.root = root;
		this.head = head;
		this.body = body;
		InitTree([[root, head, body]]);
		this.tabs = tabs.Select(pair => new Tab(pair.name, pair.view)).ToDictionary(pair => pair.view);
		Refresh();
	}
	public void Refresh () {
		head.RemoveAll();
		var barLeft = new View {
			Title = "  ",
			X = 0,
			Y = 0,
			Width = 1,
			Height = 1
		};
		head.Add(barLeft);
		foreach(var tab in tabs.Values)
			tab.AddTo(this);
		head.SetNeedsDisplay();
	}
	public Tab AddTab(string name, View view, bool show = false, View? prevItem = null) {
		var tab = new Tab(name, view);
		tab.AddTo(this);
		tabs[view] = tab;
		if(prevItem is { }pi)
			prevView[tab] = pi;
		if(show)
			FocusTab(tab);
		return tab;
	}
	public bool RemoveTab () {
		if(RemoveTab(currentBody, out var tab)) {
			if(prevView.TryGetValue(tab, out var v) && GetParentTab(v, out var t)) {
				FocusTab(t);
				v.SetFocus();
			}
			return true;
		}
		return false;
	}
	public bool RemoveTab(View view, [NotNullWhen(true)] out Tab? tab) {
		var tabList = tabs.Values.ToList();
		if(tabs.Remove(view, out tab)) {
			prevView.Remove(tab);
			if(currentBody == view) {
				body.RemoveAll();
				if(tabs.Any())
					FocusTab(tabList[Math.Clamp(tabList.IndexOf(tab), 0, tabs.Values.Count - 1)]);
			}
			Refresh();
			return true;
		}
		return false;
	}
	public bool GetParentTab(View v, out Tab? tab) {
		tab = null;
		while(v is {} && !tabs.TryGetValue(v, out tab))
			v = v.SuperView;
		return tab is {};
	}
	public void FocusTab(Tab tab, bool focus = true) {
		SelectTab(tab);
		body.Title = tab.name;
		SetBody(tab.view);
		if(focus)
			tab.view.SetFocus();
	}
	private void SelectTab(Tab tab) {
		foreach(var t in tabs.Values)
			t.Refresh();
		tab.Refresh(true);
	}
	public void SwitchTab (int inc = 1) {
		var c = tabs.Count;
		if(c < 2)
			return;
		FocusTab(
			currentBody is {} v ?
				tabs.Values.ElementAt((tabs.Keys.ToList().IndexOf(v) + inc + c) % c) :
				tabs.Values.First(),
			true
			);
	}
	public void SetBody (View view) {
		body.RemoveAll();
		body.Add(view);
	}
}
public record Tab {
	public string name;
	public View view;
	public object Session;
	public View tab;
	public View leftBar, rightBar;
	public Tab (string name, View view) {
		this.name = name;
		this.view = view;
	}
	public void AddTo (Folder folder) {
		//context menu
		//- Kill all to left
		//- Kill all to right

		var head = folder.head;
		leftBar = head.Subviews.Last();
		tab = new Lazy<View>(() => {
			bool home = name == "Home";
			var root = new Label {
				Title = name,
				X = Pos.Right(leftBar),
				Y = 0,
				Height = 1,
				Width = name.Length + (home ? 0 : 0),
			};
			root.MouseEvD(new() {
				[(int)Button1Pressed] = _ => folder.FocusTab(this)
			});

			if(!home) {
				var kill = new Label {
					Title = "[X]",
					X = Pos.AnchorEnd(3),
					Y = 0,
				};
				kill.MouseEvD(new() {
					[(int)Button1Pressed] = _ => folder.RemoveTab(view, out var _)
				});
				//InitTree([[root, kill]]);
			}
			return root;
		}).Value;
		rightBar = new View {
			Title = "%%",
			X = Pos.Right(tab) + 0,
			Y = 0,
			Width = 2,
			Height = 1,
		};
		InitTree([[head, tab, rightBar]]);
		Refresh(folder.currentBody == view);
	}
	public void Refresh (bool open = false) {
		if(open) {
			leftBar.Text = leftBar.Frame.Width == 1 ? "[" : " [";
			rightBar.Text = "] ";
		} else {
			leftBar.Text = rightBar.Text = " ";
		}
	}
	public static implicit operator View (Tab t) => t.tab;
}