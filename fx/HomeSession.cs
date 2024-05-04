using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;
using static Terminal.Gui.MouseFlags;
using static Terminal.Gui.KeyCode;
using GDShrapt.Reader;
namespace fx;
public class HomeSession {
	public View root;
	private Ctx ctx;
	public HomeSession(Main main) {
		ctx = main.ctx;
		root = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
		};

		var tree = new QuickAccessTree();
		var quickAccess = new TreeView<IFileTree>() {
			Title = "Quick Access",
			BorderStyle = LineStyle.Single,
			X = 0,
			Y = 0,
			Width = 48,
			Height = Dim.Fill(),
			TreeBuilder = tree,
			AspectGetter = tree.AspectGetter
		};

		quickAccess.AddObjects(tree.GetRoots(main));
		foreach(var t in quickAccess.Objects)
			quickAccess.Expand(t);
		quickAccess.ObjectActivated += (o, e) => {
			if(e.ActivatedObject is IFilePath item) {
				main.folder.AddTab("Expl", new ExploreSession(main, item.path).root, true);
			}
		};
		quickAccess.KeyDownD(value: new() {
			[(int)Enter | (int)ShiftMask] = _ => {
				if(quickAccess.SelectedObject is IFilePath { path: { } p } && Directory.Exists(p)) {
					main.folder.AddTab("Expl", new ExploreSession(main, p).root, false, root);
				}
				return;
			},
			['"'] = _ => {
				if(quickAccess.SelectedObject is IFilePath {path:{ }p } && Path.Exists(p)) {
					ExploreSession.ShowPreview($"Preview: {p}",
						Directory.Exists(p)?
							string.Join('\n', Directory.GetFileSystemEntries(p).Select(d => d[(p.Length+1)..])) :
							File.ReadAllText(p)
						);
				}
			},
			['?'] = _ => {
				if(quickAccess.SelectedObject is IFilePath item) {
					ExploreSession.ShowProperties(ctx.GetPathItem(item.path, ExploreSession.GetStaticProps));
				}
			},
			['/'] = _ => {
				var rowObj = quickAccess.SelectedObject;
				var (row, col) = quickAccess.GetObjectPos(rowObj) ?? (0, 0);
				SView.ShowContext(quickAccess, [.. GetSpecificActions(quickAccess, main, row, rowObj)], row + 2, col + 2);
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
		quickAccess.SelectionChanged += (a, e) => {

		};
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
				var c = SView.ShowContext(quickAccess, [.. GetSpecificActions(quickAccess, main, row, rowObj)], y, e.MouseEvent.X);
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
		/*
		pinList.OpenSelectedItem += (a, e) => {
			main.folder.AddTab("Expl", new ExploreSession(main, pinData.list[pinList.SelectedItem]).root, true);
		};
		pinList.KeyDownD(new() {
			['"'] = k => {
				var ind = pinList.SelectedItem;
				if(pinData.list.ElementAtOrDefault(ind) is not { }path) return;
				ExploreSession.Preview($"Preview: {path}", File.ReadAllText(path));
			},
			['?'] = k => {
				var ind = pinList.SelectedItem;
				if(pinData.list.ElementAtOrDefault(ind) is not { } path) return;
				ExploreSession.ShowProperties(ctx.GetPathItem(path, ExploreSession.GetStaticProps));
			},
			['/'] = k => {
				var ind = pinList.SelectedItem;
				if(pinData.list.ElementAtOrDefault(ind) is not { } path) return;
				var c = SView.ShowContext(pinList, [
					new MenuItem("Cancel", null, () => { }),
					.. ExploreSession.GetStaticActions(main, ctx.GetPathItem(path, ExploreSession.GetStaticProps))
					], ind - pinList.TopItem + 1, 2);
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
		*/
		/*

					'"' => () => {
						if(!GetItem(out var p) || p.dir) return;
						Preview($"Preview: {p.path}", File.ReadAllText(p.path));
					}
					,
					'?' => () => {
						if(!GetItem(out var p)) return;
						ShowProperties(p);
					}
					,
					'/' => () => {
						if(!GetItem(out var p, out var ind)) return;
						var c = ShowContext(p, ind - pathList.TopItem + 2, 2);
					}
		*/
		/*
		pinList.MouseEvD(new() {
			[(int)Button3Pressed] = e => {
				var prev = pinList.SelectedItem;
				var i = pinList.TopItem + e.MouseEvent.Y;
				if(i >= pinData.Count) {
					//Show same menu as .
					return;
				}
				pinList.SelectedItem = i;
				var c = SView.ShowContext(quickAccess, [
					new MenuItem("Cancel", null, () => { }),
					.. ExploreSession.GetStaticActions(main, ctx.GetPathItem(pinData.list[i], ExploreSession.GetStaticProps))
					], e.MouseEvent.Y - 1, e.MouseEvent.ScreenPosition.X - 1);
				c.MenuBar.MenuAllClosed += (object? _, EventArgs _) => {
					if(prev == -1) {
						return;
					}
					pinList.SelectedItem = prev;
				};
				e.Handled = true;
			}
		});
		*/
		SView.InitTree(
			[[root, quickAccess, /*pinList, recentList, repoList*/]]
			);
		
	}
	//https://stackoverflow.com/questions/13079569/how-do-i-get-the-path-name-from-a-file-shortcut-getting-exception/13079688#13079688
	public static IEnumerable<MenuItem> GetSpecificActions (TreeView<IFileTree> quickAccess, Main main, int row, IFileTree obj) {
		var pathItem = obj is IFilePath{path:{ }p } ? main.ctx.GetPathItem(p, ExploreSession.GetStaticProps) : null;
		return [
			new MenuItem("Cancel", null, () => { }),
				..(obj switch {
						LibraryRoot lib => GetRootActions(lib),
						LibraryItem item => GetItemActions(item),
						LibraryLeaf node => GetNodeActions(node),

						PinItem pi => GetPinActions(pi),
						_ => GetActionsNone()
					})
			];
		IEnumerable<MenuItem> GetRootActions (LibraryRoot lib) {
			yield return new MenuItem("Delete", null, () => {
				var rem = main.ctx.fx.libraryData[row];
				main.ctx.fx.libraryData.RemoveAt(row);
				quickAccess.Remove(rem);
				quickAccess.SetNeedsDisplay();
			});
			yield return new MenuItem("Rename", null, () => ExploreSession.RequestName($"Rename {lib.name}", name => {
				if(name.Any()) {
					lib.name = name;
					return true;
				}
				return false;
			}));
		}
		IEnumerable<MenuItem> GetItemActions (LibraryItem item) {
			foreach(var it in GetPathActions(item.path)) yield return it;
			yield return new MenuItem("Remove from Library", null, () => {
				var lib = quickAccess.GetParent(item) as LibraryRoot;
				lib.links.Remove(item);
				quickAccess.Remove(item);
			});
		}
		IEnumerable<MenuItem> GetNodeActions (LibraryLeaf node) {
			foreach(var it in GetPathActions(node.path)) yield return it;
			yield return new MenuItem("Ignore", null, () => {

			});
		}
		IEnumerable<MenuItem> GetPinActions (PinItem pi) {
			foreach(var it in GetPathActions(pi.path)) yield return it;
		}
		IEnumerable<MenuItem> GetPathActions (string path) {
			var pathItem = main.ctx.GetPathItem(path, ExploreSession.GetStaticProps);
			foreach(var c in main.ctx.GetCommands(pathItem)) yield return c;
			foreach(var c in ExploreSession.GetStaticActions(main, pathItem)) yield return c;
		}
		IEnumerable<MenuItem> GetActionsNone () {
			yield return new MenuItem("New Library", null, () => {
				ExploreSession.RequestName("New Library", name => {
					var add = new LibraryRoot(name);
					main.ctx.fx.libraryData.Add(add);
					quickAccess.AddObject(add);
					quickAccess.SetNeedsDisplay();
					return true;
				});
			});
		}
	}
}
public record QuickAccessTree : ITreeBuilder<IFileTree> {

	public IEnumerable<IFileTree> GetRoots (Main main) {
		List<LibraryRoot> libraryData = main.ctx.fx.libraryData;
		List<string> pinData = main.ctx.fx.pins;
		return [
			new TreeRoot("Libraries", [.. libraryData]),
			new TreeRoot("Pins", [.. pinData.Select(path => new PinItem(path))]),
			new TreeRoot("Drives", [
				..DriveInfo.GetDrives().Select(d => new LibraryItem(d.Name, true))
			]),
			new TreeRoot("Recent", [
				..main.ctx.fx.lastOpened.OrderBy(p => p.Value).Select(p => new RecentItem(p.Key, p.Value))
			])
		];
	}

	public AspectGetterDelegate<IFileTree> AspectGetter = node => node.name;
	public bool SupportsCanExpand => true;
	public bool CanExpand (IFileTree i) => GetChildren(i).Any();
	public IEnumerable<IFileTree> GetChildren (IFileTree i) => i switch {
		TreeRoot root => root.items,
		LibraryRoot lib => lib.links,

		IFilePath { path: { }path } when Directory.Exists(path) => GetLeaves(path),
		_ => []
	};
	public IEnumerable<LibraryLeaf> GetLeaves(string path) =>
		Directory.GetDirectories(path).Concat(Directory.GetFiles(path)).Select(path => new LibraryLeaf(path));
}
public interface IFileTree {
	string name { get; }
}
public interface  IFilePath {
    string path { get; }
}
public record TreeRoot (string name, IFileTree[] items) : IFileTree { }
public record PinItem(string path) : IFileTree, IFilePath {
	public string name { get; set; } = Path.GetFileName(path);
}

public record RecentItem (string path, DateTime lastAccess) : IFileTree, IFilePath {
	public string name { get; set; } = Path.GetDirectoryName(path) is { } ? Path.GetFileName(path) : path;
}
public record LibraryRoot() : IFileTree {
	public List<LibraryItem> links = new();
	public string name { get; set; } = "";

	public LibraryRoot(string name) : this() { this.name = name; }

}
public record LibraryItem (string path, bool visible) : IFileTree, IFilePath {
	public string name { get; set; } = path is { }p ? (Path.GetDirectoryName(p) is { } ? Path.GetFileName(p) : p) : ".";
	public LibraryItem () : this(null, false) { }
}
public record LibraryLeaf (string path) : IFileTree, IFilePath {
	public string name { get; set; } = Path.GetFileName(path);
}
public record ListMarker<T>(List<T> list, Func<T, int, string> GetString) : IListDataSource where T:class {
	public int Count => list.Count;
	public int Length { get; }
	public HashSet<T> marked = new();
	public bool IsMarked (int item) => list.Count == 0 ? false : marked.Contains(list[item]);
	public void Render (ListView container, ConsoleDriver driver, bool selected, int item, int col, int line, int width, int start = 0) {
		container.Move(col, line);
		T? t = list?[item];
		if(t is null) {
			RenderUstr(driver, "", col, line, width);
		} else {
			RenderUstr(driver, GetString(t, item), col, line, width, start);
		}
		void RenderUstr (ConsoleDriver driver, string ustr, int col, int line, int width, int start = 0) {
			string u = TextFormatter.ClipAndJustify(ustr, width, TextAlignment.Left);
			driver.AddStr(u);
			width -= u.GetColumns();
			while(width-- > 0) {
				driver.AddRune((Rune)' ');
			}
		}
	}
	public void UpdateMarked () {
		marked.IntersectWith(list);
	}

	public void SetMark (int item, bool value) {
		if(list.Count == 0) {
			return;
		}
		((Func<T, bool>)(value ? marked.Add : marked.Remove))(list[item]);
	}
	public IList ToList () => list;
}