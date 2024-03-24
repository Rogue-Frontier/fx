using Terminal.Gui;
using View = Terminal.Gui.View;
using static SView;
using fx;

namespace fx;
public record Folder {
	public View root, head, body;
	private List<View> bars = new();
	private List<Tab> tabsList = new();
	private Dictionary<View, Tab> tabs = new();
	public Folder(View root, params(string name, View view)[] tabs) {
		this.root = root;
		head = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = 1
		};
		body = new View() {
			X = 0,
			Y = 1,
			Width = Dim.Fill(),
			Height = Dim.Fill()
		};
		foreach(var (name, view) in tabs) {
			var tab = new Tab(name, view);
			tabsList.Add(tab);
			this.tabs[view] = tab;
		}
		Refresh();
		/*
		var barLeft = new Label(" ") {
			X = 0,
			Y = 0,
			Width = 1,
			Height = 1
		};
		head.Add(barLeft);

		foreach(var (name, view) in tabs) {
			AddTab(name, view);
		}
		*/
		InitTree([[root, head, body]]);
	}
	public void Refresh () {
		head.RemoveAll();
		var barLeft = new View(" ") {
			X = 0,
			Y = 0,
			Width = 1,
			Height = 1
		};
		head.Add(barLeft);
		foreach(var tab in tabs.Values) {
			tab.Place(this);
		}
		head.SetNeedsDisplay();
	}
	public Tab AddTab(string name, View view, bool show = false) {
		var tab = new Tab(name, view);
		tab.Place(this);
		tabs[view] = tab;
		tabsList.Add(tab);
		if(show) {
			FocusTab(tab);
		}
		return tab;
	}
	public void RemoveTab(View view) {
		if(tabs.Remove(view, out var tab)) {
			int index = tabsList.IndexOf(tab);
			tabsList.Remove(tab);
			if(body.Subviews.SingleOrDefault() is { } v) {
				if(v == view) {
					body.RemoveAll();

					//Show next tab
					if(tabsList.Any()) {
						FocusTab(tabsList[Math.Clamp(index, 0, tabsList.Count - 1)]);
					}
				}else {
					//FocusTab(tabs[v]);
				}
			}
			Refresh();
		}
	}

	public void FocusTab(Tab tab) {
		SelectTab(tab);
		SetBody(tab.view);
	}
	private void SelectTab(Tab tab) {
		foreach(var t in tabs.Values) {
			t.Refresh();
		}
		tab.Refresh(true);
	}
	public void NextTab () {
		if(tabsList.Count == 0) {
			return;
		}
		if(body.Subviews.SingleOrDefault() is { } v) {
			var tab = tabs[v];
			if(tabsList.Count == 1) {
				return;
			}
			var next = tabsList[(tabsList.IndexOf(tab) + 1) % tabsList.Count];
			FocusTab(next);
		} else {
			FocusTab(tabsList[0]);
		}
	}
	public void SetBody (View view) {
		body.RemoveAll();
		body.Add(view);
	}
}


public record Tab {
	public string name;
	public View view;

	public View tab;
	public View leftBar, rightBar;
	public Tab (string name, View view) {
		this.name = name;
		this.view = view;
	}
	public void Place (Folder folder) {
		//context menu
		//- Kill all to left
		//- Kill all to right

		var head = folder.head;
		leftBar = head.Subviews.Last();
		tab = new Lazy<View>(() => {
			var root = new Pad(name) {
				X = Pos.Right(leftBar),
				Y = 0,
				Height = 1,
				Width = name.Length + 3,
			};
			root.MouseClick += e => {
				if(e.MouseEvent.Flags == MouseFlags.Button1Pressed) {
					folder.FocusTab(this);
				}
			};

			var kill = new Pad("[X]") {
				X = Pos.AnchorEnd(3),
				Y = 0,
				Width = 3,
				Height = 1
			};
			kill.Clicked += () => {
				folder.RemoveTab(view);
			};
			InitTree([[root, kill]]);
			return root;
		}).Value;

		rightBar = new View(" ") {
			X = Pos.Right(tab),
			Y = 0,
			Height = 1,
		};
		InitTree([[head, tab, rightBar]]);
		Refresh(folder.body.Subviews.SingleOrDefault() == view);
	}
	public void Refresh (bool open = false) {
		if(open) {
			leftBar.Text = "<";
			rightBar.Text = ">";
		} else {
			leftBar.Text = rightBar.Text = " ";
		}
	}
	public static implicit operator View (Tab t) => t.tab;
}