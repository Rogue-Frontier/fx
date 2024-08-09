using LibGit2Sharp;
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
		IS_GIT_UNCHANGED = new Prop("gitUnchanged", "- Git Unchanged"),
		IS_GIT_STAGED = new Prop("gitStagedChanges", "- Staged Changes"),
		IS_GIT_UNSTAGED = new Prop("gitUnstagedChanges", "- Unstaged Changes"),
		IS_GIT_IGNORED = new Prop("gitIgnored", "- Git ignored"),

		IS_SOLUTION = new Prop("visualStudioSolution", "Visual Studio Solution"),
		IS_REPOSITORY = new Prop("gitRepositoryRoot", "Git Repository"),
		IS_ZIP = new Prop("zipArchive", "Zip Archive"),
		
		IS_PYTHON = new Prop("sourcePython", "Python Source"),
		IS_BATCH  = new Prop("sourceBatch", "Batch Source"),
		IS_BASH = new Prop("sourceBash", "Bash Source");
		
	public static IPropGen
		IN_REPOSITORY = new PropGen<RepoItem>("gitRepositoryItem",	pair => $"In Repository: {pair.root}"),
		IS_LINK_TO = new PropGen<string>("link",					dest => $"Link To: {dest}"),
		IN_LIBRARY = new PropGen<LibraryRoot>("libraryItem",			library => $"In Library: {library.name}"),
		IN_SOLUTION = new PropGen<string>("solutionItem",			slnPath => $"In Solution: {slnPath}"),
		IN_ZIP = new PropGen<ZipItem>("zipItem",						zi =>	$"In Zip: {zi.zipRoot} [{zi.zipEntry}]");
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
	public record ZipItem(string zipRoot, string zipEntry) {
		public static ZipItem From(string zipRoot, string path) =>
			new ZipItem(zipRoot, path.Replace(Path.GetFullPath($"{zipRoot}/"), ""));
	}
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