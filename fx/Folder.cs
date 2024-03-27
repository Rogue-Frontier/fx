using Terminal.Gui;
using View = Terminal.Gui.View;
using static SView;
using fx;

using static Terminal.Gui.MouseFlags;
using static Terminal.Gui.KeyCode;
namespace fx;

//TO DO: Move tabs to side
public record Folder {
	public View root, head, body;

	public View? currentBody => body.Subviews.SingleOrDefault();
	private List<View> bars = new();
	private List<Tab> tabsList = new();
	private Dictionary<View, Tab> tabs = new();
	public Folder(View root, params(string name, View view)[] tabs) {
		this.root = root;
		head = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = 3,

			Title = "Tabs",
			BorderStyle = LineStyle.Single
		};
		body = new View() {
			X = 0,
			Y = 3,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			BorderStyle = LineStyle.Single
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
		var barLeft = new View() {
			Title = " ",
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
	public bool RemoveTab () => RemoveTab(currentBody);
	public bool RemoveTab(View view) {
		if(tabs.Remove(view, out var tab)) {
			int index = tabsList.IndexOf(tab);
			tabsList.Remove(tab);
			if(currentBody is { } v) {
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
			return true;
		}
		return false;
	}

	public void FocusTab(Tab tab) {
		SelectTab(tab);
		body.Title = tab.name;
		SetBody(tab.view);
	}
	private void SelectTab(Tab tab) {
		foreach(var t in tabs.Values) {
			t.Refresh();
		}
		tab.Refresh(true);
	}
	public void SwitchTab (int inc = 1) {
		var c = tabsList.Count;
		if(c == 0) {
			return;
		}
		if(currentBody is { } v) {
			var tab = tabs[v];
			if(c == 1) {
				return;
			}
			var next = tabsList[(tabsList.IndexOf(tab) + inc + c) % c];
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
			var root = new Label() {
				AutoSize = false,
				Title = name,
				X = Pos.Right(leftBar),
				Y = 0,
				Height = 1,
				Width = name.Length + 3,
			};
			root.MouseEvD(new() {
				[(int)Button1Pressed] = _ => folder.FocusTab(this)
			});

			var kill = new Label() {
				Title = "[X]",
				X = Pos.AnchorEnd(3),
				Y = 0,
			};
			kill.MouseEvD(new() {
				[(int)Button1Pressed] = _ => folder.RemoveTab(view)
			});
			InitTree([[root, kill]]);
			return root;
		}).Value;

		rightBar = new View() {
			Title = "%",
			X = Pos.Right(tab) + 0,
			Y = 0,
			Width = 1,
			Height = 1,
		};
		InitTree([[head, tab, rightBar]]);
		Refresh(folder.currentBody == view);
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