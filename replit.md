# PSNChecker

A PSN (PlayStation Network) Username Checker written in C#. It reads usernames from a wordlist file and checks each against the PSN accounts API to determine if the username is available.

## Project Setup in Replit

- **Language / Runtime:** C# on .NET 7 SDK (originally targeted .NET Framework 4.5; modernized for cross-platform Linux build).
- **Project layout:** `PSNChecker/` contains the SDK-style csproj and `Program.cs`.
- **Dependencies:** `Newtonsoft.Json` (via NuGet PackageReference).
- **Sample input:** `PSNChecker/wordlist.txt` — one username per line.
- **Workflow:** `PSNChecker` (console output) runs:
  `dotnet run --project PSNChecker -c Release -- PSNChecker/wordlist.txt`

## Notes

This is a CLI tool, not a web application — there is no frontend or HTTP server.

The PSN public endpoint (`https://accounts.api.playstation.com/api/v1/accounts/onlineIds`) used by the original code may now return non-JSON responses (e.g., an HTML challenge/error page) since the project was last updated. The response handling has been hardened so the program still completes cleanly and prints `INVALID (non-JSON response from PSN API)` instead of crashing in that case.

## Usage

```
dotnet run --project PSNChecker -c Release -- path_to_wordlist.txt [path_to_output.txt]
```

- `path_to_wordlist.txt` — required, list of PSN usernames (one per line).
- `path_to_output.txt` — optional, writes valid (available) usernames to this file.

## Recent Changes

- 2026-04-23: Imported from GitHub. Converted legacy .NET Framework 4.5 csproj to SDK-style targeting `net7.0`. Replaced broken local `Newtonsoft.Json` HintPath with NuGet PackageReference. Added a sample `wordlist.txt`. Hardened JSON parsing in `Program.cs` to tolerate non-JSON error responses from the PSN API. Configured a console workflow to run the checker.
