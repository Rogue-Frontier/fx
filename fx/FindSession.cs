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

using static Terminal.Gui.MouseFlags;
using static Terminal.Gui.KeyCode;
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
			Text = "[Find]",
			NoDecorations = true,
			NoPadding = true
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
			Text = "[Find]",
			NoDecorations = true,
			NoPadding = true
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
			Text = "[Find]",
			NoDecorations = true,
			NoPadding = true
		};
		var findPrevButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findAllButton),
			Y = y,
			Width = 6,
			Height = 1,
			Text = "[<-]",
			NoDecorations = true,
			NoPadding = true
		};
		var findNextButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findPrevButton),
			Y = y,
			Width = 6,
			Height = 1,
			Text = "[->]",
			NoDecorations = true,
			NoPadding = true
		};
		y++;
		var replaceLabel = new Label() {
			AutoSize = false,
			X = 0,
			Y = y,
			Width = w,
			Height = 1,
			Text = "Replace",
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
			Text = "[Here]",
			NoDecorations = true,
			NoPadding = true
		};
		var replacePrevButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findAllButton),
			Y = y,
			Width = 6,
			Height=1,
			Text = "[<-]",
			NoDecorations = true,
			NoPadding = true
		};
		var replaceNextButton = new Button() {
			AutoSize = false,
			X = Pos.Right(findPrevButton),
			Y = y,
			Width = 6,
			Height=1,
			Text = "[->]",
			NoDecorations = true,
			NoPadding = true
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
				FindLine l => $"|{l.row,4}|{l.line.Replace("\r",null).Replace("\t", "    ")}"
			}
		};
		tree.ObjectActivated += (a, e) => {
			if(e.ActivatedObject is FindLine l) {
				//main.EditFile(l);
			}
		};
		root.KeyDownD(new() {
			[(int)Delete] = _ => {
				int i = 0;
			},
			[(int)Esc] = _ => {
				main.folder.RemoveTab(root);
			}
		});
		root.KeyDownD(new() {
			[(int)Esc] = _=> {
				main.folder.RemoveTab(root);
			},
			['>'] = _ => {
				main.folder.SwitchTab();
			}
		});
		rootBar.KeyDownD(new() {
			[(int)Enter] = _ => FindDirs()
		});
		filterBar.KeyDownD(new() {
			[(int)Enter] = _ => FindFiles()
		});
		findBar.KeyDownD(new() {
			[(int)Enter] = _ => FindLines()
		});

		rootShowButton.MouseEvD(new() {
			[(int)Button1Pressed] = _ => FindDirs()
		});
		filterShowButton.MouseEvD(new() {
			[(int)Button1Pressed] = _ => FindFiles()
		});
		tree.MouseClickD(new() {
			[Button3Clicked] = e => {
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
			[(int)CursorRight] = _ => {
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
			},
			['/'] = _ => {
				var (row, col) = tree.GetObjectPos(tree.SelectedObject) ?? (0, 0);
				var c = ShowContext(tree.SelectedObject, row+1, col+1);
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
			[(int)Button1Pressed] = _ => FindLines()
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
	IEnumerable<MenuItem> GetActions (IFind item) => [
		new MenuItem("Cancel", null, () => { }),

		..(item switch {
			FindDir d => GetActions(d),
			FindFile f => GetActions(f),
			FindLine l => GetActions(l),
			_ => throw new Exception()
		}),
		
		.. item is not FindLine ?
			ExploreSession.GetStaticActions(main, ctx.GetCachedPathItem(item.path, ExploreSession.GetStaticProps)) :
			[]
	];
	IEnumerable<MenuItem> GetActions (FindDir d) {
		yield return new MenuItem("Explore", "", () => {
			main.folder.AddTab($"Expl {d.name}", new ExploreSession(main, d.path).root, true);
		});
	}
	IEnumerable<MenuItem> GetActions (FindFile f) {
		/*
		yield return new MenuItem("Edit", "", () => {
			main.folder.AddTab($"Edit {f.name}", new EditSession(f.path).root, true);
		});
		*/
		yield break;
	}

	private void RequestReplace(string title, string original, Action<string> accept) {
		var confirm = new Button() { Title = "Confirm", };
		var cancel = new Button() { Title = "Cancel", };
		var d = new Dialog() {
			Title = title,
			Buttons = [confirm, cancel],
			Width = 96,
			Height = 6,
		};
		var input = new TextView() {
			X = 1,
			Y = 1,
			Width = Dim.Fill(2),
			Height = 1,
			Multiline = false,
			PreserveTrailingSpaces = true,
			TabWidth = 4,
			AllowsTab = true,

			Text = original,
		};
		input.KeyDownD(new() {
			[(int)Enter] = _ => Confirm()
		});
		void Confirm() {
			accept(input.Text.ToString());
			d.RequestStop();
		}
		confirm.MouseClick += (a, e) => Confirm();
		cancel.MouseClick += (a, e) => d.RequestStop();
		d.Add(input);
		input.SetFocus();
		Application.Run(d);
	}

	IEnumerable<MenuItem> GetActions(FindLine l) {
		yield return new MenuItem("Replace Line", null, () =>
			RequestReplace($"Edit {l.path}", l.line, result => {
				var lines = File.ReadAllLines(l.path);
				lines[l.row] = result;
				File.WriteAllLines(l.path, lines);
			})
		);

		yield return new MenuItem("Replace Match", null, () =>
			RequestReplace($"Edit {l.path}", l.capture, result => {
				var lines = File.ReadAllLines(l.path);
				ref var line = ref lines[l.row];
				line = line[..l.col] + result + line[(l.col + l.capture.Length)..];
				File.WriteAllLines(l.path, lines);
			})
		);

		yield return new MenuItem("Edit", "", () => {
			main.folder.AddTab($"Edit {l.path}", new EditSession(l.path, l.row, l.col).root, true);
		});

		yield return new MenuItem("Copy Group", "", () => { });
		yield return new MenuItem("Copy Line", "", () => {});

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


public enum LeafType {
	Dir, File, Line
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
	public LeafType LeafType =>
		linePattern == null && filePattern == null ?
			LeafType.Dir :
		linePattern == null ?
			LeafType.File :
		LeafType.Line;
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
		if(filter.LeafType != LeafType.Line) {
			return [];
		}
		try {
			return File.ReadLines(f.path)
				.Select((line, row) => (row, line, match: filter.linePattern.Match(line)))
				.Where(p => p.match.Success)
				.Select(p => new FindLine(f.path, p.row, p.match.Index, p.line, p.match.Value)).Cast<IFind>().ToList();
		} catch(IOException e) {

		} finally {

		}
		return [];
	});
	private IEnumerable<IFind> GetChildren(FindLine l) => ChildrenDict[l] = Enumerable.Empty<IFind>().ToList();
	private IEnumerable<IFind> GetLeavesOrSelf (IFind i) => i switch {
		FindDir d => GetLeavesOrSelf(d),
		FindFile f => GetLeavesOrSelf(f),
		_ => Enumerable.Empty<IFind>()
	};
	private IEnumerable<IFind> GetLeavesOrSelf (FindDir dir) {

		var subDir = Directory.GetDirectories(dir.path);
		var subDirLeaves = subDir.SelectMany(d => GetLeavesOrSelf(new FindDir(d))).ToList();
		//Leaf type: Dir
		if(filter.LeafType == LeafType.Dir) {
			if(subDir.Any())
				return subDirLeaves;
			return [dir];
		}
		var subFile = Directory.GetFiles(dir.path).Where(f => filter.filePattern.Match(f).Success);
		var subFileLeaves = subFile
			.SelectMany(f => GetLeavesOrSelf(new FindFile(f))).ToList();
		return [.. subDirLeaves, .. subFileLeaves];
	}
	private IEnumerable<IFind> GetLeavesOrSelf(FindFile f) {
		if(!filter.Accept(f)) {
			return [];
		}
		if(filter.LeafType == LeafType.File) {
			return [f];
		}
		try {
			return File.ReadAllLines(f.path)
				.Select((line, index) => (line, index, match: filter.linePattern.Match(line)))
				.Where(pair => pair.match.Success)
				.Select(pair => new FindLine(f.path, pair.index, pair.match.Index, pair.line, pair.match.Value));
		} catch(Exception e) {
			return [];
		}
	}
	private IEnumerable<IFind> GetLeavesOrSelf(FindLine l) {
		if(filter.LeafType == LeafType.Line)
			return [l];
		return [];
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
