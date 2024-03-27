using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace fx;
public interface IProp {
	string id { get; }
	string desc { get; }
}
/// <summary>
/// Standard properties used within fx
/// </summary>
/// <remarks>
/// Be careful not to assign <see cref="IS_REPOSITORY"/> or <see cref="IN_REPOSITORY"/> outside <see cref="ExploreSession.RefreshRepo(string)"/>. The repository is updated before the cwd listing.
/// </remarks>
public static class Props {
	public static IProp
		IS_LOCKED = new Prop("locked", "Locked"),
		IS_FILE = new Prop("file", "File"),
		IS_DIRECTORY = new Prop("directory", "Directory"),
		IS_STAGED = new Prop("gitStagedChanges", "- Staged Changes"),
		IS_UNSTAGED = new Prop("gitUnstagedChanges", "- Unstaged Changes"),
		IS_SOLUTION = new Prop("visualStudioSolution", "Visual Studio Solution"),
		IS_REPOSITORY = new Prop("gitRepositoryRoot", "Git Repository"),
		IS_ZIP = new Prop("zipArchive", "Zip Archive");
	public static IPropGen
		IN_REPOSITORY = new PropGen<RepoItem>("gitRepositoryItem", pair => $"In Repository: {pair.root}"),
		IS_LINK_TO = new PropGen<string>("link", dest => $"Link To: {dest}"),
		IN_LIBRARY = new PropGen<Library>("libraryItem", library => $"In Library: {library.name}"),
		IN_SOLUTION = new PropGen<string>("solutionItem", solutionPath => $"In Solution: {solutionPath}"),
		IN_ZIP = new PropGen<string>("zipItem", zipRoot => $"In Zip: {zipRoot}");
	public static string GetRoot (this Repository repo) => Path.GetFullPath($"{repo.Info.Path}/..");
	public static string GetRepoLocal (this Repository repo, string path) => path.Replace(repo.GetRoot() + Path.DirectorySeparatorChar, null);
	public static string GetRepoLocal (string root, string path) => path.Replace(root + Path.DirectorySeparatorChar, null);
	public static RepoItem CalcRepoItem (this Repository repo, string path) => CalcRepoItem(repo.GetRoot(), path);
	public static RepoItem CalcRepoItem (string root, string path) =>
		new(root, GetRepoLocal(root, path));
	public static bool HasRepo (PathItem item, out string root) {
		if(item.HasProp(IS_REPOSITORY)) {
			root = item.path;
			return true;
		}
		if(item.GetProp<RepoItem>(IN_REPOSITORY, out var repoItem)) {
			root = repoItem.root;
			return true;
		}
		root = null;
		return false;
	}
	/// <summary>Identifies a repository-contained file by local path and repository root</summary>
	public record RepoItem (string root, string local) { }
}
public record Prop (string id, string desc) : IProp { }
public record Prop<T> (string id, string desc, T data) : IProp { }
public interface IPropGen {
	string id { get; }
	public Prop<T> Make<T> (T data) => ((PropGen<T>)this).Generate(data);
}
public record PropGen<T> (string id, PropGen<T>.GetDesc getDesc) : IPropGen {
	public delegate string GetDesc (T args);
	public Prop<T> Generate (T args) => new Prop<T>(id, getDesc(args), args);
}