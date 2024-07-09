using Octokit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;
using Label = Terminal.Gui.Label;
namespace fx;
public record RepoUrl(string owner, string repo) {}
public class GithubSession {
	public View root;
	public GitHubClient client;
	public GithubSession (Main main, RepoUrl url) {
		client = new GitHubClient(new ProductHeaderValue("IHaveAStrongPassword"));
		client.Credentials = new Credentials("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");
		var id = client.Repository.Get(url.owner, url.repo).Result.Id;
		ConcurrentDictionary<string, byte[]> files = new();
		byte[] GetFile (string path) => files.GetOrAdd(path, p => client.Repository.Content.GetRawContent(url.owner, url.repo, path).Result);
		ConcurrentDictionary<string, IEnumerable<RepositoryContent>> dirs = new();
		IEnumerable<RepositoryContent> GetDir (string path = "") => dirs.GetOrAdd(path, p => (p switch {
			{ Length:>0} => client.Repository.Content.GetAllContents(id, p),
			_ => client.Repository.Content.GetAllContents(id),
		}).Result);
		var ListView = (string s, IListDataSource src) => new ListView() {
			Title = s,
			BorderStyle = LineStyle.Single,
			X = 24,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
			Source = src,
		};
		root = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
		};
		var code = new Lazy<ListView>(() => {
			var GetFiles = () => client.Repository.Content.GetAllContents(id).Result;
			var list = new ListMarker<RepositoryContent>([], (c, i) =>
				$"{c.Name} ");
			var v = ListView("Code", list);
			var l = GetFiles.Lazy();
			v.Added += (a, e) => {
				if(l.IsValueCreated) return;
				list.items = l.Value;
				v.SetNeedsDisplay();
			};
			return v;
		}).Value;
		var commits = new Lazy<ListView>(() => {
			var GetCommits = () => client.Repository.Commit.GetAll(id).Result;
			var list = new ListMarker<GitHubCommit>([.. GetCommits()], (c, i) => {
				var msg = c.Commit.Message;
				return $"{c.Sha[..6]} {(msg.Length > 48 ? $"{msg[..46]}.." : msg),-48} {c.Commit.Author.Date:yyyy/MM/dd} {c.Commit.Author.Email}";
			});
			var v = ListView("Commit", list);
			var l = GetCommits.Lazy();
			v.Added += (a, e) => {
				if(l.IsValueCreated) return;
				list.items = l.Value;
				v.SetNeedsDisplay();
			};
			return v;
		}).Value;
		var branch = new Lazy<ListView>(() => {
			var GetBranches = () => client.Repository.Branch.GetAll(id).Result;
			var list = new ListMarker<Branch>([],(c, i) => c.Name);
			var v = ListView("Branch", list);
			var l = GetBranches.Lazy();
			v.Added += (a, e) => {
				if(l.IsValueCreated) return;
				list.items = l.Value;
				v.SetNeedsDisplay();
			};
			return v;
		}).Value;
		var release = new Lazy<ListView>(() => {
			var GetReleases = () =>
				client.Repository.Release.GetAll(id).Result;
			var list = new ListMarker<Release>([], (c, i) => c.Name);
			var v = ListView("Release", list);
			var l = GetReleases.Lazy();
			v.Added += (a, e) => {
				if(l.IsValueCreated) return;
				list.items = l.Value;
				v.SetNeedsDisplay();
			};
			return v;
		}).Value;
		var issue = new Lazy<ListView>(() => {
			var GetIssues = () => client.Issue.GetAllForRepository(id).Result;
			var list = new ListMarker<Issue>([], (c, i) => c.Title);
			var v = ListView("Issue", list);

			var l = GetIssues.Lazy();
			v.Added += (a, e) => {
				if(l.IsValueCreated) return;
				list.items = l.Value;
				v.SetNeedsDisplay();
			};
			return v;
		}).Value;
		var pull = new Lazy<ListView>(() => {
			var GetPulls = () =>
			client.Repository.PullRequest.GetAllForRepository(id).Result;
			var list = new ListMarker<PullRequest>([], (c, i) => c.Title);
			var v = ListView("Pull", list);
			var l = GetPulls.Lazy();
			v.Added += (a, e) => {
				if(l.IsValueCreated) return;
				list.items = l.Value;
				v.SetNeedsDisplay();
			};
			return v;
		}).Value;
		View[] panels = [
			code,
			commits,
			branch,
			release,
			issue,
			pull
		];
		var folder = new Folder(root,
			new View() {
				X = 0,
				Y = 0,
				Width = Dim.Fill(),
				Height = 1
			},
			new View() {
				X = 0,
				Y = 1,
				Width = Dim.Fill(),
				Height = Dim.Fill(),
			},
			[..panels.Select(p => (p.Title, p))]);
		var owner = new Label() {
			Text = url.owner,
			X = 48,
			Y = 0,
		};
		var slash = new Label() {
			Text = "/",
			X = Pos.Right(owner) + 1,
			Y = 0
		};
		var repo = new Label() {
			Text = $"{url.repo}",
			X = Pos.Right(slash) + 1,
			Y = 0
		};
		SView.InitTree(
			[[root, owner, slash, repo /*pinList, recentList, repoList*/]]
			);
		root.SetNeedsDisplay();
	}
}