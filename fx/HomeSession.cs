using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace fx;
public class HomeSession {
	public View root;
	private Ctx ctx;
	public HomeSession(Main main) {
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
		var libraryPane = new FrameView() {
			Title = "Libraries",
			X = 0,
			Y = 0,
			Width = w,
			Height = Dim.Fill(),
		};
		var libraryList = new ListView() {
			X = 0,
			Y = 0,
			Width = FILL,
			Height = FILL,
			Source = new ListMarker<Library>(libraryData, (lib, i) => lib.name)
		};
		//Pinned folders, pinned files
		var pins = new FrameView() {
			Title = "Pinned",
			X = Pos.Right(libraryPane),
			Y = 0,
			Width = w,
			Height = FILL,
		};
		var pinsList = new ListView() {
			X = 0,
			Y = 0,
			Width = FILL,
			Height = FILL,
			Source = new ListWrapper(main.ctx.fx.pins)
		};
		var recent = new FrameView() {
			Title = "Recent",
			X = Pos.Right(pins),
			Y = 0,
			Width = w,
			Height = FILL
		};
		var recentList = new ListView() {
			X = 0,
			Y = 0,
			Width = FILL,
			Height = FILL,
			Source=  new ListWrapper(new List<string>())
		};
		var repo = new FrameView() {
			Title = "Repositories",
			X = Pos.Right(recent),
			Y = 0,
			Width = w,
			Height = Dim.Fill()
		};
		var repoList = new ListView() {
			X = 0,
			Y = 0,
			Width = FILL,
			Height = FILL,
			Source=  new ListWrapper(new List<GitItem>())
		};
		libraryList.MouseEvD(new() {
			[MouseFlags.Button3Pressed] = e => {
				var row = e.MouseEvent.Y;
				var index = libraryList.TopItem + row;
				var prev = libraryList.SelectedItem;
				if(index < libraryData.Count) {
					IEnumerable<MenuItem> GetActions () {
						yield return new MenuItem("Cancel", null, () => { });
						yield return new MenuItem("Delete", null, () => {
							libraryData.RemoveAt(index);
							libraryList.SetNeedsDisplay();
						});
						//Add items
					}
					var c = SView.ShowContext(libraryList, [.. GetActions()], row, e.MouseEvent.X);
					c.MenuBar.MenuAllClosed += (a, e) => {
						if(libraryData.Count == 0) {
							return;
						}
						libraryList.SelectedItem = Math.Clamp(prev, 0, libraryData.Count - 1);
					};
				} else {
					IEnumerable<MenuItem> GetActions () {
						yield return new MenuItem("New Library", null, () => {
							ExploreSession.RequestName("New Library", name => {
								libraryData.Add(new Library(name));
								libraryList.SetNeedsDisplay();
								return true;
							});
						});
					}
					var c = SView.ShowContext(libraryList, [.. GetActions()], row, e.MouseEvent.X);
				}
				e.Handled = true;
			}
		});
		SView.InitTree(
			[root, libraryPane, pins, recent, repo],
			[libraryPane, libraryList],
			[pins, pinsList],
			[recent, recentList],
			[repo, repoList]
			);
		//https://stackoverflow.com/questions/13079569/how-do-i-get-the-path-name-from-a-file-shortcut-getting-exception/13079688#13079688
	}
}
public record Library(string name) {
	public List<LibraryLink> links = new();
	public Library () : this("") { }
}
public record LibraryLink (string path, bool visible, bool expand) {
	public LibraryLink () : this(null, false, false) { }
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