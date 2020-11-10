var target = Argument("target", "Test");
var configuration = Argument("configuration", "Release");
var version = Argument("version", "0.0.0.4");

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .WithCriteria(c => HasArgument("rebuild"))
    .Does(() =>
{
    
});

Task("Publish")
    .IsDependentOn("Build")
    .Does(() => 
    {
        NuGetPushSettings settings = new NuGetPushSettings();
        settings.Source = "https://api.nuget.org/v3/index.json";

        var packages = GetFiles($"./**/{configuration}/*.{version}.nupkg");
        NuGetPush(packages, settings);

    });

Task("Build")
    .IsDependentOn("Clean")
    .Does(() =>
{
    var settings =  new DotNetCoreBuildSettings
    {
        Configuration = configuration,
        MSBuildSettings = new DotNetCoreMSBuildSettings()
    };
    settings.MSBuildSettings.WithProperty("Version",version);
    DotNetCoreBuild("./ModelingEvolution.Plumberd.sln", settings);
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    DotNetCoreTest("./ModelingEvolution.Plumberd.sln", new DotNetCoreTestSettings
    {
        Configuration = configuration,
        NoBuild = true,
    });
});

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);