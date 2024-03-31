using ClangSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Type = System.Type;

namespace fx;
public interface Regrettable {
	public static virtual Regex Pattern { get; }
}
public record Greeting : Regrettable {
	public string hello, noun;
	static Regex Regrettable.Pattern => new("(?<hello>.+) (?<noun>.+)");
};
static class Regret {
	public static bool Match<T> (string s, out T t) where T : Regrettable =>
		T.Pattern.Match(s).Convert(out t);
	public static bool Match<T> (string s, [StringSyntax("Regex")] string pattern, out T t) =>
		Regex.Match(s, pattern).Convert(out t);
	public static bool MatchDict (this string s, [StringSyntax("Regex")] string pattern, out Dictionary<string, string> result) {
		if(Regex.Match(s, pattern) is { Success: true } m) {
			result = m.Groups.Values.ToDictionary(g => g.Name, g => g.Value);
			return true;
		} else {
			result = null;
			return false;
		}
	}
	public static string[] MatchArray (this string s, [StringSyntax("Regex")] string pattern, int skip) {
		if(Regex.Match(s, pattern) is { Success: true } m) {
			return [.. m.Groups.Values.Skip(skip).Select(g =>g.Value)];
		} else {
			return [];
		}
	}
	public static string[] MatchArray (this string s, [StringSyntax("Regex")] string pattern, int skip, out int end) {
		if(Regex.Match(s, pattern) is { Success: true } m) {
			end = m.Index + m.Length;
			return [.. m.Groups.Values.Skip(skip).Select(g => g.Value)];
		} else {
			end = 0;
			return [];
		}
	}
	public static string[][] MatchMatrix (this string s, [StringSyntax("Regex")] string pattern, int skip, out int end) {
		if(Regex.Match(s, pattern) is { Success: true } m) {
			end = m.Index + m.Length;
			string[][] r = [.. (m.Groups.Values.Skip(skip).Select<Group, string[]>((Group g) =>
				[.. (g.Captures.Select(c => c.Value))]
			))];
			return r;
		} else {
			end = 0;
			return [];
		}
	}

	public static object[] Apply(this string[] arr, params Func<string, object>?[] apply) {
		var mid = Math.Min(arr.Length, apply.Length);
		return [.. (..mid).Select(i => apply[i] is { }f ? f(arr[i]) : arr[i]), ..(mid..arr.Length).Select(i => arr[i])];
	}
	public static object[] Parse (this string[] arr, params Type[] cast) {
		var mid = Math.Min(arr.Length, cast.Length);
		return [.. arr[..mid].Select((item, ind) => cast[ind] is {} t ? (object)(t switch {
			_ when t == typeof(int) => int.Parse(item),
			_ => item
		}) : item), .. (mid..arr.Length).Select(i => arr[i])];
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
	public static T Convert<T> (this Match m) {
		var t = (T)Activator.CreateInstance(typeof(T));
		foreach(var (p, set) in typeof(T).GetProperties(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Select(p => (p: p, set: p.SetMethod)).Where(p => p.set != null)) {
			set.Invoke(t, [m.Groups[p.Name].Value]);
		}
		return t;
	}
	public static bool Convert<T> (this Match m, out T t) {
		t = (m.Success is { } b && b) ? m.Convert<T>() : default;
		return b;
	}
}
