using LibGit2Sharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Terminal.Gui;

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
		};
		int w = 8;
		int y = 0;
		var rootLabel = new Label() {
			AutoSize = false,
			Title= "Dir",
			X = 0,
			Y = y,
			Width = w,
			Height = 1
		};
		rootBar = new TextField() {
			AutoSize = false,
			X = w,
			Y = y,
			Width = Dim.Fill(24),
			Height = 1,
			Text = path
		};

		var rootShowButton = new Button() {
			AutoSize = false,
			X = Pos.Right(rootBar),
			Y = y,
			Width = 6,
			Height = 1,
			Text = "Find"
		};
		y++;
		var filterLabel = new Label() {
			AutoSize = false,
			X = 0,
			Y = y,
			Width = w,
			Height = 1,
			Text = "File"
		};
		var filterBar = new TextField() {
			AutoSize = false,
			X = w,
			Y = y,
			Width = Dim.Fill(24),
			Height = 1,
		};
		var filterShowButton = new Button() {
			AutoSize = false,
			X = Pos.Right(filterBar),
			Y = y,
			Width = 6,
			Height = 1,
			Text = "Find"
		};
		y++;
		var findLabel = new Label() {
			AutoSize = false,
			X = 0,
			Y = y,
			Width = w,
			Height = 1,
			Text = "Line"
		};
		var findBar = new TextField() {
			AutoSize = false,
			X = w,
			Y = y,
			Width = Dim.Fill(24),
			Height = 1,
		};
		var findAllButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findBar),
			Y = y,
			Width = 6,
			Height = 1,
			Text = "Find"
		};
		var findPrevButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findAllButton),
			Y = y,
			Width = 6,
			Height = 1,
			Text = "<-"
		};
		var findNextButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findPrevButton),
			Y = y,
			Width = 6,
			Height = 1,
			Text = "->"
		};
		y++;
		var replaceLabel = new Label() {
			AutoSize = false,
			X = 0,
			Y = y,
			Width = w,
			Height = 1,
			Text = "Replace"
		};
		var replaceBar = new TextField() {
			AutoSize = false,
			X = w,
			Y = y,
			Width = Dim.Fill(24),
			Height = 1
		};
		var replaceAllButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findBar),
			Y = y,
			Width = 6,
			Height=1,
			Text = "Replace"
		};
		var replacePrevButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findAllButton),
			Y = y,
			Width = 6,
			Height=1,
			Text = "<-"
		};
		var replaceNextButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findPrevButton),
			Y = y,
			Width = 6,
			Height=1,
			Text = "->"
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
		tree.ObjectActivated += (a, e) => {
			if(e.ActivatedObject is FindLine l) {
				//main.EditFile(l);
			}
		};
		root.KeyDownD(new() {
			[KeyCode.Delete] = _ => {
				int i = 0;
			},
			[KeyCode.Esc] = _ => {
				main.folder.RemoveTab(root);
			}
		});
		root.KeyDownD(new() {
			[KeyCode.Esc] = _=> {
				main.folder.RemoveTab(root);
			}
		}, new() {
			['>'] = _=> {
				main.folder.SwitchTab();
			}
		});
		rootBar.KeyDownD(new() {
			[KeyCode.Enter] = _ => FindDirs()
		});
		filterBar.KeyDownD(new() {
			[KeyCode.Enter] = _ => FindFiles()
		});
		findBar.KeyDownD(new() {
			[KeyCode.Enter] = _ => FindLines()
		});

		rootShowButton.MouseEvD(new() {
			[MouseFlags.Button1Pressed] = _ => FindDirs()
		});
		filterShowButton.MouseEvD(new() {
			[MouseFlags.Button1Pressed] = _ => FindFiles()
		});
		tree.MouseClickD(new() {
			[MouseFlags.Button3Clicked] = e => {
				var prev = tree.SelectedObject;
				var row = e.MouseEvent.Y;
				if(tree.GetObjectOnRow(row) is not { } o)
					return;
				tree.SelectedObject = tree.GetObjectOnRow(row);
				var c = ShowContext(tree.SelectedObject, row - 1, e.MouseEvent.X);
				/*
				c.MenuItems.Children.ToList().ForEach(it => it.Action += () => {
					int i = 0;
				});
				*/
				c.MenuBar.MenuAllClosed += (object? a, EventArgs e) => {
					tree.SelectedObject = prev;
				};
				tree.SetNeedsDisplay();
			},
		});
		tree.KeyDownD(new() {
			[KeyCode.CursorRight] = _ => {
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
				//TODO SHOW CONTEXT
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
		findAllButton.MouseEvD(new() {
			[MouseFlags.Button1Pressed] = _ => FindLines()
		});
		SView.InitTree([root,
			rootLabel, rootBar, rootShowButton,
			filterLabel, filterBar, filterShowButton,
			findLabel, findBar, findAllButton, findPrevButton, findNextButton,
			replaceLabel, replaceBar, replaceAllButton, replacePrevButton, replaceNextButton,
			tree
			]);
	}
	ContextMenu ShowContext (IFind item, int row, int col = 0) {
		var (x, y) = tree.GetCurrentLoc();
		var c = new ContextMenu() {
			MenuItems = new(null, [
			.. GetActions(item)]),
			Position = new(x + col, y + row)
		};
		c.Show();
		c.ForceMinimumPosToZero = true;
		return c;
	}
	IEnumerable<MenuItem> GetActions (IFind item) => [..(item switch {
			FindDir d => GetActions(d),
			FindFile f => GetActions(f),
			FindLine l => GetActions(l),
			_ => throw new Exception()
		}),
		..ExploreSession.GetGeneralActions(main, ctx.GetCachedPathItem(item.path, ExploreSession.GetStaticProps))
	];
	IEnumerable<MenuItem> GetActions (FindDir d) {
		yield return new MenuItem("Explore Dir", "", () => {
			main.folder.AddTab($"Expl {d.name}", new ExploreSession(main, d.path).root, true);
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
		var i = IFind.New(GetRoot());
		if(i is FindDir d) {
			d.name = d.path;
		} else if(i is FindFile f) {
			f.name = f.path;
		}
		tree.AddObject(i);

		/*
		int row = 0;
		while(tree.GetObjectOnRow(row) is { } o) {
			tree.Expand(o);
			row++;
		}
		*/
		
		tree.SetNeedsDisplay();
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


	ConcurrentDictionary<IFind, bool> HasLeavesDict = new();
	ConcurrentDictionary<IFind, List<IFind>> ChildrenDict = new();

	public bool SupportsCanExpand => true;
	public bool CanExpand (IFind i) => HasLeavesDict.GetOrAdd(i, i => i switch {
		FindDir d =>
			GetLeavesOrSelf(d).Except([d]).Any(),
		FindFile f =>
			GetLeavesOrSelf(f).Except([f]).Any(),
		FindLine => false,
	});
	public IEnumerable<IFind> GetChildren (IFind i) =>
		ChildrenDict.TryGetValue(i, out var ch) ?
			ch :
			(i switch {
				FindDir d => GetChildren(d),
				FindFile f => GetChildren(f),
				FindLine l => GetChildren(l),
			});
	private IEnumerable<IFind> GetChildren (FindDir d) => ChildrenDict.GetOrAdd(d, _ => [
		.. Directory.GetDirectories(d.path).Select(FindDir.New).Where(HasLeavesOrIsLeaf),
		.. Directory.GetFiles(d.path).Select(FindFile.New).Where(HasLeavesOrIsLeaf),
	]);
	private bool HasLeavesOrIsLeaf (FindFile f) {

		var hasKey = HasLeavesDict.TryGetValue(f, out var b);
		if(b) 
			return true;

		var leaves = GetLeavesOrSelf(f);
		b = leaves.Any();
		if(!b && !hasKey)
			HasLeavesDict[f] = false;
		return b;
	}
	private bool HasLeavesOrIsLeaf (FindDir d) {
		var hasKey = HasLeavesDict.TryGetValue(d, out var b);
		if(b)
			return true;

		var leaves = GetLeavesOrSelf(d);
		b = leaves.Any();
		if(!b && !hasKey)
			HasLeavesDict[d] = false;
		return b;
	}
	private IEnumerable<IFind> GetChildren (FindFile f) => ChildrenDict.GetOrAdd(f, _ => {
		if(filter.linePattern is not { } lp) {
			return [];
		}
		try {
			return File.ReadLines(f.path)
				.Select((line, row) => (row, line, match: lp.Match(line)))
				.Where(p => p.match.Success)
				.Select(p => new FindLine(f.path, p.row, p.match.Index, p.line, p.match.Value)).Cast<IFind>().ToList();
		} catch(IOException e) {

		} finally {

		}
		return [];
	});
	private IEnumerable<IFind> GetChildren(FindLine l) => ChildrenDict[l] = Enumerable.Empty<IFind>().ToList();
	
	
	
	private IEnumerable<IFind> GetLeaves (IFind i) => i switch {
		FindDir d => GetLeavesOrSelf(d),
		FindFile f => GetLeavesOrSelf(f),
		_ => Enumerable.Empty<IFind>()
	};
	private IEnumerable<IFind> GetLeavesOrSelf (FindDir dir) {

		var subDir = Directory.GetDirectories(dir.path);
		var subDirLeaves = subDir.SelectMany(d => GetLeavesOrSelf(new FindDir(d))).ToList();
		//Leaf type: Dir
		if(filter.filePattern is not { }fp) {
			if(subDir.Any()) {
				return subDirLeaves;
			}
			return [dir];
		}
		var subFile = Directory.GetFiles(dir.path).Where(f => fp.Match(f).Success);
		var subFileLeaves = subFile
			.SelectMany(f => GetLeavesOrSelf(new FindFile(f))).ToList();
		return [.. subDirLeaves, .. subFileLeaves];
	}
	private IEnumerable<IFind> GetLeavesOrSelf(FindFile f) {
		if(filter.linePattern is not { }lp) {
			return [f];
		}
		try {
			return File.ReadAllLines(f.path)
				.Select((line, index) => (line, index, match: lp.Match(line)))
				.Where(pair => pair.match.Success)
				.Select(pair => new FindLine(f.path, pair.index, pair.match.Index, pair.line, pair.match.Value));
		} catch(Exception e) {
			return [];
		}
	}
}
public interface IFind {

	string path { get; }
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