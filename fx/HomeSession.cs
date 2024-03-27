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
namespace fx;
public class HomeSession {
	public View root;
	private Ctx ctx;
	public HomeSession(Main main) {
		ctx = main.ctx;
		List<Library> libraryData = main.ctx.fx.libraryData;
		root = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
		};
		var FILL = Dim.Fill();
		var w = Dim.Percent(25);
		//Libraries
		var libraryTree = new TreeView<ILibraryTree>() {
			Title = "Libraries",
			BorderStyle = LineStyle.Single,
			X = 0,
			Y = 0,
			Width = w,
			Height = FILL,
			TreeBuilder = new LibraryFinder(),
			AspectGetter = node => node switch {
				Library lib => lib.name,
				LibraryItem item => item.name,
				LibraryLeaf leaf => leaf.name,
				_ => "Node"
			}
		};
		//Pinned folders, pinned files
		var pinsList = new ListView() {
			Title = "Pinned",
			BorderStyle = LineStyle.Single,
			X = Pos.Right(libraryTree),
			Y = 0,
			Width = w,
			Height = FILL,
			Source = new ListWrapper(main.ctx.fx.pins)
		};
		var recentList = new ListView() {
			Title = "Recent",
			BorderStyle = LineStyle.Single,
			X = Pos.Right(pinsList),
			Y =0,
			Width = w,
			Height = FILL,
			Source=  new ListWrapper(new List<string>())
		};
		var repoList = new ListView() {
			Title = "Repositories",
			BorderStyle = LineStyle.Single,
			X = Pos.Right(recentList),
			Y = 0,
			Width = w,
			Height = FILL,
			Source=  new ListWrapper(new List<GitItem>())
		};
		libraryTree.AddObjects(libraryData);

		libraryTree.KeyDownD(value: new() {
			['"'] = _ => {
				if(libraryTree.SelectedObject is ILibraryPath item && File.Exists(item.path)) {
					ExploreSession.Preview($"Preview: {item.path}", File.ReadAllText(item.path));
				}
			},
			['?'] = _ => {
				if(libraryTree.SelectedObject is ILibraryPath item) {
					ExploreSession.ShowProperties(ctx.GetPathItem(item.path, ExploreSession.GetStaticProps));
				}
			},
			['/'] = _ => {
				var rowObj = libraryTree.SelectedObject;
				var (row, col) = libraryTree.GetObjectPos(rowObj) ?? (0, 0);
				SView.ShowContext(libraryTree, [.. GetSpecificActions(row, rowObj)], row + 2, col + 2);
			}
		});
		libraryTree.MouseEvD(new() {
			[(int)Button3Pressed] = e => {
				var prevObj = libraryTree.SelectedObject;
				var y = e.MouseEvent.Y;
				var row = y + libraryTree.ScrollOffsetVertical;
				var rowObj = libraryTree.GetObjectOnRow(row);
				var c = SView.ShowContext(libraryTree, [.. GetSpecificActions(row, rowObj)], y, e.MouseEvent.X);
				if(row < libraryData.Count) {
					c.MenuBar.MenuAllClosed += (a, e) => {
						if(libraryData.Count == 0) {
							return;
						}
						if(libraryTree.GetParent(prevObj) != null) {
							libraryTree.SelectedObject = prevObj;
						} else {
							//TODO: Find suitable dest
						}
					};
				}
				e.Handled = true;
			}
		});
		SView.InitTree(
			[[root, libraryTree, pinsList, recentList, repoList]]
			);
		//https://stackoverflow.com/questions/13079569/how-do-i-get-the-path-name-from-a-file-shortcut-getting-exception/13079688#13079688
		IEnumerable<MenuItem> GetSpecificActions (int row, ILibraryTree obj) {
			var path = ((obj as LibraryItem)?.path ?? (obj as LibraryLeaf)?.path);
			var pathItem = path == null ? null : ctx.GetPathItem(path, ExploreSession.GetStaticProps);
			return [
				new MenuItem("Cancel", null, () => { }),
				..(obj switch {
						Library lib => GetActions(lib),
						LibraryItem item => GetItemActions(item),
						LibraryLeaf node => GetNodeActions(node),
						_ => GetActionsNone()
					})
				];
			IEnumerable<MenuItem> GetActions (Library lib) {
				yield return new MenuItem("Delete", null, () => {
					var rem = libraryData[row];
					libraryData.RemoveAt(row);
					libraryTree.Remove(rem);
					libraryTree.SetNeedsDisplay();
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
					var lib = libraryTree.GetParent(item) as Library;
					lib.links.Remove(item);
					libraryTree.Remove(item);
				});
			}
			IEnumerable<MenuItem> GetNodeActions (LibraryLeaf node) {
				foreach(var it in GetPathActions(node.path)) yield return it;
				yield return new MenuItem("Hide", null, () => {

				});
			}
			IEnumerable<MenuItem> GetPathActions(string path) {
				if(Directory.Exists(path)) {
					yield return new MenuItem("Explore", null, () => {
						main.folder.AddTab("Expl", new ExploreSession(main, path).root, true);
					});
				}
				yield return new MenuItem("Explore Location", null, () => {
					main.folder.AddTab("Expl", new ExploreSession(main, Path.GetDirectoryName(path)).root, true);
				});


				yield return new MenuItem("Open in Terminal", null, () => {
					main.folder.AddTab($"Term {path}", new TermSession(main, path).root, true);
				});

				var pathItem = ctx.GetPathItem(path, ExploreSession.GetStaticProps);
				foreach(var c in ctx.GetCommands(pathItem)) yield return c;
				foreach(var c in ExploreSession.GetStaticActions(main, pathItem)) yield return c;
			}
			IEnumerable<MenuItem> GetActionsNone () {
				yield return new MenuItem("New Library", null, () => {
					ExploreSession.RequestName("New Library", name => {
						var add = new Library(name);
						libraryData.Add(add);
						libraryTree.AddObject(add);
						libraryTree.SetNeedsDisplay();
						return true;
					});
				});
			}
		}
	}
}

public record LibraryFinder : ITreeBuilder<ILibraryTree> {
	public bool SupportsCanExpand => true;
	public bool CanExpand (ILibraryTree i) => GetChildren(i).Any();
	public IEnumerable<ILibraryTree> GetChildren (ILibraryTree i) => i switch {
		Library lib => lib.links,
		LibraryItem item when Directory.Exists(item.path) => GetLeaves(item.path),
		LibraryLeaf leaf when Directory.Exists(leaf.path) => GetLeaves(leaf.path),
		_ => []
	};
	public IEnumerable<LibraryLeaf> GetLeaves(string path) =>
		Directory.GetDirectories(path).Concat(Directory.GetFiles(path)).Select(path => new LibraryLeaf(path));
}
public interface ILibraryTree { }
public interface  ILibraryPath {
    string path { get; }
}
public record Library() : ILibraryTree {
	public string name;
	public List<LibraryItem> links = new();
	public Library (string name) : this() { this.name = name; }
}
public record LibraryItem (string path, bool visible) : ILibraryTree, ILibraryPath {
	public string name = Path.GetFileName(path);
	public LibraryItem () : this(null, false) { }
}
public record LibraryLeaf (string path) : ILibraryTree, ILibraryPath {
	public string name = Path.GetFileName(path);
}
public record ListMarker<T>(List<T> list, Func<T, int, string> GetString) : IListDataSource where T:class {
	public int Count => list.Count;
	public int Length { get; }
	HashSet<T> marked = new();
	public bool IsMarked (int item) => marked.Contains(list[item]);
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

	public void SetMark (int item, bool value) =>
		((Func<T, bool>)(value ? marked.Add : marked.Remove))(list[item]);
	public IList ToList () => list;
}