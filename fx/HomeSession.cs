using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace fx;
public class HomeSession : ITab {
	public string TabName => "Home";
	public View TabView => root;
	public View root;
	public HomeSession() {

		root = new View() {
			X = 0,
			Y = 0,
			Width = Dim.Fill(),
			Height = Dim.Fill(),
		};
		var FILL = Dim.Fill();
		var w = Dim.Percent(25);
		//Libraries
		var libraries = new FrameView("Libraries") {
			X = 0,
			Y = 0,
			Width = w,
			Height = Dim.Fill(),
		};
		var librariesList = new ListView() {
			X = 0,
			Y = 0,
			Width = FILL,
			Height = FILL,
		};

		//Pinned folders, pinned files
		var pins = new FrameView("Pinned") {
			X = Pos.Right(libraries),
			Y = 0,
			Width = w,
			Height = FILL,
		};
		var pinsList = new ListView() {
			X = 0,
			Y = 0,
			Width = FILL,
			Height = FILL
		};
		var recent = new FrameView("Recent") {
			X = Pos.Right(pins),
			Y = 0,
			Width = w,
			Height = Dim.Fill()
		};
		var recentList = new ListView() {
			X = 0,
			Y = 0,
			Width = FILL,
			Height = FILL
		};


		var repos = new FrameView("Repositories") {
			X = Pos.Right(recent),
			Y = 0,
			Width = w,
			Height = Dim.Fill()
		};
		var repoList = new ListView() {
			X = 0,
			Y = 0,
			Width = FILL,
			Height = FILL
		};

		SView.InitTree(
			[root, libraries, pins, recent, repos],
			[libraries, librariesList],
			[pins, pinsList],
			[recent, recentList],
			[repos, repoList]
			);
		//https://stackoverflow.com/questions/13079569/how-do-i-get-the-path-name-from-a-file-shortcut-getting-exception/13079688#13079688
	}
}
public record Library (string name) {
	public List<Link> links = new();
	public record Link (string path, bool visible, bool expand);
}