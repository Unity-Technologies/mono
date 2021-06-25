using System;
using System.Collections.Generic;
using System.Linq;
using Bee.Core;
using Bee.Core.Stevedore;
using Bee.NativeProgramSupport;
using Bee.Stevedore.Program;
using Bee.Tools;
using NiceIO;

class Build
{
	static void Main()
	{
		NPath root = "../../..";
		NPath monoDir = $@"{root}/incomingbuilds\win64\monodistribution\bin-x64";

		NPath unityJitRelative = @"monodistribution\lib\mono\unityjit-win32";
		NPath classLibDir = $@"{root}/incomingbuilds\classlibs\{unityJitRelative}";
		NPath outputDir = $@"{root}/incomingbuilds\aot-classlibs-win64\{unityJitRelative}";

		// TODO: this is the version yamato has right now. Do stevedore here.
		NPath msvcRoot = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\VC\Tools\MSVC\14.29.30037";
		NPath winSdkLib = @"C:\Program Files (x86)\Windows Kits\10\Lib\10.0.19041.0\ucrt\x64";
		
		var clangExecutableArtifact = new StevedoreArtifact(new RepoName("testing"), new ArtifactId("llvm-clang-win64/12.0.0_73aaea91fe24775a257cf553f0a6db3e8845890cc8c6e35eecba97ec45fc93b6.7z"));
		
		var paths = new NPath[]
		{
			monoDir,
			$@"{msvcRoot}\bin\Hostx64\x64",
			clangExecutableArtifact.Path,
		};
		
		var _envVar = new Dictionary<string, string>
		{
			{"PATH", paths.Select(p => p.ResolveWithFileSystem().ToString(SlashMode.Native)).Append(Environment.GetEnvironmentVariable("PATH")).SeparateWith(";")},
			{"MONO_PATH", classLibDir.ToString(SlashMode.Native)}
		};

		var libPaths = new[] {$@"{msvcRoot}\lib\x64", winSdkLib};
		var libpathstr = libPaths.Select(p => $"/LIBPATH:\\\"{p.ResolveWithFileSystem().ToString(SlashMode.Native)}\\\"").SeparateWithSpace();

		var classLibs = classLibDir.Files("*.dll");
		foreach (var classLib in classLibs)
		{
			var nativeTarget = classLib.ChangeExtension($".{classLib.Extension}.dll");
			NPath pdb = $"{nativeTarget}.pdb";
			var outputFile = outputDir.Combine(nativeTarget.FileName);

			Backend.Current.AddAction("MonoAot",
				new[] {nativeTarget, pdb},
				classLibs.Append(clangExecutableArtifact.Path).ToArray(),
				monoDir.Combine("mono-bdwgc.exe").InQuotes(),
				new[] {$"--aot=keep-temps,ld-flags=\"{libpathstr}\"", "--llvm", classLib.InQuotesResolved()},
				environmentVariables: _envVar);
			Backend.Current.SetupCopyFile(outputFile, nativeTarget);
			Backend.Current.SetupCopyFile(outputDir.Combine(pdb.FileName), pdb);
		}
	}
}
