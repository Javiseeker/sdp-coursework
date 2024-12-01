# sdp-coursework
Software Development Practice Coursework - Software Repositories Mining

## Setup Windows

1. Install VS 2022 community edition - Check it has .NET 9 runtime installed.

## Setup MacOS
1. Install NET 9 SDK https://dotnet.microsoft.com/en-us/download/dotnet/9.0
2. cd to the project -> code .
3. run command: dotnet restore
4. run command: dotnet build
5. run command: dotnet run

## .NET Secrets in MacOS
1. VS Code Extensions -> search for "C# Dev Kit" -> install it

2. run command: dotnet user-secrets init

3. add secrets with this command: dotnet user-secrets set "GithubToken" "YourTokenValue"

4. optional run command to list user secrets: dotnet user-secrets list