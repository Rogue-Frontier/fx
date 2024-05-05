using GDShrapt.Reader;
using Namotion.Reflection;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Terminal.Gui;

using Str = string;
using Bit = bool;

public interface ICmdAttribute { }
/// <summary>
/// Choose one. Positional. Key and value.
/// </summary>
public class CmdAttribute : System.Attribute, ICmdAttribute {
	public CmdAttribute (string name) { this.name = name; }
	public string name { get; set; }
}
/// <summary>
/// Choose multiple. Nonpositional. Key/value.
/// </summary>
public class ParAttribute : System.Attribute, ICmdAttribute {
	public string name;
	public ParAttribute(string name) {
		this.name = name;
	}
}
/// <summary>
/// Choose multiple. Positional. Value only.
/// </summary>
public class ArgAttribute : System.Attribute, ICmdAttribute {
	public ArgAttribute(string name, bool req = true) { this.name = name; this.req = req; }
	public string name;
	public bool req;
}
/// <summary>
/// Choose multiple. Nonpositional. Key only.
/// </summary>
public class FlagAttribute : System.Attribute, ICmdAttribute {
	public string name;
	public string alt;
    public FlagAttribute(string name, string alt = null) {
		this.name = name;
		this.alt = alt;
	}
}
public class RadioAttribute : System.Attribute, ICmdAttribute { }
public interface ICmdInfo {}
public record CmdInfo (Type t, string name, ICmdInfo[] parts) : ICmdInfo {
	public static CmdInfo From(Type t, string name = null) {
		var _f = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		name ??= t.GetCustomAttribute<CmdAttribute>().name;
		return new CmdInfo(t, name,
			[..t.GetFields(_f).Select<FieldInfo, ICmdInfo>(field => {
				var att = field.GetCustomAttributes(false).OfType<ICmdAttribute>().FirstOrDefault();
				return att switch {
					FlagAttribute f => new FlagInfo(f, field),
					ArgAttribute a => new ArgInfo(a, field),
					CmdAttribute c => From(field.FieldType, c.name),
					RadioAttribute r => new RadioInfo(r, field),
					_ => null
				};
			}).Except([null])]
		);
	}
}
public record FlagInfo(FlagAttribute att, FieldInfo field) : ICmdInfo {
	public string name = att.name;
	public string doc = field.GetXmlDocsSummary();
}
public record ArgInfo (ArgAttribute att, FieldInfo field) : ICmdInfo {
	public string name = att.name;
	public string doc = field.GetXmlDocsSummary();
}
public record RadioInfo(RadioAttribute att, FieldInfo field) : ICmdInfo {
	public string doc = field.GetXmlDocsSummary();
	public string[] labels =
		[..from field in field.FieldType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			select field.GetCustomAttribute<FlagAttribute>().name];
}

public static class CmdStd {
	public static IEnumerable<CmdInfo> GetCmds () => from t in typeof(CmdStd).GetNestedTypes( BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Where(t => !t.IsInterface && t.IsAssignableTo(typeof(ICmdModel))) select CmdInfo.From(t);
	public interface ICmdModel { }
	/// <summary>
	/// Change the shell working directory.
	/// 
	/// Change the current directory to DIR.  The default DIR is the value of the
	/// HOME shell variable.
	/// 
	/// The variable CDPATH defines the search path for the directory containing
	/// DIR.  Alternative directory names in CDPATH are separated by a colon (:).
	/// 
	/// A null directory name is the same as the current directory.  If DIR begins
	/// with a slash (/), then CDPATH is not used.
	/// 
	/// If the directory is not found, and the shell option `cdable_vars' is set,
	/// the word is assumed to be  a variable name.  If that variable has a value,
	/// its value is used for DIR.
	/// 
	/// The default is to follow symbolic links, as if `-L' were specified.
	/// `..' is processed by removing the immediately previous pathname component
	/// back to a slash or the beginning of DIR.
	/// 
	/// Exit Status:
	/// Returns 0 if the directory is changed, and if $PWD is set successfully when
	/// -P is used; non-zero otherwise.
	/// </summary>
	[Cmd("cd")]
	record CDModel : ICmdModel {
		record LP {
			/// <summary>
			/// force symbolic links to be followed: resolve symbolic
			/// links in DIR after processing instances of `..'
			/// </summary>
			[Flag("-L")]	Bit l = true;
			/// <summary>
			/// use the physical directory structure without following
			/// symbolic links: resolve symbolic links in DIR before
			/// processing instances of `..'
			/// </summary>
			[Flag("-P")]	P p = null;
			record P () {
				/// <summary>
				/// if the -P option is supplied, and the current working
				/// directory cannot be determined successfully, exit with
				/// a non-zero status
				/// </summary>
				[Flag("-e")]	Bit exit;
			}
		}
		[Radio]				LP lp;
		/// <summary>
		/// on systems that support it, present a file with extended
		/// attributes as a directory containing the file attributes
		/// </summary>
		[Flag("-@")]		Bit a;
		[Arg("<dir>", 0>0)]	Str dir = ".";
	}
	[Cmd("dotnet")]
	record DotnetModel : ICmdModel {
		[Radio]	Dotnet dotnet;
		record Dotnet {
			[Flag("--info")]		Bit info;
			[Flag("--list-sdks")]	Bit listSdks;
			[Flag("run")]			Run run;
			record Run {
				[Arg("args", false)]	Str[] args;
				[Flag("--project")]		Project project;
				[Flag("--no-build")]	Bit noBuild;
				public record Project {
					[Arg("<project>")]		Str name;
				}
			}
		}
	}
	[Cmd("git")]
	record GitModel : ICmdModel {
		[Radio]	Git git;
		record Git {
			[Flag("status")]	Bit status;
			[Flag("add")]		Add add;
			[Flag("commit")]	Commit commit;
			record Add {
				[Arg("<files>")]	Str files;
			}
			record Commit {
				[Flag("-m")]	M m;
				record M {
					[Arg("<message>")]	Str message;
				}
			}
		}
	}
}