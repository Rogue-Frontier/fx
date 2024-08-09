using ClangSharp.Interop;
using ClangSharp;
using System.Text.RegularExpressions;

namespace fx {
	public class CppProject {
		public string main;
		public Dictionary<string, CppFile> source;

		public static CppProject ParseMake(string cmakelists) {
			var root = Path.GetDirectoryName(cmakelists);
			List<string> includeDirs = [];
			var mainCpp = "";
			var text = File.ReadAllText(cmakelists);
			if(text.MatchArray("target_include_directories\\((?<content>[^)]+)\\)",1) is [{ }lines]) {
				foreach(Match m in Regex.Matches(lines, "\"(?<dir>[^\"]+)\"")) {
					includeDirs.Add(Path.GetFullPath($"{root}/{m.Groups["dir"].Value}"));
				}
			}
			if(text.MatchArray("add_executable\\((?<content>[^)]+)\\)",1) is [{ } content]) {
				if(content.MatchArray("\"(?<path>[^\"]+)\"", 1) is [{ } path]) {
					mainCpp = Path.GetFullPath($"{root}/{path}");
				}
			}
			//https://stackoverflow.com/questions/29991184/clang-matchers-find-the-corresponding-node-in-ast-by-translation-unit-line-num
			Dictionary<string, CppFile> source = new();

			Queue<string> traverse = new([mainCpp]);
			while(traverse.Any()) {
				TraverseFile(traverse.Dequeue());
			}
			void TraverseFile(string path) {
				var Index = CXIndex.Create(false, false);
				var flags = CXTranslationUnit_Flags.CXTranslationUnit_DetailedPreprocessingRecord;
				var error = CXTranslationUnit.TryParse(Index, path, [], null, flags, out var TU);
				var unit = TranslationUnit.GetOrCreate(TU);
				var sourceFile = source[path] = new CppFile() { path = path };
				var ch = unit.TranslationUnitDecl.CursorChildren.Where(c => c.Location.IsFromMainFile);
				foreach(var item in ch) {
					switch(item) {
						case InclusionDirective i:
							Include(i.Handle.ToString());
							continue;
						case NamespaceDecl n:
							foreach(var c in n.CursorChildren) {
								HandleNonRoot(c);
							}
							continue;
						default:
							HandleNonRoot(item);
							continue;
					}
				}
				void Include(string item) {
					var dest = Path.GetFullPath($"{Path.GetDirectoryName(path)}/{item}");
					if(File.Exists(dest)) {
						Register(dest);
						return;
					}
					foreach(var dir in includeDirs) {
						dest = Path.GetFullPath($"{dir}/{item}");
						if(File.Exists(dest)) {
							Register(dest);
							break;
						}
					}

					void Register (string dest) {
						sourceFile.includePath[item] = dest;
						if(!source.ContainsKey(dest)) {
							source[dest] = null;
							traverse.Enqueue(dest);

						}
					}
				}
				void HandleNonRoot(Cursor item) {
					switch(item) {
						case FunctionDecl f:
							sourceFile.functionHead.Add(f.Name);
							if(f.HasBody) {
								sourceFile.functionBody.Add(f.Name);
							}
							return;
						case CXXRecordDecl c: {
							var cl = new CppClass() { name = c.Name };
							sourceFile.classDecl.Add(cl);
							foreach(var ch in c.CursorChildren) {
								HandleMember(cl, ch);
							}
							return;
						}
						case ClassTemplateDecl ct: {
							var cl = new CppClass() { name = ct.Name };
							sourceFile.classDecl.Add(cl);
							foreach(var c in ct.CursorChildren) {
								HandleMember(cl, c);
							}
							return;
						}
					}

					void HandleMember(CppClass cl, Cursor item) {
						switch(item) {
							case CXXMethodDecl m:
								cl.methodHead.Add(m.Name);
								if(m.HasBody)
									cl.methodBody.Add(m.Name);
								break;
							case FieldDecl f:
								cl.field.Add(f.Name);
								break;
						}
					}
				}
			}
			return new CppProject() {
				main = mainCpp,
				source = source
			};
		}

		/*
		public CppProject(string mainFile) {
			var dir = Path.GetDirectoryName(mainFile);
			var Index = CXIndex.Create(false, false);

			var flags = CXTranslationUnit_Flags.CXTranslationUnit_DetailedPreprocessingRecord;
			var error = CXTranslationUnit.TryParse(Index, mainFile, [], null, flags, out var TU);
			var main = TranslationUnit.GetOrCreate(TU);

			return;
			/*
			var fdec = tran.TranslationUnitDecl.CursorChildren.OfType<FunctionDecl>();
			
			var fl = fdec.Where(f => f.Location.IsFromMainFile).Select(f => {
				f.Location.GetFileLocation(out var _, out var row, out var col, out var _);
				return (f.Name, row, col);
			});
			/



		}
		*/
	}

	public record CppFile() {
		public string path;
		public Dictionary<string, string> includePath = new();

		public HashSet<string> functionHead = new();
		public HashSet<string> functionBody = new();
		public HashSet<CppClass> classDecl = new();
	}
	public record Loc (CppFile file, int row = -1, int col = -1);
	public record CppClass {
		public string name;
		public HashSet<string> field = new();
		public HashSet<string> methodHead = new();
		public HashSet<string> methodBody = new();
	}
	
	public record CppFunction {
		Loc head, body;
	}
}
