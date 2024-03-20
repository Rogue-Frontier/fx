
using System.Diagnostics;
Dictionary<string, string> temps = new();
if(args is [string path]) {

	path = Path.GetFullPath(path);
	if(!File.Exists(path)) {
		Console.WriteLine($"Invalid file {path}");
		return;
	}

	string temp;
	if(new FileInfo($"{path}.dat") is { Exists:true } f) {
		Console.WriteLine("Temp found");
		temp = File.ReadAllText(f.FullName);
		if(Directory.Exists(temp)) {
			goto Run;
		}
	}
	Console.WriteLine("Initializing temp");
	temp = $"{Directory.CreateTempSubdirectory()}{Path.DirectorySeparatorChar}{Path.GetFileNameWithoutExtension(path)}";



	Process.Start(new ProcessStartInfo() {
		FileName = "dotnet",
		Arguments = $"new console -o {temp}",
		UseShellExecute = false
	}).WaitForExit();
	

	File.WriteAllText($"{path}.dat", temp);
	Run:

	var dest = $"{temp}{Path.DirectorySeparatorChar}Program.cs";


	Console.WriteLine($"Source: {path}");
	Console.WriteLine($"Dest:   {dest}");

	File.Copy(path, dest,true);
	

	var p = Process.Start(new ProcessStartInfo() {
		FileName = "dotnet",
		Arguments = $"run --project {temp}",
		WorkingDirectory = Environment.CurrentDirectory,
		UseShellExecute = false
	});
	p.WaitForExit();
	if(p.ExitCode != 0) {
		Directory.Delete(temp);
	}
}