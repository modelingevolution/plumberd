New-Cake -Name "plumberd" -Root "../"

Add-CakeStep -Name "Build All" -Action {  Invoke-DotnetBuild -All  }
Add-CakeStep -Name "Publish to nuget.org" -Aciton { Invoke-NugetPublish -SourceUrl "https://nuget.org" }
