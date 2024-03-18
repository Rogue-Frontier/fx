using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Terminal.Gui;
using static System.Net.Mime.MediaTypeNames;
public interface Regrettable {
	public static virtual Regex Pattern { get; }
}
public record Greeting(string hello, string noun) : Regrettable {
	public Greeting () : this(null, null) { }
	static Regex Regrettable.Pattern => new("(?<hello>.+) (?<noun>.+)");
};
static class Regret {
	public static bool Match<T>(string s, [StringSyntax("Regex")] string pattern, out T t) =>
		Regex.Match(s, pattern).Convert(out t);

	public static bool Match<T> (string s, out T t) where T:Regrettable =>
		T.Pattern.Match(s).Convert(out t);

	public static bool MatchDict(this string s, [StringSyntax("Regex")] string pattern, out Dictionary<string, string> result) {
		if(Regex.Match(s, pattern) is { Success: true } m) {
			result = m.Groups.Values.ToDictionary(g => g.Name, g => g.Value);
			return true;
		} else {
			result = null;
			return false;
		}
	}
	public static string[] MatchArray(this string s, [StringSyntax("Regex")] string pattern) {
		if(Regex.Match(s, pattern) is { Success: true } m) {
			return [.. m.Groups.Values.Select(g => g.Value)];
		} else {
			return [];
		}
	}
		
	public static bool MatchOne (this string s, [StringSyntax("Regex")] string pattern, out string result) {
		if(Regex.Match(s, pattern).Groups is [_, { Value: { } dest }]) {
			result = dest;
			return true;
		} else {
			result = null;
			return false;
		}
	}

	public static T Convert<T>(this Match m) {
		var t = (T)Activator.CreateInstance(typeof(T));
		foreach(var (p, set) in typeof(T).GetProperties(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Select(p => (p:p, set: p.SetMethod)).Where(p => p.set != null)) {
			set.Invoke(t, [m.Groups[p.Name].Value]);
		}
		return t;
	}
	public static bool Convert<T>(this Match m, out T t) {
		t = (m.Success is { } b && b) ? m.Convert<T>() : default;
		return b;
	}
}