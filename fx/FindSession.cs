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
public class FindSession : ITab {
	public string TabName => "Find";
	public View TabView => root;
	public View root;
	public TreeView<IFind> tree;
	FindFilter filter = new();
	public TextField rootBar;
	private Ctx ctx;
	public FindSession (Main main) {
		ctx = main.ctx;
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
		rootBar = new TextField(fx.cwd.Replace(ctx.USER_PROFILE, Ctx.USER_PROFILE_MASK)) {
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
				main.EditFile(l);
			}
		};
		root.AddKey(new() {
			[Key.DeleteChar] = () => {
				int i = 0;
			},
			[Key.Delete] = () => {
				main.folder.RemoveTab(root);
			}
		});

		root.AddKey(new() {
			[Key.Esc] = () => {
				main.folder.RemoveTab(root);
			}
		}, new() {
			['>'] = () => {
				main.folder.NextTab();
			}
		});

		rootBar.AddKey(new() {
			[Key.Enter] = FindDirs
		});
		filterBar.AddKey(new() {
			[Key.Enter] = FindFiles
		});
		findBar.AddKey(new() {
			[Key.Enter] = FindLines
		});


		rootShowButton.Clicked += FindDirs;
		filterShowButton.Clicked += FindFiles;


		tree.AddMouse(new() {
			[MouseFlags.Button3Clicked] = e => {
				
				tree.SelectedObject = tree.GetObjectOnRow(e.MouseEvent.Y);
				tree.SetNeedsDisplay();
			},
		});
		tree.AddKey(new() {
			[Key.CursorRight] = () => {
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
		//tree.ExpandAll();
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
		FindDir d => GetAll(d).Any(),
		FindFile f => GetChildren(f).Any(),
		FindLine => false
	};
	//private bool CanShow (IFind f) => CanShow(f as dynamic);
	private bool CanShow (FindDir d) => true;
	private bool CanShow (FindFile f) => filter.Accept(f) && (filter.linePattern == null || GetChildren(f).Any());
	private bool CanShow (FindLine d) => filter.linePattern is { }lp;
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