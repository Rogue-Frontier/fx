using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace fx;
public class FindSession {
	public View root;
	public TreeView<IFind> tree;
	FindFilter filter = new();
	public TextField rootBar;
	private Ctx ctx;

	private Main main;
	public FindSession (Main main, string path) {
		ctx = main.ctx;
		this.main = main;
		Fx fx = ctx.fx;
		root = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			CanFocus = true,
		};
		int w = 8;
		int y = 0;
		var rootLabel = new Label("Dir") {
			X = 0,
			Y = y,
			Width = w
		};
		rootBar = new TextField(path) {
			X = w,
			Y = y,
			Width = Dim.Fill(24),
		};

		var rootShowButton = new Button("All", false) {
			X = Pos.Right(rootBar),
			Y = y,
			Width = 6
		};
		y++;
		var filterLabel = new Label("File") {
			X = 0,
			Y = y,
			Width = w
		};
		var filterBar = new TextField() {
			X = w,
			Y = y,
			Width = Dim.Fill(24),
		};
		var filterShowButton = new Button("All", false) {
			X = Pos.Right(filterBar),
			Y = y,
			Width = 6
		};
		y++;
		var findLabel = new Label("Text") {
			X = 0,
			Y = y,
			Width = w
		};
		var findBar = new TextField() {
			X = w,
			Y = y,
			Width = Dim.Fill(24),
		};
		var findAllButton = new Button("All", false) {
			X = Pos.Right(findBar),
			Y = y,
			Width = 6
		};
		var findPrevButton = new Button("<-", false) {
			X = Pos.Right(findAllButton),
			Y = y,
			Width = 6
		};
		var findNextButton = new Button("->", false) {
			X = Pos.Right(findPrevButton),
			Y = y,
			Width = 6
		};
		y++;
		var replaceLabel = new Label("Replace") {
			X = 0,
			Y = y,
			Width = w
		};
		var replaceBar = new TextField() {
			X = w,
			Y = y,
			Width = Dim.Fill(24),
		};
		var replaceAllButton = new Button("All", false) {
			X = Pos.Right(findBar),
			Y = y,
			Width = 6
		};
		var replacePrevButton = new Button("<-", false) {
			X = Pos.Right(findAllButton),
			Y = y,
			Width = 6
		};
		var replaceNextButton = new Button("->", false) {
			X = Pos.Right(findPrevButton),
			Y = y,
			Width = 6
		};
		y++;
		y++;
		tree = new TreeView<IFind>(new TreeFinder(filter)) {
			X = 0,
			Y = y,
			Width = Dim.Fill(0),
			Height = Dim.Fill(0),
			AspectGetter = f => f switch {
				FindDir d => $"{d.name}/",
				FindFile ff => ff.name,
				FindLine l => $"{l.row,3}|{l.line}"
			}
		};
		tree.ObjectActivated += e => {
			if(e.ActivatedObject is FindLine l) {
				//main.EditFile(l);
			}
		};
		root.AddKey(new() {
			[Key.DeleteChar] = _=> {
				int i = 0;
			},
			[Key.Esc] = _=> {
				main.folder.RemoveTab(root);
			}
		});
		root.AddKey(new() {
			[Key.Esc] = _=> {
				main.folder.RemoveTab(root);
			}
		}, new() {
			['>'] = _=> {
				main.folder.SwitchTab();
			}
		});
		rootBar.AddKey(new() {
			[Key.Enter] = _ => FindDirs()
		});
		filterBar.AddKey(new() {
			[Key.Enter] = _ => FindFiles()
		});
		findBar.AddKey(new() {
			[Key.Enter] = _ => FindLines()
		});
		rootShowButton.Clicked += FindDirs;
		filterShowButton.Clicked += FindFiles;
		tree.AddMouse(new() {
			[MouseFlags.Button3Clicked] = e => {
				var prev = tree.SelectedObject;
				var row = e.MouseEvent.Y;
				if(tree.GetObjectOnRow(row) is not { } o)
					return;
				tree.SelectedObject = tree.GetObjectOnRow(row);
				var c = ShowContext(tree.SelectedObject, row);
				/*
				c.MenuItems.Children.ToList().ForEach(it => it.Action += () => {
					int i = 0;
				});
				*/
				c.MenuBar.MenuAllClosed += () => {
					tree.SelectedObject = prev;
				};
				tree.SetNeedsDisplay();
			},
		});
		tree.AddKey(new() {
			[Key.CursorRight] = _ => {
				if(!tree.IsExpanded(tree.SelectedObject)) {
					if(!tree.CanExpand(tree.SelectedObject)) {
						return;
					}
					tree.Expand(tree.SelectedObject);
					tree.SetNeedsDisplay();
					return;
				}
				if(tree.GetChildren(tree.SelectedObject).FirstOrDefault() is { } c) {
					tree.SelectedObject = c;
				}
				tree.SetNeedsDisplay();
			}
		}, new() {
			['/'] = _=> {
				
			}
		});
		void FindFiles () {
			filter = filter with {
				filePattern = new(filterBar.Text.ToString()),
				linePattern = null
			};
			SetFilter(filter);
		}
		void FindLines () {
			filter = filter with {
				filePattern = new(filterBar.Text.ToString()),
				linePattern = new(findBar.Text.ToString())
			};
			SetFilter(filter);
		}
		findAllButton.Clicked += FindLines;
		//Print button (no replace)
		FindFiles();
		SView.InitTree([root,
			rootLabel, rootBar, rootShowButton,
			filterLabel, filterBar, filterShowButton,
			findLabel, findBar, findAllButton, findPrevButton, findNextButton,
			replaceLabel, replaceBar, replaceAllButton, replacePrevButton, replaceNextButton,
			tree
			]);
	}
	ContextMenu ShowContext (IFind item, int row) {
		var (x, y) = tree.GetCurrentLoc();
		var c = new ContextMenu(x, y+row, new MenuBarItem(null, [
			.. GetActions(item)]));
		c.Show();
		c.ForceMinimumPosToZero = true;
		return c;
	}
	IEnumerable<MenuItem> GetActions (IFind item) => item switch {
		FindDir d => GetActions(d),
		FindFile f => GetActions(f),
		FindLine l => GetActions(l),
		_ => throw new Exception()
	};
	IEnumerable<MenuItem> GetActions (FindDir d) {
		yield return new MenuItem("Explore Dir", "", () => {
			main.folder.AddTab($"Expl({d.name})", new ExploreSession(main, d.path).root, true);
		});
	}
	IEnumerable<MenuItem> GetActions (FindFile f) {
		yield return new MenuItem("Edit File", "", () => {
			main.folder.AddTab($"Edit {f.name}", new EditSession(f.path).root, true);
		});
		yield break;
	}
	IEnumerable<MenuItem> GetActions(FindLine l) {
		yield return new MenuItem("Edit Line", "", () => {
			main.folder.AddTab($"Edit {l.path}", new EditSession(l.path, l.row, l.col).root, true);
		});
		yield break;
	}
	public void FindDirs () {
		filter = filter with {
			filePattern = null,
			linePattern = null,
		};
		SetFilter(filter);
	}
	void SetFilter (FindFilter filter) {
		tree.ClearObjects();
		tree.TreeBuilder = new TreeFinder(filter);
		var root = GetRoot();
		var i = IFind.New(root);
		if(i is FindDir d) {
			d.name = d.path;
		} else if(i is FindFile f) {
			f.name = f.path;
		}
		tree.AddObject(i);
		tree.ExpandAll();
	}
	string GetRoot () =>
		rootBar.Text.ToString().Replace(Ctx.USER_PROFILE_MASK, ctx.USER_PROFILE);
}
/// <summary>
/// 
/// </summary>
/// <param name="filePattern">If <c>null</c>, then do not show files</param>
/// <param name="linePattern">If <c>null</c>, then do not show lines </param>
/// <param name="replace">Format</param>
public record FindFilter (Regex filePattern, Regex linePattern, string replace) {
	public FindFilter () : this(null, null, null) { }
	public bool Accept (FindFile f) => filePattern?.Match(f.name).Success ?? false;
	public bool Accept (string line) => linePattern?.Match(line).Success ?? false;
	public void Replace (string line) => linePattern.Replace(line, replace);
}
public record TreeFinder (FindFilter filter) : ITreeBuilder<IFind> {
	public bool SupportsCanExpand => true;
	public bool CanExpand (IFind i) => i switch {
		FindDir d =>
			filter.filePattern != null || filter.linePattern != null ?
				GetLeaves(d).Any() :
				GetChildren(d).Any(),
		FindFile f => GetChildren(f).Any(),
		FindLine => false
	};
	//private bool CanShow (IFind f) => CanShow(f as dynamic);
	private bool CanShow (FindDir d) => true;
	private bool CanShow (FindFile f) => filter.Accept(f) && (filter.linePattern == null || GetChildren(f).Any());
	private bool CanShow (FindLine d) => filter.linePattern != null;
	public IEnumerable<IFind> GetChildren (IFind f) => GetChildren(f as dynamic);
	private IEnumerable<IFind> GetChildren (FindDir d) => [
		.. Directory.GetDirectories(d.path).Select(FindDir.New).Where(CanShow),
		.. Directory.GetFiles(d.path).Select(FindFile.New).Where(CanShow),
	];
	private IEnumerable<IFind> GetChildren(FindFile f) {
		IEnumerable<string> lines = [];
		try {
			lines = File.ReadLines(f.path);
		} catch(IOException e) {}
		if(filter.linePattern is { }lp) {
			foreach(var (row, line) in lines.Index()) {
				if(lp.Match(line) is { Success: true } match) {
					yield return new FindLine(f.path, row, match.Index, line, match.Value);
				}
			}
		}
	}
	private IEnumerable<IFind> GetChildren(FindLine l) => Enumerable.Empty<IFind>();
	private IEnumerable<IFind> GetAll (FindDir d) => [
		.. Directory.GetDirectories(d.path).Select(FindDir.New),
		.. Directory.GetFiles(d.path).Select(FindFile.New),
	];
	private IEnumerable<IFind> GetLeaves (IFind i) => i switch {
		FindDir d => GetLeaves(d),
		FindFile f => GetLeaves(f),
		_ => Enumerable.Empty<IFind>()
	};
	private IEnumerable<IFind> GetLeaves (FindDir dir) {
		
		var subDir = Directory.GetDirectories(dir.path);
		var subDirLeaves = subDir.SelectMany(d => GetLeaves(new FindDir(d)));
		//Leaf type: Dir
		if(filter.filePattern == null) {
			if(subDir.Any()) {
				return subDirLeaves;
			}
			return [dir];
		}
		var subFile = Directory.GetFiles(dir.path);
		var subFileLeaves = subFile.SelectMany(f => GetLeaves(new FindFile(f)));
		return [.. subDirLeaves, .. subFileLeaves];
	}
	private IEnumerable<IFind> GetLeaves(FindFile f) {
		if(filter.linePattern is not { }lp) {
			return [f];
		}
		return File.ReadAllLines(f.path)
			.Select((line, index) => (line, index, match:lp.Match(line)))
			.Where(pair => pair.match.Success)
			.Select(pair => new FindLine(f.path, pair.index, pair.match.Index, pair.line, pair.match.Value));
	}
}
public interface IFind {
	public static IFind New (string path) =>
		Directory.Exists(path) ?
			new FindDir(path) :
			new FindFile(path);
}
public record FindDir (string path) : IFind {
	public string name = Path.GetFileName(path);
	public static FindDir New (string path) => new(path);
}
public record FindFile (string path) : IFind {
	public string name = Path.GetFileName(path);
	public static FindFile New (string path) => new(path);
}
public record FindLine (string path, int row, int col, string line, string capture) : IFind {}